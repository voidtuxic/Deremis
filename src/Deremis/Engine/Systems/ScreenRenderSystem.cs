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
        public const PixelFormat COLOR_PIXEL_FORMAT = PixelFormat.R32_G32_B32_A32_Float;
        public const PixelFormat DEPTH_PIXEL_FORMAT = PixelFormat.R32_Float;
        public const string BloomTextureName = "bloomTex";
        public const float TextureScale = 1;
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

        public RenderTexture ScreenColorTexture { get; private set; }
        public RenderTexture ScreenDepthTexture { get; private set; }
        public Framebuffer ScreenFramebuffer { get; private set; }
        public TextureSampleCount MSAA { get; set; } = TextureSampleCount.Count4;

        private readonly Application app;
        private readonly World world;
        private readonly CommandList commandList;

        private Material screenRenderMaterial;
        public Mesh ScreenRenderMesh { get; private set; }

        public Application App => app;

        private readonly Dictionary<string, Material> screenPassMaterials = new Dictionary<string, Material>();
        private readonly Dictionary<string, RenderTexture> renderTextures = new Dictionary<string, RenderTexture>();

        public ScreenRenderSystem(Application app, World world)
        {
            this.world = world;
            this.app = app;
            commandList = app.Factory.CreateCommandList();
            commandList.Name = "ScreenCommandList";
        }

        private void CreateRenderContext()
        {
            DisposeScreenTargets();

            ScreenColorTexture = GetRenderTexture("screen_color", COLOR_PIXEL_FORMAT, 1);
            ScreenDepthTexture = GetRenderTexture("screen_depth", DEPTH_PIXEL_FORMAT, 1, true);
            var bloomRt = GetRenderTexture(ScreenRenderSystem.BloomTextureName, COLOR_PIXEL_FORMAT, 1);

            ScreenFramebuffer = app.Factory.CreateFramebuffer(new FramebufferDescription(
                ScreenDepthTexture.RenderTarget.VeldridTexture,
                ScreenColorTexture.RenderTarget.VeldridTexture,
                bloomRt.RenderTarget.VeldridTexture));
        }

        public void InitScreenData()
        {
            CreateRenderContext();

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
            screenRenderMaterial.SetTexture("screenTex", ScreenColorTexture.CopyTexture);
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

        public void Update(float deltaSeconds)
        {
            if (screenPassMaterials.Count != 0)
            {
                foreach (var material in screenPassMaterials.Values)
                {
                    UpdateRenderTextures(commandList, material.Shader.PassColorTargetBaseName);
                    UpdateScreenBuffer(material, material.Framebuffer);
                }
                UpdateScreenTexture(commandList);
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
                commandList.SetFramebuffer(framebuffer);
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
                UpdateRenderTextures(commandList, material.Shader.PassColorTargetBaseName);
            }
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

        public Material GetScreenPass(string name, Shader shader, float scale, Framebuffer fb = null)
        {
            var material = GetScreenPass(name);
            if (material == null)
            {
                material = app.MaterialManager.CreateMaterial(name, shader);
                material.SetScreenSampler();
                var gbufferTextureViews = new List<TextureView>();
                var gbufferTextures = new List<Veldrid.Texture>();
                foreach (var resource in shader.Resources)
                {
                    if (resource.Key.Equals("screen"))
                    {
                        gbufferTextureViews.Add(ScreenColorTexture.CopyTexture.View);
                        continue;
                    }
                    if (resource.Value.Kind == ResourceKind.TextureReadOnly)
                    {
                        var rt = GetRenderTexture(resource.Key, COLOR_PIXEL_FORMAT, scale);
                        if (material.Shader.IsMultipass && resource.Key.Contains(material.Shader.PassColorTargetBaseName))
                        {
                            gbufferTextures.Add(rt.RenderTarget.VeldridTexture);
                        }
                        gbufferTextureViews.Add(rt.CopyTexture.View);
                    }
                }
                material.Build(fb ?? ScreenFramebuffer, gbufferTextureViews);
                if (material.Shader.IsMultipass)
                {
                    material.SetupMultipass(gbufferTextures);
                }
                app.GraphicsDevice.WaitForIdle();
                RegisterScreenPass(material);
            }
            return material;
        }

        public RenderTexture GetRenderTexture(string name, PixelFormat format, float scale, bool isDepth = false)
        {
            if (renderTextures.ContainsKey(name)) return renderTextures[name];
            var rt = new RenderTexture(this, name, (uint)(app.Width / scale), (uint)(app.Height / scale), format, isDepth);
            renderTextures.Add(name, rt);
            return rt;
        }

        public void UpdateScreenTexture(CommandList commandList)
        {
            UpdateRenderTextures(commandList, "screen_color");
        }

        public void UpdateDepthTexture(CommandList commandList)
        {
            UpdateRenderTextures(commandList, "screen_depth");
        }

        public void TransferTexture(Veldrid.Texture left, Veldrid.Texture right, CommandList commandList)
        {
            commandList.Begin();
            commandList.CopyTexture(left, right);
            commandList.End();
            SubmitAndWait(commandList);
        }

        public void UpdateRenderTextures(CommandList commandList, params string[] limit)
        {
            if (renderTextures.Count == 0) return;
            commandList.Begin();
            if (limit.Length == 0)
            {
                foreach (var rt in renderTextures.Values)
                {
                    rt.UpdateCopyTexture(commandList);
                }
            }
            else
            {
                foreach (var rt in limit)
                {
                    if (rt == null) continue;
                    renderTextures[rt].UpdateCopyTexture(commandList);
                }
            }
            commandList.End();
            SubmitAndWait(commandList);
        }

        // public void GetDeferredFramebuffer(Shader shader, out Framebuffer fb, out List<TextureView> gbufferTextureViews)
        // {
        //     gbufferTextureViews = new List<TextureView>();
        //     var colorTargets = new List<Veldrid.Texture>();
        //     for (int i = 0; i < shader.Outputs.Count; i++)
        //     {
        //         PixelFormat outputFormat = shader.Outputs[i].Item2;
        //         var rt = GetRenderTexture(shader.Outputs[i].Item1, outputFormat);
        //         colorTargets.Add(rt.RenderTarget.VeldridTexture);
        //         gbufferTextureViews.Add(rt.CopyTexture.View);
        //     }
        //     if (deferredFramebuffers.ContainsKey(shader))
        //     {
        //         fb = deferredFramebuffers[shader];
        //     }
        //     else
        //     {
        //         fb = Factory.CreateFramebuffer(new FramebufferDescription(ScreenDepthTexture, colorTargets.ToArray()));
        //         deferredFramebuffers.Add(shader, fb);
        //     }
        // }

        // public void ClearDeferredFramebuffers(CommandList commandList)
        // {
        //     if (deferredFramebuffers.Count == 0) return;
        //     commandList.Begin();
        //     foreach (var item in deferredFramebuffers)
        //     {
        //         commandList.SetFramebuffer(item.Value);
        //         for (uint i = 0; i < item.Key.Outputs.Count; i++)
        //         {
        //             commandList.ClearColorTarget(i, RgbaFloat.Clear);
        //         }
        //     }
        //     commandList.End();
        //     SubmitAndWait(commandList);
        // }

        public void SubmitAndWait()
        {
            app.GraphicsDevice.SubmitCommands(commandList);
            app.GraphicsDevice.WaitForIdle();
        }

        public void SubmitAndWait(CommandList otherList)
        {
            app.GraphicsDevice.SubmitCommands(otherList);
            app.GraphicsDevice.WaitForIdle();
        }

        private void DisposeScreenTargets()
        {
            foreach (var rt in renderTextures.Values)
            {
                rt.Dispose();
            }
            ScreenFramebuffer?.Dispose();
            ScreenColorTexture?.Dispose();
            ScreenDepthTexture?.Dispose();
        }
    }
}