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
using Texture = Deremis.Engine.Objects.Texture;

namespace Deremis.System
{
    public sealed class Application : IDisposable
    {
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
                WindowWidth = 1920,
                WindowHeight = 1080,
                WindowTitle = "Deremis"
            };
            window = VeldridStartup.CreateWindow(ref windowCI);

            GraphicsDeviceOptions options = new GraphicsDeviceOptions
            {
                PreferStandardClipSpaceYDirection = true,
                PreferDepthRangeZeroToOne = true,
                SwapchainDepthFormat = PixelFormat.R16_UNorm,
                SyncToVerticalBlank = true,
                SwapchainSrgbFormat = true
            };
            GraphicsDevice = VeldridStartup.CreateGraphicsDevice(window, options, GraphicsBackend.Direct3D11);
            Factory = GraphicsDevice.ResourceFactory;

            DefaultWorld = new World();
            Draw = new DrawCallSystem(this, DefaultWorld);
            ParallelSystemRunner = new DefaultParallelRunner(Environment.ProcessorCount);
            MainSystem = new SequentialListSystem<float>();
            MainSystem.Add(Draw);

            AssetManager = new AssetManager("./Assets/");
            MaterialManager = new MaterialManager(this);

            LoadDefaultAssets();

            context.Initialize(this);
        }

        private void LoadDefaultAssets()
        {
            AssetManager.Get<Shader>(new AssetDescription
            {
                name = "phong",
                path = "Shaders/phong.xml",
                type = 1
            });
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