using System.Numerics;

namespace Deremis.Engine.Systems.Components
{
    public struct Camera
    {
        public Matrix4x4 projection;

        public static Camera CreatePerspective(float fieldOfView, float aspectRatio, float near, float far)
        {
            return new Camera { projection = Matrix4x4.CreatePerspectiveFieldOfView(fieldOfView, aspectRatio, near, far) };
        }
    }
}