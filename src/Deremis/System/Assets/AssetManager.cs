using System;
using System.IO;
using Deremis.Engine.Objects;

namespace Deremis.System.Assets
{
    public class AssetManager : IDisposable
    {
        public static AssetManager current;

        // 0 : Assimp
        private static readonly IAssetHandler[] handlers = new IAssetHandler[] {
            new AssimpHandler(),
            new ShaderHandler(),
        };
        private readonly string rootPath;

        public string RootPath => rootPath;

        public AssetManager(string rootPath)
        {
            if (current != null)
            {
                // log duplicate
            }
            current = this;
            this.rootPath = rootPath;
        }

        public T Get<T>(AssetDescription description) where T : DObject
        {
            if (description.type >= handlers.Length) return default;

            return handlers[description.type].Get<T>(description);
        }

        public string Rebase(string path)
        {
            return Path.Combine(rootPath, path);
        }

        public void Dispose()
        {
            foreach (var handler in handlers)
            {
                handler.Dispose();
            }
        }
    }
}