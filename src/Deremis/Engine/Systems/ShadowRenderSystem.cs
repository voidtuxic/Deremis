using System;
using System.Collections.Generic;
using System.Numerics;
using DefaultEcs;
using DefaultEcs.System;
using DefaultEcs.Threading;
using Deremis.Engine.Objects;
using Deremis.Engine.Rendering.Resources;
using Deremis.Engine.Systems.Components;
using Deremis.Engine.Systems.Extensions;
using Deremis.Platform;
using Deremis.Platform.Assets;
using Veldrid;
using Veldrid.Utilities;
using Shader = Deremis.Engine.Objects.Shader;
using Texture = Deremis.Engine.Objects.Texture;

namespace Deremis.Engine.Systems
{
    public class ShadowRenderSystem : AEntityMultiMapSystem<float, Drawable>
    {
        public const uint SHADOW_MAP_FAR = 40;
        public const uint SHADOW_MAP_RADIUS = 100;
        public const uint SHADOW_MAP_WIDTH = 2048;
        public static AssetDescription ShadowMapShader = new AssetDescription
        {
            name = "shadow_map",
            path = "Shaders/shadow_map.xml"
        };

        public static string GetMapName(uint distance)
        {
            return $"shadowMap{distance}";
        }

        public static bool IsShadowMap(string propertyName)
        {
            return propertyName.Equals("shadowMap");
        }

        private readonly Application app;
        private readonly uint distance;
        private readonly CommandList commandList;
        private readonly EntitySet cameraSet;
        private readonly EntitySet lightSet;

        private readonly List<ShadowRenderSystem> cascades = new List<ShadowRenderSystem>();

        private Transform mainCameraTransform;
        private Transform sunTransform;
        public Matrix4x4 LightSpaceMatrix { get; private set; }
        private Mesh mesh;

        public Material MapMaterial { get; private set; }
        public Framebuffer Framebuffer { get; private set; }
        private Veldrid.Texture depthTexture;
        public Texture DepthTexture { get; private set; }

        public ShadowRenderSystem(Application app, World world) : this(app, world, 1)
        {
            var cascade1 = new ShadowRenderSystem(app, world, 4);
            var cascade2 = new ShadowRenderSystem(app, world, 16);
            cascades.Add(cascade1);
            cascades.Add(cascade2);
        }

        public ShadowRenderSystem(Application app, World world, uint distance) : base(
            world.GetEntities()
                .With<Drawable>()
                .With<Transform>()
                .With<ShadowMapped>()
                .With<Render>(CanRenderToShadowMap)
                .AsMultiMap<Drawable>())
        {
            this.app = app;
            this.distance = distance;
            commandList = app.Factory.CreateCommandList();
            commandList.Name = "ShadowCommandList";
            cameraSet = world.GetEntities()
                .With<Camera>()
                .With<Transform>()
                .AsSet();
            lightSet = world.GetEntities()
                .With<Light>()
                .With<Transform>()
                .AsSet();

            CreateResources();
            MapMaterial = app.MaterialManager.CreateMaterial(
                "shadow_map",
                app.AssetManager.Get<Shader>(ShadowMapShader),
                Framebuffer);
        }

        public void CreateResources()
        {
            DisposeScreenTargets();

            depthTexture = app.Factory.CreateTexture(TextureDescription.Texture2D(
                            SHADOW_MAP_WIDTH, SHADOW_MAP_WIDTH, 1, 1,
                            PixelFormat.D32_Float_S8_UInt, TextureUsage.DepthStencil | TextureUsage.Sampled, TextureSampleCount.Count1));
            depthTexture.Name = GetMapName(distance);
            Framebuffer = app.Factory.CreateFramebuffer(new FramebufferDescription(depthTexture));
            DepthTexture = new Texture(depthTexture.Name, depthTexture, app.Factory.CreateTextureView(depthTexture));
        }

        public void SubmitAndWait()
        {
            app.GraphicsDevice.SubmitCommands(commandList);
            app.GraphicsDevice.WaitForIdle();
        }

        public TextureView GetView(int dist)
        {
            if (distance == dist) return DepthTexture.View;

            TextureView view = null;
            foreach (var cascade in cascades)
            {
                view = cascade.GetView(dist);
                if (view != null) break;
            }

            return view;
        }

        public Matrix4x4 GetCascadeLightViewMatrix(int index)
        {
            if (index >= cascades.Count) return Matrix4x4.Identity;
            return cascades[index].LightSpaceMatrix;
        }

        protected override void PreUpdate(float state)
        {
            commandList.Begin();
            SetFramebuffer();
            commandList.ClearDepthStencil(0f);
            commandList.UpdateBuffer(app.MaterialManager.MaterialBuffer, 0, MapMaterial.GetValueArray());
            commandList.End();
            SubmitAndWait();

            Span<Entity> cameras = stackalloc Entity[cameraSet.Count];
            cameraSet.GetEntities().CopyTo(cameras);
            foreach (ref readonly Entity camEntity in cameras)
            {
                ref var transform = ref camEntity.Get<Transform>();
                mainCameraTransform = transform;
                break;
            }
            Span<Entity> lights = stackalloc Entity[lightSet.Count];
            lightSet.GetEntities().CopyTo(lights);
            foreach (ref readonly Entity lightEntity in lights)
            {
                ref var transform = ref lightEntity.Get<Transform>();
                ref var light = ref lightEntity.Get<Light>();

                if (light.type == 0)
                {
                    sunTransform = transform;
                    break;
                }
            }

            UpdateLightspaceViewProj();
        }

        protected override void PreUpdate(float state, Drawable key)
        {
            mesh = app.ForwardRender.GetMesh(key.mesh);
            commandList.Begin();
            SetFramebuffer();
            commandList.SetVertexBuffer(0, mesh.VertexBuffer);
            if (mesh.Indexed)
                commandList.SetIndexBuffer(mesh.IndexBuffer, IndexFormat.UInt32);
            commandList.SetPipeline(MapMaterial.GetPipeline(0));
            commandList.SetGraphicsResourceSet(0, app.MaterialManager.GeneralResourceSet);
            commandList.SetGraphicsResourceSet(1, MapMaterial.ResourceSet);
        }

        protected override void Update(float state, in Drawable key, in Entity entity)
        {
            var transform = entity.GetWorldTransform();
            var world = transform.ToMatrix();
            commandList.UpdateBuffer(
                app.MaterialManager.TransformBuffer,
                0,
                new TransformResource
                {
                    worldMatrix = world,
                    lightSpaceMatrix1 = LightSpaceMatrix
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
        }

        protected override void PostUpdate(float state)
        {
            SubmitAndWait();

            foreach (var cascade in cascades)
            {
                cascade.Update(state);
            }
        }

        private static bool CanRenderToShadowMap(in Render render)
        {
            return render.Shadows;
        }

        private void SetFramebuffer()
        {
            const uint shadowMapSize = SHADOW_MAP_WIDTH;
            commandList.SetFramebuffer(Framebuffer);
            commandList.SetViewport(0, new Viewport(0, 0, shadowMapSize, shadowMapSize, 0, 1));
            commandList.SetScissorRect(0, 0, 0, shadowMapSize, shadowMapSize);
        }

        // taken from https://github.com/mellinoe/veldrid/blob/eef8375169d1960a322f47f95e9b6ee8126e7b43/src/NeoDemo/Scene.cs#L405
        private void UpdateLightspaceViewProj()
        {
            uint far = SHADOW_MAP_FAR * distance;
            const float _lScale = 1f;
            const float _rScale = 1f;
            const float _tScale = 1f;
            const float _bScale = 1f;
            const float _nScale = 4f;
            const float _fScale = 4f;

            Vector3 lightDir = sunTransform.Forward;
            Vector3 viewDir = mainCameraTransform.Forward;
            Vector3 viewPos = mainCameraTransform.position;
            Vector3 unitY = Vector3.UnitY;
            FrustumCorners cameraCorners;

            FrustumHelpers.ComputePerspectiveFrustumCorners(
                    ref viewPos,
                    ref viewDir,
                    ref unitY,
                    MathF.PI / 4f,
                    far,
                    0,
                    app.AspectRatio,
                    out cameraCorners);

            Vector3 frustumCenter = Vector3.Zero;
            frustumCenter += cameraCorners.NearTopLeft;
            frustumCenter += cameraCorners.NearTopRight;
            frustumCenter += cameraCorners.NearBottomLeft;
            frustumCenter += cameraCorners.NearBottomRight;
            frustumCenter += cameraCorners.FarTopLeft;
            frustumCenter += cameraCorners.FarTopRight;
            frustumCenter += cameraCorners.FarBottomLeft;
            frustumCenter += cameraCorners.FarBottomRight;
            frustumCenter /= 8f;

            float radius = (cameraCorners.NearTopLeft - cameraCorners.FarBottomRight).Length() / 2.0f;
            float texelsPerUnit = SHADOW_MAP_WIDTH / (radius * 2.0f);

            Matrix4x4 scalar = Matrix4x4.CreateScale(texelsPerUnit, texelsPerUnit, texelsPerUnit);

            Vector3 baseLookAt = -lightDir;

            Matrix4x4 lookat = Matrix4x4.CreateLookAt(Vector3.Zero, baseLookAt, Vector3.UnitY);
            lookat = scalar * lookat;
            Matrix4x4.Invert(lookat, out Matrix4x4 lookatInv);

            frustumCenter = Vector3.Transform(frustumCenter, lookat);
            frustumCenter.X = (int)frustumCenter.X;
            frustumCenter.Y = (int)frustumCenter.Y;
            frustumCenter = Vector3.Transform(frustumCenter, lookatInv);

            Vector3 lightPos = frustumCenter - (lightDir * radius * 2f);

            Matrix4x4 lightView = Matrix4x4.CreateLookAt(lightPos, frustumCenter, Vector3.UnitY);

            Matrix4x4 lightProjection = Matrix4x4.CreateOrthographicOffCenter(
                -radius * _lScale,
                radius * _rScale,
                -radius * _bScale,
                radius * _tScale,
                radius * _fScale,
                -radius * _nScale);

            LightSpaceMatrix = lightView * lightProjection;
        }

        private void DisposeScreenTargets()
        {
            Framebuffer?.Dispose();
            DepthTexture?.Dispose();
            DepthTexture?.Dispose();
        }

        public override void Dispose()
        {
            base.Dispose();
            DisposeScreenTargets();

            foreach (var cascade in cascades)
            {
                cascade.Dispose();
            }
        }
    }
}