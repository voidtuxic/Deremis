using System;
using System.Collections.Generic;
using System.Numerics;
using Deremis.Engine.Systems;
using Deremis.Platform;
using Deremis.Platform.Assets;
using Veldrid;

namespace Deremis.Engine.Objects
{
    public class Material : DObject
    {
        private static SamplerDescription shadowMapSampler = new SamplerDescription
        {
            AddressModeU = SamplerAddressMode.Border,
            AddressModeV = SamplerAddressMode.Border,
            AddressModeW = SamplerAddressMode.Border,
            Filter = SamplerFilter.MinLinear_MagLinear_MipLinear,
            LodBias = 0,
            MinimumLod = 0,
            MaximumLod = uint.MaxValue,
            BorderColor = SamplerBorderColor.TransparentBlack
        };
        private static SamplerDescription screenSampler = new SamplerDescription
        {
            AddressModeU = SamplerAddressMode.Border,
            AddressModeV = SamplerAddressMode.Border,
            AddressModeW = SamplerAddressMode.Border,
            Filter = SamplerFilter.MinLinear_MagLinear_MipLinear,
            LodBias = 0,
            MinimumLod = 0,
            MaximumLod = uint.MaxValue,
            BorderColor = SamplerBorderColor.TransparentBlack
        };

        private readonly List<Pipeline> pipelines = new List<Pipeline>();

        public override string Type => "Material";
        public Shader Shader { get; private set; }
        public Sampler Sampler { get; set; } = Application.current.GraphicsDevice.Aniso4xSampler;
        public ResourceSet ResourceSet { get; private set; }
        public Framebuffer Framebuffer { get; private set; }
        public Framebuffer PassFramebuffer { get; private set; }
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
                val.Value = GetDefaultParameterValue(property.Value);
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

            ClearPipelines();

            if (Framebuffer != app.ScreenFramebuffer)
            {
                Framebuffer?.Dispose();
            }
            Framebuffer = framebuffer ?? app.ScreenFramebuffer;

            var resources = new List<Shader.Resource>(this.resources.Values).ToArray();
            Array.Sort(resources, new ShaderResourceOrderCompare());

            bool hasShadowmap = false;
            var layoutDescriptions = new List<ResourceLayoutElementDescription>();
            foreach (var resource in resources)
            {
                layoutDescriptions.Add(new ResourceLayoutElementDescription(resource.Name, resource.Kind, ShaderStages.Fragment));
                if (ShadowRenderSystem.IsShadowMap(resource.Name))
                {
                    layoutDescriptions.Add(new ResourceLayoutElementDescription(resource.Name, resource.Kind, ShaderStages.Fragment));
                    layoutDescriptions.Add(new ResourceLayoutElementDescription(resource.Name, resource.Kind, ShaderStages.Fragment));
                    hasShadowmap = true;
                }
            }
            layoutDescriptions.Add(new ResourceLayoutElementDescription("texSampler", ResourceKind.Sampler, ShaderStages.Fragment));
            if (hasShadowmap)
            {
                layoutDescriptions.Add(new ResourceLayoutElementDescription("shadowMapSampler", ResourceKind.Sampler, ShaderStages.Fragment));
            }
            resourceLayout = app.Factory.CreateResourceLayout(new ResourceLayoutDescription(layoutDescriptions.ToArray()));

            var description = Shader.GetPipelineDescription(0);
            description.ResourceLayouts = new ResourceLayout[] { app.MaterialManager.GeneralResourceLayout, resourceLayout };
            description.Outputs = Framebuffer.OutputDescription;
            pipelines.Add(app.Factory.CreateGraphicsPipeline(description));

            if (Shader.IsDeferred && gbufferTextureViews != null)
            {
                DeferredLightingMaterial = app.MaterialManager.CreateMaterial("deferred_lighting", Shader.DeferredLightingShader);
                DeferredLightingMaterial.SetupGbuffer(gbufferTextureViews);
                app.ForwardRender.RegisterDeferred(this);
            }
            else if (gbufferTextureViews != null)
            {
                SetupGbuffer(gbufferTextureViews);
            }

            BuildResourceSet();
        }

        public void SetupGbuffer(List<TextureView> gbufferTextureViews)
        {
            if (gbufferTextureViews.Count > resources.Count) return;

            var resourceNames = new List<string>(resources.Keys).ToArray();
            for (var i = 0; i < gbufferTextureViews.Count; i++)
            {
                SetTexture(resourceNames[i], gbufferTextureViews[i]);
            }
        }

        public void SetupMultipass(List<Veldrid.Texture> colorTargets)
        {
            if (!Shader.IsMultipass) return;
            var app = Application.current;

            ClearPipelines();
            PassFramebuffer?.Dispose();
            PassFramebuffer = app.Factory.CreateFramebuffer(new FramebufferDescription(app.ScreenDepthTexture, colorTargets.ToArray()));

            for (var i = 0; i < Shader.PassCount; i++)
            {
                var description = Shader.GetPipelineDescription(i);
                description.ResourceLayouts = new ResourceLayout[] { app.MaterialManager.GeneralResourceLayout, resourceLayout };
                description.Outputs = i == Shader.PassCount - 1 ? Framebuffer.OutputDescription : PassFramebuffer.OutputDescription;
                pipelines.Add(app.Factory.CreateGraphicsPipeline(description));
            }
        }

        public void SetupMultipass(Framebuffer framebuffer)
        {
            if (!Shader.IsMultipass) return;
            var app = Application.current;

            ClearPipelines();
            PassFramebuffer?.Dispose();
            PassFramebuffer = framebuffer;

            for (var i = 0; i < Shader.PassCount; i++)
            {
                var description = Shader.GetPipelineDescription(i);
                description.ResourceLayouts = new ResourceLayout[] { app.MaterialManager.GeneralResourceLayout, resourceLayout };
                description.Outputs = framebuffer.OutputDescription;
                pipelines.Add(app.Factory.CreateGraphicsPipeline(description));
            }
        }

        public void BuildResourceSet()
        {
            var app = Application.current;
            ResourceSet?.Dispose();
            var bindableResources = new List<BindableResource>();

            var resources = new List<Shader.Resource>(this.resources.Values).ToArray();
            Array.Sort(resources, new ShaderResourceOrderCompare());

            bool hasShadowmap = false;
            foreach (var resource in resources)
            {
                if (ShadowRenderSystem.IsShadowMap(resource.Name))
                {
                    bindableResources.Add(app.ShadowRender.GetView(1));
                    bindableResources.Add(app.ShadowRender.GetView(4));
                    bindableResources.Add(app.ShadowRender.GetView(16));
                    hasShadowmap = true;
                    continue;
                }
                bindableResources.Add(resource.Value ?? GetDefaultResourceValue(resource));
            }
            bindableResources.Add(Sampler);
            if (hasShadowmap)
            {
                bindableResources.Add(Application.current.Factory.CreateSampler(shadowMapSampler));
            }

            ResourceSet = app.Factory.CreateResourceSet(new ResourceSetDescription(resourceLayout, bindableResources.ToArray()));
            ResourceSet.Name = Name;
        }

        public void SetProperty<T>(string name, T value) where T : unmanaged
        {
            SetPropertyInternal(name, value);
        }

        public void SetProperty<T>(string name, T[] value) where T : unmanaged
        {
            SetPropertyInternal(name, value);
        }

        private void SetPropertyInternal<T>(string name, T value)
        {
            if (DeferredLightingMaterial != null)
            {
                DeferredLightingMaterial.SetPropertyInternal(name, value);
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

        public void SetScreenSampler()
        {
            SetSampler(screenSampler);
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
                        if (property.ArrayCount == 1)
                            array = new[] { (float)property.Value };
                        else array = (float[])property.Value;
                        break;
                    case VertexElementFormat.Float2:
                        if (property.ArrayCount == 1)
                        {
                            array = new float[2];
                            ((Vector2)property.Value).CopyTo(array);
                        }
                        else
                        {
                            var data = (Vector2[])property.Value;
                            var floats = new List<float>();
                            foreach (var val in data)
                            {
                                floats.Add(val.X);
                                floats.Add(val.Y);
                            }
                            array = floats.ToArray();
                        }
                        break;
                    case VertexElementFormat.Float3:
                        if (property.ArrayCount == 1)
                        {
                            array = new float[3];
                            ((Vector3)property.Value).CopyTo(array);
                        }
                        else
                        {
                            var data = (Vector3[])property.Value;
                            var floats = new List<float>();
                            foreach (var val in data)
                            {
                                floats.Add(val.X);
                                floats.Add(val.Y);
                                floats.Add(val.Z);
                            }
                            array = floats.ToArray();
                        }
                        break;
                    case VertexElementFormat.Float4:
                        if (property.ArrayCount == 1)
                        {
                            array = new float[4];
                            ((Vector4)property.Value).CopyTo(array);
                        }
                        else
                        {
                            var data = (Vector4[])property.Value;
                            var floats = new List<float>();
                            foreach (var val in data)
                            {
                                floats.Add(val.X);
                                floats.Add(val.Y);
                                floats.Add(val.Z);
                                floats.Add(val.W);
                            }
                            array = floats.ToArray();
                        }
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

        public Pipeline GetPipeline(int passIndex)
        {
            if (pipelines.Count <= passIndex) return null;
            return pipelines[passIndex];
        }

        private void ClearPipelines()
        {
            foreach (var pipeline in pipelines)
            {
                pipeline.Dispose();
            }
            pipelines.Clear();
        }

        public override void Dispose()
        {
            resourceLayout?.Dispose();
            ResourceSet?.Dispose();
            ClearPipelines();
            Framebuffer?.Dispose();
            PassFramebuffer?.Dispose();
        }

        public static object GetDefaultParameterValue(Shader.Property property)
        {
            switch (property.Format)
            {
                case VertexElementFormat.Float1: return property.ArrayCount > 1 ? new float[property.ArrayCount] : 0f;
                case VertexElementFormat.Float2: return property.ArrayCount > 1 ? new Vector2[property.ArrayCount] : Vector2.Zero;
                case VertexElementFormat.Float3: return property.ArrayCount > 1 ? new Vector3[property.ArrayCount] : Vector3.Zero;
                case VertexElementFormat.Float4: return property.ArrayCount > 1 ? new Vector4[property.ArrayCount] : Vector4.Zero;
                default: return null;
            }
        }

        public static BindableResource GetDefaultResourceValue(Shader.Resource resource)
        {
            if (resource.Kind == ResourceKind.Sampler) return Application.current.GraphicsDevice.LinearSampler;
            Texture missingTex = null;
            if (resource.IsNormal)
            {
                missingTex = AssetManager.current.Get<Texture>(Application.MissingNormalTex);
            }
            else
            {
                missingTex = AssetManager.current.Get<Texture>(Application.MissingTex);
            }
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