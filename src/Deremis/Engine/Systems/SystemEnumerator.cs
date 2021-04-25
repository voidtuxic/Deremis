using System;
using System.Collections;
using System.Collections.Generic;
using DefaultEcs.System;

namespace Deremis.Engine.Systems
{
    public class SystemEnumerator<T> : IEnumerator<ISystem<T>>
    {
        private SequentialListSystem<T> _collection;
        private int curIndex;
        private ISystem<T> current;

        public SystemEnumerator(SequentialListSystem<T> collection)
        {
            _collection = collection;
            curIndex = -1;
            current = default;
        }

        public bool MoveNext()
        {
            if (++curIndex >= _collection.Count)
            {
                return false;
            }
            else
            {
                current = _collection[curIndex];
            }
            return true;
        }

        public void Reset() { curIndex = -1; }

        void IDisposable.Dispose() { }

        public ISystem<T> Current
        {
            get { return current; }
        }

        object IEnumerator.Current
        {
            get { return Current; }
        }
    }
}

