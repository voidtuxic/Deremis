using System;
using System.Numerics;
using DefaultEcs;
using DefaultEcs.System;
using Deremis.Engine.Systems.Components;
using Deremis.Platform;
using Veldrid;

namespace Deremis.Viewer
{
    public class FreeCamera
    {
        private readonly Application app;
        private readonly EntitySet cameraSet;
        private int cameraId;

        private Vector2 previousMousePosition;
        public Vector2 MouseSensitivity { get; set; } = new Vector2(0.5f);
        public float Speed { get; set; } = 30f;

        private Vector2 moveDelta;

        public FreeCamera(Application app)
        {
            this.app = app;
            var world = app.DefaultWorld;
            cameraSet = world.GetEntities().With<Camera>().With<Transform>().With<Metadata>().AsSet();
            app.MainSystem.Add(new ActionSystem<float>(Update));
            app.Window.KeyUp += OnKeyUp;
            app.Window.KeyDown += OnKeyDown;
        }

        private void OnKeyUp(KeyEvent obj)
        {
            switch (obj.Key)
            {
                case Key.W:
                case Key.S:
                    moveDelta.Y = 0;
                    break;
                case Key.D:
                case Key.A:
                    moveDelta.X = 0;
                    break;
            }
        }

        private void OnKeyDown(KeyEvent obj)
        {
            switch (obj.Key)
            {
                case Key.W:
                    moveDelta.Y = 1;
                    break;
                case Key.S:
                    moveDelta.Y = -1;
                    break;
                case Key.D:
                    moveDelta.X = -1;
                    break;
                case Key.A:
                    moveDelta.X = 1;
                    break;
            }
        }

        public void SetCameraId(int id)
        {
            cameraId = id;
        }

        public void Update(float deltaSeconds)
        {
            if (cameraSet.Count == 0)
            {
                return;
            }

            Span<Entity> cameras = stackalloc Entity[cameraSet.Count];
            cameraSet.GetEntities().CopyTo(cameras);
            foreach (ref readonly Entity camEntity in cameras)
            {
                ref var metadata = ref camEntity.Get<Metadata>();
                if (metadata.entityId == cameraId)
                {
                    HandleInput(deltaSeconds, in camEntity);
                    break;
                }
            }
        }

        private void HandleInput(float deltaSeconds, in Entity camEntity)
        {
            ref var transform = ref camEntity.Get<Transform>();

            var mousePosition = app.InputSnapshot.MousePosition;
            var lookDelta = mousePosition - previousMousePosition;
            var right = transform.Right;
            right = Vector3.Normalize(new Vector3(right.X, 0, right.Z));

            var rotation = transform.rotation;
            rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -lookDelta.X * MouseSensitivity.X * deltaSeconds)
                * Quaternion.CreateFromAxisAngle(right, lookDelta.Y * MouseSensitivity.Y * deltaSeconds)
                * rotation;

            transform.rotation = rotation;

            if (moveDelta.LengthSquared() != 0)
            {
                var move = Vector2.Normalize(moveDelta);
                transform.position += move.Y * transform.Forward * Speed * deltaSeconds;
                transform.position += move.X * right * Speed * deltaSeconds;
            }

            previousMousePosition = mousePosition;
        }
    }
}