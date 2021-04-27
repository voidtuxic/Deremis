using Veldrid;
using VeldridTexture = Veldrid.Texture;

namespace Deremis.Engine.Objects
{
    public class Texture : DObject
    {
        public VeldridTexture VeldridTexture { get; private set; }
        public TextureView View { get; private set; }

        public Texture(string name, VeldridTexture veldridTexture, TextureView view) : base(name)
        {
            this.VeldridTexture = veldridTexture;
            this.View = view;
        }

        public override void Dispose()
        {
            VeldridTexture?.Dispose();
            View?.Dispose();
        }
    }
}