using System.Numerics;
using System.Runtime.CompilerServices;
using Veldrid;

namespace Deremis.Engine.Rendering
{
    public struct Vertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 UV;
        public Vector3 Tangent;
        public Vector3 Bitangent;

        public static uint SizeInBytes = (uint)Unsafe.SizeOf<Vertex>();

        public static VertexLayoutDescription VertexLayout = new VertexLayoutDescription(
            new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
            new VertexElementDescription("Normal", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
            new VertexElementDescription("UV", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
            new VertexElementDescription("Tangent", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
            new VertexElementDescription("Bitangent", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3));
    }
}