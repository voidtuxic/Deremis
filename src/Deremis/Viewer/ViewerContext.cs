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

            var hdrTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Cubemaps/gradient/gradient.hdr", new TextureHandler.Options(false, false, false, true)));
            var hdrIrrTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Cubemaps/gradient/gradient_irr_###.tga", new TextureHandler.Options(cubemap: true)));
            var hdrRadTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Cubemaps/gradient/gradient_rad_###_***.tga", new TextureHandler.Options(cubemap: true, mipmapCount: 5)));
            var brdfLutTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/Cubemaps/ibl_brdf_lut.png"));

            var panaModel = AssetManager.current.Get<Model>(new AssetDescription("Meshes/low.obj"));
            var tableModel = AssetManager.current.Get<Model>(new AssetDescription("Meshes/plane.obj"));

            var shaderfwd = AssetManager.current.Get<Shader>(new AssetDescription("Shaders/pbr_instanced.xml"));

            var panaDiffuseTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/low_albedo.png"));
            var panaNormalTex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/low_normal.png"));
            var panaMRATex = AssetManager.current.Get<Texture>(new AssetDescription("Textures/low_mra.png"));

            var scene = new Scene(app, "hello deremis");

            app.ForwardRender.SetAsCurrentScene(scene);

            Skybox.Init(scene, hdrTex);

            var camera = scene.CreateCamera();
            camera.Set(Transform.FromTarget(new Vector3(50, 40, -50), Vector3.Zero, Vector3.UnitY));
            var freeCam = new FreeCamera(app);
            int entityId = camera.Get<Metadata>().entityId;
            freeCam.SetCameraId(entityId);
            app.Cull.SetCameraId(entityId);

            var light = scene.CreateLight(
                color: new Vector3(1f, 0.9f, 0.75f),
                type: 0
            );
            light.Set(new Transform(Vector3.Zero, Quaternion.CreateFromYawPitchRoll(MathF.PI / 4f, -MathF.PI / 4f, 0), Vector3.One));
            // var spotlight = scene.CreateLight(
            //     color: Vector3.UnitY * 5,
            //     type: 1, range: 50
            // );

            // app.MainSystem.Add(new ActionSystem<float>(delta =>
            // {
            //     spotlight.SetSameAs<Transform>(camera);
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
            panaMat.SetProperty("metallic", 0.0f);
            panaMat.SetProperty("roughness", 1.0f);
            panaMat.SetProperty("ao", 1.0f);
            // panaMat.SetProperty("emissiveStrength", 1.0f);
            panaMat.SetTexture("albedoTexture", panaDiffuseTex);
            panaMat.SetTexture("mraTexture", panaMRATex);
            panaMat.SetTexture("normalTexture", panaNormalTex);
            panaMat.SetSampler(sampler);
            panaMat.SetTexture("environmentTexture", hdrIrrTex.View);
            panaMat.SetTexture("prefilteredEnvTexture", hdrRadTex.View);
            panaMat.SetTexture("brdfLutTex", brdfLutTex.View);
            var tableMat = app.MaterialManager.CreateMaterial("table", shaderfwd);
            tableMat.SetProperty("albedo", Vector3.One * 0.5f);
            tableMat.SetProperty("metallic", 0.0f);
            tableMat.SetProperty("roughness", 1f);
            tableMat.SetProperty("ao", 1.0f);
            tableMat.SetSampler(sampler);
            tableMat.SetTexture("environmentTexture", hdrIrrTex.View);
            tableMat.SetTexture("prefilteredEnvTexture", hdrRadTex.View);
            tableMat.SetTexture("brdfLutTex", brdfLutTex.View);

            // var tableEntity = tableModel.Spawn(scene, tableMat.Name, new Transform(new Vector3(0, -2, 0), Quaternion.Identity, Vector3.One));

            var rings = 5;
            var offset = 15;
            var count = 8;
            var step = MathF.PI * 2f / count;
            var random = new Random();
            var rng = new Tedd.RandomUtils.FastRandom();
            for (var x = 0; x < rings; x++)
            {
                var localOff = offset + (x * offset) + (x * offset / 5);
                for (var y = 0; y < count; y++)
                {
                    var entityfwd = panaModel.Spawn(scene, panaMat.Name,
                        new Transform(new Vector3(MathF.Cos(step * y) * (localOff),
                                                  0,
                                                  MathF.Sin(step * y) * (localOff)),
                        Quaternion.CreateFromYawPitchRoll(rng.NextFloat() * MathF.PI, rng.NextFloat() * MathF.PI, rng.NextFloat() * MathF.PI),
                        Vector3.One * (1 + x / 1.15f)));
                    // entityfwd.SetAsChildOf(tableEntity);

                    // light = scene.CreateLight(
                    //     color: new Vector3(rng.NextFloat(),
                    //                        rng.NextFloat(),
                    //                        rng.NextFloat()) * 10,
                    //     type: 1, range: 20
                    // );
                    // light.Set(entityfwd.GetWorldTransform());
                    // app.MainSystem.Add(new ActionSystem<float>(delta =>
                    // {
                    //     rotate += delta / 10f;
                    //     entityfwd.Set(new Transform(new Vector3(
                    //         MathF.Cos(rotate / 2f) * 30,
                    //         0, MathF.Sin(rotate / 2f) * 30),
                    //         Quaternion.CreateFromYawPitchRoll(MathF.PI / 2f + rotate, 0, 0), Vector3.One / 3f));
                    // }));
                }
            }
        }
    }
}