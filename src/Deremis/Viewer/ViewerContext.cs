using System;
using System.Numerics;
using DefaultEcs.System;
using Deremis.Engine.Core;
using Deremis.Engine.Objects;
using Deremis.Engine.Rendering;
using Deremis.Engine.Rendering.Helpers;
using Deremis.Engine.Systems.Components;
using Deremis.Engine.Systems.Extensions;
using Deremis.Platform;
using Deremis.Platform.Assets;
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

            var hdrTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Cubemaps/env.hdr", new TextureHandler.Options(false, false, false, true)));

            var stenModel = AssetManager.current.Get<Model>(new AssetDescription("Meshes/sten.obj"));
            var panaModel = AssetManager.current.Get<Model>(new AssetDescription("Meshes/pana.obj"));
            var tableModel = AssetManager.current.Get<Model>(new AssetDescription("Meshes/plane.obj"));

            var shader = AssetManager.current.Get<Shader>(new AssetDescription("Shaders/phong_gbuffer.xml"));
            var shaderfwd = AssetManager.current.Get<Shader>(new AssetDescription("Shaders/pbr.xml"));
            var ssaoShader = AssetManager.current.Get<Shader>(new AssetDescription("Shaders/screen/ssao.xml"));

            var stenDiffuseTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/sten_albedo.png"));
            var stenSpecularTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/sten_metalness.png"));
            var stenNormalTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/sten_normals.jpg"));
            var stenAOTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/sten_occlusion.png"));

            var panaDiffuseTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Panasonic_TR_555_C.png"));
            var panaSpecularTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Panasonic_TR_555_R.png"));
            var panaNormalTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Panasonic_TR_555_N.png"));
            var panaAOTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Panasonic_TR_555_AO.png"));
            var panaMetallicTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Panasonic_TR_555_M.png"));
            var panaEmissiveTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Panasonic_TR_555_EM.png"));

            // var tableDiffuseTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Table_Mt_albedo.jpg"));
            // var tableSpecularTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Table_Mt_roughness.jpg"));
            // var tableNormalTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Table_Mt_normal.jpg"));

            // var cubemap = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Cubemaps/skybox_###.jpg", new TextureHandler.Options(mipmaps: false, cubemap: true)));

            Skybox.Init(app, hdrTex);

            var camera = app.CreateCamera();
            camera.Set(Transform.FromTarget(new Vector3(20, 5, 0), Vector3.Zero, Vector3.UnitY));

            var light = app.CreateLight(
                color: new Vector3(1f, 0.9f, 0.75f),
                type: 0
            );
            light.Set(new Transform(Vector3.Zero, Quaternion.CreateFromYawPitchRoll(MathF.PI + MathF.PI / 3f, -MathF.PI / 3f, 0), Vector3.One));
            light = app.CreateLight(
                color: Vector3.UnitY * 10f,
                type: 1,
                range: 50
            );
            light.Set(new Transform(new Vector3(-2, 4, 15), Quaternion.Identity, Vector3.One));
            light = app.CreateLight(
                color: Vector3.UnitX * 10f,
                type: 1,
                range: 50
            );
            light.Set(new Transform(new Vector3(0, 4, 25), Quaternion.Identity, Vector3.One));

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
            var stenMat = app.MaterialManager.CreateMaterial("sten", shaderfwd);
            stenMat.SetProperty("albedo", Vector3.One);
            stenMat.SetProperty("metallic", 0.35f);
            stenMat.SetProperty("roughness", 0.5f);
            stenMat.SetProperty("ao", 1.0f);
            stenMat.SetTexture("albedoTexture", stenDiffuseTex);
            stenMat.SetTexture("roughnessTexture", stenSpecularTex);
            stenMat.SetTexture("normalTexture", stenNormalTex);
            stenMat.SetTexture("aoTexture", stenAOTex);
            stenMat.SetSampler(sampler);
            var panaMat = app.MaterialManager.CreateMaterial("pana", shaderfwd);
            panaMat.SetProperty("albedo", Vector3.One);
            panaMat.SetProperty("metallic", 0.75f);
            panaMat.SetProperty("roughness", 1.0f);
            panaMat.SetProperty("ao", 1.0f);
            panaMat.SetProperty("emissiveStrength", 1.0f);
            panaMat.SetTexture("albedoTexture", panaDiffuseTex);
            panaMat.SetTexture("roughnessTexture", panaSpecularTex);
            panaMat.SetTexture("normalTexture", panaNormalTex);
            panaMat.SetTexture("emissiveTexture", panaEmissiveTex);
            panaMat.SetTexture("metallicTexture", panaMetallicTex);
            panaMat.SetTexture("aoTexture", panaAOTex);
            panaMat.SetSampler(sampler);
            var tableMat = app.MaterialManager.CreateMaterial("table", shaderfwd);
            tableMat.SetProperty("albedo", Vector3.One);
            tableMat.SetProperty("metallic", 0.0f);
            tableMat.SetProperty("roughness", 0.1f);
            tableMat.SetProperty("ao", 1.0f);
            tableMat.SetProperty("ambientStrength", 0.1f);
            tableMat.SetProperty("diffuseColor", Vector3.One * 0.9f);
            tableMat.SetProperty("specularStrength", 0.1f);
            tableMat.SetProperty("specularColor", Vector3.One);
            // tableMat.SetTexture("diffuseTexture", tableDiffuseTex);
            // tableMat.SetTexture("specularTexture", tableSpecularTex);
            // tableMat.SetTexture("normalTexture", tableNormalTex);
            tableMat.SetSampler(sampler);

            var entity = stenModel.Spawn(app, stenMat.Name, new Transform(
                new Vector3(1.35f, 0, 2),
                Quaternion.CreateFromYawPitchRoll(MathF.PI, 0, MathF.PI / 5.5f),
                Vector3.One));
            var entityfwd = panaModel.Spawn(app, panaMat.Name, new Transform(new Vector3(0, 4.5f, 20), Quaternion.CreateFromYawPitchRoll(MathF.PI / 2f, 0, 0), Vector3.One));
            var tableEntity = tableModel.Spawn(app, tableMat.Name, new Transform(new Vector3(0, 0, 0), Quaternion.Identity, Vector3.One));

            entityfwd.SetAsChildOf(tableEntity);
            entity.SetAsChildOf(entityfwd);

            // var ssaoMaterial = app.GetScreenPass("ssao", ssaoShader);
            // ssaoMaterial.SetProperty("aoRadius", Vector4.One);
            // app.Render.RegisterScreenPass(ssaoMaterial);

            float rotate = 0;
            app.MainSystem.Add(new ActionSystem<float>(delta =>
            {
                rotate += delta;
                var transform = Transform.FromTarget(new Vector3(20 * MathF.Cos(-rotate / 4f), 10, 20 * MathF.Sin(-rotate / 4f) + 20), new Vector3(0, 0, 20), Vector3.UnitY);
                // light.Set(transform);
                camera.Set(transform);

                // light.Set(new Transform(Vector3.Zero, Quaternion.CreateFromYawPitchRoll(rotate / 2f, -MathF.PI / 4f, 0), Vector3.One));
                // entityfwd.Set(new Transform(new Vector3(0, 4.5f, 20), Quaternion.CreateFromYawPitchRoll(-rotate / 2f, 0, 0), Vector3.One));
            }));
        }
    }
}