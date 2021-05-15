using System;
using System.Collections.Generic;
using DefaultEcs;
using DefaultEcs.System;
using DefaultEcs.Threading;
using Deremis.Engine.Objects;
using Deremis.Engine.Systems.Components;
using Octree;
using Veldrid.Utilities;

namespace Deremis.Engine.Systems
{
    public class LightVolumeSystem : AEntitySetSystem<float>
    {
        public const int MAX_LIGHTS = 4;

        private (Transform, Light) sunLight;
        private BoundsOctree<Light> pointLightOctree;
        private readonly Dictionary<Light, Transform> pointLightTransforms = new Dictionary<Light, Transform>();
        private readonly Scene scene;

        public Light SunLight => sunLight.Item2;

        public LightVolumeSystem(Scene scene, IParallelRunner runner)
            : base(
                scene.World.GetEntities()
                    .With<Light>()
                    .WhenChanged<Transform>()
                    .AsSet(),
                runner)
        {
            this.scene = scene;
            pointLightOctree = new BoundsOctree<Light>(2048, Point.Zero, 1, 1);
        }

        protected override void Update(float state, in Entity entity)
        {
            ref var metadata = ref entity.Get<Metadata>();
            if (!metadata.scene.Equals(scene.Name)) return;
            ref var transform = ref entity.Get<Transform>();
            ref var light = ref entity.Get<Light>();
            if (light.type == 1)
            {
                pointLightTransforms.Remove(light);
                pointLightOctree.Remove(light);
            }
            RegisterLight(transform, light);
        }

        public void RegisterLight(in Transform transform, in Light light)
        {
            switch (light.type)
            {
                case 0:
                    sunLight = (transform, light);
                    break;
                case 1:
                    pointLightTransforms.Add(light, transform);
                    pointLightOctree.Add(light, new Octree.BoundingBox(new Point(transform.position.X, transform.position.Y, transform.position.Z), Point.One * light.range));
                    break;
            }
        }

        public float[] GetNearbyValues(in Transform transform, float radius)
        {
            var lightValues = new List<float>();
            lightValues.AddRange(SunLight.GetValueArray(ref sunLight.Item1));
            var lights = new List<Light>();
            pointLightOctree.GetColliding(lights, new Octree.BoundingBox(new Point(transform.position.X, transform.position.Y, transform.position.Z), Point.One * radius));
            for (var i = 1; i < MAX_LIGHTS; i++)
            {
                if (lights.Count >= i)
                {
                    var lightTransform = pointLightTransforms[lights[i - 1]];
                    lightValues.AddRange(lights[i - 1].GetValueArray(ref lightTransform));
                }
                else
                {
                    var emptyTransform = new Transform();
                    lightValues.AddRange(new Light().GetValueArray(ref emptyTransform));
                }
            }
            return lightValues.ToArray();
        }
    }
}