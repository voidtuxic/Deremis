using System.Numerics;

namespace Deremis.Engine.Systems.Components
{
    public struct Transform
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;

        public Vector3 forward => Vector3.Transform(-Vector3.UnitZ, rotation);

        public Matrix4x4 ToMatrix()
        {
            return Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position);
        }

        public Matrix4x4 ToViewMatrix()
        {
            return ToViewMatrix(Vector3.UnitY);
        }

        public Matrix4x4 ToViewMatrix(Vector3 up)
        {
            return Matrix4x4.CreateLookAt(position, position + forward, up);
        }

        public static Transform FromMatrix(Matrix4x4 matrix)
        {
            Vector3 position;
            Quaternion rotation;
            Vector3 scale;
            Matrix4x4.Decompose(matrix, out scale, out rotation, out position);
            return new Transform
            {
                position = position,
                rotation = rotation,
                scale = scale
            };
        }
    }
}