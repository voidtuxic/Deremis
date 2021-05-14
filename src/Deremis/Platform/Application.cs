using System;
using System.Collections.Generic;
using System.Numerics;
using DefaultEcs;
using DefaultEcs.System;
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
        public ScreenRenderSystem ScreenRender { get; private set; }
        public SSAOSystem SSAO { get; private set; }
        public CullSystem Cull { get; private set; }
        public IParallelRunner ParallelSystemRunner { get; private set; }
        public SequentialListSystem<float> MainSystem { get; private set; }

        public AssetManager AssetManager { get; private set; }
        public MaterialManager MaterialManager { get; private set; }
        private IContext context;

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
                // Debug = true,
#endif
            };
            GraphicsDevice = VeldridStartup.CreateGraphicsDevice(Window, options, GraphicsBackend.Direct3D11);
            Factory = GraphicsDevice.ResourceFactory;

            AssetManager = new AssetManager("./Assets/");
            MaterialManager = new MaterialManager(this);

            DefaultWorld = new World();
            ParallelSystemRunner = new DefaultParallelRunner(Environment.ProcessorCount);
            ScreenRender = new ScreenRenderSystem(this, DefaultWorld);
            ForwardRender = new ForwardRenderSystem(this, DefaultWorld);
            ShadowRender = new ShadowRenderSystem(this, DefaultWorld);
            SSAO = new SSAOSystem(this, DefaultWorld);
            Cull = new CullSystem(this, DefaultWorld, ParallelSystemRunner);
            MainSystem = new SequentialListSystem<float>();
            MainSystem.Add(Cull);
            MainSystem.Add(SSAO);
            MainSystem.Add(ShadowRender);
            MainSystem.Add(ForwardRender);
            MainSystem.Add(new ActionSystem<float>(ScreenRender.Update));

            ScreenRender.InitScreenData();
            SSAO.CreateResources();

            LoadDefaultAssets();

            context.Initialize(this);
        }

        private void LoadDefaultAssets()
        {
            AssetManager.Get<Shader>(new AssetDescription("Shaders/pbr.xml"));
            AssetManager.Get<Texture>(MissingTex);
            AssetManager.Get<Texture>(MissingNormalTex);

            var bloomRt = ScreenRender.GetRenderTexture(ScreenRenderSystem.BloomTextureName, ScreenRenderSystem.COLOR_PIXEL_FORMAT, 1);
            var bloomDepthRt = ScreenRender.GetRenderTexture("bloom_depth", ScreenRenderSystem.DEPTH_PIXEL_FORMAT, 1, true);
            var bloomFb = Factory.CreateFramebuffer(new FramebufferDescription(bloomDepthRt.RenderTarget.VeldridTexture, bloomRt.RenderTarget.VeldridTexture));
            ScreenRender.GetScreenPass("bloom", AssetManager.current.Get<Shader>(ScreenRenderSystem.BloomBlurShader), 1, bloomFb);
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

        public void Dispose()
        {
            MaterialManager.Dispose();
            AssetManager.Dispose();

            ParallelSystemRunner.Dispose();
            DefaultWorld.Dispose();

            GraphicsDevice.Dispose();
        }
    }
}