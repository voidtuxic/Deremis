using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Deremis.Engine.Objects;
using Deremis.Engine.Rendering.Resources;
using Deremis.Platform;
using Deremis.Platform.Assets;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Veldrid;
using Shader = Deremis.Engine.Objects.Shader;
using Texture = Deremis.Engine.Objects.Texture;

namespace Deremis.Engine.Rendering.Helpers
{
    public static class Skybox
    {
        public const string NAME = "skybox";
        public const int MAP_SIZE = 128;

        public static AssetDescription Shader = new AssetDescription
        {
            name = "skybox_hdr",
            path = "Shaders/skybox_hdr.xml"
        };
        public static AssetDescription IrradianceShader = new AssetDescription
        {
            name = "irradiance",
            path = "Shaders/irradiance.xml"
        };

        private static bool initialized = false;
        // ruthlessly pasted from https://learnopengl.com/code_viewer.php?code=advanced/cubemaps_skybox_data
        private static Vector3[] vertices = {
            new Vector3(-1.0f,  1.0f, -1.0f),
            new Vector3(-1.0f, -1.0f, -1.0f),
            new Vector3(1.0f, -1.0f, -1.0f),
            new Vector3(1.0f, -1.0f, -1.0f),
            new Vector3(1.0f,  1.0f, -1.0f),
            new Vector3(-1.0f,  1.0f, -1.0f),

            new Vector3(-1.0f, -1.0f,  1.0f),
            new Vector3(-1.0f, -1.0f, -1.0f),
            new Vector3(-1.0f,  1.0f, -1.0f),
            new Vector3(-1.0f,  1.0f, -1.0f),
            new Vector3(-1.0f,  1.0f,  1.0f),
            new Vector3(-1.0f, -1.0f,  1.0f),

            new Vector3(1.0f, -1.0f, -1.0f),
            new Vector3(1.0f, -1.0f,  1.0f),
            new Vector3(1.0f,  1.0f,  1.0f),
            new Vector3(1.0f,  1.0f,  1.0f),
            new Vector3(1.0f,  1.0f, -1.0f),
            new Vector3(1.0f, -1.0f, -1.0f),

            new Vector3(-1.0f, -1.0f,  1.0f),
            new Vector3(-1.0f,  1.0f,  1.0f),
            new Vector3(1.0f,  1.0f,  1.0f),
            new Vector3(1.0f,  1.0f,  1.0f),
            new Vector3(1.0f, -1.0f,  1.0f),
            new Vector3(-1.0f, -1.0f,  1.0f),

            new Vector3(-1.0f,  1.0f, -1.0f),
            new Vector3(1.0f,  1.0f, -1.0f),
            new Vector3(1.0f,  1.0f,  1.0f),
            new Vector3(1.0f,  1.0f,  1.0f),
            new Vector3(-1.0f,  1.0f,  1.0f),
            new Vector3(-1.0f,  1.0f, -1.0f),

            new Vector3(-1.0f, -1.0f, -1.0f),
            new Vector3(-1.0f, -1.0f,  1.0f),
            new Vector3(1.0f, -1.0f, -1.0f),
            new Vector3(1.0f, -1.0f, -1.0f),
            new Vector3(-1.0f, -1.0f,  1.0f),
            new Vector3(1.0f, -1.0f,  1.0f)
        };
        private static Matrix4x4 captureProjection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 2f, 1.0f, 0.1f, 10.0f);
        private static Matrix4x4[] captureViews = new[]
        {
            Matrix4x4.CreateLookAt(new Vector3(0.0f, 0.0f, 0.0f), new Vector3( 1.0f,  0.0f,  0.0f), new Vector3(0.0f, -1.0f,  0.0f)),
            Matrix4x4.CreateLookAt(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(-1.0f,  0.0f,  0.0f), new Vector3(0.0f, -1.0f,  0.0f)),
            Matrix4x4.CreateLookAt(new Vector3(0.0f, 0.0f, 0.0f), new Vector3( 0.0f,  1.0f,  0.0f), new Vector3(0.0f,  0.0f,  1.0f)),
            Matrix4x4.CreateLookAt(new Vector3(0.0f, 0.0f, 0.0f), new Vector3( 0.0f, -1.0f,  0.0f), new Vector3(0.0f,  0.0f, -1.0f)),
            Matrix4x4.CreateLookAt(new Vector3(0.0f, 0.0f, 0.0f), new Vector3( 0.0f,  0.0f,  1.0f), new Vector3(0.0f, -1.0f,  0.0f)),
            Matrix4x4.CreateLookAt(new Vector3(0.0f, 0.0f, 0.0f), new Vector3( 0.0f,  0.0f, -1.0f), new Vector3(0.0f, -1.0f,  0.0f))
        };
        private static Mesh mesh;
        private static Texture hdrIrradianceTexture;
        private static Texture hdrBRDFIntegrationTexture;

        public static Texture HDRIrradianceTexture => hdrIrradianceTexture;
        public static Texture HDRBRDFIntegrationTexture => hdrBRDFIntegrationTexture;

        public static Mesh GetMesh()
        {
            if (mesh != null) return mesh;

            mesh = new Mesh("skybox");
            mesh.Indexed = false;

            foreach (var vertex in vertices)
            {
                mesh.Add(new Vertex
                {
                    Position = vertex
                });
            }

            mesh.UpdateBuffers();
            return mesh;
        }

        public static void SetHDR(Scene scene, Texture hdr)
        {
            var material = scene.App.MaterialManager.GetMaterial(NAME);

            if (material == null)
                material = scene.App.MaterialManager.CreateMaterial(NAME, scene.App.AssetManager.Get<Shader>(Shader));

            material.SetSampler(Veldrid.SamplerDescription.Linear);
            material.SetTexture("skybox", hdr);
        }

        public static void Init(Scene scene, Texture hdr)
        {
            SetHDR(scene, hdr);
            if (!initialized)
            {
                var mesh = GetMesh();
                scene.Spawn(NAME, mesh, NAME, false);
                initialized = true;
                // BuildIBL(app, hdr);
            }
        }

        public static void BuildIBL(Application app, Texture hdr)
        {
            hdrIrradianceTexture?.Dispose();
            hdrBRDFIntegrationTexture?.Dispose();
            var depthTexture = app.Factory.CreateTexture(TextureDescription.Texture2D(
                MAP_SIZE, MAP_SIZE, 1, 1,
                Application.DEPTH_PIXEL_FORMAT, TextureUsage.DepthStencil, TextureSampleCount.Count1));
            depthTexture.Name = "irradiance_cubemap_depth";
            var cubemapTexture = app.Factory.CreateTexture(TextureDescription.Texture2D(
                MAP_SIZE, MAP_SIZE, 1, 1,
                PixelFormat.R8_G8_B8_A8_SNorm, TextureUsage.Sampled | TextureUsage.Cubemap, TextureSampleCount.Count1));
            var brdfIntegrationTexture = app.Factory.CreateTexture(TextureDescription.Texture2D(
                MAP_SIZE, MAP_SIZE, 1, 1,
                PixelFormat.R8_G8_B8_A8_SNorm, TextureUsage.Sampled | TextureUsage.Cubemap, TextureSampleCount.Count1));

            var textures = new Veldrid.Texture[6];
            var materials = new Material[6];
            var framebuffers = new Framebuffer[6];
            for (var i = 0; i < 6; i++)
            {
                TextureDescription colorTextureDescription = TextureDescription.Texture2D(
                    MAP_SIZE, MAP_SIZE, 1, 1,
                    PixelFormat.R8_G8_B8_A8_SNorm, TextureUsage.RenderTarget, TextureSampleCount.Count1);
                var texture = app.Factory.CreateTexture(ref colorTextureDescription);
                texture.Name = $"irradiance_cubemap{i}";
                colorTextureDescription.Usage = TextureUsage.Staging;
                textures[i] = texture;
                framebuffers[i] = app.Factory.CreateFramebuffer(new FramebufferDescription(depthTexture, textures[i]));
                materials[i] = app.MaterialManager.CreateMaterial("skybox_irradiance", app.AssetManager.Get<Shader>(IrradianceShader), framebuffers[i]);
                materials[i].SetTexture("skybox", hdr);

            }
            var commandList = app.Factory.CreateCommandList();
            var mesh = GetMesh();

            commandList.Begin();
            for (var i = 0; i < 6; i++)
            {
                commandList.SetFramebuffer(framebuffers[i]);
                commandList.SetFullViewport(0);

                commandList.UpdateBuffer(app.MaterialManager.MaterialBuffer, 0, materials[i].GetValueArray());
                commandList.SetVertexBuffer(0, mesh.VertexBuffer);
                commandList.SetPipeline(materials[i].GetPipeline(0));
                commandList.SetGraphicsResourceSet(0, app.MaterialManager.GeneralResourceSet);
                commandList.SetGraphicsResourceSet(1, materials[i].ResourceSet);
                commandList.UpdateBuffer(
                    app.MaterialManager.TransformBuffer,
                    0,
                    new TransformResource
                    {
                        viewMatrix = captureViews[i],
                        projMatrix = captureProjection
                    });
                commandList.Draw(
                    vertexCount: mesh.VertexCount,
                    instanceCount: 1,
                    vertexStart: 0,
                    instanceStart: 0);
            }
            commandList.End();
            app.GraphicsDevice.SubmitCommands(commandList);
            app.GraphicsDevice.WaitForIdle();


            commandList.Begin();
            for (var i = 0; i < 6; i++)
            {
                commandList.CopyTexture(
                    textures[i], 0, 0, 0, 0, 0,
                    cubemapTexture, 0, 0, 0, 0, (uint)i,
                    MAP_SIZE, MAP_SIZE, 1, 1);
            }
            commandList.End();
            app.GraphicsDevice.SubmitCommands(commandList);
            app.GraphicsDevice.WaitForIdle();

            hdrIrradianceTexture = new Texture("irradiance_cubemap", cubemapTexture, app.Factory.CreateTextureView(cubemapTexture));
            hdrBRDFIntegrationTexture = new Texture("irradiance_brdf_integration", brdfIntegrationTexture, app.Factory.CreateTextureView(brdfIntegrationTexture));

            commandList.Dispose();
            for (var i = 0; i < 6; i++)
            {
                framebuffers[i].Dispose();
                textures[i].Dispose();
            }
            depthTexture.Dispose();
        }
    }
}