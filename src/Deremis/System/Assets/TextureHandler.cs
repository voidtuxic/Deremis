using System.Collections.Concurrent;
using Deremis.Engine.Objects;
using Veldrid.ImageSharp;

namespace Deremis.System.Assets
{
    public class TextureHandler : IAssetHandler
    {
        public string Name => "Texture Handler";
        private readonly ConcurrentDictionary<string, Texture> loadedTextures = new ConcurrentDictionary<string, Texture>();

        public T Get<T>(AssetDescription description) where T : DObject
        {
            if (loadedTextures.ContainsKey(description.name)) return loadedTextures[description.name] as T;

            var app = Application.current;
            var imageSharpTex = new ImageSharpTexture(AssetManager.current.Rebase(description.path));
            var veldridTex = imageSharpTex.CreateDeviceTexture(app.GraphicsDevice, app.Factory);
            var texture = new Texture(description.name, veldridTex, app.Factory.CreateTextureView(veldridTex));

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