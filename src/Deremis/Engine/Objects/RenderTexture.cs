using Deremis.Engine.Systems;
using Deremis.Platform;
using Veldrid;
using VeldridTexture = Veldrid.Texture;

namespace Deremis.Engine.Objects
{
    public class RenderTexture : DObject
    {
        public Texture RenderTarget { get; private set; }
        public Texture CopyTexture { get; private set; }

        public TextureView View => CopyTexture.View;

        private bool isMultisampled;

        public RenderTexture(ScreenRenderSystem screen, string name, uint width, uint height, PixelFormat format, bool isDepth) : base(name)
        {
            isMultisampled = screen.MSAA != TextureSampleCount.Count1;
            var usage = isDepth ? TextureUsage.DepthStencil : TextureUsage.RenderTarget;
            TextureDescription texDescription = TextureDescription.Texture2D(
                width, height, 1, 1,
                format, usage, screen.MSAA);
            var renderTargetTex = screen.App.Factory.CreateTexture(ref texDescription);
            renderTargetTex.Name = $"{name}_render";
            RenderTarget = new Texture($"{name}_render", renderTargetTex, null);

            texDescription = TextureDescription.Texture2D(
                width, height, 1, 1,
                format, TextureUsage.Storage | TextureUsage.Sampled, TextureSampleCount.Count1);
            var copyTex = screen.App.Factory.CreateTexture(ref texDescription);
            var copyView = screen.App.Factory.CreateTextureView(copyTex);
            copyTex.Name = $"{name}_copy";
            copyView.Name = $"{name}_copy";
            CopyTexture = new Texture($"{name}_copy", copyTex, copyView);
        }

        /// CALL ONLY AFTER COMMANDLIST.BEGIN
        public void UpdateCopyTexture(CommandList commandList)
        {
            if (isMultisampled)
            {
                commandList.ResolveTexture(RenderTarget.VeldridTexture, CopyTexture.VeldridTexture);
            }
            else
            {
                commandList.CopyTexture(RenderTarget.VeldridTexture, CopyTexture.VeldridTexture);
            }
        }

        public override void Dispose()
        {
            RenderTarget.Dispose();
            CopyTexture.Dispose();
        }
    }
}