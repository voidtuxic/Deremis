
using System;
using System.Numerics;
using Deremis.Engine.Core;
using Deremis.Engine.Systems.Components;
using Deremis.Platform;

namespace Deremis.Engine.Rendering.Helpers
{
    /**
     * Represents the 3D cuboidal area of the world in which objects will cast
     * shadows (basically represents the orthographic projection area for the shadow
     * render pass). It is updated each frame to optimise the area, making it as
     * small as possible (to allow for optimal shadow map resolution) while not
     * being too small to avoid objects not having shadows when they should.
     * Everything inside the cuboidal area represented by this object will be
     * rendered to the shadow map in the shadow render pass. Everything outside the
     * area won't be.
     * 
     * @author Karl
     * taken and adapted to C# from https://www.dropbox.com/sh/g9vnfiubdglojuh/AACpq1KDpdmB8ZInYxhsKj2Ma/shadows?dl=0&preview=ShadowBox.java&subfolder_nav_tracking=1
     */
    public class ShadowBox
    {
        private const float OFFSET = 10;
        private readonly Application app;

        private float minX, maxX;
        private float minY, maxY;
        private float minZ, maxZ;
        private Matrix4x4 lightViewMatrix = Matrix4x4.Identity;
        private Matrix4x4 lightProjectionMatrix = Matrix4x4.Identity;

        private float farHeight, farWidth, nearHeight, nearWidth;

        /**
         * Creates a new shadow box and calculates some initial values relating to
         * the camera's view frustum, namely the width and height of the near plane
         * and (possibly adjusted) far plane.
         * 
         * @param lightViewMatrix
         *            - basically the "view matrix" of the light. Can be used to
         *            transform a point from world space into "light" space (i.e.
         *            changes a point's coordinates from being in relation to the
         *            world's axis to being in terms of the light's local axis).
         * @param camera
         *            - the in-game camera.
         */
        public ShadowBox(Application app)
        {
            this.app = app;
            CalculateWidthsAndHeights();
        }

        public Matrix4x4 GetLightViewProjectionMatrix(Transform cameraTransform, Transform lightTransform)
        {
            Vector3 center = -GetCenter();
            // lightProjectionMatrix = Matrix4x4.Identity;
            // lightProjectionMatrix.M11 = 2f / Width;
            // lightProjectionMatrix.M22 = 2f / Height;
            // lightProjectionMatrix.M33 = 2f / Length;
            // lightProjectionMatrix.M44 = 1;
            lightProjectionMatrix = Matrix4x4.CreateOrthographicOffCenter(
                -Application.SHADOW_MAP_FAR / 2f,
                Application.SHADOW_MAP_FAR / 2f,
                -Application.SHADOW_MAP_FAR / 2f,
                Application.SHADOW_MAP_FAR / 2f,
                0, Application.SHADOW_MAP_FAR);
            var forward = lightTransform.Forward;
            lightViewMatrix = Matrix4x4.CreateWorld(Vector3.Zero, forward, Vector3.UnitY);

            return lightProjectionMatrix * lightViewMatrix;
        }

        /**
         * Vector4.UnitYdates the bounds of the shadow box based on the light direction and the
         * camera's view frustum, to make sure that the box covers the smallest area
         * possible while still ensuring that everything inside the camera's view
         * (within a certain range) will cast shadows.
         */
        public void Update(Transform cameraTransform)
        {
            Quaternion rotation = cameraTransform.rotation;
            Vector3 forward = cameraTransform.Forward;



            Vector3 toFar = forward * Application.SHADOW_MAP_FAR;
            Vector3 toNear = forward * 0.1f;
            Vector3 centerNear = toNear + cameraTransform.position;
            Vector3 centerFar = toFar + cameraTransform.position;

            Vector3[] points = CalculateFrustumVertices(rotation, forward, centerNear,
                    centerFar);

            bool first = true;
            foreach (var point in points)
            {
                if (first)
                {
                    minX = point.X;
                    maxX = point.X;
                    minY = point.Y;
                    maxY = point.Y;
                    minZ = point.Z;
                    maxZ = point.Z;
                    first = false;
                    continue;
                }
                if (point.X > maxX)
                {
                    maxX = point.X;
                }
                else if (point.X < minX)
                {
                    minX = point.X;
                }
                if (point.Y > maxY)
                {
                    maxY = point.Y;
                }
                else if (point.Y < minY)
                {
                    minY = point.Y;
                }
                if (point.Z > maxZ)
                {
                    maxZ = point.Z;
                }
                else if (point.Z < minZ)
                {
                    minZ = point.Z;
                }
            }
            maxZ += OFFSET;
        }

        /**
         * Calculates the center of the "view cuboid" in light space first, and then
         * converts this to world space using the inverse light's view matrix.
         * 
         * @return The center of the "view cuboid" in world space.
         */
        public Vector3 GetCenter()
        {
            float x = (minX + maxX) / 2f;
            float y = (minY + maxY) / 2f;
            float z = (minZ + maxZ) / 2f;
            var cen = new Vector3(x, y, z);
            Matrix4x4.Invert(lightViewMatrix, out Matrix4x4 invertedLight);
            return Vector3.Transform(cen, invertedLight);
        }

        public float Width => maxX - minX;
        public float Height => maxY - minY;
        public float Length => maxZ - minZ;

        /**
         * Calculates the position of the vertex at each corner of the view frustum
         * in light space (8 vertices in total, so this returns 8 positions).
         * 
         * @param rotation
         *            - camera's rotation.
         * @param -Vector4.UnitZ
         *            - the direction that the camera is aiming, and thus the
         *            direction of the frustum.
         * @param centerNear
         *            - the center point of the frustum's near plane.
         * @param centerFar
         *            - the center point of the frustum's (possibly adjusted) far
         *            plane.
         * @return The positions of the vertices of the frustum in light space.
         */
        private Vector3[] CalculateFrustumVertices(Quaternion rotation, Vector3 forward,
                Vector3 centerNear, Vector3 centerFar)
        {
            Vector3 upVector = Vector3.Transform(Vector3.UnitY, rotation);
            Vector3 rightVector = Vector3.Cross(forward, upVector);
            Vector3 downVector = new Vector3(-upVector.X, -upVector.Y, -upVector.Z);
            Vector3 leftVector = new Vector3(-rightVector.X, -rightVector.Y, -rightVector.Z);
            Vector3 farTop = centerFar + new Vector3(upVector.X * farHeight, upVector.Y * farHeight, upVector.Z * farHeight);
            Vector3 farBottom = centerFar + new Vector3(downVector.X * farHeight, downVector.Y * farHeight, downVector.Z * farHeight);
            Vector3 nearTop = centerNear + new Vector3(upVector.X * nearHeight, upVector.Y * nearHeight, upVector.Z * nearHeight);
            Vector3 nearBottom = centerNear + new Vector3(downVector.X * nearHeight, downVector.Y * nearHeight, downVector.Z * nearHeight);
            Vector3[] points = new Vector3[8];
            points[0] = CalculateLightSpaceFrustumCorner(farTop, rightVector, farWidth);
            points[1] = CalculateLightSpaceFrustumCorner(farTop, leftVector, farWidth);
            points[2] = CalculateLightSpaceFrustumCorner(farBottom, rightVector, farWidth);
            points[3] = CalculateLightSpaceFrustumCorner(farBottom, leftVector, farWidth);
            points[4] = CalculateLightSpaceFrustumCorner(nearTop, rightVector, nearWidth);
            points[5] = CalculateLightSpaceFrustumCorner(nearTop, leftVector, nearWidth);
            points[6] = CalculateLightSpaceFrustumCorner(nearBottom, rightVector, nearWidth);
            points[7] = CalculateLightSpaceFrustumCorner(nearBottom, leftVector, nearWidth);
            return points;
        }

        /**
         * Calculates one of the corner vertices of the view frustum in world space
         * and converts it to light space.
         * 
         * @param startPoint
         *            - the starting center point on the view frustum.
         * @param direction
         *            - the direction of the corner from the start point.
         * @param width
         *            - the distance of the corner from the start point.
         * @return - The relevant corner vertex of the view frustum in light space.
         */
        private Vector3 CalculateLightSpaceFrustumCorner(Vector3 startPoint, Vector3 direction,
                float width)
        {
            Vector3 point = startPoint + new Vector3(direction.X * width, direction.Y * width, direction.Z * width);
            return Vector3.Transform(point, lightViewMatrix);
        }

        /**
         * Calculates the width and height of the near and far planes of the
         * camera's view frustum. However, this doesn't have to use the "actual" far
         * plane of the view frustum. It can use a shortened view frustum if desired
         * by bringing the far-plane closer, which would increase shadow resolution
         * but means that distant objects wouldn't cast shadows.
         */
        private void CalculateWidthsAndHeights()
        {
            // TODO this value prolly shouldn't be here
            var fov = MathF.PI / 8f;
            farWidth = Application.SHADOW_MAP_FAR * MathF.Tan(fov);
            nearWidth = 0.1f * MathF.Tan(fov);
            farHeight = farWidth / app.AspectRatio;
            nearHeight = nearWidth / app.AspectRatio;
        }

    }
}