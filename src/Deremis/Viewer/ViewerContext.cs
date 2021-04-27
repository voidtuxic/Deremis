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

            // AssetManager.current.Get<Shader>(new AssetDescription("Shaders/phong_gbuffer.xml", 1));

            var model = AssetManager.current.Get<Model>(new AssetDescription("Meshes/sten.obj", 0));
            var shader = AssetManager.current.Get<Shader>(new AssetDescription("Shaders/phong_gbuffer.xml", 1));
            var shaderfwd = AssetManager.current.Get<Shader>(new AssetDescription("Shaders/phong.xml", 1));

            var diffuseTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/sten_albedo.png", 2));
            var specularTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/sten_metalness.png", 2));
            var normalTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/sten_normals.jpg", 2));

            var cubemap = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Cubemaps/skybox_###.jpg", 2, new TextureHandler.Options(mipmaps: false, cubemap: true)));

            Skybox.Init(app, cubemap);

            var camera = app.CreateCamera();
            camera.Set(Transform.FromTarget(new Vector3(20, 5, 0), Vector3.Zero, Vector3.UnitY));

            var light = app.CreateLight(
                color: new Vector3(1f, 0.9f, 0.75f),
                type: 0
            );
            light.Set(new Transform(Vector3.Zero, Quaternion.CreateFromYawPitchRoll(MathF.PI / 4f, -MathF.PI / 3f, 0), Vector3.One));
            light = app.CreateLight(
                color: Vector3.UnitX,
                type: 2,
                innerCutoff: MathF.Cos(15f * MathF.PI / 180f),
                outerCutoff: MathF.Cos(20f * MathF.PI / 180f)
            );
            light.Set(new Transform(new Vector3(-10, 0, 1), Quaternion.CreateFromYawPitchRoll(-MathF.PI / 2f, 0, 0), Vector3.One));

            SamplerDescription sampler = new SamplerDescription
            {
                AddressModeU = SamplerAddressMode.Wrap,
                AddressModeV = SamplerAddressMode.Wrap,
                AddressModeW = SamplerAddressMode.Wrap,
                Filter = SamplerFilter.Anisotropic,
                LodBias = 0,
                MinimumLod = 0,
                MaximumLod = uint.MaxValue,
                MaximumAnisotropy = 16,
            };
            var material = app.MaterialManager.CreateMaterial(shader.Name, shader);
            material.SetProperty("ambientStrength", 0.001f);
            material.SetProperty("diffuseColor", Vector3.One);
            material.SetProperty("specularStrength", 0.005f);
            material.SetProperty("specularColor", Vector3.One);
            material.SetTexture("diffuseTexture", diffuseTex);
            material.SetTexture("specularTexture", specularTex);
            material.SetTexture("normalTexture", normalTex);
            material.SetSampler(sampler);
            var materialfwd = app.MaterialManager.CreateMaterial(shaderfwd.Name, shaderfwd);
            materialfwd.SetProperty("ambientStrength", 0.001f);
            materialfwd.SetProperty("diffuseColor", Vector3.One);
            materialfwd.SetProperty("specularStrength", 0.005f);
            materialfwd.SetProperty("specularColor", Vector3.One);
            materialfwd.SetTexture("diffuseTexture", diffuseTex);
            materialfwd.SetTexture("specularTexture", specularTex);
            materialfwd.SetTexture("normalTexture", normalTex);
            materialfwd.SetSampler(sampler);

            var entity = model.Spawn(app, material.Name);
            var entity2 = model.Spawn(app, material.Name);
            entity2.Set(new Transform(new Vector3(0, 0, -5), Quaternion.Identity, Vector3.One));
            var entityfwd = model.Spawn(app, materialfwd.Name);
            entityfwd.Set(new Transform(new Vector3(0, 0, 5), Quaternion.Identity, Vector3.One));

            float rotate = 0;
            app.MainSystem.Add(new ActionSystem<float>(delta =>
            {
                rotate += delta;
                entity.Set(new Transform(Vector3.Zero, Quaternion.CreateFromYawPitchRoll(rotate, 0, 0), Vector3.One));
                camera.Set(Transform.FromTarget(new Vector3(20 * MathF.Cos(-rotate / 4f), 5, 20 * MathF.Sin(-rotate / 4f)), Vector3.Zero, Vector3.UnitY));
            }));
        }
    }
}