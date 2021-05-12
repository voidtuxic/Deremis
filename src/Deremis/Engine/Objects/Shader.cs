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
        private readonly List<byte[]> fragmentCodes = new List<byte[]>();
        private readonly List<GraphicsPipelineDescription> pipelines = new List<GraphicsPipelineDescription>();

        // TODO add platform check
        public bool IsPlatformDependent { get; set; }

        public struct Property
        {
            public int Order;
            public VertexElementFormat Format;
            public object Value;
            public int ArrayCount;
        }

        public struct Resource
        {
            public int Order;
            public string Name;
            public ResourceKind Kind;
            public BindableResource Value;
            public bool IsNormal;
        }

        public List<Veldrid.Shader[]> Shaders { get; } = new List<Veldrid.Shader[]>();
        public GraphicsPipelineDescription DefaultPipeline { get; private set; }
        public int PassCount => pipelines.Count;
        public Dictionary<string, Property> Properties { get; private set; } = new Dictionary<string, Property>();
        public Dictionary<string, Resource> Resources { get; private set; } = new Dictionary<string, Resource>();
        public List<(string, PixelFormat)> Outputs { get; private set; } = new List<(string, PixelFormat)>();
        public bool IsDeferred { get; private set; }
        public Shader DeferredLightingShader { get; private set; }
        public bool IsMultipass { get; private set; }
        public int PassColorTargetCount { get; private set; }
        public string PassColorTargetBaseName { get; private set; }

        public Shader(string name) : base(name)
        {
        }

        public void SetDeferred(Shader deferredLightingShader)
        {
            IsDeferred = true;
            DeferredLightingShader = deferredLightingShader;
        }

        public void SetMultipass(string name, int targetCount)
        {
            IsMultipass = true;
            PassColorTargetBaseName = name;
            PassColorTargetCount = targetCount;
        }

        public void SetVertexCode(string code)
        {
            vertexCode = Encoding.UTF8.GetBytes(code);
        }

        public void SetFragmentCode(int passIndex, string code)
        {
            if (fragmentCodes.Count <= passIndex)
            {
                for (var i = fragmentCodes.Count; i <= passIndex; i++)
                {
                    fragmentCodes.Add(null);
                }
            }
            fragmentCodes[passIndex] = Encoding.UTF8.GetBytes(code);
        }

        public void SetDefaultPipeline(GraphicsPipelineDescription pipelineDescription)
        {
            DefaultPipeline = pipelineDescription;
        }

        public GraphicsPipelineDescription GetPipelineDescription(int passIndex)
        {
            if (pipelines.Count <= passIndex) return DefaultPipeline;

            return pipelines[passIndex];
        }

        public void Build()
        {
            var vertexShaderDesc = new ShaderDescription(
                ShaderStages.Vertex,
                vertexCode,
                "main");
            ClearShaders();
            for (var i = 0; i < fragmentCodes.Count; i++)
            {
                var fragmentShaderDesc = new ShaderDescription(
                    ShaderStages.Fragment,
                    fragmentCodes[i],
                    "main");

                if (IsPlatformDependent)
                {
                    Shaders.Add(new Veldrid.Shader[] {
                        Application.current.Factory.CreateShader(vertexShaderDesc),
                        Application.current.Factory.CreateShader(fragmentShaderDesc)
                    });
                }
                else
                {
                    Shaders.Add(Application.current.Factory.CreateFromSpirv(vertexShaderDesc, fragmentShaderDesc));
                }

                var pipeline = DefaultPipeline;
                pipeline.ShaderSet = new ShaderSetDescription(
                    vertexLayouts: new VertexLayoutDescription[] { Vertex.VertexLayout },
                    shaders: Shaders[i]
                );
                pipelines.Add(pipeline);
            }
        }

        public override void Dispose()
        {
            ClearShaders();
        }

        private void ClearShaders()
        {
            foreach (var pair in Shaders)
            {
                foreach (var shader in pair)
                {
                    shader.Dispose();
                }
            }
            Shaders.Clear();
        }
    }
}