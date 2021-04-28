using System;
using System.Collections.Generic;
using System.IO;
using Deremis.Engine.Objects;

namespace Deremis.Platform.Assets
{
    public class AssetManager : IDisposable
    {
        public static AssetManager current;

        // 0 : Assimp
        private readonly Dictionary<Type, IAssetHandler> handlers = new Dictionary<Type, IAssetHandler>();
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
            handlers.Add(typeof(Model), new AssimpHandler());
            handlers.Add(typeof(Shader), new ShaderHandler());
            handlers.Add(typeof(Texture), new TextureHandler());
        }

        public T Get<T>(AssetDescription description) where T : DObject
        {
            if (!handlers.ContainsKey(typeof(T))) return null;

            return handlers[typeof(T)].Get<T>(description);
        }

        public string Rebase(string path)
        {
            return Path.Combine(rootPath, path);
        }

        public void Dispose()
        {
            foreach (var handler in handlers.Values)
            {
                handler.Dispose();
            }
        }
    }
}