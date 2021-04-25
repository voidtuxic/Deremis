using System;
using System.Collections.Concurrent;
using Deremis.System;
using Deremis.Engine.Objects;
using Veldrid;
using Shader = Deremis.Engine.Objects.Shader;
using Deremis.Engine.Rendering.Resources;

namespace Deremis.Engine.Rendering
{
    public class MaterialManager : IDisposable
    {
        private const uint MAX_MATERIAL_BUFFER_SIZE = 256;

        public static MaterialManager current;
        private readonly Application app;

        private readonly ConcurrentDictionary<string, Material> materials = new ConcurrentDictionary<string, Material>();

        public DeviceBuffer TransformBuffer { get; private set; }
        public DeviceBuffer MaterialBuffer { get; private set; }

        public ResourceSet[] ResourceSets { get; private set; }
        private ResourceLayout[] resourceLayouts;
        private ResourceLayoutElementDescription[] resourceLayoutElementDescriptions = {
            new ResourceLayoutElementDescription ("Transform", ResourceKind.UniformBuffer, ShaderStages.Vertex | ShaderStages.Fragment),
            new ResourceLayoutElementDescription ("Material", ResourceKind.UniformBuffer, ShaderStages.Fragment),
        };

        public MaterialManager(Application app)
        {
            current = this;
            this.app = app;
            ResourceLayoutDescription resourceLayoutDescription = new ResourceLayoutDescription(resourceLayoutElementDescriptions);
            ResourceLayout sharedLayout = app.Factory.CreateResourceLayout(resourceLayoutDescription);
            resourceLayouts = new ResourceLayout[] { sharedLayout };

            TransformBuffer = app.Factory.CreateBuffer(new BufferDescription(
                TransformResource.SizeInBytes,
                BufferUsage.UniformBuffer | BufferUsage.Dynamic));
            MaterialBuffer = app.Factory.CreateBuffer(new BufferDescription(
                MAX_MATERIAL_BUFFER_SIZE,
                BufferUsage.UniformBuffer | BufferUsage.Dynamic));
            BindableResource[] bindableResources = { TransformBuffer, MaterialBuffer };
            ResourceSetDescription resourceSetDescription = new ResourceSetDescription(sharedLayout, bindableResources);
            ResourceSets = new ResourceSet[] { app.Factory.CreateResourceSet(resourceSetDescription) };
        }

        public Material CreateMaterial(string name, Shader shader)
        {
            if (materials.ContainsKey(name)) return materials[name];
            var description = shader.DefaultPipeline;
            description.ResourceLayouts = resourceLayouts;
            description.Outputs = app.GraphicsDevice.SwapchainFramebuffer.OutputDescription;
            materials.TryAdd(name, new Material(name, shader, app.Factory.CreateGraphicsPipeline(description)));
            return materials[name];
        }

        public Material GetMaterial(string name)
        {
            if (!materials.ContainsKey(name)) return null;
            return materials[name];
        }

        public void Dispose()
        {
            TransformBuffer.Dispose();
            foreach (var pipeline in materials.Values)
            {
                pipeline.Dispose();
            }
        }
    }
}