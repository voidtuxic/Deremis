using System;
using System.Numerics;
using DefaultEcs.System;
using Deremis.Engine.Core;
using Deremis.Engine.Objects;
using Deremis.Engine.Rendering;
using Deremis.Engine.Systems.Components;
using Deremis.System;
using Deremis.System.Assets;

namespace Deremis.Viewer
{
    public class ViewerContext : IContext
    {
        public string Name => "Deremis Viewer";
        private Application app;

        public void Initialize(Application app)
        {
            this.app = app;

            var model = AssetManager.current.Get<Model>(new AssetDescription
            {
                name = "Sten",
                path = "Meshes/sten.obj",
                type = 0
            });
            var shader = AssetManager.current.Get<Shader>(new AssetDescription
            {
                name = "phong",
                path = "Shaders/phong.xml",
                type = 1
            });

            var diffuseTex = AssetManager.current.Get<Texture>(new AssetDescription
            {
                name = "diffuseTex",
                path = "Textures/sten_albedo.png",
                type = 2
            });
            var specularTex = AssetManager.current.Get<Texture>(new AssetDescription
            {
                name = "specularTex",
                path = "Textures/sten_metalness.png",
                type = 2
            });
            var normalTex = AssetManager.current.Get<Texture>(new AssetDescription
            {
                name = "normalTex",
                path = "Textures/sten_normals.jpg",
                type = 2
            });

            var camera = app.CreateCamera();
            camera.Set(new Transform
            {
                position = new Vector3(-10, 5, 10),
                rotation = Quaternion.CreateFromYawPitchRoll(-MathF.PI / 4, -MathF.PI / 8, 0),
                scale = Vector3.One
            });

            var light = app.CreateLight(
                color: new Vector3(1f, 0.9f, 0.75f),
                type: 0
            );
            light.Set(new Transform
            {
                position = Vector3.Zero,
                rotation = Quaternion.CreateFromYawPitchRoll(0, -MathF.PI / 2f, 0),
                scale = Vector3.One
            });
            light = app.CreateLight(
                color: Vector3.UnitX,
                type: 2,
                innerCutoff: MathF.Cos(15f * MathF.PI / 180f),
                outerCutoff: MathF.Cos(20f * MathF.PI / 180f)
            );
            light.Set(new Transform
            {
                position = new Vector3(-10, 0, 1),
                rotation = Quaternion.CreateFromYawPitchRoll(-MathF.PI / 2f, 0, 0),
                scale = Vector3.One
            });

            var material = app.MaterialManager.CreateMaterial(shader.Name, shader);
            material.SetProperty("ambientStrength", 0.001f);
            material.SetProperty("diffuseColor", Vector3.One);
            material.SetProperty("specularStrength", 0.15f);
            material.SetProperty("specularColor", Vector3.One);
            material.SetTexture("diffuseTexture", diffuseTex);
            material.SetTexture("specularTexture", specularTex);
            material.SetTexture("normalTexture", normalTex);

            var entity = model.Spawn(app, material.Name);

            float rotate = 0;
            app.MainSystem.Add(new ActionSystem<float>(delta =>
            {
                rotate += delta;
                entity.Set(new Transform
                {
                    position = Vector3.Zero,
                    rotation = Quaternion.CreateFromYawPitchRoll(rotate, 0, 0),
                    scale = Vector3.One
                });
            }));
        }
    }
}