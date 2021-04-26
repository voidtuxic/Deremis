using System;
using System.Numerics;
using DefaultEcs.System;
using Deremis.Engine.Core;
using Deremis.Engine.Objects;
using Deremis.Engine.Rendering;
using Deremis.Engine.Rendering.Helpers;
using Deremis.Engine.Systems.Components;
using Deremis.System;
using Deremis.System.Assets;
using Veldrid;
using Shader = Deremis.Engine.Objects.Shader;
using Texture = Deremis.Engine.Objects.Texture;

namespace Deremis.Viewer
{
    public class ViewerContext : IContext
    {
        public string Name => "Deremis Viewer";
        private Application app;

        public void Initialize(Application app)
        {
            this.app = app;

            var model = AssetManager.current.Get<Model>(new AssetDescription("Meshes/sten.obj", 0));
            var shader = AssetManager.current.Get<Shader>(new AssetDescription("Shaders/phong.xml", 1));

            var diffuseTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/sten_albedo.png", 2));
            var specularTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/sten_metalness.png", 2));
            var normalTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/sten_normals.jpg", 2));

            var cubemap = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Cubemaps/skybox_###.jpg", 2, new TextureHandler.Options(mipmaps: false, cubemap: true)));

            Skybox.Init(app, cubemap);

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
                rotation = Quaternion.CreateFromYawPitchRoll(MathF.PI / 4f, -MathF.PI / 3f, 0),
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
            material.SetProperty("specularStrength", 0.005f);
            material.SetProperty("specularColor", Vector3.One);
            material.SetTexture("diffuseTexture", diffuseTex);
            material.SetTexture("specularTexture", specularTex);
            material.SetTexture("normalTexture", normalTex);
            material.SetSampler(new SamplerDescription
            {
                AddressModeU = SamplerAddressMode.Wrap,
                AddressModeV = SamplerAddressMode.Wrap,
                AddressModeW = SamplerAddressMode.Wrap,
                Filter = SamplerFilter.Anisotropic,
                LodBias = 0,
                MinimumLod = 0,
                MaximumLod = uint.MaxValue,
                MaximumAnisotropy = 16,
            });

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