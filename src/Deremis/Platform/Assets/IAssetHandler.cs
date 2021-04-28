using System;
using Deremis.Engine.Objects;

namespace Deremis.Platform.Assets
{
    public interface IAssetHandler : IDisposable
    {
        string Name { get; }
        T Get<T>(AssetDescription description) where T : DObject;
    }
}