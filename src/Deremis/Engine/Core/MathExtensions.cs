using System;

namespace Deremis.Engine.Core
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
    }
}