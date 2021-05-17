using System;
using Deremis.Engine.Objects;
using Veldrid;

namespace Deremis.Engine.Systems
{
    public class DrawState : IDisposable
    {
        public string key;
        public CommandList commandList;
        public bool isValid;
        public Material material;
        public Mesh mesh;
        public DeviceBuffer instanceBuffer;

        public void Dispose()
        {
            commandList.Dispose();
            material.Dispose();
            mesh.Dispose();
            instanceBuffer.Dispose();
        }
    }
}