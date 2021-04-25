using System;
using System.Collections.Generic;
using System.Numerics;
using Veldrid;

namespace Deremis.Engine.Objects
{
    public class Material : DObject
    {
        public override string Type => "Material";
        public Shader Shader { get; private set; }
        public Pipeline Pipeline { get; private set; }

        private readonly Dictionary<string, Shader.Property> properties = new Dictionary<string, Shader.Property>();

        public Material(string name, Shader shader, Pipeline pipeline) : base(name)
        {
            this.Shader = shader;
            this.Pipeline = pipeline;

            foreach (var property in shader.Properties)
            {
                var val = property.Value;
                val.Value = GetDefaultParameterValue(property.Value.Format);
                properties.Add(property.Key, val);
            }
        }

        public void SetProperty<T>(string name, T value) where T : unmanaged
        {
            if (!properties.ContainsKey(name)) return;
            var property = properties[name];
            property.Value = value;
            properties[name] = property;
        }

        public float[] GetValueArray()
        {
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
                                values.Add(0);
                            }
                        }
                    }
                    values.AddRange(array);
                }
            }

            return values.ToArray();
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

        public class ShaderPropertyOrderCompare : IComparer<Shader.Property>
        {
            public int Compare(Shader.Property x, Shader.Property y)
            {
                return x.Order.CompareTo(y.Order);
            }
        }
    }
}