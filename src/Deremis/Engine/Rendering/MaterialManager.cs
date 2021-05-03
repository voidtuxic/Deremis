using System;
using System.Collections.Concurrent;
using Deremis.Platform;
using Deremis.Engine.Objects;
using Veldrid;
using Shader = Deremis.Engine.Objects.Shader;
using Deremis.Engine.Rendering.Resources;
using System.Collections.Generic;
using Texture = Deremis.Engine.Objects.Texture;

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
            TransformBuffer.Name = "TransformBuffer";
            MaterialBuffer = app.Factory.CreateBuffer(new BufferDescription(
                MAX_MATERIAL_BUFFER_SIZE,
                BufferUsage.UniformBuffer | BufferUsage.Dynamic));
            MaterialBuffer.Name = "MaterialBuffer";
            LightBuffer = app.Factory.CreateBuffer(new BufferDescription(
                MAX_LIGHT_BUFFER_SIZE,
                BufferUsage.UniformBuffer | BufferUsage.Dynamic));
            LightBuffer.Name = "LightBuffer";
            BindableResource[] bindableResources = { TransformBuffer, MaterialBuffer, LightBuffer };
            ResourceSetDescription resourceSetDescription = new ResourceSetDescription(GeneralResourceLayout, bindableResources);
            GeneralResourceSet = app.Factory.CreateResourceSet(resourceSetDescription);
        }

        public Material CreateMaterial(string name, Shader shader, Framebuffer framebuffer = null)
        {
            if (materials.ContainsKey(name)) return materials[name];

            var material = new Material(name, shader);

            Framebuffer fb = framebuffer;
            List<TextureView> gbufferTextureViews = null;
            // assume the user know what they're doing with the framebuffer
            if (shader.IsDeferred && fb == null)
            {
                gbufferTextureViews = new List<TextureView>();
                var colorTargets = new List<Veldrid.Texture>();
                for (int i = 0; i < shader.Outputs.Count; i++)
                {
                    PixelFormat outputFormat = shader.Outputs[i];
                    var rt = app.CreateRenderTexture($"{name}_gbuffer{i}", outputFormat);
                    colorTargets.Add(rt.RenderTarget.VeldridTexture);
                    gbufferTextureViews.Add(rt.CopyTexture.View);
                }
                fb = app.Factory.CreateFramebuffer(new FramebufferDescription(app.ScreenDepthTexture, colorTargets.ToArray()));
            }

            material.Build(fb, gbufferTextureViews);
            materials.TryAdd(name, material);
            return material;
        }

        public Material GetMaterial(string name)
        {
            if (!materials.ContainsKey(name)) return null;
            return materials[name];
        }

        public void PrepareMaterials()
        {
            foreach (var material in materials.Values)
            {
                material.IsFramebufferCleared = false;
            }
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