using System.Collections.Generic;
using System.Text;
using Deremis.Engine.Rendering;
using Deremis.Platform;
using Veldrid;
using Veldrid.SPIRV;

namespace Deremis.Engine.Objects
{
    public class Shader : DObject
    {
        private byte[] vertexCode;
        private byte[] fragmentCode;

        // TODO add platform check
        public bool IsPlatformDependent { get; set; }

        public struct Property
        {
            public int Order;
            public VertexElementFormat Format;
            public object Value;
        }

        public struct Resource
        {
            public int Order;
            public string Name;
            public ResourceKind Kind;
            public BindableResource Value;
            public bool IsNormal;
        }

        public Veldrid.Shader[] Shaders { get; private set; }
        public GraphicsPipelineDescription DefaultPipeline { get; private set; }
        public Dictionary<string, Property> Properties { get; private set; } = new Dictionary<string, Property>();
        public Dictionary<string, Resource> Resources { get; private set; } = new Dictionary<string, Resource>();
        public List<(string, PixelFormat)> Outputs { get; private set; } = new List<(string, PixelFormat)>();
        public bool IsDeferred { get; private set; }
        public Shader DeferredLightingShader { get; private set; }

        public Shader(string name) : base(name)
        {
        }

        public void SetDeferred(Shader deferredLightingShader)
        {
            IsDeferred = true;
            DeferredLightingShader = deferredLightingShader;
        }

        public void SetVertexCode(string code)
        {
            vertexCode = Encoding.UTF8.GetBytes(code);
        }

        public void SetFragmentCode(string code)
        {
            fragmentCode = Encoding.UTF8.GetBytes(code);
        }

        public void SetDefaultPipeline(GraphicsPipelineDescription pipelineDescription)
        {
            DefaultPipeline = pipelineDescription;
        }

        public void Build()
        {
            var vertexShaderDesc = new ShaderDescription(
                ShaderStages.Vertex,
                vertexCode,
                "main");
            var fragmentShaderDesc = new ShaderDescription(
                ShaderStages.Fragment,
                fragmentCode,
                "main");

            if (IsPlatformDependent)
            {
                Shaders = new Veldrid.Shader[] {
                    Application.current.Factory.CreateShader(vertexShaderDesc),
                    Application.current.Factory.CreateShader(fragmentShaderDesc)
                };
            }
            else
            {
                Shaders = Application.current.Factory.CreateFromSpirv(vertexShaderDesc, fragmentShaderDesc);
            }

            var pipeline = DefaultPipeline;
            pipeline.ShaderSet = new ShaderSetDescription(
                vertexLayouts: new VertexLayoutDescription[] { Vertex.VertexLayout },
                shaders: Shaders
            );
            SetDefaultPipeline(pipeline);
        }

        public override void Dispose()
        {
            foreach (var shader in Shaders)
            {
                shader.Dispose();
            }
        }
    }
}