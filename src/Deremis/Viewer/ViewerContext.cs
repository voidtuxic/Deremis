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

            var camera = app.CreateCamera();
            camera.Set(new Transform
            {
                position = new Vector3(-10, 10, 10),
                rotation = Quaternion.CreateFromYawPitchRoll(-MathF.PI / 4, -MathF.PI / 5, 0),
                scale = Vector3.One
            });

            var material = app.MaterialManager.CreateMaterial(shader.Name, shader);
            material.SetProperty("lightColor", Vector3.One);
            material.SetProperty("lightPosition", new Vector3(0, 10, 10));
            material.SetProperty("ambientStrength", 0.1f);
            material.SetProperty("diffuseColor", Vector3.One);
            material.SetProperty("specularStrength", 1f);
            material.SetProperty("specularColor", Vector3.One);
            material.SetTexture("diffuseTexture", diffuseTex);

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