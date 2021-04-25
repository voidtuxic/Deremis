using System.Numerics;
using System.Runtime.CompilerServices;

namespace Deremis.Engine.Rendering.Resources
{
    public struct TransformResource
    {
        public Matrix4x4 viewProjMatrix;
        public Matrix4x4 worldMatrix;
        public Matrix4x4 normalWorldMatrix;
        public static uint SizeInBytes = (uint)Unsafe.SizeOf<TransformResource>();
    }
}