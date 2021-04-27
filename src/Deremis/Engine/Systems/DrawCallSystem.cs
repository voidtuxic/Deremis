using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using DefaultEcs;
using DefaultEcs.System;
using Deremis.Engine.Objects;
using Deremis.Engine.Rendering.Resources;
using Deremis.Engine.Systems.Components;
using Deremis.System;
using Deremis.System.Assets;
using Veldrid;
using Shader = Deremis.Engine.Objects.Shader;

namespace Deremis.Engine.Systems
{
    [With(typeof(Transform))]
    [Without(typeof(Deferred))]
    public class DrawCallSystem : AEntityMultiMapSystem<float, Drawable>
    {
        public static DrawCallSystem current;
        public static AssetDescription ScreenShader = new AssetDescription
        {
            name = "screen_passthrough",
            path = "Shaders/screen/passthrough.xml",
            type = 1
        };

        private readonly Application app;
        private readonly CommandList commandList;

        private readonly EntitySet cameraSet;
        private readonly EntitySet lightSet;
        private readonly EntityMultiMap<Drawable> deferredObjectsMap;

        private readonly ConcurrentDictionary<string, Mesh> meshes = new ConcurrentDictionary<string, Mesh>();
        private readonly Dictionary<string, Material> deferredMaterials = new Dictionary<string, Material>();

        private bool isDrawValid;
        private Material material;
        private Mesh mesh;
        private Matrix4x4 viewMatrix;
        private Matrix4x4 projMatrix;
        private Matrix4x4 viewProjMatrix;

        public RgbaFloat ClearColor { get; set; } = RgbaFloat.Black;
        private Material screenRenderMaterial;
        private Mesh screenRenderMesh;

        public DrawCallSystem(Application app, World world) : base(world)
        {
            current = this;
            this.app = app;
            commandList = app.Factory.CreateCommandList();
            commandList.Name = "MainCommandList";
            cameraSet = world.GetEntities().With<Camera>().With<Transform>().AsSet();
            lightSet = world.GetEntities().With<Light>().With<Transform>().AsSet();
            deferredObjectsMap = world.GetEntities().With<Drawable>().With<Transform>().With<Deferred>().AsMultiMap<Drawable>();
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
            screenRenderMaterial.SetTexture("screenTex", app.CopyView);
        }

        public string RegisterMesh(string name, Mesh mesh)
        {
            meshes.TryAdd(name, mesh);
            return name;
        }

        public void RegisterDeferred(Material deferredMat)
        {
            deferredMaterials.Add(deferredMat.Name, deferredMat);
        }

        private void SetFramebuffer(Framebuffer framebuffer = null)
        {
            commandList.SetFramebuffer(framebuffer ?? app.ScreenFramebuffer);
            commandList.SetFullViewports();
        }

        protected override void PreUpdate(float state)
        {
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
            app.GraphicsDevice.SubmitCommands(commandList);
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

        private bool InitDrawable(Drawable key, bool checkDeferred = true)
        {
            material = app.MaterialManager.GetMaterial(key.material);
            if (material == null || (checkDeferred && material.Shader.IsDeferred))
            {
                return false;
            }
            mesh = null;
            if (meshes.ContainsKey(key.mesh)) mesh = meshes[key.mesh];
            var pipeline = material.Pipeline;
            if (pipeline != null && mesh != null)
            {
                SetPipeline(pipeline);
                isDrawValid = true;
            }
            else return false;
            return true;
        }

        private void SetPipeline(Pipeline pipeline)
        {
            commandList.Begin();
            SetFramebuffer(material.Framebuffer);
            if (material.Shader.IsDeferred)
            {
                for (uint i = 0; i < material.Shader.Outputs.Count; i++)
                {
                    commandList.ClearColorTarget(i, RgbaFloat.Clear);
                }
            }
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
                ref var transform = ref entity.Get<Transform>();
                Draw(ref transform);
            }
        }

        private void Draw(ref Transform transform)
        {
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
                    projMatrix = projMatrix
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
            app.UpdateRenderTextures(commandList);
            app.UpdateDepthCopyTexture(commandList);

            // draw deferred after forward... feels dumb
            if (deferredMaterials.Count != 0)
            {
                foreach (var key in deferredObjectsMap.Keys)
                {
                    if (InitDrawable(key, false) && deferredObjectsMap.TryGetEntities(key, out var entities))
                    {
                        isDrawValid = true;
                        Update(0, in key, entities);
                    }
                }

                foreach (var material in deferredMaterials.Values)
                {
                    UpdateScreenBuffer(material.DeferredLightingMaterial, app.ScreenFramebuffer);
                }
            }

            app.UpdateCopyTexture(commandList);
            UpdateScreenBuffer(screenRenderMaterial, app.GraphicsDevice.SwapchainFramebuffer);

            app.GraphicsDevice.SwapBuffers();
        }

        private void UpdateScreenBuffer(Material material, Framebuffer framebuffer)
        {
            commandList.Begin();

            SetFramebuffer(framebuffer);
            // commandList.ClearColorTarget(0, ClearColor);

            commandList.SetVertexBuffer(0, screenRenderMesh.VertexBuffer);
            commandList.SetPipeline(material.Pipeline);
            commandList.SetGraphicsResourceSet(0, app.MaterialManager.GeneralResourceSet);
            commandList.SetGraphicsResourceSet(1, material.ResourceSet);

            commandList.UpdateBuffer(app.MaterialManager.MaterialBuffer, 0, material.GetValueArray());
            commandList.Draw(
                vertexCount: screenRenderMesh.VertexCount,
                instanceCount: 1,
                vertexStart: 0,
                instanceStart: 0);

            commandList.End();
            app.GraphicsDevice.SubmitCommands(commandList);
            app.GraphicsDevice.WaitForIdle();
        }
    }
}