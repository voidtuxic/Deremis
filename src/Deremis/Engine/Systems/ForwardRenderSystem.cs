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
        private readonly CommandList commandList;

        private readonly EntitySet cameraSet;
        private readonly EntitySet lightSet;
        // private readonly EntityMultiMap<Drawable> deferredObjectsMap;

        private readonly ConcurrentDictionary<string, Mesh> meshes = new ConcurrentDictionary<string, Mesh>();
        // private readonly Dictionary<string, Material> deferredMaterials = new Dictionary<string, Material>();

        private bool isDrawValid;
        private Material material;
        private Mesh mesh;
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
            commandList = app.Factory.CreateCommandList();
            commandList.Name = "MainCommandList";
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

        private void SetFramebuffer(Framebuffer framebuffer = null)
        {
            commandList.SetFramebuffer(framebuffer ?? app.ScreenFramebuffer);
            commandList.SetFullViewports();
        }

        protected override void PreUpdate(float state)
        {
            app.ClearDeferredFramebuffers(commandList);

            commandList.Begin();

            SetFramebuffer();
            commandList.ClearColorTarget(0, ClearColor);
            commandList.ClearColorTarget(1, RgbaFloat.Clear); // bloom
            commandList.ClearDepthStencil(1f);

            if (cameraSet.Count == 0)
            {
                isDrawValid = false;
                return;
            }
            isDrawValid = true;

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
            commandList.UpdateBuffer(app.MaterialManager.LightBuffer, 0, lightValues.ToArray());
            commandList.End();
            SubmitAndWait();
        }

        protected override void PreUpdate(float state, Drawable key)
        {
            if (!isDrawValid) return;
            isDrawValid = InitDrawable(key);
        }

        private bool InitDrawable(Drawable key, Material drawMat = null, bool checkDeferred = true, bool begin = true)
        {
            material = drawMat ?? app.MaterialManager.GetMaterial(key.material);
            if (material == null || (checkDeferred && material.Shader.IsDeferred))
            {
                return false;
            }
            mesh = GetMesh(key.mesh);
            var pipeline = material.GetPipeline(0);
            if (pipeline != null && mesh != null)
            {
                SetPipeline(pipeline, begin);
                isDrawValid = true;
            }
            else return false;
            return true;
        }

        private void SetPipeline(Pipeline pipeline, bool begin)
        {
            if (begin)
                commandList.Begin();
            SetFramebuffer(material.Framebuffer);
            commandList.UpdateBuffer(app.MaterialManager.MaterialBuffer, 0, material.GetValueArray());
            commandList.SetVertexBuffer(0, mesh.VertexBuffer);
            if (mesh.Indexed)
                commandList.SetIndexBuffer(mesh.IndexBuffer, IndexFormat.UInt32);
            commandList.SetPipeline(pipeline);
            commandList.SetGraphicsResourceSet(0, app.MaterialManager.GeneralResourceSet);
            commandList.SetGraphicsResourceSet(1, material.ResourceSet);
        }

        protected override void Update(float state, in Drawable key, ReadOnlySpan<Entity> entities)
        {
            if (!isDrawValid) return;

            foreach (ref readonly var entity in entities)
            {
                if (entity.Get<Render>().Screen)
                    Draw(in entity);
            }
        }

        private void Draw(in Entity entity)
        {
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

            if (mesh.Indexed)
                commandList.DrawIndexed(
                    indexCount: mesh.IndexCount,
                    instanceCount: 1,
                    indexStart: 0,
                    vertexOffset: 0,
                    instanceStart: 0);
            else
                commandList.Draw(
                    vertexCount: mesh.VertexCount,
                    instanceCount: 1,
                    vertexStart: 0,
                    instanceStart: 0);
        }

        protected override void PostUpdate(float state, Drawable key)
        {
            commandList.End();
            app.GraphicsDevice.SubmitCommands(commandList);
            // kinda defeats the purpose of general preupdate...
            isDrawValid = true;
        }

        protected override void PostUpdate(float state)
        {
            app.UpdateScreenTexture(commandList);
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



        public void SubmitAndWait()
        {
            app.GraphicsDevice.SubmitCommands(commandList);
            app.GraphicsDevice.WaitForIdle();
        }
    }
}