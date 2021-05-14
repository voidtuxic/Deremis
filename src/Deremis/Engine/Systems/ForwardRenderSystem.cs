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
        public static ForwardRenderSystem current;

        private readonly Application app;
        private readonly CommandList mainCommandList;

        private readonly EntitySet cameraSet;
        private readonly EntitySet lightSet;
        // private readonly EntityMultiMap<Drawable> deferredObjectsMap;

        public struct DrawState
        {
            public int hashCode;
            public CommandList commandList;
            public bool isValid;
            public Material material;
            public Mesh mesh;
        }

        private readonly ConcurrentDictionary<string, Mesh> meshes = new ConcurrentDictionary<string, Mesh>();
        private readonly ConcurrentDictionary<int, DrawState> drawStates = new ConcurrentDictionary<int, DrawState>();
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
            cameraSet = world.GetEntities()
                .With<Camera>()
                .With<Transform>()
                .AsSet();
            lightSet = world.GetEntities()
                .With<Light>()
                .With<Transform>()
                .AsSet();
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

            Span<Entity> lights = stackalloc Entity[lightSet.Count];
            var lightValues = new List<float>();
            lightSet.GetEntities().CopyTo(lights);
            foreach (ref readonly Entity lightEntity in lights)
            {
                ref var transform = ref lightEntity.Get<Transform>();
                ref var light = ref lightEntity.Get<Light>();

                lightValues.AddRange(light.GetValueArray(ref transform));
            }
            mainCommandList.UpdateBuffer(app.MaterialManager.LightBuffer, 0, lightValues.ToArray());
            mainCommandList.End();

            app.GraphicsDevice.SubmitCommands(mainCommandList);
            app.GraphicsDevice.WaitForIdle();
        }

        private DrawState GetState(int hashCode)
        {
            if (!drawStates.ContainsKey(hashCode))
            {
                var commandList = app.Factory.CreateCommandList();
                var state = new DrawState { commandList = commandList, hashCode = hashCode };
                if (drawStates.TryAdd(hashCode, state))
                {
                    return state;
                }
                else
                {
                    commandList.Dispose();
                    return default;
                }
            }
            return drawStates[hashCode];
        }

        private void SetState(int hashCode, DrawState state)
        {
            if (!drawStates.ContainsKey(hashCode)) return;
            drawStates[hashCode] = state;
        }

        private void InitDrawable(in Drawable key)
        {
            var state = GetState(key.GetHashCode());
            state.material = app.MaterialManager.GetMaterial(key.material);
            state.mesh = GetMesh(key.mesh);
            if (state.material == null || state.mesh == null || mainCommandList == null)
            {
                state.isValid = false;
                SetState(key.GetHashCode(), state);
                return;
            }
            state.isValid = true;
            var pipeline = state.material.GetPipeline(0);
            SetPipeline(pipeline, ref state);
            SetState(key.GetHashCode(), state);
        }

        private void SetPipeline(Pipeline pipeline, ref DrawState state)
        {
            var commandList = state.commandList;
            commandList.Begin();
            commandList.SetFramebuffer(app.ScreenRender.ScreenFramebuffer);
            commandList.SetFullViewports();
            commandList.UpdateBuffer(app.MaterialManager.MaterialBuffer, 0, state.material.GetValueArray());
            commandList.SetVertexBuffer(0, state.mesh.VertexBuffer);
            if (state.mesh.Indexed)
                commandList.SetIndexBuffer(state.mesh.IndexBuffer, IndexFormat.UInt32);
            commandList.SetPipeline(pipeline);
            commandList.SetGraphicsResourceSet(0, app.MaterialManager.GeneralResourceSet);
            commandList.SetGraphicsResourceSet(1, state.material.ResourceSet);
            state.commandList = commandList;
        }

        protected override void Update(float deltaSeconds, in Drawable key, ReadOnlySpan<Entity> entities)
        {
            var state = GetState(key.GetHashCode());
            InitDrawable(in key);
            if (!state.isValid) return;

            foreach (ref readonly var entity in entities)
            {
                if (entity.Get<Render>().Screen)
                {
                    Draw(in key, in entity);
                }
            }

            var commandList = state.commandList;
            commandList.End();

            app.GraphicsDevice.SubmitCommands(commandList);
        }

        private void Draw(in Drawable key, in Entity entity)
        {
            var state = GetState(key.GetHashCode());
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