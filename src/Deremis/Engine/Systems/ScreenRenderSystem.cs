using System.Collections.Generic;
using System.Numerics;
using DefaultEcs;
using Deremis.Engine.Objects;
using Deremis.Platform;
using Deremis.Platform.Assets;
using Veldrid;
using Shader = Deremis.Engine.Objects.Shader;

namespace Deremis.Engine.Systems
{
    public class ScreenRenderSystem
    {
        public const string BloomTextureName = "bloomTex";
        public static AssetDescription ScreenShader = new AssetDescription
        {
            name = "screen_postprocess",
            path = "Shaders/screen/postprocess.xml"
        };

        public static AssetDescription BloomBlurShader = new AssetDescription
        {
            name = "bloom_blur",
            path = "Shaders/screen/bloom_blur.xml"
        };

        private readonly Application app;
        private readonly World world;
        private readonly CommandList commandList;

        private Material screenRenderMaterial;
        public Mesh ScreenRenderMesh { get; private set; }
        private readonly Dictionary<string, Material> screenPassMaterials = new Dictionary<string, Material>();

        public ScreenRenderSystem(Application app, World world)
        {
            this.world = world;
            this.app = app;
            commandList = app.Factory.CreateCommandList();
            commandList.Name = "ScreenCommandList";
            InitScreenData();
        }

        private void InitScreenData()
        {
            ScreenRenderMesh = new Mesh("screen");
            ScreenRenderMesh.Indexed = false;
            ScreenRenderMesh.Add(new Rendering.Vertex
            {
                Position = new Vector3(-1, 1, 0),
                UV = new Vector2(0, 0)
            });
            ScreenRenderMesh.Add(new Rendering.Vertex
            {
                Position = new Vector3(-1, -1, 0),
                UV = new Vector2(0, 1)
            });
            ScreenRenderMesh.Add(new Rendering.Vertex
            {
                Position = new Vector3(1, -1, 0),
                UV = new Vector2(1, 1)
            });
            ScreenRenderMesh.Add(new Rendering.Vertex
            {
                Position = new Vector3(-1, 1, 0),
                UV = new Vector2(0, 0)
            });
            ScreenRenderMesh.Add(new Rendering.Vertex
            {
                Position = new Vector3(1, -1, 0),
                UV = new Vector2(1, 1)
            });
            ScreenRenderMesh.Add(new Rendering.Vertex
            {
                Position = new Vector3(1, 1, 0),
                UV = new Vector2(1, 0)
            });
            ScreenRenderMesh.UpdateBuffers();
            screenRenderMaterial = app.MaterialManager.CreateMaterial(
                ScreenShader.name,
                app.AssetManager.Get<Shader>(ScreenShader),
                app.GraphicsDevice.SwapchainFramebuffer);
            screenRenderMaterial.SetTexture("screenTex", app.CopyTexture);
            screenRenderMaterial.SetSampler(new SamplerDescription
            {
                AddressModeU = SamplerAddressMode.Border,
                AddressModeV = SamplerAddressMode.Border,
                AddressModeW = SamplerAddressMode.Border,
                Filter = SamplerFilter.Anisotropic,
                LodBias = 0,
                MinimumLod = 0,
                MaximumAnisotropy = 16,
                MaximumLod = uint.MaxValue,
                BorderColor = SamplerBorderColor.TransparentBlack
            });
        }

        public Material GetScreenPass(string name)
        {
            if (screenPassMaterials.ContainsKey(name)) return screenPassMaterials[name];

            return null;
        }

        public void RegisterScreenPass(Material material)
        {
            screenPassMaterials.TryAdd(material.Name, material);
        }

        public void Update(float deltaSeconds)
        {
            if (screenPassMaterials.Count != 0)
            {
                foreach (var material in screenPassMaterials.Values)
                {
                    app.UpdateRenderTextures(commandList, material.Shader.PassColorTargetBaseName);
                    UpdateScreenBuffer(material, material.Framebuffer);
                }
                app.UpdateScreenTexture(commandList);
            }

            UpdateScreenBuffer(screenRenderMaterial, app.GraphicsDevice.SwapchainFramebuffer);
            app.GraphicsDevice.SwapBuffers();
        }

        private void UpdateScreenBuffer(Material material, Framebuffer framebuffer)
        {
            commandList.Begin();
            commandList.UpdateBuffer(app.MaterialManager.MaterialBuffer, 0, material.GetValueArray());
            commandList.End();

            for (var i = 0; i < material.Shader.PassCount; i++)
            {
                commandList.Begin();
                bool isLastPass = i == material.Shader.PassCount - 1;
                commandList.SetFramebuffer(isLastPass ? framebuffer : material.PassFramebuffer);
                commandList.SetFullViewports();

                commandList.SetVertexBuffer(0, ScreenRenderMesh.VertexBuffer);
                commandList.SetPipeline(material.GetPipeline(i));
                commandList.SetGraphicsResourceSet(0, app.MaterialManager.GeneralResourceSet);
                commandList.SetGraphicsResourceSet(1, material.ResourceSet);

                commandList.Draw(
                    vertexCount: ScreenRenderMesh.VertexCount,
                    instanceCount: 1,
                    vertexStart: 0,
                    instanceStart: 0);
                commandList.End();
                SubmitAndWait();
                app.UpdateRenderTextures(commandList, material.Shader.PassColorTargetBaseName);
            }
        }

        public void SubmitAndWait()
        {
            app.GraphicsDevice.SubmitCommands(commandList);
            app.GraphicsDevice.WaitForIdle();
        }
    }
}