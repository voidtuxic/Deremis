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
        private VeldridTexture resolveTexture;

        public RenderTexture(Application app, string name, uint width, uint height, PixelFormat format, bool isDepth) : base(name)
        {
            isMultisampled = app.MSAA != TextureSampleCount.Count1;
            var usage = isDepth ? TextureUsage.DepthStencil : TextureUsage.RenderTarget;
            TextureDescription texDescription = TextureDescription.Texture2D(
                width, height, 1, 1,
                format, usage, app.MSAA);
            var renderTargetTex = app.Factory.CreateTexture(ref texDescription);
            renderTargetTex.Name = $"{name}_render";
            RenderTarget = new Texture($"{name}_render", renderTargetTex, null);

            if (isMultisampled)
            {
                texDescription.SampleCount = TextureSampleCount.Count1;
                resolveTexture = app.Factory.CreateTexture(ref texDescription);
                resolveTexture.Name = $"{name}_resolve";
            }

            texDescription = TextureDescription.Texture2D(
                width, height, 1, 1,
                format, TextureUsage.Storage | TextureUsage.Sampled, TextureSampleCount.Count1);
            var copyTex = app.Factory.CreateTexture(ref texDescription);
            var copyView = app.Factory.CreateTextureView(copyTex);
            copyTex.Name = $"{name}_copy";
            copyView.Name = $"{name}_copy";
            CopyTexture = new Texture($"{name}_copy", copyTex, copyView);
        }

        /// CALL ONLY AFTER COMMANDLIST.BEGIN
        public void UpdateCopyTexture(CommandList commandList)
        {
            if (isMultisampled)
            {
                commandList.ResolveTexture(RenderTarget.VeldridTexture, resolveTexture);
                commandList.CopyTexture(resolveTexture, CopyTexture.VeldridTexture);
            }
            else
            {
                commandList.CopyTexture(RenderTarget.VeldridTexture, CopyTexture.VeldridTexture);
            }
        }

        public override void Dispose()
        {
            resolveTexture?.Dispose();
            RenderTarget.Dispose();
            CopyTexture.Dispose();
        }
    }
}