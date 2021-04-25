using System;
using System.Collections.Concurrent;
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

        private readonly ConcurrentDictionary<string, Mesh> meshes = new ConcurrentDictionary<string, Mesh>();

        private bool isDrawSystemValid;
        private Material material;
        private uint indexCount;
        private Matrix4x4 viewProjMatrix;

        public RgbaFloat ClearColor { get; set; } = new RgbaFloat(0.1f, 0.1f, 0.1f, 1);

        public DrawCallSystem(Application app, World world) : base(world)
        {
            current = this;
            this.app = app;
            commandList = app.Factory.CreateCommandList();
            cameraSet = world.GetEntities().With<Camera>().With<Transform>().AsSet();
        }

        public string RegisterMesh(string name, Mesh mesh)
        {
            meshes.TryAdd(name, mesh);
            return name;
        }

        protected override void PreUpdate(float state)
        {
            commandList.Begin();

            commandList.SetFramebuffer(app.GraphicsDevice.SwapchainFramebuffer);
            commandList.SetFullViewports();
            commandList.ClearColorTarget(0, ClearColor);
            commandList.ClearDepthStencil(1f);

            if (cameraSet.Count == 0)
            {
                isDrawSystemValid = false;
                return;
            }
            isDrawSystemValid = true;
            Span<Entity> cameras = stackalloc Entity[cameraSet.Count];
            cameraSet.GetEntities().CopyTo(cameras);
            foreach (ref readonly Entity camEntity in cameras)
            {
                ref var transform = ref camEntity.Get<Transform>();
                var view = transform.ToViewMatrix();
                var projection = camEntity.Get<Camera>().projection;
                viewProjMatrix = Matrix4x4.Multiply(view, projection);

                // TODO handle more than one camera
                break;
            }
        }

        protected override void PreUpdate(float state, Drawable key)
        {
            if (!isDrawSystemValid) return;
            material = app.MaterialManager.GetMaterial(key.material);
            if (material == null)
            {
                isDrawSystemValid = false;
                return;
            }
            commandList.UpdateBuffer(app.MaterialManager.MaterialBuffer, 0, material.GetValueArray());
            var pipeline = material.Pipeline;
            Mesh mesh = null;
            if (meshes.ContainsKey(key.mesh)) mesh = meshes[key.mesh];
            if (pipeline != null && mesh != null)
            {
                commandList.SetVertexBuffer(0, mesh.VertexBuffer);
                commandList.SetIndexBuffer(mesh.IndexBuffer, IndexFormat.UInt32);
                commandList.SetPipeline(pipeline);
                for (uint i = 0; i < app.MaterialManager.ResourceSets.Length; i++)
                {
                    commandList.SetGraphicsResourceSet(i, app.MaterialManager.ResourceSets[i]);
                }
                indexCount = mesh.IndexCount;
                isDrawSystemValid = true;
            }
            else isDrawSystemValid = false;
        }

        protected override void Update(float state, in Drawable key, ReadOnlySpan<Entity> entities)
        {
            if (!isDrawSystemValid) return;

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
                    });
                commandList.DrawIndexed(
                    indexCount: indexCount,
                    instanceCount: 1,
                    indexStart: 0,
                    vertexOffset: 0,
                    instanceStart: 0);
            }
        }

        protected override void PostUpdate(float state, Drawable key)
        {
        }

        protected override void PostUpdate(float state)
        {
            commandList.End();
            app.GraphicsDevice.SubmitCommands(commandList);
            app.GraphicsDevice.SwapBuffers();
        }
    }
}