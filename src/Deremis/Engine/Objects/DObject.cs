using System;

namespace Deremis.Engine.Objects
{
    public abstract class DObject : IDisposable
    {
        public virtual string Type => "DeremisObject";
        public string Name { get; private set; }

        protected DObject(string name)
        {
            Name = name;
        }

        public virtual void Dispose()
        {
        }
    }
}