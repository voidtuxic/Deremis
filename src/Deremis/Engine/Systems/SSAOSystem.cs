using System;
using System.Numerics;
using DefaultEcs;
using DefaultEcs.System;
using Deremis.Engine.Objects;
using Deremis.Engine.Rendering.Resources;
using Deremis.Engine.Systems.Components;
using Deremis.Engine.Systems.Extensions;
using Deremis.Platform;
using Deremis.Platform.Assets;
using Veldrid;
using Shader = Deremis.Engine.Objects.Shader;
using Texture = Deremis.Engine.Objects.Texture;

namespace Deremis.Engine.Systems
{
    public class SSAOSystem : AEntityMultiMapSystem<float, Drawable>
    {
        public static AssetDescription GbufferShader = new AssetDescription
        {
            name = "ssao_gbuffer",
            path = "Shaders/ssao_gbuffer.xml"
        };
        private CommandList commandList;
        private readonly Application app;

        public Framebuffer SceneFramebuffer { get; private set; }
        public Framebuffer ScreenFramebuffer { get; private set; }
        public Texture Texture { get; private set; }
        public Material GbufferMaterial { get; private set; }

        private readonly EntitySet cameraSet;
        private Transform mainCameraTransform;
        private Matrix4x4 viewProjMatrix;
        private Mesh mesh;

        public SSAOSystem(Application app, World world) : base(
            world.GetEntities()
                .With<Drawable>()
                .With<Transform>()
                .With<Render>(CanRender)
                .AsMultiMap<Drawable>())
        {
            this.app = app;
            commandList = app.Factory.CreateCommandList();
            cameraSet = world.GetEntities()
                .With<Camera>()
                .With<Transform>()
                .AsSet();
            CreateResources();
            GbufferMaterial = app.MaterialManager.CreateMaterial(
                "ssao_gbuffer",
                app.AssetManager.Get<Shader>(GbufferShader),
                SceneFramebuffer);
        }

        public void CreateResources()
        {
            DisposeScreenTargets();

            var positionRt = app.GetRenderTexture("position", Application.COLOR_PIXEL_FORMAT);
            var normalRt = app.GetRenderTexture("normal", Application.COLOR_PIXEL_FORMAT);
            SceneFramebuffer = app.Factory.CreateFramebuffer(new FramebufferDescription(app.ScreenDepthTexture, positionRt.RenderTarget.VeldridTexture, normalRt.RenderTarget.VeldridTexture));

            var renderTex = app.GetRenderTexture("ssaoTex", Application.COLOR_PIXEL_FORMAT);
            ScreenFramebuffer = app.Factory.CreateFramebuffer(new FramebufferDescription(app.ScreenDepthTexture, renderTex.RenderTarget.VeldridTexture));
            Texture = renderTex.CopyTexture;
        }

        protected override void PreUpdate(float state)
        {
            commandList.Begin();
            commandList.SetFramebuffer(SceneFramebuffer);
            commandList.SetFullViewports();
            commandList.ClearColorTarget(0, RgbaFloat.Clear);
            commandList.ClearColorTarget(1, RgbaFloat.Clear);
            commandList.ClearDepthStencil(1f);
            commandList.UpdateBuffer(app.MaterialManager.MaterialBuffer, 0, GbufferMaterial.GetValueArray());
            commandList.End();
            SubmitAndWait();

            Span<Entity> cameras = stackalloc Entity[cameraSet.Count];
            cameraSet.GetEntities().CopyTo(cameras);
            foreach (ref readonly Entity camEntity in cameras)
            {
                ref var transform = ref camEntity.Get<Transform>();
                mainCameraTransform = transform;
                var viewMatrix = transform.ToViewMatrix();
                var projMatrix = camEntity.Get<Camera>().projection;
                viewProjMatrix = Matrix4x4.Multiply(viewMatrix, projMatrix);
                break;
            }
        }

        protected override void PreUpdate(float state, Drawable key)
        {
            mesh = app.ForwardRender.GetMesh(key.mesh);
            commandList.Begin();
            commandList.SetFramebuffer(SceneFramebuffer);
            commandList.SetFullViewports();
            commandList.SetVertexBuffer(0, mesh.VertexBuffer);
            if (mesh.Indexed)
                commandList.SetIndexBuffer(mesh.IndexBuffer, IndexFormat.UInt32);
            commandList.SetPipeline(GbufferMaterial.GetPipeline(0));
            commandList.SetGraphicsResourceSet(0, app.MaterialManager.GeneralResourceSet);
            commandList.SetGraphicsResourceSet(1, GbufferMaterial.ResourceSet);
        }

        protected override void Update(float state, in Drawable key, in Entity entity)
        {
            var transform = entity.GetWorldTransform();
            var world = transform.ToMatrix();
            var normalWorld = world;
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
        }

        protected override void PostUpdate(float state)
        {
            SubmitAndWait();
        }

        public void SubmitAndWait()
        {
            app.GraphicsDevice.SubmitCommands(commandList);
            app.GraphicsDevice.WaitForIdle();
        }

        private static bool CanRender(in Render render)
        {
            return render.Screen;
        }

        private void DisposeScreenTargets()
        {
            Texture?.Dispose();
            SceneFramebuffer?.Dispose();
            ScreenFramebuffer?.Dispose();
        }
    }
}