using System;
using System.Collections.Generic;
using System.Numerics;
using DefaultEcs;
using DefaultEcs.System;
using Deremis.Engine.Math;
using Deremis.Engine.Objects;
using Deremis.Engine.Rendering.Resources;
using Deremis.Engine.Systems.Components;
using Deremis.Engine.Systems.Extensions;
using Deremis.Platform;
using Deremis.Platform.Assets;
using Tedd.RandomUtils;
using Veldrid;
using Shader = Deremis.Engine.Objects.Shader;
using Texture = Deremis.Engine.Objects.Texture;

namespace Deremis.Engine.Systems
{
    public class SSAOSystem : DrawableSystem
    {
        public const string RenderTextureName = "ssaoTex";
        public const float TextureScale = 2f;
        public static AssetDescription GbufferShader = new AssetDescription
        {
            name = "ssao_gbuffer",
            path = "Shaders/ssao_gbuffer.xml"
        };
        public static AssetDescription ScreenShader = new AssetDescription
        {
            name = "ssao_screen",
            path = "Shaders/screen/ssao.xml"
        };
        public static AssetDescription BlurShader = new AssetDescription
        {
            name = "ssao_screen_blur",
            path = "Shaders/screen/ssao_blur.xml"
        };
        public static AssetDescription NoiseTexture = new AssetDescription
        {
            name = "noise",
            path = "Textures/noise.png"
        };

        public Framebuffer SceneFramebuffer { get; private set; }
        public Framebuffer ScreenFramebuffer { get; private set; }
        public Texture Texture { get; private set; }
        public Material GbufferMaterial { get; private set; }
        public List<Material> ScreenMaterials { get; private set; } = new List<Material>();

        private readonly EntitySet cameraSet;
        private Transform mainCameraTransform;
        private Matrix4x4 viewMatrix;
        private Matrix4x4 viewProjMatrix;
        private Matrix4x4 projMatrix;
        private Mesh mesh;

        public SSAOSystem(Application app, World world) : base(app, world,
            world.GetEntities()
                .With<Drawable>()
                .With<Transform>()
                .With<Render>(CanRenderToScreen)
                .AsMultiMap<Drawable>())
        {
            cameraSet = world.GetEntities()
                .With<Camera>()
                .With<Transform>()
                .AsSet();
        }

        public void CreateResources()
        {
            DisposeScreenTargets();

            var renderDepthTex = app.ScreenRender.GetRenderTexture("ssao_depth", ScreenRenderSystem.DEPTH_PIXEL_FORMAT, TextureScale, true);
            var positionRt = app.ScreenRender.GetRenderTexture("ssao_position", ScreenRenderSystem.COLOR_PIXEL_FORMAT, TextureScale);
            var normalRt = app.ScreenRender.GetRenderTexture("ssao_normal", ScreenRenderSystem.COLOR_PIXEL_FORMAT, TextureScale);
            SceneFramebuffer = app.Factory.CreateFramebuffer(new FramebufferDescription(renderDepthTex.RenderTarget.VeldridTexture, positionRt.RenderTarget.VeldridTexture, normalRt.RenderTarget.VeldridTexture));

            var renderTex = app.ScreenRender.GetRenderTexture(RenderTextureName, ScreenRenderSystem.COLOR_PIXEL_FORMAT, TextureScale);
            ScreenFramebuffer = app.Factory.CreateFramebuffer(new FramebufferDescription(renderDepthTex.RenderTarget.VeldridTexture, renderTex.RenderTarget.VeldridTexture));
            Texture = renderTex.CopyTexture;

            GbufferMaterial = app.MaterialManager.CreateMaterial(
                "ssao_gbuffer",
                app.AssetManager.Get<Shader>(GbufferShader),
                SceneFramebuffer);
            var ssaoMat = app.MaterialManager.CreateMaterial(
                "ssao_screen",
                app.AssetManager.Get<Shader>(ScreenShader),
                ScreenFramebuffer);
            ssaoMat.SetTexture("positionTex", positionRt.CopyTexture);
            ssaoMat.SetTexture("normalTex", normalRt.CopyTexture);

            var ssaoBlurMat = app.MaterialManager.CreateMaterial(
                "ssao_screen_blur",
                app.AssetManager.Get<Shader>(BlurShader),
                ScreenFramebuffer);
            ssaoBlurMat.SetTexture(RenderTextureName, renderTex.CopyTexture);
            ssaoBlurMat.SetupMultipass(ScreenFramebuffer);

            ScreenMaterials.Add(ssaoMat);
            ScreenMaterials.Add(ssaoBlurMat);
        }

        protected override void PreUpdate(float state)
        {
            mainCommandList.Begin();
            mainCommandList.SetFramebuffer(SceneFramebuffer);
            mainCommandList.SetFullViewports();
            mainCommandList.ClearColorTarget(0, RgbaFloat.Clear);
            mainCommandList.ClearColorTarget(1, RgbaFloat.Clear);
            mainCommandList.ClearDepthStencil(1f);
            mainCommandList.UpdateBuffer(app.MaterialManager.MaterialBuffer, 0, GbufferMaterial.GetValueArray());
            mainCommandList.End();
            SubmitAndWait();

            Span<Entity> cameras = stackalloc Entity[cameraSet.Count];
            cameraSet.GetEntities().CopyTo(cameras);
            foreach (ref readonly Entity camEntity in cameras)
            {
                ref var transform = ref camEntity.Get<Transform>();
                mainCameraTransform = transform;
                viewMatrix = transform.ToViewMatrix();
                projMatrix = camEntity.Get<Camera>().projection;
                viewProjMatrix = Matrix4x4.Multiply(viewMatrix, projMatrix);
                break;
            }
        }

        protected override void PreUpdate(float state, Drawable key)
        {
            mesh = app.ForwardRender.GetMesh(key.mesh);
            mainCommandList.Begin();
            mainCommandList.SetFramebuffer(SceneFramebuffer);
            mainCommandList.SetFullViewports();
            mainCommandList.SetVertexBuffer(0, mesh.VertexBuffer);
            if (mesh.Indexed)
                mainCommandList.SetIndexBuffer(mesh.IndexBuffer, IndexFormat.UInt32);
            mainCommandList.SetPipeline(GbufferMaterial.GetPipeline(0));
            mainCommandList.SetGraphicsResourceSet(0, app.MaterialManager.GeneralResourceSet);
            mainCommandList.SetGraphicsResourceSet(1, GbufferMaterial.ResourceSet);
        }

        protected override void Update(float state, in Drawable key, in Entity entity)
        {
            if (key.mesh.Equals(Rendering.Helpers.Skybox.NAME)) return;
            var transform = entity.GetWorldTransform();
            var world = transform.ToMatrix();
            var normalWorld = world;
            if (Matrix4x4.Invert(world, out normalWorld))
            {
                normalWorld = Matrix4x4.Transpose(normalWorld);
            }
            mainCommandList.UpdateBuffer(
                app.MaterialManager.TransformBuffer,
                0,
                new TransformResource
                {
                    viewProjMatrix = viewProjMatrix,
                    viewMatrix = viewMatrix,
                    projMatrix = projMatrix,
                    worldMatrix = world,
                    normalWorldMatrix = normalWorld,
                });
            if (mesh.Indexed)
                mainCommandList.DrawIndexed(
                    indexCount: mesh.IndexCount,
                    instanceCount: 1,
                    indexStart: 0,
                    vertexOffset: 0,
                    instanceStart: 0);
            else
                mainCommandList.Draw(
                    vertexCount: mesh.VertexCount,
                    instanceCount: 1,
                    vertexStart: 0,
                    instanceStart: 0);
        }

        protected override void PostUpdate(float state, Drawable key)
        {
            mainCommandList.End();
            app.GraphicsDevice.SubmitCommands(mainCommandList);
        }

        protected override void PostUpdate(float state)
        {
            app.GraphicsDevice.WaitForIdle();
            app.ScreenRender.UpdateRenderTextures(mainCommandList, "ssao_position", "ssao_normal");

            mainCommandList.Begin();

            mainCommandList.UpdateBuffer(
                app.MaterialManager.TransformBuffer,
                0,
                new TransformResource
                {
                    viewProjMatrix = viewProjMatrix,
                    viewMatrix = viewMatrix,
                    projMatrix = projMatrix
                });
            mainCommandList.End();

            for (var i = 0; i < ScreenMaterials.Count; i++)
            {
                for (var j = 0; j < ScreenMaterials[i].Shader.PassCount; j++)
                {
                    mainCommandList.Begin();
                    mainCommandList.SetFramebuffer(ScreenFramebuffer);
                    mainCommandList.SetFullViewports();
                    if (i == 0) mainCommandList.ClearColorTarget(0, RgbaFloat.Clear);
                    if (j == 0) mainCommandList.UpdateBuffer(app.MaterialManager.MaterialBuffer, 0, ScreenMaterials[i].GetValueArray());

                    mainCommandList.SetVertexBuffer(0, app.ScreenRender.ScreenRenderMesh.VertexBuffer);
                    mainCommandList.SetPipeline(ScreenMaterials[i].GetPipeline(j));
                    mainCommandList.SetGraphicsResourceSet(0, app.MaterialManager.GeneralResourceSet);
                    mainCommandList.SetGraphicsResourceSet(1, ScreenMaterials[i].ResourceSet);

                    mainCommandList.Draw(
                        vertexCount: app.ScreenRender.ScreenRenderMesh.VertexCount,
                        instanceCount: 1,
                        vertexStart: 0,
                        instanceStart: 0);
                    mainCommandList.End();
                    SubmitAndWait();
                    app.ScreenRender.UpdateRenderTextures(mainCommandList, RenderTextureName);
                }
            }
            app.GraphicsDevice.WaitForIdle();
        }

        public void SubmitAndWait()
        {
            app.GraphicsDevice.SubmitCommands(mainCommandList);
            app.GraphicsDevice.WaitForIdle();
        }

        private void DisposeScreenTargets()
        {
            Texture?.Dispose();
            SceneFramebuffer?.Dispose();
            ScreenFramebuffer?.Dispose();
        }
    }
}