using System.Collections.Generic;
using DefaultEcs;
using Deremis.Engine.Systems.Components;
using Deremis.Engine.Systems.Extensions;
using Deremis.System;

namespace Deremis.Engine.Objects
{
    public class Model : DObject
    {
        internal struct Node
        {
            public int mesh;
            public Transform transform;
        }
        public override string Type => "Model";

        public List<Mesh> Meshes { get; private set; }
        private List<Node> nodes;

        public Model(string name) : base(name)
        {
        }

        public void AppendMesh(Mesh mesh)
        {
            if (Meshes == null) Meshes = new List<Mesh>();
            Meshes.Add(mesh);
        }

        public void AppendNode(int mesh, Transform transform)
        {
            if (mesh >= Meshes.Count || Meshes[mesh] == null) return;
            if (nodes == null) nodes = new List<Node>();
            nodes.Add(new Node { mesh = mesh, transform = transform });
        }

        public Entity Spawn(Application app, string material, Transform transform)
        {
            Entity entity = app.CreateTransform(Name);
            entity.Set(transform);
            foreach (var node in nodes)
            {
                var mesh = Meshes[node.mesh];
                if (mesh == null) continue;
                var child = app.Spawn(mesh.Name, mesh, material);
                child.Set(node.transform);
                child.SetAsChildOf(entity);
            }
            return entity;
        }

        public override void Dispose()
        {
            foreach (var mesh in Meshes)
            {
                mesh?.Dispose();
            }
        }
    }
}