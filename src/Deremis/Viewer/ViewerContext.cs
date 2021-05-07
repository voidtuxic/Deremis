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

            var hdrTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Cubemaps/env2.hdr", new TextureHandler.Options(false, false, false, true)));

            var stenModel = AssetManager.current.Get<Model>(new AssetDescription("Meshes/sten.obj"));
            var panaModel = AssetManager.current.Get<Model>(new AssetDescription("Meshes/pana.obj"));
            var tableModel = AssetManager.current.Get<Model>(new AssetDescription("Meshes/plane.obj"));

            var shader = AssetManager.current.Get<Shader>(new AssetDescription("Shaders/pbr_gbuffer.xml"));
            var shaderfwd = AssetManager.current.Get<Shader>(new AssetDescription("Shaders/pbr.xml"));

            var panaDiffuseTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Panasonic_TR_555_C.png"));
            var panaNormalTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Panasonic_TR_555_N.png"));
            var panaMRATex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Panasonic_TR_555_MRA.png"));
            var panaEmissiveTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Panasonic_TR_555_EM.png"));

            var tableDiffuseTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/rocky_albedo.png"));
            var tableSpecularTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/rocky_mra.png"));
            var tableNormalTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/rocky_normal.png"));

            Skybox.Init(app, hdrTex);

            var camera = app.CreateCamera();
            camera.Set(Transform.FromTarget(new Vector3(20, 5, 0), Vector3.Zero, Vector3.UnitY));
            var freeCam = new FreeCamera(app);
            int entityId = camera.Get<Metadata>().entityId;
            freeCam.SetCameraId(entityId);
            app.Cull.SetCameraId(entityId);

            var light = app.CreateLight(
                color: new Vector3(1f, 0.9f, 0.75f),
                type: 0
            );
            light.Set(new Transform(Vector3.Zero, Quaternion.CreateFromYawPitchRoll(MathF.PI - MathF.PI / 3f, -MathF.PI / 3f, 0), Vector3.One));

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

            var panaMat = app.MaterialManager.CreateMaterial("pana", shader);
            panaMat.SetProperty("albedo", Vector3.One);
            panaMat.SetProperty("metallic", 0.75f);
            panaMat.SetProperty("roughness", 1.0f);
            panaMat.SetProperty("ao", 1.0f);
            panaMat.SetProperty("emissiveStrength", 1.0f);
            panaMat.SetTexture("albedoTexture", panaDiffuseTex);
            panaMat.SetTexture("mraTexture", panaMRATex);
            panaMat.SetTexture("normalTexture", panaNormalTex);
            panaMat.SetTexture("emissiveTexture", panaEmissiveTex);
            panaMat.SetSampler(sampler);
            var tableMat = app.MaterialManager.CreateMaterial("table", shader);
            tableMat.SetProperty("albedo", Vector3.One);
            tableMat.SetProperty("metallic", 0.0f);
            tableMat.SetProperty("roughness", 1.0f);
            tableMat.SetProperty("ao", 1.0f);
            tableMat.SetTexture("albedoTexture", tableDiffuseTex);
            tableMat.SetTexture("mraTexture", tableSpecularTex);
            tableMat.SetTexture("normalTexture", tableNormalTex);
            tableMat.SetSampler(sampler);
            tableMat.DeferredLightingMaterial.SetTexture("environmentTexture", hdrTex);

            var tableEntity = tableModel.Spawn(app, tableMat.Name, new Transform(new Vector3(0, -2, 0), Quaternion.Identity, Vector3.One));

            var length = 10;
            var offset = 10;
            var random = new Random();
            for (var x = 0; x < length; x++)
            {
                for (var y = 0; y < length; y++)
                {
                    var entityfwd = panaModel.Spawn(app, panaMat.Name,
                        new Transform(new Vector3(x * offset - length / 2f * offset, 4.5f, y * offset - length / 2f * offset),
                        Quaternion.CreateFromYawPitchRoll(MathF.PI / 2f + (float)random.NextDouble() * MathF.PI, 0, 0), Vector3.One));
                    entityfwd.SetAsChildOf(tableEntity);
                }
            }
        }
    }
}