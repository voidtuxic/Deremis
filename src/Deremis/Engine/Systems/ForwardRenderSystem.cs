using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using DefaultEcs;
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
    public class ForwardRenderSystem : DrawableSystem
    {
        public static ForwardRenderSystem current;

        private Scene currentScene;
        private Transform emptyTransform = new Transform();

        // private readonly EntityMultiMap<Drawable> deferredObjectsMap;

        private readonly ConcurrentDictionary<string, Mesh> meshes = new ConcurrentDictionary<string, Mesh>();
        // private readonly Dictionary<string, Material> deferredMaterials = new Dictionary<string, Material>();

        private Matrix4x4 viewMatrix;
        private Matrix4x4 projMatrix;
        private Matrix4x4 viewProjMatrix;

        public RgbaFloat ClearColor { get; set; } = RgbaFloat.Black;

        public ForwardRenderSystem(Application app, World world) : base(app, world, world
            .GetEntities()
            .With<Drawable>()
            .With<Transform>()
            .Without<Deferred>()
            .With<Render>(CanRenderToScreen)
            .AsMultiMap<Drawable>())
        {
            current = this;
            // deferredObjectsMap = world.GetEntities()
            //     .With<Drawable>()
            //     .With<Transform>()
            //     .With<Deferred>()
            //     .With<Render>(CanRenderToScreen)
            //     .AsMultiMap<Drawable>();
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

        protected override void Update(float deltaSeconds, in Drawable key, ReadOnlySpan<Entity> entities)
        {
            var material = app.MaterialManager.GetMaterial(key.material);
            var state = GetState(key.ToString(), material.Shader.IsInstanced);

            var commandList = state.CommandList;
            InitDraw(in key, state, entities);
            if (!state.IsValid) return;
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

                if (state.Mesh.Indexed)
                    commandList.DrawIndexed(
                        indexCount: state.Mesh.IndexCount,
                        instanceCount: (uint)entities.Length,
                        indexStart: 0,
                        vertexOffset: 0,
                        instanceStart: 0);
                else
                    commandList.Draw(
                        vertexCount: state.Mesh.VertexCount,
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
                        Draw(in key, in state, in entity);
                    }
                }
            }
            commandList.End();

            app.GraphicsDevice.SubmitCommands(commandList);
        }

        private void InitDraw(in Drawable key, DrawState state, ReadOnlySpan<Entity> entities)
        {
            if (state.Material == null) state.Material = app.MaterialManager.GetMaterial(key.material);
            if (state.Mesh == null) state.Mesh = GetMesh(key.mesh);
            if (state.Material == null || state.Mesh == null || mainCommandList == null)
            {
                state.IsValid = false;
                return;
            }
            state.IsValid = true;
            var pipeline = state.Material.GetPipeline(0);
            SetPipeline(pipeline, state, entities);
        }

        private void SetPipeline(Pipeline pipeline, DrawState state, ReadOnlySpan<Entity> entities)
        {
            var list = state.CommandList;
            list.Begin();
            list.SetFramebuffer(app.ScreenRender.ScreenFramebuffer);
            list.SetFullViewports();
            list.UpdateBuffer(app.MaterialManager.MaterialBuffer, 0, state.Material.GetValueArray());
            list.SetPipeline(pipeline);
            list.SetVertexBuffer(0, state.Mesh.VertexBuffer);
            if (state.Material.Shader.IsInstanced)
            {
                var worlds = state.Worlds;
                worlds.Clear();
                foreach (var entity in entities)
                {
                    worlds.Add(entity.GetWorldTransform().ToMatrix());
                }
                list.UpdateBuffer(state.InstanceBuffer, 0, worlds.ToArray());
                list.SetVertexBuffer(1, state.InstanceBuffer);
            }
            if (state.Mesh.Indexed)
                list.SetIndexBuffer(state.Mesh.IndexBuffer, IndexFormat.UInt32);
            list.SetGraphicsResourceSet(0, app.MaterialManager.GeneralResourceSet);
            list.SetGraphicsResourceSet(1, state.Material.ResourceSet);
            state.CommandList = list;
        }

        private void Draw(in Drawable key, in DrawState state, in Entity entity)
        {
            if (!state.IsValid) return;
            var commandList = state.CommandList;
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

            if (state.Mesh.Indexed)
                commandList.DrawIndexed(
                    indexCount: state.Mesh.IndexCount,
                    instanceCount: 1,
                    indexStart: 0,
                    vertexOffset: 0,
                    instanceStart: 0);
            else
                commandList.Draw(
                    vertexCount: state.Mesh.VertexCount,
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