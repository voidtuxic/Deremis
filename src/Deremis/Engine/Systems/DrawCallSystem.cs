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
using Veldrid;

namespace Deremis.Engine.Systems
{
    [With(typeof(Transform))]
    public class DrawCallSystem : AEntityMultiMapSystem<float, Drawable>
    {
        public static DrawCallSystem current;

        private readonly Application app;
        private readonly CommandList commandList;

        private readonly EntitySet cameraSet;
        private readonly EntitySet lightSet;

        private readonly ConcurrentDictionary<string, Mesh> meshes = new ConcurrentDictionary<string, Mesh>();

        private bool isDrawValid;
        private Material material;
        private Mesh mesh;
        private Matrix4x4 viewMatrix;
        private Matrix4x4 projMatrix;
        private Matrix4x4 viewProjMatrix;

        public RgbaFloat ClearColor { get; set; } = RgbaFloat.Black;

        public DrawCallSystem(Application app, World world) : base(world)
        {
            current = this;
            this.app = app;
            commandList = app.Factory.CreateCommandList();
            cameraSet = world.GetEntities().With<Camera>().With<Transform>().AsSet();
            lightSet = world.GetEntities().With<Light>().With<Transform>().AsSet();
        }

        public string RegisterMesh(string name, Mesh mesh)
        {
            meshes.TryAdd(name, mesh);
            return name;
        }

        private void SetFramebuffer()
        {
            commandList.SetFramebuffer(app.GraphicsDevice.SwapchainFramebuffer);
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
            material = app.MaterialManager.GetMaterial(key.material);
            if (material == null)
            {
                isDrawValid = false;
                return;
            }
            var pipeline = material.Pipeline;
            mesh = null;
            if (meshes.ContainsKey(key.mesh)) mesh = meshes[key.mesh];
            if (pipeline != null && mesh != null)
            {
                commandList.Begin();
                SetFramebuffer();
                commandList.UpdateBuffer(app.MaterialManager.MaterialBuffer, 0, material.GetValueArray());
                commandList.SetVertexBuffer(0, mesh.VertexBuffer);
                if (mesh.Indexed)
                    commandList.SetIndexBuffer(mesh.IndexBuffer, IndexFormat.UInt32);
                commandList.SetPipeline(pipeline);
                commandList.SetGraphicsResourceSet(0, app.MaterialManager.GeneralResourceSet);
                commandList.SetGraphicsResourceSet(1, material.ResourceSet);
                isDrawValid = true;
            }
            else isDrawValid = false;
        }

        protected override void Update(float state, in Drawable key, ReadOnlySpan<Entity> entities)
        {
            if (!isDrawValid) return;

            foreach (var entity in entities)
            {
                ref var transform = ref entity.Get<Transform>();
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
        }

        protected override void PostUpdate(float state, Drawable key)
        {
            commandList.End();
            app.GraphicsDevice.SubmitCommands(commandList);
        }

        protected override void PostUpdate(float state)
        {
            app.GraphicsDevice.SwapBuffers();
        }
    }
}