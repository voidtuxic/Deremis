using System;
using System.Collections.Generic;
using System.Numerics;
using DefaultEcs;
using Deremis.Engine.Systems;
using Deremis.Engine.Systems.Components;
using Deremis.Platform;

namespace Deremis.Engine.Objects
{
    public class Scene : DObject
    {
        private readonly Application app;
        private int entityCounter = 0;
        private int lightCounter = 0;
        private bool isEnabled = true;

        private readonly EntitySet sceneEntitiesSet;
        private readonly EntitySet disabledSceneEntitiesSet;
        private readonly EntitySet cameraSet;
        private readonly EntitySet lightSet;

        public World World => app.DefaultWorld;
        public Application App => app;
        public EntitySet CameraSet => cameraSet;
        public EntitySet LightSet => lightSet;

        public LightVolumeSystem LightVolumes { get; private set; }


        public Scene(Application app, string name) : base(name)
        {
            this.app = app;
            sceneEntitiesSet = World.GetEntities().With<Metadata>(IsSceneMetadata).AsSet();
            disabledSceneEntitiesSet = World.GetDisabledEntities().With<Metadata>(IsSceneMetadata).AsSet();
            cameraSet = World.GetEntities()
                .With<Camera>()
                .With<Transform>()
                .With<Metadata>(IsSceneMetadata)
                .AsSet();
            lightSet = World.GetEntities()
                .With<Light>()
                .With<Transform>()
                .With<Metadata>(IsSceneMetadata)
                .AsSet();
            LightVolumes = new LightVolumeSystem(this);
        }

        private bool IsSceneMetadata(in Metadata value)
        {
            return value.scene.Equals(this.Name);
        }

        public void Enable()
        {
            app.MainSystem.Insert(0, LightVolumes);
            if (!isEnabled)
            {
                Span<Entity> entities = stackalloc Entity[disabledSceneEntitiesSet.Count];
                disabledSceneEntitiesSet.GetEntities().CopyTo(entities);
                foreach (ref readonly Entity entity in entities)
                {
                    entity.Enable();
                }
                isEnabled = true;
            }
        }

        public void Disable()
        {
            app.MainSystem.Remove(LightVolumes);
            if (isEnabled)
            {
                Span<Entity> entities = stackalloc Entity[sceneEntitiesSet.Count];
                sceneEntitiesSet.GetEntities().CopyTo(entities);
                foreach (ref readonly Entity entity in entities)
                {
                    entity.Disable();
                }
                isEnabled = false;
            }
        }

        public void Unload()
        {
            Span<Entity> entities = stackalloc Entity[sceneEntitiesSet.Count];
            sceneEntitiesSet.GetEntities().CopyTo(entities);
            foreach (ref readonly Entity entity in entities)
            {
                entity.Dispose();
            }
        }

        public Entity CreateEntity(string name = "Entity")
        {
            var entity = World.CreateEntity();
            entity.Set(new Metadata { entityId = entityCounter, name = name, scene = this.Name });
            entityCounter++;
            return entity;
        }

        public Entity CreateTransform(string name)
        {
            var entity = CreateEntity(name);
            entity.Set(new Transform
            {
                position = Vector3.Zero,
                rotation = Quaternion.Identity,
                scale = Vector3.One
            });
            return entity;
        }

        public Entity CreateCamera(string name = "Camera", float fov = MathF.PI / 3f, float near = 0.1f, float far = 500)
        {
            var entity = CreateTransform(name);
            entity.Set(Camera.CreatePerspective(fov, app.Width / (float)app.Height, near, far));
            return entity;
        }

        public Entity CreateLight(string name = "Light", Vector3 color = default, int type = 0, float range = 1, float innerCutoff = 0, float outerCutoff = 0)
        {
            var entity = CreateTransform(name);
            Light light = new Light
            {
                id = lightCounter,
                color = color,
                type = type,
                range = range,
                innerCutoff = innerCutoff,
                outerCutoff = outerCutoff
            };
            entity.Set(light);
            LightVolumes.RegisterLight(entity.Get<Transform>(), light);
            lightCounter++;
            return entity;
        }

        public Entity Spawn(string name, Mesh mesh, string materialName, bool shadows = true)
        {
            var material = app.MaterialManager.GetMaterial(materialName);
            var entity = CreateTransform(name);
            entity.Set(new Drawable
            {
                mesh = app.ForwardRender.RegisterMesh(mesh.Name, mesh),
                material = materialName
            });
            entity.Set(new Render(false, shadows));
            if (material.Shader.IsDeferred)
            {
                entity.Set(new Deferred());
            }
            if (shadows)
            {
                entity.Set(new ShadowMapped());
            }

            return entity;
        }

        public override void Dispose()
        {
            Unload();
            base.Dispose();
        }
    }
}