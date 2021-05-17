using System;
using System.Collections.Generic;
using System.Numerics;
using Deremis.Engine.Objects;
using Veldrid;

namespace Deremis.Engine.Systems
{
    public class DrawState : IDisposable
    {
        public string Key { get; set; }
        public CommandList CommandList { get; set; }
        public bool IsValid { get; set; }
        public Material Material { get; set; }
        public Mesh Mesh { get; set; }
        public DeviceBuffer InstanceBuffer { get; set; }
        public List<Matrix4x4> Worlds { get; set; }

        public void Dispose()
        {
            CommandList.Dispose();
            Material.Dispose();
            Mesh.Dispose();
            InstanceBuffer.Dispose();
        }
    }
}