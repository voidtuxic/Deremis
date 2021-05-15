using System.Text;
using Deremis.Engine.Objects;

namespace Deremis.Engine.Systems.Components
{
    public struct Drawable
    {
        public string mesh;
        public string material;

        public override int GetHashCode()
        {
            return mesh.GetHashCode() & material.GetHashCode();
        }

        public override string ToString()
        {
            var sb = new StringBuilder(mesh.Length + material.Length + 1);
            sb.AppendFormat("{0}_{1}", mesh, material);
            return sb.ToString();
        }
    }
}