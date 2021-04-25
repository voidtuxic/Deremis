using System.Collections.Concurrent;
using Assimp;
using Deremis.Engine.Objects;
using Deremis.Engine.Rendering;
using System.Numerics;
using Deremis.Engine.Systems.Components;

using Mesh = Deremis.Engine.Objects.Mesh;
using AssimpMesh = Assimp.Mesh;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace Deremis.System.Assets
{
    public static class AssimpExtensions
    {
        public static Vector3 ToNumerics(this Vector3D vec)
        {
            return new Vector3(vec.X, vec.Y, vec.Z);
        }
        public static Matrix4x4 ToNumerics(this Assimp.Matrix4x4 mat)
        {
            return new Matrix4x4(
                mat.A1, mat.A2, mat.A3, mat.A4,
                mat.B1, mat.B2, mat.B3, mat.B4,
                mat.C1, mat.C2, mat.C3, mat.C4,
                mat.D1, mat.D2, mat.D3, mat.D4
            );
        }
    }

    public class AssimpHandler : IAssetHandler
    {
        private static PostProcessSteps ASSIMP_POSTPROCESS = PostProcessPreset.TargetRealTimeMaximumQuality | PostProcessSteps.MakeLeftHanded;

        public string Name => "Assimp Handler";

        private AssimpContext context;
        private readonly ConcurrentDictionary<string, Model> loadedMeshes = new ConcurrentDictionary<string, Model>();

        public AssimpHandler()
        {
            context = new AssimpContext();
        }

        public T Get<T>(AssetDescription description) where T : DObject
        {
            if (loadedMeshes.ContainsKey(description.name)) return loadedMeshes[description.name] as T;

            var assimpScene = context.ImportFile(AssetManager.current.Rebase(description.path), ASSIMP_POSTPROCESS);

            var model = new Model(description.name);

            if (assimpScene.HasMeshes)
            {
                foreach (var assimpMesh in assimpScene.Meshes)
                {
                    BuildMesh(model, assimpMesh);
                }
            }

            BuildModelScene(model, assimpScene.RootNode);

            loadedMeshes.TryAdd(description.name, model);
            return model as T;
        }

        private void BuildModelScene(Model model, Node node)
        {
            if (node.HasMeshes)
            {
                var transform = Transform.FromMatrix(node.Transform.ToNumerics());

                foreach (var mesh in node.MeshIndices)
                {
                    model.AppendNode(mesh, transform);
                }
            }

            if (node.HasChildren)
            {
                foreach (var child in node.Children)
                {
                    BuildModelScene(model, child);
                }
            }
        }

        private static void BuildMesh(Model model, AssimpMesh assimpMesh)
        {
            if (assimpMesh.FaceCount == 0 || assimpMesh.VertexCount < 3)
            {
                // need empties for node correspondance
                model.AppendMesh(null);
                return;
            }
            var mesh = new Mesh(assimpMesh.Name);
            for (var i = 0; i < assimpMesh.VertexCount; i++)
            {
                var uv = assimpMesh.TextureCoordinateChannels[0][i];
                var vertex = new PBRVertex
                {
                    Position = assimpMesh.Vertices[i].ToNumerics(),
                    Normal = assimpMesh.Normals[i].ToNumerics(),
                    UV = new Vector2(uv.X, 1f - uv.Y),
                    Tangent = assimpMesh.Tangents[i].ToNumerics(),
                    Bitangent = assimpMesh.BiTangents[i].ToNumerics()
                };

                mesh.Add(vertex);
            }

            for (var i = 0; i < assimpMesh.FaceCount; i++)
            {
                var face = assimpMesh.Faces[i];
                if (face.Indices.Count == 3)
                {
                    mesh.Add(face.Indices);
                }
            }
            if (mesh.UpdateBuffers())
            {
                model.AppendMesh(mesh);
            }
            else
            {
                // need empties for node correspondance
                model.AppendMesh(null);
                mesh.Dispose();
            }
        }

        public void Dispose()
        {
            foreach (var mesh in loadedMeshes.Values)
            {
                mesh.Dispose();
            }
            context.Dispose();
        }
    }
}