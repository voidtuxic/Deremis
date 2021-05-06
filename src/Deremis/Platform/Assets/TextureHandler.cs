using System;
using System.Collections.Concurrent;
using System.IO;
using Deremis.Engine.Objects;
using Deremis.Platform.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using StbImageSharp;
using Veldrid.ImageSharp;

namespace Deremis.Platform.Assets
{
    public class TextureHandler : IAssetHandler
    {
        public struct Options
        {
            public bool mipmaps;
            public bool srgb;
            public bool cubemap;
            public bool hdr;

            public Options(bool mipmaps = true, bool srgb = false, bool cubemap = false, bool hdr = false)
            {
                this.mipmaps = mipmaps;
                this.srgb = srgb;
                this.cubemap = cubemap;
                this.hdr = hdr;
            }
        }
        public string Name => "Texture Handler";
        private readonly ConcurrentDictionary<string, Texture> loadedTextures = new ConcurrentDictionary<string, Texture>();

        public T Get<T>(AssetDescription description) where T : DObject
        {
            if (loadedTextures.ContainsKey(description.name)) return loadedTextures[description.name] as T;

            var options = new Options();
            if (description.options != null) options = (Options)description.options;
            var path = AssetManager.current.Rebase(description.path);

            var app = Application.current;
            Veldrid.Texture veldridTex = null;

            if (options.hdr)
            {
                using (var stream = File.OpenRead(path))
                {
                    var info = ImageInfo.FromStream(stream);
                    if (info.HasValue)
                    {
                        using (var image = new Image<RgbaVector>(info.Value.Width, info.Value.Height))
                        {
                            var result = ImageResultFloat.FromStream(stream);
                            var data = result.Data;
                            for (int i = 0; i < info.Value.Width * info.Value.Height; ++i)
                            {
                                var r = data[i * 3];
                                var g = data[i * 3 + 1];
                                var b = data[i * 3 + 2];
                                var x = i % info.Value.Width;
                                var y = i / info.Value.Width;
                                Span<RgbaVector> pixelRowSpan = image.GetPixelRowSpan(y);
                                pixelRowSpan[x] = new RgbaVector(r, g, b);
                            }
                            var imageSharpTex = new ImageSharpHDRTexture(image);
                            veldridTex = imageSharpTex.CreateDeviceTexture(app.GraphicsDevice, app.Factory);
                        }
                    }
                }
            }
            else if (options.cubemap)
            {
                var posX = path.Replace("###", "1");
                var negX = path.Replace("###", "2");
                var posY = path.Replace("###", "3");
                var negY = path.Replace("###", "4");
                var posZ = path.Replace("###", "5");
                var negZ = path.Replace("###", "6");
                var imageSharpTex = new ImageSharpCubemapTexture(posX, negX, posY, negY, posZ, negZ, false);
                veldridTex = imageSharpTex.CreateDeviceTexture(app.GraphicsDevice, app.Factory);
            }
            else
            {
                var imageSharpTex = new ImageSharpTexture(path, options.mipmaps, options.srgb);
                veldridTex = imageSharpTex.CreateDeviceTexture(app.GraphicsDevice, app.Factory);
            }
            var texture = new Texture(description.name, veldridTex, app.Factory.CreateTextureView(new Veldrid.TextureViewDescription(veldridTex)));

            return texture as T;
        }

        public void Dispose()
        {
            foreach (var texture in loadedTextures.Values)
            {
                texture.Dispose();
            }
        }
    }
}