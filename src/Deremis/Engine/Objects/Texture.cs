using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Veldrid;
using VeldridTexture = Veldrid.Texture;

namespace Deremis.Engine.Objects
{
    public class Texture : DObject
    {
        public VeldridTexture VeldridTexture { get; private set; }
        public TextureView View { get; private set; }
        public Image<RgbaVector> Image { get; set; }

        public Texture(string name, VeldridTexture veldridTexture, TextureView view) : base(name)
        {
            this.VeldridTexture = veldridTexture;
            if (this.VeldridTexture != null)
                this.VeldridTexture.Name = name;
            this.View = view;
            if (View != null)
                View.Name = name;
        }

        public override void Dispose()
        {
            VeldridTexture?.Dispose();
            View?.Dispose();
        }
    }
}