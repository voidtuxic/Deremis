using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
            public int mipmapCount;
            public int baseSize;

            public Options(bool mipmaps = true, bool srgb = false, bool cubemap = false, bool hdr = false, int mipmapCount = 1, int baseSize = 128)
            {
                this.mipmaps = mipmaps;
                this.srgb = srgb;
                this.cubemap = cubemap;
                this.hdr = hdr;
                this.mipmapCount = mipmapCount;
                this.baseSize = baseSize;
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
            Image<RgbaVector> hdrImage = null;

            if (options.hdr)
            {
                using (var stream = File.OpenRead(path))
                {
                    var info = ImageInfo.FromStream(stream);
                    if (info.HasValue)
                    {
                        hdrImage = new Image<RgbaVector>(info.Value.Width, info.Value.Height);
                        var result = ImageResultFloat.FromStream(stream);
                        var data = result.Data;
                        for (int i = 0; i < info.Value.Width * info.Value.Height; ++i)
                        {
                            var r = data[i * 3];
                            var g = data[i * 3 + 1];
                            var b = data[i * 3 + 2];
                            var x = i % info.Value.Width;
                            var y = i / info.Value.Width;
                            Span<RgbaVector> pixelRowSpan = hdrImage.GetPixelRowSpan(y);
                            pixelRowSpan[x] = new RgbaVector(r, g, b);
                        }
                        var imageSharpTex = new ImageSharpHDRTexture(hdrImage);
                        veldridTex = imageSharpTex.CreateDeviceTexture(app.GraphicsDevice, app.Factory);
                    }
                }
            }
            else if (options.cubemap)
            {
                var posXImages = new List<Image<Rgba32>>(options.mipmapCount);
                var posYImages = new List<Image<Rgba32>>(options.mipmapCount);
                var posZImages = new List<Image<Rgba32>>(options.mipmapCount);
                var negXImages = new List<Image<Rgba32>>(options.mipmapCount);
                var negYImages = new List<Image<Rgba32>>(options.mipmapCount);
                var negZImages = new List<Image<Rgba32>>(options.mipmapCount);
                for (var i = 0; i < options.mipmapCount; i++)
                {
                    var filename = path;
                    if (options.mipmapCount > 1)
                    {
                        var ratio = MathF.Pow(2, i);
                        filename = filename.Replace("***", $"{i}_{options.baseSize / ratio}x{options.baseSize / ratio}");
                    }
                    var posX = filename.Replace("###", "posx");
                    var negX = filename.Replace("###", "negx");
                    var posY = filename.Replace("###", "posy");
                    var negY = filename.Replace("###", "negy");
                    var posZ = filename.Replace("###", "posz");
                    var negZ = filename.Replace("###", "negz");

                    posXImages.Add(Image.Load<Rgba32>(posX));
                    posYImages.Add(Image.Load<Rgba32>(negX));
                    posZImages.Add(Image.Load<Rgba32>(posY));
                    negXImages.Add(Image.Load<Rgba32>(negY));
                    negYImages.Add(Image.Load<Rgba32>(posZ));
                    negZImages.Add(Image.Load<Rgba32>(negZ));
                }
                var imageSharpTex = new ImageSharpCubemapTexture(
                    posXImages.ToArray(),
                    posYImages.ToArray(),
                    posZImages.ToArray(),
                    negXImages.ToArray(),
                    negYImages.ToArray(),
                    negZImages.ToArray()
                );
                veldridTex = imageSharpTex.CreateDeviceTexture(app.GraphicsDevice, app.Factory);
            }
            else
            {
                var imageSharpTex = new ImageSharpTexture(path, options.mipmaps, options.srgb);
                veldridTex = imageSharpTex.CreateDeviceTexture(app.GraphicsDevice, app.Factory);
            }
            var texture = new Texture(description.name, veldridTex, app.Factory.CreateTextureView(new Veldrid.TextureViewDescription(veldridTex)));
            texture.Image = hdrImage;
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