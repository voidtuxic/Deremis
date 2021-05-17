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
    public class ShadowRenderSystem : DrawableSystem
    {
        public const uint SHADOW_MAP_FAR = 50;
        public const uint SHADOW_MAP_RADIUS = 100;
        public const uint SHADOW_MAP_WIDTH = 1024;
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

        private readonly uint distance;
        private readonly EntitySet cameraSet;
        private readonly EntitySet lightSet;

        private readonly List<ShadowRenderSystem> cascades = new List<ShadowRenderSystem>();

        private Transform mainCameraTransform;
        private Transform sunTransform;
        public Matrix4x4 LightSpaceMatrix { get; private set; }

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

        public ShadowRenderSystem(Application app, World world, uint distance) : base(app, world,
            world.GetEntities()
                .With<Drawable>()
                .With<Transform>()
                .With<ShadowMapped>()
                .With<Render>(CanRenderToShadowMap)
                .AsMultiMap<Drawable>())
        {
            this.distance = distance;
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
            app.GraphicsDevice.SubmitCommands(mainCommandList);
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
            mainCommandList.Begin();
            SetFramebuffer(mainCommandList);
            mainCommandList.ClearDepthStencil(0f);
            mainCommandList.UpdateBuffer(app.MaterialManager.MaterialBuffer, 0, MapMaterial.GetValueArray());
            mainCommandList.End();
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

            mainCommandList.Begin();
            mainCommandList.UpdateBuffer(
                    app.MaterialManager.TransformBuffer,
                    0,
                    new TransformResource
                    {
                        lightSpaceMatrix1 = LightSpaceMatrix,
                    });
            mainCommandList.End();
            SubmitAndWait();
        }

        protected override void Update(float deltaSeconds, in Drawable key, ReadOnlySpan<Entity> entities)
        {
            var state = GetState(key.ToString(), true);
            if (state.Mesh == null) state.Mesh = app.ForwardRender.GetMesh(key.mesh);

            var commandList = state.CommandList;

            commandList.Begin();
            SetFramebuffer(commandList);

            commandList.SetPipeline(MapMaterial.GetPipeline(0));
            commandList.SetVertexBuffer(0, state.Mesh.VertexBuffer);
            var worlds = state.Worlds;
            worlds.Clear();
            foreach (var entity in entities)
            {
                worlds.Add(entity.GetWorldTransform().ToMatrix());
            }
            commandList.UpdateBuffer(state.InstanceBuffer, 0, worlds.ToArray());
            commandList.SetVertexBuffer(1, state.InstanceBuffer);
            if (state.Mesh.Indexed)
                commandList.SetIndexBuffer(state.Mesh.IndexBuffer, IndexFormat.UInt32);
            commandList.SetGraphicsResourceSet(0, app.MaterialManager.GeneralResourceSet);
            commandList.SetGraphicsResourceSet(1, MapMaterial.ResourceSet);

            if (state.Mesh.Indexed)
                commandList.DrawIndexed(
                    indexCount: state.Mesh.IndexCount,
                    instanceCount: (uint)entities.Length,
                    indexStart: 0,
                    vertexOffset: 0,
                    instanceStart: 0);
            else
                commandList.Draw(
                    vertexCount: state.Mesh.VertexCount,
                    instanceCount: (uint)entities.Length,
                    vertexStart: 0,
                    instanceStart: 0);

            commandList.End();
            app.GraphicsDevice.SubmitCommands(commandList);
        }

        protected override void PostUpdate(float state)
        {
            app.GraphicsDevice.WaitForIdle();
            // SubmitAndWait();

            foreach (var cascade in cascades)
            {
                cascade.Update(state);
            }
        }

        private void SetFramebuffer(CommandList commandList)
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