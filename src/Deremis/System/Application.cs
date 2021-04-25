using System;
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

namespace Deremis.System
{
    public sealed class Application : IDisposable
    {
        public static Application current;

        private Sdl2Window window;

        public GraphicsDevice GraphicsDevice { get; private set; }
        public ResourceFactory Factory { get; private set; }

        public World DefaultWorld { get; private set; }
        public DrawCallSystem Draw { get; private set; }
        public IParallelRunner ParallelSystemRunner { get; private set; }
        public SequentialListSystem<float> MainSystem { get; private set; }
        private int entityCounter = 0;

        public AssetManager AssetManager { get; private set; }
        public MaterialManager MaterialManager { get; private set; }
        private IContext context;

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
                WindowWidth = 1280,
                WindowHeight = 960,
                WindowTitle = "Deremis"
            };
            window = VeldridStartup.CreateWindow(ref windowCI);

            GraphicsDeviceOptions options = new GraphicsDeviceOptions
            {
                PreferStandardClipSpaceYDirection = true,
                PreferDepthRangeZeroToOne = true,
                SwapchainDepthFormat = PixelFormat.R16_UNorm,
                SyncToVerticalBlank = true
            };
            GraphicsDevice = VeldridStartup.CreateGraphicsDevice(window, options);
            Factory = GraphicsDevice.ResourceFactory;

            DefaultWorld = new World();
            Draw = new DrawCallSystem(this, DefaultWorld);
            ParallelSystemRunner = new DefaultParallelRunner(Environment.ProcessorCount);
            MainSystem = new SequentialListSystem<float>();
            MainSystem.Add(Draw);

            AssetManager = new AssetManager("./Assets/");
            MaterialManager = new MaterialManager(this);

            context.Initialize(this);
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

        public Entity Spawn(string name, Mesh mesh, string material)
        {
            var entity = CreateTransform(name);
            entity.Set(new Drawable
            {
                mesh = Draw.RegisterMesh(mesh.Name, mesh),
                material = material
            });

            return entity;
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