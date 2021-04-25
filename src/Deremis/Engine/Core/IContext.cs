using Deremis.System;

namespace Deremis.Engine.Core
{
    public interface IContext
    {
        string Name { get; }

        void Initialize(Application app);
    }
}