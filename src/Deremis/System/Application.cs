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
using Deremis.System.Assets;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using Shader = Deremis.Engine.Objects.Shader;
using Texture = Deremis.Engine.Objects.Texture;

namespace Deremis.System
{
    public sealed class Application : IDisposable
    {
        public const PixelFormat COLOR_PIXEL_FORMAT = PixelFormat.R32_G32_B32_A32_Float;
        public const PixelFormat DEPTH_PIXEL_FORMAT = PixelFormat.R16_UNorm;
        public static AssetDescription MissingTex = new AssetDescription
        {
            name = "missing",
            path = "Textures/missing.tga",
            type = 2
        };

        public static Application current;

        private Sdl2Window window;

        public GraphicsDevice GraphicsDevice { get; private set; }
        public ResourceFactory Factory { get; private set; }

        public World DefaultWorld { get; private set; }
        public RenderSystem Render { get; private set; }
        public IParallelRunner ParallelSystemRunner { get; private set; }
        public SequentialListSystem<float> MainSystem { get; private set; }
        private int entityCounter = 0;

        public AssetManager AssetManager { get; private set; }
        public MaterialManager MaterialManager { get; private set; }
        private IContext context;

        private Veldrid.Texture screenColorTexture;
        public Veldrid.Texture ScreenDepthTexture { get; private set; }
        public Framebuffer ScreenFramebuffer { get; private set; }
        private Veldrid.Texture copyTexture;
        public TextureView CopyView { get; private set; }
        private Veldrid.Texture depthCopyTexture;
        public TextureView DepthCopyView { get; private set; }

        private readonly List<RenderTexture> renderTextures = new List<RenderTexture>();

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
                WindowWidth = 1600,
                WindowHeight = 1080,
                WindowTitle = "Deremis"
            };
            window = VeldridStartup.CreateWindow(ref windowCI);

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
            GraphicsDevice = VeldridStartup.CreateGraphicsDevice(window, options, GraphicsBackend.Direct3D11);
            Factory = GraphicsDevice.ResourceFactory;

            CreateRenderContext();

            AssetManager = new AssetManager("./Assets/");
            MaterialManager = new MaterialManager(this);

            DefaultWorld = new World();
            Render = new RenderSystem(this, DefaultWorld);
            ParallelSystemRunner = new DefaultParallelRunner(Environment.ProcessorCount);
            MainSystem = new SequentialListSystem<float>();
            MainSystem.Add(Render);

            LoadDefaultAssets();

            context.Initialize(this);
        }

        public void CreateRenderContext()
        {
            DisposeScreenTargets();

            TextureDescription colorTextureDescription = TextureDescription.Texture2D(
                (uint)window.Width, (uint)window.Height, 1, 1,
                COLOR_PIXEL_FORMAT, TextureUsage.RenderTarget, TextureSampleCount.Count1);
            screenColorTexture = Factory.CreateTexture(ref colorTextureDescription);
            screenColorTexture.Name = "screenColor";
            ScreenDepthTexture = Factory.CreateTexture(TextureDescription.Texture2D(
                (uint)window.Width, (uint)window.Height, 1, 1,
                DEPTH_PIXEL_FORMAT, TextureUsage.DepthStencil, TextureSampleCount.Count1));
            ScreenDepthTexture.Name = "screenDepth";
            ScreenFramebuffer = Factory.CreateFramebuffer(new FramebufferDescription(ScreenDepthTexture, screenColorTexture));

            copyTexture = Factory.CreateTexture(TextureDescription.Texture2D(
                (uint)window.Width, (uint)window.Height, 1, 1,
                COLOR_PIXEL_FORMAT, TextureUsage.Storage | TextureUsage.Sampled, TextureSampleCount.Count1));
            copyTexture.Name = "screenColorCopy";
            CopyView = Factory.CreateTextureView(copyTexture);
            depthCopyTexture = Factory.CreateTexture(TextureDescription.Texture2D(
                (uint)window.Width, (uint)window.Height, 1, 1,
                DEPTH_PIXEL_FORMAT, TextureUsage.Storage | TextureUsage.Sampled, TextureSampleCount.Count1));
            depthCopyTexture.Name = "screenDepthCopy";
            DepthCopyView = Factory.CreateTextureView(depthCopyTexture);
        }

        private void LoadDefaultAssets()
        {
            AssetManager.Get<Shader>(new AssetDescription("Shaders/phong.xml", 1));
            AssetManager.Get<Texture>(MissingTex);
        }

        public void Run()
        {
            var lastTime = DateTime.Now;
            while (window.Exists)
            {
                var now = DateTime.Now;
                var delta = (float)(now - lastTime).TotalSeconds;

                window.PumpEvents();

                MainSystem.Update(delta);

                lastTime = now;
            }
        }

        public RenderTexture CreateRenderTexture(string name, PixelFormat format, bool isDepth = false)
        {
            var rt = new RenderTexture(this, name, (uint)window.Width, (uint)window.Height, format, isDepth);
            renderTextures.Add(rt);
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

        public Entity CreateCamera(string name = "Camera", float fov = MathF.PI / 4f, float near = 0.1f, float far = 1000)
        {
            var entity = CreateTransform(name);
            entity.Set(Camera.CreatePerspective(fov, window.Width / (float)window.Height, near, far));
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

        public Entity Spawn(string name, Mesh mesh, string materialName)
        {
            var material = MaterialManager.GetMaterial(materialName);
            var entity = CreateTransform(name);
            entity.Set(new Drawable
            {
                mesh = Render.RegisterMesh(mesh.Name, mesh),
                material = materialName
            });
            if (material.Shader.IsDeferred)
            {
                entity.Set(new Deferred());
            }

            return entity;
        }

        public void UpdateCopyTexture(CommandList commandList)
        {
            commandList.Begin();
            commandList.CopyTexture(screenColorTexture, copyTexture);
            commandList.End();
            GraphicsDevice.SubmitCommands(commandList);
            GraphicsDevice.WaitForIdle();
        }

        public void UpdateDepthCopyTexture(CommandList commandList)
        {
            commandList.Begin();
            commandList.CopyTexture(ScreenDepthTexture, depthCopyTexture);
            commandList.End();
            GraphicsDevice.SubmitCommands(commandList);
            GraphicsDevice.WaitForIdle();
        }

        public void UpdateRenderTextures(CommandList commandList)
        {
            commandList.Begin();
            foreach (var rt in renderTextures)
            {
                rt.UpdateCopyTexture(commandList);
            }
            commandList.End();
            GraphicsDevice.SubmitCommands(commandList);
            GraphicsDevice.WaitForIdle();
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
            foreach (var rt in renderTextures)
            {
                rt.Dispose();
            }
            ScreenFramebuffer?.Dispose();
            screenColorTexture?.Dispose();
            ScreenDepthTexture?.Dispose();
            CopyView?.Dispose();
            copyTexture?.Dispose();
            DepthCopyView?.Dispose();
            depthCopyTexture?.Dispose();
        }
    }
}