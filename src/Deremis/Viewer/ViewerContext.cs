using System;
using System.Numerics;
using DefaultEcs.System;
using Deremis.Engine.Core;
using Deremis.Engine.Math;
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

            var hdrTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Cubemaps/env3/env3.hdr", new TextureHandler.Options(false, false, false, true)));
            var hdrIrrTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Cubemaps/env3/env3_irr_###.tga", new TextureHandler.Options(cubemap: true)));
            var hdrRadTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Cubemaps/env3/env3_rad_###_***.tga", new TextureHandler.Options(cubemap: true, mipmapCount: 5)));
            var brdfLutTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Cubemaps/ibl_brdf_lut.png"));

            var panaModel = AssetManager.current.Get<Model>(new AssetDescription("Meshes/mg08.obj"));
            var tableModel = AssetManager.current.Get<Model>(new AssetDescription("Meshes/plane.obj"));

            var shaderfwd = AssetManager.current.Get<Shader>(new AssetDescription("Shaders/pbr.xml"));

            var panaDiffuseTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/albedo.png"));
            var panaNormalTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/normal.png"));
            var panaMRATex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/mra.png"));

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
            light.Set(new Transform(Vector3.Zero, Quaternion.CreateFromYawPitchRoll(MathF.PI / 4f, -MathF.PI / 8f, 0), Vector3.One));
            // light = app.CreateLight(
            //     color: Vector3.One,
            //     type: 2, innerCutoff: DMath.ToRadians(56), outerCutoff: DMath.ToRadians(60)
            // );

            // app.MainSystem.Add(new ActionSystem<float>(delta =>
            // {
            //     light.SetSameAs<Transform>(camera);
            // }));

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

            var panaMat = app.MaterialManager.CreateMaterial("pana", shaderfwd);
            panaMat.SetProperty("albedo", Vector3.One);
            panaMat.SetProperty("metallic", 1.0f);
            panaMat.SetProperty("roughness", 1.0f);
            panaMat.SetProperty("ao", 1.0f);
            panaMat.SetTexture("albedoTexture", panaDiffuseTex);
            panaMat.SetTexture("mraTexture", panaMRATex);
            panaMat.SetTexture("normalTexture", panaNormalTex);
            panaMat.SetSampler(sampler);
            panaMat.SetTexture("environmentTexture", hdrIrrTex.View);
            panaMat.SetTexture("prefilteredEnvTexture", hdrRadTex.View);
            panaMat.SetTexture("brdfLutTex", brdfLutTex.View);
            var tableMat = app.MaterialManager.CreateMaterial("table", shaderfwd);
            tableMat.SetProperty("albedo", Vector3.One);
            tableMat.SetProperty("metallic", 0.0f);
            tableMat.SetProperty("roughness", 1f);
            tableMat.SetProperty("ao", 1.0f);
            tableMat.SetSampler(sampler);
            tableMat.SetTexture("environmentTexture", hdrIrrTex.View);
            tableMat.SetTexture("prefilteredEnvTexture", hdrRadTex.View);
            tableMat.SetTexture("brdfLutTex", brdfLutTex.View);

            var tableEntity = tableModel.Spawn(app, tableMat.Name, new Transform(new Vector3(0, -2, 0), Quaternion.Identity, Vector3.One));

            var length = 1;
            var offset = 1;
            var random = new Random();
            var rotate = 0f;
            for (var x = 0; x < length; x++)
            {
                for (var y = 0; y < length; y++)
                {
                    var entityfwd = panaModel.Spawn(app, panaMat.Name,
                        new Transform(new Vector3(x * offset - length / 2f * offset, 0, y * offset - length / 2f * offset),
                        Quaternion.CreateFromYawPitchRoll(MathF.PI / 2f, 0, 0), Vector3.One / 2f));
                    entityfwd.SetAsChildOf(tableEntity);
                    app.MainSystem.Add(new ActionSystem<float>(delta =>
                    {
                        rotate += delta;
                        entityfwd.Set(new Transform(new Vector3(x * offset - length / 2f * offset, 0, y * offset - length / 2f * offset),
                            Quaternion.CreateFromYawPitchRoll(MathF.PI / 2f + rotate, 0, 0), Vector3.One / 2f));
                    }));
                }
            }
        }
    }
}