using System;
using System.Collections.Generic;
using System.Numerics;
using Deremis.System;
using Deremis.System.Assets;
using Veldrid;

namespace Deremis.Engine.Objects
{
    public class Material : DObject
    {
        public override string Type => "Material";
        public Shader Shader { get; private set; }
        public Pipeline Pipeline { get; private set; }
        public Sampler Sampler { get; set; } = Application.current.GraphicsDevice.Aniso4xSampler;
        public ResourceSet ResourceSet { get; private set; }
        public Framebuffer Framebuffer { get; private set; }
        public Material DeferredLightingMaterial { get; private set; }

        private bool isBufferDirty = true;
        private readonly Dictionary<string, Shader.Property> properties = new Dictionary<string, Shader.Property>();
        private readonly Dictionary<string, Shader.Resource> resources = new Dictionary<string, Shader.Resource>();
        private float[] rawValues;
        private ResourceLayout resourceLayout;

        public Material(string name, Shader shader) : base(name)
        {
            this.Shader = shader;

            foreach (var property in shader.Properties)
            {
                var val = property.Value;
                val.Value = GetDefaultParameterValue(property.Value.Format);
                properties.Add(property.Key, val);
            }

            foreach (var resource in shader.Resources)
            {
                var val = resource.Value;
                resources.Add(resource.Key, val);
            }
        }

        public void Build(Framebuffer framebuffer, List<TextureView> gbufferTextureViews)
        {
            var app = Application.current;
            Pipeline?.Dispose();
            Framebuffer?.Dispose();
            Framebuffer = framebuffer ?? app.ScreenFramebuffer;

            var resources = new List<Shader.Resource>(this.resources.Values).ToArray();
            Array.Sort(resources, new ShaderResourceOrderCompare());

            var layoutDescriptions = new List<ResourceLayoutElementDescription>();
            foreach (var resource in resources)
            {
                layoutDescriptions.Add(new ResourceLayoutElementDescription(resource.Name, resource.Kind, ShaderStages.Fragment));
            }
            layoutDescriptions.Add(new ResourceLayoutElementDescription("Sampler", ResourceKind.Sampler, ShaderStages.Fragment));
            resourceLayout = app.Factory.CreateResourceLayout(new ResourceLayoutDescription(layoutDescriptions.ToArray()));

            var description = Shader.DefaultPipeline;
            description.ResourceLayouts = new ResourceLayout[] { app.MaterialManager.GeneralResourceLayout, resourceLayout };
            description.Outputs = Framebuffer.OutputDescription;
            Pipeline = app.Factory.CreateGraphicsPipeline(description);

            if (Shader.IsDeferred && gbufferTextureViews != null)
            {
                DeferredLightingMaterial = app.MaterialManager.CreateMaterial("deferred_lighting", Shader.DeferredLightingShader);
                DeferredLightingMaterial.SetupGbuffer(gbufferTextureViews);
                app.Draw.RegisterDeferred(this);
            }

            BuildResourceSet();
        }

        public void SetupGbuffer(List<TextureView> gbufferTextureViews)
        {
            if (gbufferTextureViews.Count != resources.Count) return;

            var resourceNames = new List<string>(resources.Keys).ToArray();
            for (var i = 0; i < gbufferTextureViews.Count; i++)
            {
                SetTexture(resourceNames[i], gbufferTextureViews[i]);
            }
        }

        public void BuildResourceSet()
        {
            var app = Application.current;
            ResourceSet?.Dispose();
            var bindableResources = new List<BindableResource>();

            var resources = new List<Shader.Resource>(this.resources.Values).ToArray();
            Array.Sort(resources, new ShaderResourceOrderCompare());

            var layoutDescriptions = new List<ResourceLayoutElementDescription>();
            foreach (var resource in resources)
            {
                bindableResources.Add(resource.Value ?? GetDefaultResourceValue(resource.Kind));
            }
            bindableResources.Add(Sampler);

            ResourceSet = app.Factory.CreateResourceSet(new ResourceSetDescription(resourceLayout, bindableResources.ToArray()));
            ResourceSet.Name = $"{Name}_RS";
        }

        public void SetProperty<T>(string name, T value) where T : unmanaged
        {
            if (DeferredLightingMaterial != null)
            {
                DeferredLightingMaterial.SetProperty(name, value);
            }

            if (!properties.ContainsKey(name)) return;
            var property = properties[name];
            property.Value = value;
            properties[name] = property;
            isBufferDirty = true;
        }

        public void SetTexture(string name, Texture texture)
        {
            SetTexture(name, texture.View);
        }

        public void SetTexture(string name, TextureView view)
        {
            if (!resources.ContainsKey(name)) return;
            var resource = resources[name];
            resource.Value = view;
            resources[name] = resource;
            BuildResourceSet();
        }

        public void SetSampler(SamplerDescription description)
        {
            Sampler = Application.current.Factory.CreateSampler(description);
            BuildResourceSet();
        }

        public float[] GetValueArray()
        {
            if (!isBufferDirty && rawValues != null) return rawValues;

            var values = new List<float>();
            var properties = new List<Shader.Property>(this.properties.Values).ToArray();
            Array.Sort(properties, new ShaderPropertyOrderCompare());

            foreach (var property in properties)
            {
                float[] array = null;
                switch (property.Format)
                {
                    case VertexElementFormat.Float1:
                        array = new[] { (float)property.Value };
                        break;
                    case VertexElementFormat.Float2:
                        array = new float[2];
                        ((Vector2)property.Value).CopyTo(array);
                        break;
                    case VertexElementFormat.Float3:
                        array = new float[3];
                        ((Vector3)property.Value).CopyTo(array);
                        break;
                    case VertexElementFormat.Float4:
                        array = new float[4];
                        ((Vector4)property.Value).CopyTo(array);
                        break;
                }
                if (array != null)
                {
                    // padding for vectors above 2 components
                    if (array.Length > 2)
                    {
                        var remnants = 2 - values.Count % 2;
                        if (remnants != 2)
                        {
                            for (var i = 0; i < remnants; i++)
                            {
                                values.Add(0f);
                            }
                        }
                    }
                    values.AddRange(array);
                }
            }
            rawValues = values.ToArray();
            isBufferDirty = false;
            return rawValues;
        }

        public override void Dispose()
        {
            resourceLayout?.Dispose();
            ResourceSet?.Dispose();
            Pipeline?.Dispose();
            Framebuffer?.Dispose();
        }

        public static object GetDefaultParameterValue(VertexElementFormat format)
        {
            switch (format)
            {
                case VertexElementFormat.Float1: return 1;
                case VertexElementFormat.Float2: return Vector2.Zero;
                case VertexElementFormat.Float3: return Vector3.Zero;
                case VertexElementFormat.Float4: return Vector4.Zero;
                default: return null;
            }
        }

        public static BindableResource GetDefaultResourceValue(ResourceKind kind)
        {
            // TODO other kinds???
            var missingTex = AssetManager.current.Get<Texture>(Application.MissingTex);
            if (missingTex != null)
            {
                return missingTex.View;
            }
            return null;
        }

        public class ShaderPropertyOrderCompare : IComparer<Shader.Property>
        {
            public int Compare(Shader.Property x, Shader.Property y)
            {
                return x.Order.CompareTo(y.Order);
            }
        }

        public class ShaderResourceOrderCompare : IComparer<Shader.Resource>
        {
            public int Compare(Shader.Resource x, Shader.Resource y)
            {
                return x.Order.CompareTo(y.Order);
            }
        }
    }
}