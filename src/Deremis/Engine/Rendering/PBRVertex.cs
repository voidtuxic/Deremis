using System.Numerics;
using System.Runtime.CompilerServices;
using Veldrid;

namespace Deremis.Engine.Rendering
{
    public struct PBRVertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 UV;

        public static uint SizeInBytes = (uint)Unsafe.SizeOf<PBRVertex>();

        public static VertexLayoutDescription VertexLayout = new VertexLayoutDescription(
            new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
            new VertexElementDescription("Normal", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
            new VertexElementDescription("UV", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2));
    }
}