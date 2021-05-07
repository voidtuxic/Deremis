using System;
using System.Numerics;
using Veldrid.Utilities;

namespace Deremis.Engine.Systems.Components
{
    public struct Transform
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;

        public Vector3 Forward => Vector3.Transform(-Vector3.UnitZ, rotation);
        public Vector3 Right => new Vector3(Forward.Z, Forward.Y, -Forward.X);

        public Transform(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }

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
            return Matrix4x4.CreateLookAt(position, position + Forward, up);
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

        public static Transform FromTarget(Vector3 position, Vector3 target, Vector3 up)
        {
            return new Transform
            {
                position = position,
                rotation = GetQuaternion(-Vector3.UnitZ, (target - position)),
                scale = Vector3.One
            };
        }

        // from https://stackoverflow.com/a/11741520
        private static Quaternion GetQuaternion(Vector3 u, Vector3 v)
        {
            float k_cos_theta = Vector3.Dot(u, v);
            float k = MathF.Sqrt(u.LengthSquared() * v.LengthSquared());

            if (k_cos_theta / k == -1)
            {
                // 180 degree rotation around any orthogonal vector
                return new Quaternion(Vector3.Normalize(GetOrthogonal(u)), 0);
            }

            return Quaternion.Normalize(new Quaternion(Vector3.Cross(u, v), k_cos_theta + k));
        }

        private static Vector3 GetOrthogonal(Vector3 v)
        {
            float x = MathF.Abs(v.X);
            float y = MathF.Abs(v.Y);
            float z = MathF.Abs(v.Z);

            Vector3 other = x < y ? (x < z ? Vector3.UnitX : Vector3.UnitZ) : (y < z ? Vector3.UnitY : Vector3.UnitZ);
            return Vector3.Cross(v, other);
        }

        public BoundingBox Apply(BoundingBox baseBox)
        {
            return BoundingBox.Transform(baseBox, ToMatrix());
        }

        public static Transform operator +(Transform l, Transform r)
        {
            return new Transform(
                r.position + Vector3.Transform(l.position, r.rotation),
                Quaternion.Concatenate(l.rotation, r.rotation),
                l.scale * r.scale);
        }
    }
}