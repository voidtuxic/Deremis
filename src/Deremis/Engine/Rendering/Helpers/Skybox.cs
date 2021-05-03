using System.Numerics;
using Deremis.Engine.Objects;
using Deremis.Platform;
using Deremis.Platform.Assets;

namespace Deremis.Engine.Rendering.Helpers
{
    public static class Skybox
    {
        private const string name = "skybox";

        public static AssetDescription Shader = new AssetDescription
        {
            name = "skybox_cubemap",
            path = "Shaders/skybox_cubemap.xml"
        };

        private static bool initialized = false;
        // ruthlessly pasted from https://learnopengl.com/code_viewer.php?code=advanced/cubemaps_skybox_data
        private static Vector3[] vertices = {
            new Vector3(-1.0f,  1.0f, -1.0f),
            new Vector3(-1.0f, -1.0f, -1.0f),
            new Vector3(1.0f, -1.0f, -1.0f),
            new Vector3(1.0f, -1.0f, -1.0f),
            new Vector3(1.0f,  1.0f, -1.0f),
            new Vector3(-1.0f,  1.0f, -1.0f),

            new Vector3(-1.0f, -1.0f,  1.0f),
            new Vector3(-1.0f, -1.0f, -1.0f),
            new Vector3(-1.0f,  1.0f, -1.0f),
            new Vector3(-1.0f,  1.0f, -1.0f),
            new Vector3(-1.0f,  1.0f,  1.0f),
            new Vector3(-1.0f, -1.0f,  1.0f),

            new Vector3(1.0f, -1.0f, -1.0f),
            new Vector3(1.0f, -1.0f,  1.0f),
            new Vector3(1.0f,  1.0f,  1.0f),
            new Vector3(1.0f,  1.0f,  1.0f),
            new Vector3(1.0f,  1.0f, -1.0f),
            new Vector3(1.0f, -1.0f, -1.0f),

            new Vector3(-1.0f, -1.0f,  1.0f),
            new Vector3(-1.0f,  1.0f,  1.0f),
            new Vector3(1.0f,  1.0f,  1.0f),
            new Vector3(1.0f,  1.0f,  1.0f),
            new Vector3(1.0f, -1.0f,  1.0f),
            new Vector3(-1.0f, -1.0f,  1.0f),

            new Vector3(-1.0f,  1.0f, -1.0f),
            new Vector3(1.0f,  1.0f, -1.0f),
            new Vector3(1.0f,  1.0f,  1.0f),
            new Vector3(1.0f,  1.0f,  1.0f),
            new Vector3(-1.0f,  1.0f,  1.0f),
            new Vector3(-1.0f,  1.0f, -1.0f),

            new Vector3(-1.0f, -1.0f, -1.0f),
            new Vector3(-1.0f, -1.0f,  1.0f),
            new Vector3(1.0f, -1.0f, -1.0f),
            new Vector3(1.0f, -1.0f, -1.0f),
            new Vector3(-1.0f, -1.0f,  1.0f),
            new Vector3(1.0f, -1.0f,  1.0f)
        };
        private static Mesh mesh;

        public static Mesh GetMesh()
        {
            if (mesh != null) return mesh;

            mesh = new Mesh("skybox");
            mesh.Indexed = false;

            foreach (var vertex in vertices)
            {
                mesh.Add(new Vertex
                {
                    Position = vertex
                });
            }

            mesh.UpdateBuffers();
            return mesh;
        }

        public static void SetCubemap(Application app, Texture cubemap)
        {
            var material = app.MaterialManager.GetMaterial(name);

            if (material == null)
                material = app.MaterialManager.CreateMaterial(name, app.AssetManager.Get<Shader>(Shader));

            // material.SetSampler(Veldrid.SamplerDescription.Point);
            material.SetTexture("skybox", cubemap);
        }

        public static void Init(Application app, Texture cubemap)
        {
            SetCubemap(app, cubemap);
            if (!initialized)
            {
                var mesh = GetMesh();
                app.Spawn(name, mesh, name, false);
                initialized = true;
            }
        }
    }
}