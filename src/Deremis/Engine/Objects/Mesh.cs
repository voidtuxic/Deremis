using System.Collections.Generic;
using Deremis.Engine.Rendering;
using Deremis.Platform;
using Veldrid;
using Veldrid.Utilities;

namespace Deremis.Engine.Objects
{
    public class Mesh : DObject
    {
        public override string Type => "Mesh";

        private readonly List<Vertex> vertices = new List<Vertex>();
        private readonly List<int> indices = new List<int>();

        public bool Indexed { get; set; } = true;
        public BoundingBox BoundingBox { get; set; }

        public DeviceBuffer VertexBuffer { get; private set; }
        public DeviceBuffer IndexBuffer { get; private set; }

        public uint VertexCount => (uint)vertices.Count;
        public uint IndexCount => (uint)indices.Count;

        public Mesh(string name) : base(name)
        {
        }

        public void Add(Vertex vertex)
        {
            vertices.Add(vertex);
        }

        public void Add(IEnumerable<int> face)
        {
            indices.AddRange(face);
        }

        public bool UpdateBuffers(bool resize = true)
        {
            if (vertices.Count == 0) return false;
            if (resize)
            {
                VertexBuffer?.Dispose();
                IndexBuffer?.Dispose();
                VertexBuffer = Application.current.Factory.CreateBuffer(new BufferDescription(
                    (uint)vertices.Count * Vertex.SizeInBytes, BufferUsage.VertexBuffer));
                VertexBuffer.Name = $"{Name}_vertex";
                if (Indexed)
                {
                    IndexBuffer = Application.current.Factory.CreateBuffer(new BufferDescription(
                        (uint)indices.Count * sizeof(int), BufferUsage.IndexBuffer));
                    IndexBuffer.Name = $"{Name}_index";
                }
            }
            if (VertexBuffer != null)
            {
                Application.current.GraphicsDevice.UpdateBuffer(VertexBuffer, 0, vertices.ToArray());
                if (Indexed)
                {
                    Application.current.GraphicsDevice.UpdateBuffer(IndexBuffer, 0, indices.ToArray());
                }
                return true;
            }
            return false;
        }

        public override void Dispose()
        {
            VertexBuffer?.Dispose();
            IndexBuffer?.Dispose();
        }
    }
}