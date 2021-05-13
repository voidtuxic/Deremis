using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using DefaultEcs;
using DefaultEcs.System;
using DefaultEcs.Threading;
using Deremis.Engine.Objects;
using Deremis.Engine.Rendering.Helpers;
using Deremis.Engine.Rendering.Resources;
using Deremis.Engine.Systems.Components;
using Deremis.Engine.Systems.Extensions;
using Deremis.Platform;
using Deremis.Platform.Assets;
using Veldrid;
using Veldrid.Utilities;
using Shader = Deremis.Engine.Objects.Shader;

namespace Deremis.Engine.Systems
{
    public class ForwardRenderSystem : AEntityMultiMapSystem<float, Drawable>
    {
        public const string BloomTextureName = "bloomTex";
        public static ForwardRenderSystem current;
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
        private readonly CommandList commandList;

        private readonly EntitySet cameraSet;
        private readonly EntitySet lightSet;
        private readonly EntityMultiMap<Drawable> deferredObjectsMap;

        private readonly ConcurrentDictionary<string, Mesh> meshes = new ConcurrentDictionary<string, Mesh>();
        private readonly Dictionary<string, Material> deferredMaterials = new Dictionary<string, Material>();
        private readonly Dictionary<string, Material> screenPassMaterials = new Dictionary<string, Material>();

        private bool isDrawValid;
        private Material material;
        private Mesh mesh;
        private Matrix4x4 viewMatrix;
        private Matrix4x4 projMatrix;
        private Matrix4x4 viewProjMatrix;

        public RgbaFloat ClearColor { get; set; } = RgbaFloat.Black;
        private Material screenRenderMaterial;
        public Mesh ScreenRenderMesh { get; private set; }

        public ForwardRenderSystem(Application app, World world) : base(world
            .GetEntities()
            .With<Drawable>()
            .With<Transform>()
            .Without<Deferred>()
            .With<Render>(CanRenderToScreen)
            .AsMultiMap<Drawable>())
        {
            current = this;
            this.app = app;
            commandList = app.Factory.CreateCommandList();
            commandList.Name = "MainCommandList";
            cameraSet = world.GetEntities()
                .With<Camera>()
                .With<Transform>()
                .AsSet();
            lightSet = world.GetEntities()
                .With<Light>()
                .With<Transform>()
                .AsSet();
            deferredObjectsMap = world.GetEntities()
                .With<Drawable>()
                .With<Transform>()
                .With<Deferred>()
                .With<Render>(CanRenderToScreen)
                .AsMultiMap<Drawable>();
            InitScreenData();
        }

        private static bool CanRenderToScreen(in Render render)
        {
            return render.Screen;
        }

        private static bool CanRenderToShadowMap(in Render render)
        {
            return render.Shadows;
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

        public string RegisterMesh(string name, Mesh mesh)
        {
            meshes.TryAdd(name, mesh);
            return name;
        }

        public void RegisterDeferred(Material deferredMat)
        {
            foreach (var mat in deferredMaterials.Values)
            {
                if (mat.DeferredLightingMaterial == deferredMat.DeferredLightingMaterial)
                    return;
            }
            deferredMaterials.TryAdd(deferredMat.Name, deferredMat);
        }

        public Material GetScreenPass(string name)
        {
            if (screenPassMaterials.ContainsKey(name)) return screenPassMaterials[name];

            return null;
        }

        public Mesh GetMesh(string name)
        {
            if (meshes.ContainsKey(name)) return meshes[name];
            return null;
        }

        public void RegisterScreenPass(Material material)
        {
            screenPassMaterials.TryAdd(material.Name, material);
        }

        private void SetFramebuffer(Framebuffer framebuffer = null)
        {
            commandList.SetFramebuffer(framebuffer ?? app.ScreenFramebuffer);
            commandList.SetFullViewports();
        }

        protected override void PreUpdate(float state)
        {
            app.ClearDeferredFramebuffers(commandList);

            commandList.Begin();

            SetFramebuffer();
            commandList.ClearColorTarget(0, ClearColor);
            commandList.ClearColorTarget(1, RgbaFloat.Clear); // bloom
            commandList.ClearDepthStencil(1f);

            if (cameraSet.Count == 0)
            {
                isDrawValid = false;
                return;
            }
            isDrawValid = true;

            Span<Entity> cameras = stackalloc Entity[cameraSet.Count];
            cameraSet.GetEntities().CopyTo(cameras);
            foreach (ref readonly Entity camEntity in cameras)
            {
                ref var transform = ref camEntity.Get<Transform>();
                viewMatrix = transform.ToViewMatrix();
                projMatrix = camEntity.Get<Camera>().projection;
                viewProjMatrix = Matrix4x4.Multiply(viewMatrix, projMatrix);

                // TODO handle more than one camera
                break;
            }

            Span<Entity> lights = stackalloc Entity[lightSet.Count];
            var lightValues = new List<float>();
            lightSet.GetEntities().CopyTo(lights);
            foreach (ref readonly Entity lightEntity in lights)
            {
                ref var transform = ref lightEntity.Get<Transform>();
                ref var light = ref lightEntity.Get<Light>();

                lightValues.AddRange(light.GetValueArray(ref transform));
            }
            commandList.UpdateBuffer(app.MaterialManager.LightBuffer, 0, lightValues.ToArray());
            commandList.End();
            SubmitAndWait();
        }

        protected override void PreUpdate(float state, Drawable key)
        {
            if (!isDrawValid) return;
            isDrawValid = InitDrawable(key);
        }

        private bool InitDrawable(Drawable key, Material drawMat = null, bool checkDeferred = true, bool begin = true)
        {
            material = drawMat ?? app.MaterialManager.GetMaterial(key.material);
            if (material == null || (checkDeferred && material.Shader.IsDeferred))
            {
                return false;
            }
            mesh = GetMesh(key.mesh);
            var pipeline = material.GetPipeline(0);
            if (pipeline != null && mesh != null)
            {
                SetPipeline(pipeline, begin);
                isDrawValid = true;
            }
            else return false;
            return true;
        }

        private void SetPipeline(Pipeline pipeline, bool begin)
        {
            if (begin)
                commandList.Begin();
            SetFramebuffer(material.Framebuffer);
            commandList.UpdateBuffer(app.MaterialManager.MaterialBuffer, 0, material.GetValueArray());
            commandList.SetVertexBuffer(0, mesh.VertexBuffer);
            if (mesh.Indexed)
                commandList.SetIndexBuffer(mesh.IndexBuffer, IndexFormat.UInt32);
            commandList.SetPipeline(pipeline);
            commandList.SetGraphicsResourceSet(0, app.MaterialManager.GeneralResourceSet);
            commandList.SetGraphicsResourceSet(1, material.ResourceSet);
        }

        protected override void Update(float state, in Drawable key, ReadOnlySpan<Entity> entities)
        {
            if (!isDrawValid) return;

            foreach (ref readonly var entity in entities)
            {
                if (entity.Get<Render>().Screen)
                    Draw(in entity);
            }
        }

        private void Draw(in Entity entity)
        {
            var transform = entity.GetWorldTransform();
            var world = transform.ToMatrix();
            var normalWorld = Matrix4x4.Identity;
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
                    viewMatrix = viewMatrix,
                    projMatrix = projMatrix,
                    lightSpaceMatrix1 = app.ShadowRender.LightSpaceMatrix,
                    lightSpaceMatrix2 = app.ShadowRender.GetCascadeLightViewMatrix(0),
                    lightSpaceMatrix3 = app.ShadowRender.GetCascadeLightViewMatrix(1),
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
            app.GraphicsDevice.SubmitCommands(commandList);
            // kinda defeats the purpose of general preupdate...
            isDrawValid = true;
        }

        protected override void PostUpdate(float state)
        {
            DrawScreenPasses();
            app.UpdateScreenTexture(commandList);

            UpdateScreenBuffer(screenRenderMaterial, app.GraphicsDevice.SwapchainFramebuffer);

            app.GraphicsDevice.SwapBuffers();
        }

        private void DrawDeferred()
        {
            if (deferredMaterials.Count != 0)
            {
                commandList.Begin();
                foreach (var key in deferredObjectsMap.Keys)
                {
                    if (InitDrawable(key, null, false, false) && deferredObjectsMap.TryGetEntities(key, out var entities))
                    {
                        isDrawValid = true;
                        Update(0, in key, entities);
                    }
                }
                commandList.End();
                SubmitAndWait();

                app.UpdateRenderTextures(commandList);

                foreach (var material in deferredMaterials.Values)
                {
                    UpdateScreenBuffer(material.DeferredLightingMaterial, app.ScreenFramebuffer);
                }
            }
        }

        private void DrawScreenPasses()
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
        }

        private void UpdateScreenBuffer(Material material, Framebuffer framebuffer)
        {
            commandList.Begin();

            commandList.UpdateBuffer(
                app.MaterialManager.TransformBuffer,
                0,
                new TransformResource
                {
                    viewProjMatrix = viewProjMatrix,
                    viewMatrix = viewMatrix,
                    projMatrix = projMatrix
                });
            commandList.UpdateBuffer(app.MaterialManager.MaterialBuffer, 0, material.GetValueArray());
            commandList.End();

            for (var i = 0; i < material.Shader.PassCount; i++)
            {
                commandList.Begin();
                bool isLastPass = i == material.Shader.PassCount - 1;
                SetFramebuffer(isLastPass ? framebuffer : material.PassFramebuffer);

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