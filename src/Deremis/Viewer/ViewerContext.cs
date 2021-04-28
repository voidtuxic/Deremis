using System;
using System.Numerics;
using DefaultEcs.System;
using Deremis.Engine.Core;
using Deremis.Engine.Objects;
using Deremis.Engine.Rendering;
using Deremis.Engine.Rendering.Helpers;
using Deremis.Engine.Systems.Components;
using Deremis.Engine.Systems.Extensions;
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

            var stenModel = AssetManager.current.Get<Model>(new AssetDescription("Meshes/sten.obj", 0));
            var panaModel = AssetManager.current.Get<Model>(new AssetDescription("Meshes/pana.obj", 0));
            var tableModel = AssetManager.current.Get<Model>(new AssetDescription("Meshes/table.obj", 0));
            var shader = AssetManager.current.Get<Shader>(new AssetDescription("Shaders/phong_gbuffer.xml", 1));
            var shaderfwd = AssetManager.current.Get<Shader>(new AssetDescription("Shaders/phong.xml", 1));

            var stenDiffuseTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/sten_albedo.png", 2));
            var stenSpecularTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/sten_metalness.png", 2));
            var stenNormalTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/sten_normals.jpg", 2));

            var panaDiffuseTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Panasonic_TR_555_C.png", 2));
            var panaSpecularTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Panasonic_TR_555_R.png", 2));
            var panaNormalTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Panasonic_TR_555_N.png", 2));
            var panaEmissiveTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Panasonic_TR_555_EM.png", 2));

            var tableDiffuseTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Table_Mt_albedo.jpg", 2));
            var tableSpecularTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Table_Mt_roughness.jpg", 2));
            var tableNormalTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Table_Mt_normal.jpg", 2));

            var cubemap = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Cubemaps/skybox_###.jpg", 2, new TextureHandler.Options(mipmaps: false, cubemap: true)));

            Skybox.Init(app, cubemap);

            var camera = app.CreateCamera();
            camera.Set(Transform.FromTarget(new Vector3(20, 5, 0), Vector3.Zero, Vector3.UnitY));

            var light = app.CreateLight(
                color: new Vector3(1f, 0.9f, 0.75f),
                type: 0
            );
            light.Set(new Transform(Vector3.Zero, Quaternion.CreateFromYawPitchRoll(-MathF.PI / 4f, -MathF.PI / 3f, 0), Vector3.One));
            light = app.CreateLight(
                color: new Vector3(1f, 0.75f, 0.9f),
                type: 2,
                innerCutoff: MathF.Cos(7.5f * MathF.PI / 180f),
                outerCutoff: MathF.Cos(10f * MathF.PI / 180f)
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
            var stenMat = app.MaterialManager.CreateMaterial("sten", shader);
            stenMat.SetProperty("ambientStrength", 0.1f);
            stenMat.SetProperty("diffuseColor", Vector3.One);
            stenMat.SetProperty("specularStrength", 0.1f);
            stenMat.SetProperty("specularColor", Vector3.One);
            stenMat.SetTexture("diffuseTexture", stenDiffuseTex);
            stenMat.SetTexture("specularTexture", stenSpecularTex);
            stenMat.SetTexture("normalTexture", stenNormalTex);
            stenMat.SetSampler(sampler);
            var panaMat = app.MaterialManager.CreateMaterial(shaderfwd.Name, shaderfwd);
            panaMat.SetProperty("ambientStrength", 0.1f);
            panaMat.SetProperty("diffuseColor", Vector3.One);
            panaMat.SetProperty("specularStrength", 0.5f);
            panaMat.SetProperty("emissiveStrength", 1f);
            panaMat.SetProperty("specularColor", Vector3.One);
            panaMat.SetTexture("diffuseTexture", panaDiffuseTex);
            panaMat.SetTexture("specularTexture", panaSpecularTex);
            panaMat.SetTexture("normalTexture", panaNormalTex);
            panaMat.SetTexture("emissiveTexture", panaEmissiveTex);
            panaMat.SetSampler(sampler);
            var tableMat = app.MaterialManager.CreateMaterial("table", shader);
            tableMat.SetProperty("ambientStrength", 0.1f);
            tableMat.SetProperty("diffuseColor", Vector3.One);
            tableMat.SetProperty("specularStrength", 0.1f);
            tableMat.SetProperty("specularColor", Vector3.One);
            tableMat.SetTexture("diffuseTexture", tableDiffuseTex);
            tableMat.SetTexture("specularTexture", tableSpecularTex);
            tableMat.SetTexture("normalTexture", tableNormalTex);
            tableMat.SetSampler(sampler);

            var entity = stenModel.Spawn(app, stenMat.Name, new Transform(
                new Vector3(1.35f, 0, 2),
                Quaternion.CreateFromYawPitchRoll(MathF.PI, 0, MathF.PI / 5.5f),
                Vector3.One));
            var entityfwd = panaModel.Spawn(app, panaMat.Name, new Transform(new Vector3(0, 10, 2), Quaternion.CreateFromYawPitchRoll(2f * MathF.PI / 3f, 0, 0), Vector3.One));
            var tableEntity = tableModel.Spawn(app, tableMat.Name, new Transform(Vector3.Zero, Quaternion.Identity, Vector3.One));

            entityfwd.SetAsChildOf(tableEntity);
            entity.SetAsChildOf(entityfwd);

            float rotate = 0;
            app.MainSystem.Add(new ActionSystem<float>(delta =>
            {
                rotate += delta;
                var transform = Transform.FromTarget(new Vector3(20 * MathF.Cos(-rotate / 4f), 5, 20 * MathF.Sin(-rotate / 4f)), Vector3.Zero, Vector3.UnitY);
                light.Set(transform);
                camera.Set(transform);
                tableEntity.Set(new Transform(new Vector3(0, -6, 0), Quaternion.CreateFromYawPitchRoll(rotate / 2f, 0, 0), Vector3.One));
            }));
        }
    }
}