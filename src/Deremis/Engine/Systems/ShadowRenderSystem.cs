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
using Veldrid;
using Veldrid.Utilities;

namespace Deremis.Engine.Systems
{
    public class ShadowRenderSystem : AEntityMultiMapSystem<float, Drawable>
    {
        private readonly Application app;

        private readonly CommandList commandList;
        private readonly EntitySet cameraSet;
        private readonly EntitySet lightSet;

        private Transform mainCameraTransform;
        private Transform sunTransform;
        public Matrix4x4 LightSpaceMatrix { get; private set; }
        private Matrix4x4 viewMatrix;
        private Matrix4x4 projMatrix;
        private Matrix4x4 viewProjMatrix;
        private Mesh mesh;

        public ShadowRenderSystem(Application app, World world) : base(
            world.GetEntities()
                .With<Drawable>()
                .With<Transform>()
                .With<ShadowMapped>()
                .With<Render>(CanRenderToShadowMap)
                .AsMultiMap<Drawable>())
        {
            this.app = app;
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
        }

        public void SubmitAndWait()
        {
            app.GraphicsDevice.SubmitCommands(commandList);
            app.GraphicsDevice.WaitForIdle();
        }

        protected override void PreUpdate(float state)
        {
            commandList.Begin();
            SetFramebuffer();
            commandList.ClearDepthStencil(0f);
            commandList.UpdateBuffer(app.MaterialManager.MaterialBuffer, 0, app.ShadowMapMaterial.GetValueArray());
            commandList.End();
            SubmitAndWait();

            Span<Entity> cameras = stackalloc Entity[cameraSet.Count];
            cameraSet.GetEntities().CopyTo(cameras);
            foreach (ref readonly Entity camEntity in cameras)
            {
                ref var transform = ref camEntity.Get<Transform>();
                mainCameraTransform = transform;
                viewMatrix = transform.ToViewMatrix();
                projMatrix = camEntity.Get<Camera>().projection;
                viewProjMatrix = Matrix4x4.Multiply(viewMatrix, projMatrix);
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
            commandList.SetPipeline(app.ShadowMapMaterial.GetPipeline(0));
            commandList.SetGraphicsResourceSet(0, app.MaterialManager.GeneralResourceSet);
            commandList.SetGraphicsResourceSet(1, app.ShadowMapMaterial.ResourceSet);
        }

        protected override void Update(float state, in Drawable key, in Entity entity)
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
                    lightSpaceMatrix = LightSpaceMatrix
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
        }

        private static bool CanRenderToShadowMap(in Render render)
        {
            return render.Shadows;
        }

        private void SetFramebuffer()
        {
            const uint shadowMapSize = Application.SHADOW_MAP_WIDTH;
            commandList.SetFramebuffer(app.ShadowFramebuffer);
            commandList.SetViewport(0, new Viewport(0, 0, shadowMapSize, shadowMapSize, 0, 1));
            commandList.SetScissorRect(0, 0, 0, shadowMapSize, shadowMapSize);
        }

        // taken from https://github.com/mellinoe/veldrid/blob/eef8375169d1960a322f47f95e9b6ee8126e7b43/src/NeoDemo/Scene.cs#L405
        private void UpdateLightspaceViewProj()
        {
            const uint far = Application.SHADOW_MAP_FAR;
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
            float texelsPerUnit = Application.SHADOW_MAP_WIDTH / (radius * 2.0f);

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
    }
}