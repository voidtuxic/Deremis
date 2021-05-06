using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Veldrid;

namespace Deremis.Platform.Helpers
{
    /// <summary>
    /// Based off <see cref="Veldrid.ImageSharp.ImageSharpTexture"/>
    /// </summary>
    public class ImageSharpHDRTexture
    {
        /// <summary>
        /// An array of images, each a single element in the mipmap chain.
        /// The first element is the largest, most detailed level, and each subsequent element
        /// is half its size, down to 1x1 pixel.
        /// </summary>
        public Image<RgbaVector>[] Images { get; }

        /// <summary>
        /// The width of the largest image in the chain.
        /// </summary>
        public uint Width => (uint)Images[0].Width;

        /// <summary>
        /// The height of the largest image in the chain.
        /// </summary>
        public uint Height => (uint)Images[0].Height;

        /// <summary>
        /// The pixel format of all images.
        /// </summary>
        public PixelFormat Format { get; }

        /// <summary>
        /// The size of each pixel, in bytes.
        /// </summary>
        public uint PixelSizeInBytes => sizeof(byte) * 16;

        /// <summary>
        /// The number of levels in the mipmap chain. This is equal to the length of the Images array.
        /// </summary>
        public uint MipLevels => (uint)Images.Length;

        public ImageSharpHDRTexture(string path) : this(Image.Load<RgbaVector>(path)) { }
        public ImageSharpHDRTexture(Stream stream) : this(Image.Load<RgbaVector>(stream)) { }
        public ImageSharpHDRTexture(Image<RgbaVector> image)
        {
            Format = PixelFormat.R32_G32_B32_A32_Float;
            Images = new Image<RgbaVector>[] { image };
        }

        public unsafe Texture CreateDeviceTexture(GraphicsDevice gd, ResourceFactory factory)
        {
            return CreateTextureViaUpdate(gd, factory);
        }

        private unsafe Texture CreateTextureViaUpdate(GraphicsDevice gd, ResourceFactory factory)
        {
            Texture tex = factory.CreateTexture(TextureDescription.Texture2D(
                Width, Height, MipLevels, 1, Format, TextureUsage.Sampled));
            for (int level = 0; level < MipLevels; level++)
            {
                Image<RgbaVector> image = Images[level];
                if (!image.TryGetSinglePixelSpan(out Span<RgbaVector> pixelSpan))
                {
                    throw new VeldridException("Unable to get image pixelspan.");
                }
                fixed (void* pin = &MemoryMarshal.GetReference(pixelSpan))
                {
                    gd.UpdateTexture(
                        tex,
                        (IntPtr)pin,
                        (uint)(PixelSizeInBytes * image.Width * image.Height),
                        0,
                        0,
                        0,
                        (uint)image.Width,
                        (uint)image.Height,
                        1,
                        (uint)level,
                        0);
                }
            }

            return tex;
        }
    }
}