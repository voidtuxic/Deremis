using System;
using CommandLine;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace TexturePacker
{
    class Program
    {
        public class Options
        {
            [Option('r', "red", Required = true, HelpText = "Red channel image path")]
            public string Red { get; set; }
            [Option('g', "green", Required = true, HelpText = "Green channel image path")]
            public string Green { get; set; }
            [Option('b', "blue", Required = true, HelpText = "Blue channel image path")]
            public string Blue { get; set; }
        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(o =>
                {
                    var rImg = Image.Load<Rgba32>(o.Red);
                    var gImg = Image.Load<Rgba32>(o.Green);
                    gImg.Mutate(x => x.Resize(rImg.Width, rImg.Height));
                    var bImg = Image.Load<Rgba32>(o.Blue);
                    bImg.Mutate(x => x.Resize(rImg.Width, rImg.Height));

                    using (var image = new Image<Rgba32>(rImg.Width, rImg.Height))
                    {
                        for (int y = 0; y < image.Height; y++)
                        {
                            Span<Rgba32> pixelRowSpan = image.GetPixelRowSpan(y);
                            for (int x = 0; x < image.Width; x++)
                            {
                                pixelRowSpan[x] = new Rgba32(rImg[x, y].R, gImg[x, y].R, bImg[x, y].R, 255);
                            }
                        }

                        image.SaveAsPng("packed.png");
                    }
                });
        }
    }
}