using System;

namespace Deremis.Engine.Math
{
    public static class DMath
    {
        public static float ToRadians(float degrees)
        {
            return degrees * MathF.PI / 180f;
        }
        public static float ToDegrees(float radians)
        {
            return radians * 180f / MathF.PI;
        }

        public static float Lerp(float a, float b, float f)
        {
            return a + f * (b - a);
        }
    }
}