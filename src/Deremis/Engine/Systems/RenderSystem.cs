using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using DefaultEcs;
using DefaultEcs.System;
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
    [With(typeof(Transform))]
    [Without(typeof(Deferred))]
    public class RenderSystem : AEntityMultiMapSystem<float, Drawable>
    {
        public static RenderSystem current;
        public static AssetDescription ScreenShader = new AssetDescription
        {
            name = "screen_passthrough",
            path = "Shaders/screen/passthrough.xml"
        };

        private readonly Application app;
        private readonly CommandList commandList;

        private readonly EntitySet cameraSet;
        private readonly EntitySet lightSet;
        private readonly EntityMultiMap<Drawable> deferredObjectsMap;
        private readonly EntityMultiMap<Drawable> shadowedObjectsMap;

        private readonly ConcurrentDictionary<string, Mesh> meshes = new ConcurrentDictionary<string, Mesh>();
        private readonly Dictionary<string, Material> deferredMaterials = new Dictionary<string, Material>();
        private readonly Dictionary<string, Material> screenPassMaterials = new Dictionary<string, Material>();

        private bool isDrawValid;
        private Material material;
        private Mesh mesh;
        private Transform mainCameraTransform;
        private Transform sunTransform;
        private Matrix4x4 viewMatrix;
        private Matrix4x4 projMatrix;
        private Matrix4x4 viewProjMatrix;
        private Matrix4x4 lightSpaceMatrix;

        public RgbaFloat ClearColor { get; set; } = RgbaFloat.Black;
        private Material screenRenderMaterial;
        private Mesh screenRenderMesh;

        public RenderSystem(Application app, World world) : base(world)
        {
            current = this;
            this.app = app;
            commandList = app.Factory.CreateCommandList();
            commandList.Name = "MainCommandList";
            cameraSet = world.GetEntities().With<Camera>().With<Transform>().AsSet();
            lightSet = world.GetEntities().With<Light>().With<Transform>().AsSet();
            deferredObjectsMap = world.GetEntities().With<Drawable>().With<Transform>().With<Deferred>().AsMultiMap<Drawable>();
            shadowedObjectsMap = world.GetEntities().With<Drawable>().With<Transform>().With<ShadowMapped>().AsMultiMap<Drawable>();
            InitScreenData();
        }

        private void InitScreenData()
        {
            screenRenderMesh = new Mesh("screen");
            screenRenderMesh.Indexed = false;
            screenRenderMesh.Add(new Rendering.Vertex
            {
                Position = new Vector3(-1, 1, 0),
                UV = new Vector2(0, 0)
            });
            screenRenderMesh.Add(new Rendering.Vertex
            {
                Position = new Vector3(-1, -1, 0),
                UV = new Vector2(0, 1)
            });
            screenRenderMesh.Add(new Rendering.Vertex
            {
                Position = new Vector3(1, -1, 0),
                UV = new Vector2(1, 1)
            });
            screenRenderMesh.Add(new Rendering.Vertex
            {
                Position = new Vector3(-1, 1, 0),
                UV = new Vector2(0, 0)
            });
            screenRenderMesh.Add(new Rendering.Vertex
            {
                Position = new Vector3(1, -1, 0),
                UV = new Vector2(1, 1)
            });
            screenRenderMesh.Add(new Rendering.Vertex
            {
                Position = new Vector3(1, 1, 0),
                UV = new Vector2(1, 0)
            });
            screenRenderMesh.UpdateBuffers();
            screenRenderMaterial = app.MaterialManager.CreateMaterial(
                ScreenShader.name,
                app.AssetManager.Get<Shader>(ScreenShader),
                app.GraphicsDevice.SwapchainFramebuffer);
            screenRenderMaterial.SetTexture("screenTex", app.CopyTexture);
            // screenRenderMaterial.SetTexture("extraTex", app.ShadowDepthTexture);
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
                mainCameraTransform = transform;
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

                // TODO last directional currently takes precedence
                if (light.type == 0)
                {
                    sunTransform = transform;
                }

                lightValues.AddRange(light.GetValueArray(ref transform));
            }
            commandList.UpdateBuffer(app.MaterialManager.LightBuffer, 0, lightValues.ToArray());
            commandList.End();
            SubmitAndWait();
            DrawShadowMap();
        }

        protected override void PreUpdate(float state, Drawable key)
        {
            if (!isDrawValid) return;
            if (InitDrawable(key))
            {
                isDrawValid = true;
            }
            else
            {
                isDrawValid = false;
            }
        }

        private bool InitDrawable(Drawable key, Material drawMat = null, bool checkDeferred = true, bool begin = true)
        {
            material = drawMat ?? app.MaterialManager.GetMaterial(key.material);
            if (material == null || (checkDeferred && material.Shader.IsDeferred))
            {
                return false;
            }
            mesh = null;
            if (meshes.ContainsKey(key.mesh)) mesh = meshes[key.mesh];
            var pipeline = material.Pipeline;
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

            foreach (var entity in entities)
            {
                Draw(entity);
            }
        }

        private void Draw(Entity entity)
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
                    lightSpaceMatrix = lightSpaceMatrix
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
            DrawDeferred();
            app.UpdateScreenTexture(commandList);
            DrawScreenPasses();
            app.UpdateScreenTexture(commandList);

            UpdateScreenBuffer(screenRenderMaterial, app.GraphicsDevice.SwapchainFramebuffer);

            app.GraphicsDevice.SwapBuffers();
        }

        // TODO only one directional light
        private void DrawShadowMap()
        {
            UpdateLightspaceViewProj();

            const uint shadowMapSize = Application.SHADOW_MAP_WIDTH;
            commandList.Begin();
            commandList.SetFramebuffer(app.ShadowFramebuffer);
            commandList.SetViewport(0, new Viewport(0, 0, shadowMapSize, shadowMapSize, 0, 1));
            commandList.SetScissorRect(0, 0, 0, shadowMapSize, shadowMapSize);
            commandList.ClearDepthStencil(0f);
            commandList.End();
            SubmitAndWait();

            commandList.Begin();
            foreach (var key in shadowedObjectsMap.Keys)
            {
                if (InitDrawable(key, app.ShadowMapMaterial, false, false) && shadowedObjectsMap.TryGetEntities(key, out var entities))
                {
                    isDrawValid = true;
                    Update(0, in key, entities);
                }
            }
            commandList.End();
            SubmitAndWait();
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

            lightSpaceMatrix = lightView * lightProjection;
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
                    UpdateScreenBuffer(material, app.ScreenFramebuffer);
                }
            }
        }

        private void UpdateScreenBuffer(Material material, Framebuffer framebuffer)
        {
            commandList.Begin();

            SetFramebuffer(framebuffer);

            var world = Matrix4x4.Identity;
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
                    lightSpaceMatrix = lightSpaceMatrix
                });
            commandList.UpdateBuffer(app.MaterialManager.MaterialBuffer, 0, material.GetValueArray());

            commandList.SetVertexBuffer(0, screenRenderMesh.VertexBuffer);
            commandList.SetPipeline(material.Pipeline);
            commandList.SetGraphicsResourceSet(0, app.MaterialManager.GeneralResourceSet);
            commandList.SetGraphicsResourceSet(1, material.ResourceSet);

            commandList.Draw(
                vertexCount: screenRenderMesh.VertexCount,
                instanceCount: 1,
                vertexStart: 0,
                instanceStart: 0);

            commandList.End();
            SubmitAndWait();
        }

        public void SubmitAndWait()
        {
            app.GraphicsDevice.SubmitCommands(commandList);
            app.GraphicsDevice.WaitForIdle();
        }
    }
}