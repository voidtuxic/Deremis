using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Deremis.Engine.Rendering.Resources
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TransformResource
    {
        public Matrix4x4 viewProjMatrix;
        public Matrix4x4 worldMatrix;
        public Matrix4x4 normalWorldMatrix;
        public Matrix4x4 viewMatrix;
        public Matrix4x4 projMatrix;
        public Matrix4x4 lightSpaceMatrix1;
        public Matrix4x4 lightSpaceMatrix2;
        public Matrix4x4 lightSpaceMatrix3;

        public static uint SizeInBytes = (uint)Unsafe.SizeOf<TransformResource>();
    }
}