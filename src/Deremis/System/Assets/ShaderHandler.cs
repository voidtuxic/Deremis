using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Deremis.Engine.Objects;
using Veldrid;
using Shader = Deremis.Engine.Objects.Shader;

namespace Deremis.System.Assets
{
    public class ShaderHandler : IAssetHandler
    {
        public string Name => "Shader Handler";
        private readonly ConcurrentDictionary<string, Shader> loadedShaders = new ConcurrentDictionary<string, Shader>();
        private readonly ConcurrentDictionary<string, string> internalShaders = new ConcurrentDictionary<string, string>();

        public T Get<T>(AssetDescription description) where T : DObject
        {
            if (loadedShaders.ContainsKey(description.name)) return loadedShaders[description.name] as T;

            var content = File.ReadAllText(AssetManager.current.Rebase(description.path));
            var shader = new Shader(description.name);

            var doc = new XmlDocument();
            doc.PreserveWhitespace = true;
            doc.LoadXml(content);
            var root = doc["shader"];

            foreach (XmlNode child in root)
            {
                switch (child.Name)
                {
                    case "config":
                        SetupConfig(child, shader);
                        break;
                    case "properties":
                        SetupProperties(child, shader);
                        break;
                    case "resources":
                        SetupResources(child, shader);
                        break;
                    case "vertex":
                        shader.SetVertexCode(BuildCode(child.InnerText));
                        break;
                    case "fragment":
                        shader.SetFragmentCode(BuildCode(child.InnerText));
                        break;
                }
            }
            shader.Build();
            loadedShaders.TryAdd(description.name, shader);
            return shader as T;
        }

        private string BuildCode(string rawCode)
        {
            var code = new List<string>(rawCode.Split("\r\n"));
            var codeBuilder = new StringBuilder();

            foreach (var line in code)
            {
                if (line.StartsWith("#include \""))
                {
                    var fileInclude = line.Remove(0, 10);
                    fileInclude = fileInclude.Trim('"');
                    var content = GetInternal(fileInclude);
                    codeBuilder.AppendLine(BuildCode(content));
                }
                else
                {
                    codeBuilder.AppendLine(line);
                }
            }
            // string pattern = @"\b\w+es\b";
            // var rgx = new Regex(pattern);
            // var match = rgx.Matches(code);

            return codeBuilder.ToString();
        }

        private string GetInternal(string name)
        {
            if (internalShaders.ContainsKey(name)) return internalShaders[name];
            var content = File.ReadAllText(AssetManager.current.Rebase($"Shaders/{name}"));
            internalShaders.TryAdd(name, content);
            return content;
        }

        private void SetupConfig(XmlNode node, Shader shader)
        {
            var pipelineDescription = new GraphicsPipelineDescription();
            // TODO support more properties
            foreach (XmlNode child in node)
            {
                switch (child.Name)
                {
                    case "BlendState":
                        switch (child.InnerText)
                        {
                            case "SingleAlphaBlend":
                                pipelineDescription.BlendState = BlendStateDescription.SingleAlphaBlend;
                                break;
                            case "SingleOverrideBlend":
                                pipelineDescription.BlendState = BlendStateDescription.SingleOverrideBlend;
                                break;
                            case "SingleAdditiveBlend":
                                pipelineDescription.BlendState = BlendStateDescription.SingleAdditiveBlend;
                                break;
                            case "SingleDisabled":
                                pipelineDescription.BlendState = BlendStateDescription.SingleDisabled;
                                break;
                        }
                        break;
                    case "DepthStencilState":
                        pipelineDescription.DepthStencilState = new DepthStencilStateDescription(
                            depthTestEnabled: bool.Parse(child.Attributes["depthTestEnabled"].Value),
                            depthWriteEnabled: bool.Parse(child.Attributes["depthWriteEnabled"].Value),
                            comparisonKind: Enum.Parse<ComparisonKind>(child.Attributes["comparisonKind"].Value));
                        break;
                    case "RasterizerState":
                        pipelineDescription.RasterizerState = new RasterizerStateDescription(
                            cullMode: Enum.Parse<FaceCullMode>(child.Attributes["cullMode"].Value),
                            fillMode: Enum.Parse<PolygonFillMode>(child.Attributes["fillMode"].Value),
                            frontFace: Enum.Parse<FrontFace>(child.Attributes["frontFace"].Value),
                            depthClipEnabled: bool.Parse(child.Attributes["depthClipEnabled"].Value),
                            scissorTestEnabled: bool.Parse(child.Attributes["scissorTestEnabled"].Value));
                        break;
                    case "PrimitiveTopology":
                        pipelineDescription.PrimitiveTopology = Enum.Parse<PrimitiveTopology>(child.InnerText);
                        break;
                }
            }
            shader.SetDefaultPipeline(pipelineDescription);
        }

        private void SetupProperties(XmlNode node, Shader shader)
        {
            int index = 0;
            foreach (XmlNode child in node)
            {
                var format = VertexElementFormat.Float1;
                switch (child.Name)
                {
                    case "float":
                        break;
                    case "vec2":
                        format = VertexElementFormat.Float2;
                        break;
                    case "vec3":
                        format = VertexElementFormat.Float3;
                        break;
                    case "vec4":
                        format = VertexElementFormat.Float4;
                        break;
                    default: continue;
                }
                shader.Properties.Add(child.Attributes["name"].Value, new Shader.Property
                {
                    Order = index,
                    Format = format
                });
                index++;
            }
        }

        private void SetupResources(XmlNode node, Shader shader)
        {
            int index = 0;
            foreach (XmlNode child in node)
            {
                var kind = ResourceKind.TextureReadOnly;
                // TODO more resource kinds???
                switch (child.Name)
                {
                    case "texture2d":
                        break;
                    default: continue;
                }
                shader.Resources.Add(child.Attributes["name"].Value, new Shader.Resource
                {
                    Name = child.Attributes["name"].Value,
                    Order = index,
                    Kind = kind
                });
                index++;
            }
        }

        public void Dispose()
        {
            foreach (var shader in loadedShaders.Values)
            {
                shader.Dispose();
            }
        }
    }
}