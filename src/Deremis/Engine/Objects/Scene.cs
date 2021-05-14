using System;
using System.Collections.Generic;
using System.Numerics;
using DefaultEcs;
using Deremis.Engine.Systems.Components;
using Deremis.Platform;

namespace Deremis.Engine.Objects
{
    public class Scene : DObject
    {
        private readonly Application app;
        private int entityCounter = 0;
        private EntitySet sceneEntitiesSet;
        private EntitySet disabledSceneEntitiesSet;

        private World world => app.DefaultWorld;

        public Application App => app;

        public Scene(Application app, string name) : base(name)
        {
            this.app = app;
            sceneEntitiesSet = world.GetEntities().With<Metadata>(IsSceneMetadata).AsSet();
            disabledSceneEntitiesSet = world.GetDisabledEntities().With<Metadata>(IsSceneMetadata).AsSet();
        }

        private bool IsSceneMetadata(in Metadata value)
        {
            return value.scene.Equals(this.Name);
        }

        public void Enable()
        {
            Span<Entity> entities = stackalloc Entity[disabledSceneEntitiesSet.Count];
            disabledSceneEntitiesSet.GetEntities().CopyTo(entities);
            foreach (ref readonly Entity entity in entities)
            {
                entity.Enable();
            }
        }

        public void Disable()
        {
            Span<Entity> entities = stackalloc Entity[sceneEntitiesSet.Count];
            sceneEntitiesSet.GetEntities().CopyTo(entities);
            foreach (ref readonly Entity entity in entities)
            {
                entity.Disable();
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
            var entity = world.CreateEntity();
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

        public Entity CreateCamera(string name = "Camera", float fov = MathF.PI / 4f, float near = 0.1f, float far = 500)
        {
            var entity = CreateTransform(name);
            entity.Set(Camera.CreatePerspective(fov, app.Width / (float)app.Height, near, far));
            return entity;
        }

        public Entity CreateLight(string name = "Light", Vector3 color = default, int type = 0, float range = 1, float innerCutoff = 0, float outerCutoff = 0)
        {
            var entity = CreateTransform(name);
            entity.Set(new Light
            {
                color = color,
                type = type,
                range = range,
                innerCutoff = innerCutoff,
                outerCutoff = outerCutoff
            });
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