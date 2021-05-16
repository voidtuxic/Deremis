using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using DefaultEcs;
using DefaultEcs.System;
using DefaultEcs.Threading;
using Deremis.Engine.Objects;
using Deremis.Engine.Rendering.Helpers;
using Deremis.Engine.Rendering.Resources;
using Deremis.Engine.Systems.Components;
using Deremis.Engine.Systems.Extensions;
using Deremis.Platform;
using Deremis.Platform.Assets;
using Veldrid;
using Veldrid.Utilities;
using Shader = Deremis.Engine.Objects.Shader;

namespace Deremis.Engine.Systems
{
    public class ForwardRenderSystem : AEntityMultiMapSystem<float, Drawable>
    {
        public const uint INSTANCE_BUFFER_SIZE = 64 * 1024;
        public static ForwardRenderSystem current;

        private readonly Application app;
        private readonly CommandList mainCommandList;

        private Scene currentScene;
        private Transform emptyTransform = new Transform();

        // private readonly EntityMultiMap<Drawable> deferredObjectsMap;

        internal class DrawState
        {
            public string key;
            public CommandList commandList;
            public bool isValid;
            public Material material;
            public Mesh mesh;
            public DeviceBuffer instanceBuffer;
        }

        private readonly ConcurrentDictionary<string, Mesh> meshes = new ConcurrentDictionary<string, Mesh>();
        private readonly ConcurrentDictionary<string, DrawState> drawStates = new ConcurrentDictionary<string, DrawState>();
        // private readonly Dictionary<string, Material> deferredMaterials = new Dictionary<string, Material>();

        private Matrix4x4 viewMatrix;
        private Matrix4x4 projMatrix;
        private Matrix4x4 viewProjMatrix;

        public RgbaFloat ClearColor { get; set; } = RgbaFloat.Black;

        public ForwardRenderSystem(Application app, World world) : base(world
            .GetEntities()
            .With<Drawable>()
            .With<Transform>()
            .Without<Deferred>()
            .With<Render>(CanRenderToScreen)
            .AsMultiMap<Drawable>())
        {
            current = this;
            this.app = app;
            mainCommandList = app.Factory.CreateCommandList();
            mainCommandList.Name = "MainCommandList";
            // deferredObjectsMap = world.GetEntities()
            //     .With<Drawable>()
            //     .With<Transform>()
            //     .With<Deferred>()
            //     .With<Render>(CanRenderToScreen)
            //     .AsMultiMap<Drawable>();
        }

        private static bool CanRenderToScreen(in Render render)
        {
            return render.Screen;
        }

        private static bool CanRenderToShadowMap(in Render render)
        {
            return render.Shadows;
        }

        public void SetAsCurrentScene(Scene scene)
        {
            currentScene = scene;
            scene.Enable();
        }

        public string RegisterMesh(string name, Mesh mesh)
        {
            meshes.TryAdd(name, mesh);
            return name;
        }

        public void RegisterDeferred(Material deferredMat)
        {
            // foreach (var mat in deferredMaterials.Values)
            // {
            //     if (mat.DeferredLightingMaterial == deferredMat.DeferredLightingMaterial)
            //         return;
            // }
            // deferredMaterials.TryAdd(deferredMat.Name, deferredMat);
        }

        public Mesh GetMesh(string name)
        {
            if (meshes.ContainsKey(name)) return meshes[name];
            return null;
        }

        protected override void PreUpdate(float state)
        {
            // app.ScreenRender.ClearDeferredFramebuffers(mainCommandList);

            mainCommandList.Begin();

            mainCommandList.SetFramebuffer(app.ScreenRender.ScreenFramebuffer);
            mainCommandList.SetFullViewports();
            mainCommandList.ClearColorTarget(0, ClearColor);
            // mainCommandList.ClearColorTarget(1, RgbaFloat.Clear); // bloom
            mainCommandList.ClearDepthStencil(1f);

            if (currentScene != null)
            {
                var cameraSet = currentScene.CameraSet;
                Span<Entity> cameras = stackalloc Entity[cameraSet.Count];
                cameraSet.GetEntities().CopyTo(cameras);
                foreach (ref readonly Entity camEntity in cameras)
                {
                    ref var transform = ref camEntity.Get<Transform>();
                    viewMatrix = transform.ToViewMatrix();
                    projMatrix = camEntity.Get<Camera>().projection;
                    viewProjMatrix = Matrix4x4.Multiply(viewMatrix, projMatrix);

                    // TODO handle more than one camera
                    break;
                }
            }
            mainCommandList.End();

            app.GraphicsDevice.SubmitCommands(mainCommandList);
            app.GraphicsDevice.WaitForIdle();
        }

        private DrawState GetState(string key)
        {
            if (!drawStates.ContainsKey(key))
            {
                var commandList = app.Factory.CreateCommandList();
                var state = new DrawState { commandList = commandList, key = key };
                if (drawStates.TryAdd(key, state))
                {
                    return state;
                }
                else
                {
                    commandList.Dispose();
                    return null;
                }
            }
            return drawStates[key];
        }

        protected override void Update(float deltaSeconds, in Drawable key, ReadOnlySpan<Entity> entities)
        {
            var state = GetState(key.ToString());
            var material = app.MaterialManager.GetMaterial(key.material);

            if (material.Shader.IsInstanced && state.instanceBuffer == null)
            {
                state.instanceBuffer = app.Factory.CreateBuffer(new BufferDescription(INSTANCE_BUFFER_SIZE, BufferUsage.VertexBuffer | BufferUsage.Dynamic));
            }

            var commandList = state.commandList;
            InitDrawable(in key, entities);
            if (!state.isValid) return;
            if (material.Shader.IsInstanced)
            {
                commandList.UpdateBuffer(
                    app.MaterialManager.TransformBuffer,
                    0,
                    new TransformResource
                    {
                        viewProjMatrix = viewProjMatrix,
                        viewMatrix = viewMatrix,
                        projMatrix = projMatrix,
                        lightSpaceMatrix1 = app.ShadowRender.LightSpaceMatrix,
                        lightSpaceMatrix2 = app.ShadowRender.GetCascadeLightViewMatrix(0),
                        lightSpaceMatrix3 = app.ShadowRender.GetCascadeLightViewMatrix(1),
                    });

                // TODO instances only get the sun light
                var lightValues = currentScene.LightVolumes.SunLight.GetValueArray(ref emptyTransform);
                commandList.UpdateBuffer(app.MaterialManager.LightBuffer, 0, lightValues);

                if (state.mesh.Indexed)
                    commandList.DrawIndexed(
                        indexCount: state.mesh.IndexCount,
                        instanceCount: (uint)entities.Length,
                        indexStart: 0,
                        vertexOffset: 0,
                        instanceStart: 0);
                else
                    commandList.Draw(
                        vertexCount: state.mesh.VertexCount,
                        instanceCount: (uint)entities.Length,
                        vertexStart: 0,
                        instanceStart: 0);
            }
            else
            {
                foreach (ref readonly var entity in entities)
                {
                    if (entity.Get<Render>().Screen)
                    {
                        Draw(in key, in entity);
                    }
                }
            }
            commandList.End();

            app.GraphicsDevice.SubmitCommands(commandList);
        }

        private void InitDrawable(in Drawable key, ReadOnlySpan<Entity> entities)
        {
            string drawKey = key.ToString();
            var state = GetState(drawKey);
            state.material = app.MaterialManager.GetMaterial(key.material);
            state.mesh = GetMesh(key.mesh);
            if (state.material == null || state.mesh == null || mainCommandList == null)
            {
                state.isValid = false;
                return;
            }
            state.isValid = true;
            var pipeline = state.material.GetPipeline(0);
            SetPipeline(pipeline, state, entities);
        }

        private void SetPipeline(Pipeline pipeline, DrawState state, ReadOnlySpan<Entity> entities)
        {
            var list = state.commandList;
            list.Begin();
            list.SetFramebuffer(app.ScreenRender.ScreenFramebuffer);
            list.SetFullViewports();
            list.UpdateBuffer(app.MaterialManager.MaterialBuffer, 0, state.material.GetValueArray());
            list.SetPipeline(pipeline);
            list.SetVertexBuffer(0, state.mesh.VertexBuffer);
            if (state.material.Shader.IsInstanced)
            {
                var worlds = new List<Matrix4x4>(entities.Length);
                foreach (var entity in entities)
                {
                    worlds.Add(entity.GetWorldTransform().ToMatrix());
                }
                list.UpdateBuffer(state.instanceBuffer, 0, worlds.ToArray());
                list.SetVertexBuffer(1, state.instanceBuffer);
            }
            if (state.mesh.Indexed)
                list.SetIndexBuffer(state.mesh.IndexBuffer, IndexFormat.UInt32);
            list.SetGraphicsResourceSet(0, app.MaterialManager.GeneralResourceSet);
            list.SetGraphicsResourceSet(1, state.material.ResourceSet);
            state.commandList = list;
        }

        private void Draw(in Drawable key, in Entity entity)
        {
            var state = GetState(key.ToString());
            if (!state.isValid) return;
            var commandList = state.commandList;
            var transform = entity.GetWorldTransform();
            var world = transform.ToMatrix();
            var normalWorld = Matrix4x4.Identity;
            if (Matrix4x4.Invert(world, out normalWorld))
            {
                normalWorld = Matrix4x4.Transpose(normalWorld);
            }
            commandList.UpdateBuffer(
                app.MaterialManager.TransformBuffer,
                0,
                new TransformResource
                {
                    viewProjMatrix = viewProjMatrix,
                    worldMatrix = world,
                    normalWorldMatrix = normalWorld,
                    viewMatrix = viewMatrix,
                    projMatrix = projMatrix,
                    lightSpaceMatrix1 = app.ShadowRender.LightSpaceMatrix,
                    lightSpaceMatrix2 = app.ShadowRender.GetCascadeLightViewMatrix(0),
                    lightSpaceMatrix3 = app.ShadowRender.GetCascadeLightViewMatrix(1),
                });

            var lightValues = currentScene.LightVolumes.GetNearbyValues(transform, 50);
            commandList.UpdateBuffer(app.MaterialManager.LightBuffer, 0, lightValues);

            if (state.mesh.Indexed)
                commandList.DrawIndexed(
                    indexCount: state.mesh.IndexCount,
                    instanceCount: 1,
                    indexStart: 0,
                    vertexOffset: 0,
                    instanceStart: 0);
            else
                commandList.Draw(
                    vertexCount: state.mesh.VertexCount,
                    instanceCount: 1,
                    vertexStart: 0,
                    instanceStart: 0);
        }

        protected override void PostUpdate(float deltaSeconds)
        {
            app.GraphicsDevice.WaitForIdle();
            app.ScreenRender.UpdateScreenTexture(mainCommandList);
        }

        private void DrawDeferred()
        {
            // if (deferredMaterials.Count != 0)
            // {
            //     commandList.Begin();
            //     foreach (var key in deferredObjectsMap.Keys)
            //     {
            //         if (InitDrawable(key, null, false, false) && deferredObjectsMap.TryGetEntities(key, out var entities))
            //         {
            //             isDrawValid = true;
            //             Update(0, in key, entities);
            //         }
            //     }
            //     commandList.End();
            //     SubmitAndWait();

            //     app.UpdateRenderTextures(commandList);

            //     foreach (var material in deferredMaterials.Values)
            //     {
            //         UpdateScreenBuffer(material.DeferredLightingMaterial, app.ScreenFramebuffer);
            //     }
            // }
        }
    }
}