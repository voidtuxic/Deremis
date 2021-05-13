using System;
using System.Collections.Generic;
using System.Numerics;
using DefaultEcs;
using DefaultEcs.Threading;
using Deremis.Engine.Core;
using Deremis.Engine.Objects;
using Deremis.Engine.Rendering;
using Deremis.Engine.Systems;
using Deremis.Engine.Systems.Components;
using Deremis.Platform.Assets;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using Shader = Deremis.Engine.Objects.Shader;
using Texture = Deremis.Engine.Objects.Texture;

namespace Deremis.Platform
{
    public sealed class Application : IDisposable
    {
        public const PixelFormat COLOR_PIXEL_FORMAT = PixelFormat.R32_G32_B32_A32_Float;
        public const PixelFormat DEPTH_PIXEL_FORMAT = PixelFormat.R32_Float;
        public static AssetDescription MissingTex = new AssetDescription
        {
            name = "missing",
            path = "Textures/missing.tga"
        };
        public static AssetDescription MissingNormalTex = new AssetDescription
        {
            name = "missingn",
            path = "Textures/missingn.tga"
        };

        public static Application current;

        public Sdl2Window Window { get; private set; }
        public InputSnapshot InputSnapshot { get; private set; }

        public GraphicsDevice GraphicsDevice { get; private set; }
        public ResourceFactory Factory { get; private set; }

        public World DefaultWorld { get; private set; }
        public ForwardRenderSystem ForwardRender { get; private set; }
        public ShadowRenderSystem ShadowRender { get; private set; }
        public SSAOSystem SSAO { get; private set; }
        public CullSystem Cull { get; private set; }
        public IParallelRunner ParallelSystemRunner { get; private set; }
        public SequentialListSystem<float> MainSystem { get; private set; }
        private int entityCounter = 0;

        public AssetManager AssetManager { get; private set; }
        public MaterialManager MaterialManager { get; private set; }
        private IContext context;

        private Veldrid.Texture screenColorTexture;
        public Veldrid.Texture ScreenDepthTexture { get; private set; }
        public Framebuffer ScreenFramebuffer { get; private set; }
        public Texture CopyTexture { get; private set; }
        public Texture DepthCopyTexture { get; private set; }
        public TextureSampleCount MSAA { get; set; } = TextureSampleCount.Count4;

        private readonly Dictionary<string, RenderTexture> renderTextures = new Dictionary<string, RenderTexture>();
        private readonly Dictionary<Shader, Framebuffer> deferredFramebuffers = new Dictionary<Shader, Framebuffer>();

        public float SSAA => 1f;
        public uint Width => (uint)(Window.Width * SSAA);
        public uint Height => (uint)(Window.Height * SSAA);
        public float AspectRatio => (float)Width / (float)Height;

        public Application(string[] args, IContext context)
        {
            if (current != null)
            {
                // log duplicate, throw exception
            }
            current = this;
            this.context = context;
            Initialize();
        }

        private void Initialize()
        {
            WindowCreateInfo windowCI = new WindowCreateInfo()
            {
                X = 100,
                Y = 100,
                WindowWidth = 1920,
                WindowHeight = 1080,
                WindowTitle = "Deremis",
            };
            Window = VeldridStartup.CreateWindow(ref windowCI);

            GraphicsDeviceOptions options = new GraphicsDeviceOptions
            {
                PreferStandardClipSpaceYDirection = true,
                PreferDepthRangeZeroToOne = true,
                SyncToVerticalBlank = true,
                SwapchainSrgbFormat = true,
#if DEBUG
                Debug = true,
#endif
            };
            GraphicsDevice = VeldridStartup.CreateGraphicsDevice(Window, options, GraphicsBackend.Direct3D11);
            Factory = GraphicsDevice.ResourceFactory;

            CreateRenderContext();

            AssetManager = new AssetManager("./Assets/");
            MaterialManager = new MaterialManager(this);

            DefaultWorld = new World();
            ParallelSystemRunner = new DefaultParallelRunner(Environment.ProcessorCount);
            ForwardRender = new ForwardRenderSystem(this, DefaultWorld);
            ShadowRender = new ShadowRenderSystem(this, DefaultWorld);
            SSAO = new SSAOSystem(this, DefaultWorld);
            Cull = new CullSystem(this, DefaultWorld, ParallelSystemRunner);
            MainSystem = new SequentialListSystem<float>();
            MainSystem.Add(Cull);
            MainSystem.Add(SSAO);
            MainSystem.Add(ShadowRender);
            MainSystem.Add(ForwardRender);

            LoadDefaultAssets();

            context.Initialize(this);
        }

        public void CreateRenderContext()
        {
            DisposeScreenTargets();

            TextureDescription colorTextureDescription = TextureDescription.Texture2D(
                Width, Height, 1, 1,
                COLOR_PIXEL_FORMAT, TextureUsage.RenderTarget, MSAA);
            screenColorTexture = Factory.CreateTexture(ref colorTextureDescription);
            screenColorTexture.Name = "screenColor";
            ScreenDepthTexture = Factory.CreateTexture(TextureDescription.Texture2D(
                Width, Height, 1, 1,
                DEPTH_PIXEL_FORMAT, TextureUsage.DepthStencil, MSAA));
            ScreenDepthTexture.Name = "screenDepth";
            var bloomRt = GetRenderTexture(ForwardRenderSystem.BloomTextureName, COLOR_PIXEL_FORMAT);
            ScreenFramebuffer = Factory.CreateFramebuffer(new FramebufferDescription(ScreenDepthTexture, screenColorTexture, bloomRt.RenderTarget.VeldridTexture));

            var copyTexture = Factory.CreateTexture(TextureDescription.Texture2D(
                Width, Height, 1, 1,
                COLOR_PIXEL_FORMAT, TextureUsage.Storage | TextureUsage.Sampled, TextureSampleCount.Count1));
            copyTexture.Name = "screenColorCopy";
            CopyTexture = new Texture(copyTexture.Name, copyTexture, Factory.CreateTextureView(copyTexture));
            var depthCopyTexture = Factory.CreateTexture(TextureDescription.Texture2D(
                Width, Height, 1, 1,
                DEPTH_PIXEL_FORMAT, TextureUsage.Storage | TextureUsage.Sampled, TextureSampleCount.Count1));
            depthCopyTexture.Name = "screenDepthCopy";
            DepthCopyTexture = new Texture(depthCopyTexture.Name, depthCopyTexture, Factory.CreateTextureView(depthCopyTexture));

        }

        private void LoadDefaultAssets()
        {
            AssetManager.Get<Shader>(new AssetDescription("Shaders/pbr.xml"));
            AssetManager.Get<Texture>(MissingTex);
            AssetManager.Get<Texture>(MissingNormalTex);

            var bloomRt = GetRenderTexture(ForwardRenderSystem.BloomTextureName, Application.COLOR_PIXEL_FORMAT);
            var bloomFb = Factory.CreateFramebuffer(new FramebufferDescription(ScreenDepthTexture, bloomRt.RenderTarget.VeldridTexture));
            GetScreenPass("bloom", AssetManager.current.Get<Shader>(ForwardRenderSystem.BloomBlurShader), bloomFb);
        }

        public void Run()
        {
            var lastTime = DateTime.Now;
            while (Window.Exists)
            {
                var now = DateTime.Now;
                var delta = (float)(now - lastTime).TotalSeconds;

                InputSnapshot = Window.PumpEvents();

                MainSystem.Update(delta);

                lastTime = now;
            }
        }

        public void GetDeferredFramebuffer(Shader shader, out Framebuffer fb, out List<TextureView> gbufferTextureViews)
        {
            gbufferTextureViews = new List<TextureView>();
            var colorTargets = new List<Veldrid.Texture>();
            for (int i = 0; i < shader.Outputs.Count; i++)
            {
                PixelFormat outputFormat = shader.Outputs[i].Item2;
                var rt = GetRenderTexture(shader.Outputs[i].Item1, outputFormat);
                colorTargets.Add(rt.RenderTarget.VeldridTexture);
                gbufferTextureViews.Add(rt.CopyTexture.View);
            }
            if (deferredFramebuffers.ContainsKey(shader))
            {
                fb = deferredFramebuffers[shader];
            }
            else
            {
                fb = Factory.CreateFramebuffer(new FramebufferDescription(ScreenDepthTexture, colorTargets.ToArray()));
                deferredFramebuffers.Add(shader, fb);
            }
        }

        public Material GetScreenPass(string name, Shader shader, Framebuffer fb = null)
        {
            var material = ForwardRender.GetScreenPass(name);
            if (material == null)
            {
                material = MaterialManager.CreateMaterial(name, shader);
                material.SetScreenSampler();
                var gbufferTextureViews = new List<TextureView>();
                var gbufferTextures = new List<Veldrid.Texture>();
                foreach (var resource in shader.Resources)
                {
                    if (resource.Key.Equals("screen"))
                    {
                        gbufferTextureViews.Add(CopyTexture.View);
                        continue;
                    }
                    if (resource.Value.Kind == ResourceKind.TextureReadOnly)
                    {
                        var rt = GetRenderTexture(resource.Key, COLOR_PIXEL_FORMAT);
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
                GraphicsDevice.WaitForIdle();
                ForwardRender.RegisterScreenPass(material);
            }
            return material;
        }

        public RenderTexture GetRenderTexture(string name, PixelFormat format, bool isDepth = false)
        {
            if (renderTextures.ContainsKey(name)) return renderTextures[name];
            var rt = new RenderTexture(this, name, Width, Height, format, isDepth);
            renderTextures.Add(name, rt);
            return rt;
        }

        public Entity CreateEntity(string name = "Entity")
        {
            var entity = DefaultWorld.CreateEntity();
            entity.Set(new Metadata { entityId = entityCounter, name = name });
            entityCounter++;
            return entity;
        }

        public Entity CreateTransform(string name)
        {
            var entity = CreateEntity(name);
            entity.Set(new Transform
            {
                position = Vector3.Zero,
                rotation = Quaternion.Identity,
                scale = Vector3.One
            });
            return entity;
        }

        public Entity CreateCamera(string name = "Camera", float fov = MathF.PI / 4f, float near = 0.1f, float far = 500)
        {
            var entity = CreateTransform(name);
            entity.Set(Camera.CreatePerspective(fov, Window.Width / (float)Window.Height, near, far));
            return entity;
        }

        public Entity CreateLight(string name = "Light", Vector3 color = default, int type = 0, float range = 1, float innerCutoff = 0, float outerCutoff = 0)
        {
            var entity = CreateTransform(name);
            entity.Set(new Light
            {
                color = color,
                type = type,
                range = range,
                innerCutoff = innerCutoff,
                outerCutoff = outerCutoff
            });
            return entity;
        }

        public Entity Spawn(string name, Mesh mesh, string materialName, bool shadows = true)
        {
            var material = MaterialManager.GetMaterial(materialName);
            var entity = CreateTransform(name);
            entity.Set(new Drawable
            {
                mesh = ForwardRender.RegisterMesh(mesh.Name, mesh),
                material = materialName
            });
            entity.Set(new Render(false, shadows));
            if (material.Shader.IsDeferred)
            {
                entity.Set(new Deferred());
            }
            if (shadows)
            {
                entity.Set(new ShadowMapped());
            }

            return entity;
        }

        public void SubmitAndWait(CommandList commandList)
        {
            GraphicsDevice.SubmitCommands(commandList);
            GraphicsDevice.WaitForIdle();
        }

        public void UpdateScreenTexture(CommandList commandList)
        {
            if (MSAA == TextureSampleCount.Count1)
            {
                TransferTexture(screenColorTexture, CopyTexture.VeldridTexture, commandList);
            }
            else
            {
                commandList.Begin();
                commandList.ResolveTexture(screenColorTexture, CopyTexture.VeldridTexture);
                commandList.End();
                SubmitAndWait(commandList);
            }
        }

        public void UpdateDepthTexture(CommandList commandList)
        {
            TransferTexture(ScreenDepthTexture, DepthCopyTexture.VeldridTexture, commandList);
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

        public void ClearDeferredFramebuffers(CommandList commandList)
        {
            if (deferredFramebuffers.Count == 0) return;
            commandList.Begin();
            foreach (var item in deferredFramebuffers)
            {
                commandList.SetFramebuffer(item.Value);
                for (uint i = 0; i < item.Key.Outputs.Count; i++)
                {
                    commandList.ClearColorTarget(i, RgbaFloat.Clear);
                }
            }
            commandList.End();
            SubmitAndWait(commandList);
        }

        public void Dispose()
        {
            DisposeScreenTargets();

            MaterialManager.Dispose();
            AssetManager.Dispose();

            ParallelSystemRunner.Dispose();
            DefaultWorld.Dispose();

            GraphicsDevice.Dispose();
        }

        private void DisposeScreenTargets()
        {
            foreach (var rt in renderTextures.Values)
            {
                rt.Dispose();
            }
            ScreenFramebuffer?.Dispose();
            screenColorTexture?.Dispose();
            ScreenDepthTexture?.Dispose();
            CopyTexture?.Dispose();
            DepthCopyTexture?.Dispose();
        }
    }
}