using System.Collections.Generic;
using System.Numerics;

namespace Deremis.Engine.Systems.Components
{
    public struct Light
    {
        public float type;
        public Vector3 color;
        public float range;
        public float innerCutoff;
        public float outerCutoff;

        // this is fucking cursed
        public float[] GetValueArray(ref Transform transform)
        {
            var values = new List<float>();

            float[] array = new float[3];
            transform.position.CopyTo(array);
            values.AddRange(array);
            values.Add(0f); // padding
            transform.forward.CopyTo(array);
            values.AddRange(array);
            values.Add(0f); // padding
            color.CopyTo(array);
            values.AddRange(array);

            values.Add(type);
            values.Add(range);
            values.Add(innerCutoff);
            values.Add(outerCutoff);
            values.Add(0f); // padding

            return values.ToArray();
        }
    }
}