using System;
using System.Numerics;
using DefaultEcs;
using DefaultEcs.Command;
using DefaultEcs.System;
using DefaultEcs.Threading;
using Deremis.Engine.Objects;
using Deremis.Engine.Systems.Components;
using Deremis.Engine.Systems.Extensions;
using Deremis.Platform;
using Veldrid.Utilities;

namespace Deremis.Engine.Systems
{
    [With(typeof(Transform))]
    [With(typeof(Drawable))]
    public class CullSystem : AEntitySetSystem<float>
    {
        private readonly Application app;
        private readonly EntitySet cameraSet;

        private EntityCommandRecorder recorder;
        private BoundingFrustum frustum;
        private BoundingSphere shadowSphere;
        private int cameraId;

        public CullSystem(Application app, World world, IParallelRunner runner) : base(world, runner)
        {
            this.app = app;
            cameraSet = world.GetEntities().With<Camera>().With<Transform>().With<Metadata>().AsSet();
            recorder = new EntityCommandRecorder(16384);
        }

        public void SetCameraId(int id)
        {
            cameraId = id;
        }

        protected override void PreUpdate(float elaspedTime)
        {
            Span<Entity> cameras = stackalloc Entity[cameraSet.Count];
            cameraSet.GetEntities().CopyTo(cameras);
            foreach (ref readonly Entity camEntity in cameras)
            {
                ref var metadata = ref camEntity.Get<Metadata>();
                if (metadata.entityId == cameraId)
                {
                    ref var transform = ref camEntity.Get<Transform>();
                    ref var camera = ref camEntity.Get<Camera>();
                    frustum = new BoundingFrustum(transform.ToViewMatrix() * camera.projection);
                    shadowSphere = new BoundingSphere(transform.position, Application.SHADOW_MAP_FAR);
                    break;
                }
            }
            recorder.Clear();
        }

        protected override void Update(float state, in Entity entity)
        {
            ref var drawable = ref entity.Get<Drawable>();
            // TODO omg dirty
            if (drawable.mesh.Equals(Rendering.Helpers.Skybox.NAME))
            {
                ref var r = ref entity.Get<Render>();
                if (!r.Screen)
                {
                    EntityRecord record = recorder.Record(entity);
                    record.Set(new Render(true, false));
                }
                return;
            }
            Mesh mesh = app.ForwardRender.GetMesh(drawable.mesh);
            if (mesh == null) return;
            var transform = entity.GetWorldTransform();
            var boundingBox = transform.Apply(mesh.BoundingBox);
            var containment = frustum.Contains(boundingBox);
            ref var render = ref entity.Get<Render>();
            if (render.Screen)
            {
                if (containment == ContainmentType.Disjoint)
                {
                    EntityRecord record = recorder.Record(entity);
                    bool shadows = false;
                    if (entity.Has<ShadowMapped>())
                    {
                        shadows = shadowSphere.Contains(transform.position);
                    }
                    record.Set(new Render(false, shadows));
                }
            }
            else
            {
                if (containment == ContainmentType.Contains || containment == ContainmentType.Intersects)
                {
                    EntityRecord record = recorder.Record(entity);
                    record.Set(new Render(true, entity.Has<ShadowMapped>()));
                }
            }
        }

        protected override void PostUpdate(float elaspedTime)
        {
            recorder.Execute();
        }
    }
}