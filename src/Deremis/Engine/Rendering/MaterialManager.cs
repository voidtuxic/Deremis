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
        private const uint MAX_LIGHT_BUFFER_SIZE = 256;

        public static MaterialManager current;
        private readonly Application app;

        private readonly ConcurrentDictionary<string, Material> materials = new ConcurrentDictionary<string, Material>();

        public DeviceBuffer TransformBuffer { get; private set; }
        public DeviceBuffer MaterialBuffer { get; private set; }
        public DeviceBuffer LightBuffer { get; private set; }

        public ResourceSet GeneralResourceSet { get; private set; }
        public ResourceLayout GeneralResourceLayout { get; private set; }
        private ResourceLayoutElementDescription[] resourceLayoutElementDescriptions = {
            new ResourceLayoutElementDescription ("Transform", ResourceKind.UniformBuffer, ShaderStages.Vertex | ShaderStages.Fragment),
            new ResourceLayoutElementDescription ("Material", ResourceKind.UniformBuffer, ShaderStages.Fragment),
            new ResourceLayoutElementDescription ("Light", ResourceKind.UniformBuffer, ShaderStages.Fragment),
        };

        public MaterialManager(Application app)
        {
            current = this;
            this.app = app;
            ResourceLayoutDescription resourceLayoutDescription = new ResourceLayoutDescription(resourceLayoutElementDescriptions);
            GeneralResourceLayout = app.Factory.CreateResourceLayout(resourceLayoutDescription);

            TransformBuffer = app.Factory.CreateBuffer(new BufferDescription(
                TransformResource.SizeInBytes,
                BufferUsage.UniformBuffer | BufferUsage.Dynamic));
            MaterialBuffer = app.Factory.CreateBuffer(new BufferDescription(
                MAX_MATERIAL_BUFFER_SIZE,
                BufferUsage.UniformBuffer | BufferUsage.Dynamic));
            LightBuffer = app.Factory.CreateBuffer(new BufferDescription(
                MAX_LIGHT_BUFFER_SIZE,
                BufferUsage.UniformBuffer | BufferUsage.Dynamic));
            BindableResource[] bindableResources = { TransformBuffer, MaterialBuffer, LightBuffer };
            ResourceSetDescription resourceSetDescription = new ResourceSetDescription(GeneralResourceLayout, bindableResources);
            GeneralResourceSet = app.Factory.CreateResourceSet(resourceSetDescription);
        }

        public Material CreateMaterial(string name, Shader shader, Framebuffer framebuffer = null)
        {
            if (materials.ContainsKey(name)) return materials[name];

            var material = new Material(name, shader);
            material.Build(framebuffer);
            materials.TryAdd(name, material);
            return material;
        }

        public Material GetMaterial(string name)
        {
            if (!materials.ContainsKey(name)) return null;
            return materials[name];
        }

        public void Dispose()
        {
            GeneralResourceLayout.Dispose();
            GeneralResourceSet.Dispose();
            TransformBuffer.Dispose();
            MaterialBuffer.Dispose();
            LightBuffer.Dispose();
            foreach (var material in materials.Values)
            {
                material.Dispose();
            }
        }
    }
}