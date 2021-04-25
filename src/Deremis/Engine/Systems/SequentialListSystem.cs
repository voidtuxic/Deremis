using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs.System;

namespace Deremis.Engine.Systems
{
    /// <summary>
    /// Represents a collection of <see cref="ISystem{T}"/> to update sequentially.
    /// Based off the original DefaultECS <see cref="SequentialSystem{T}" />.
    /// </summary>
    /// <typeparam name="T">The type of the object used as state to update the systems.</typeparam>
    public sealed class SequentialListSystem<T> : ISystem<T>, ICollection<ISystem<T>>
    {
        #region Fields

        private readonly List<ISystem<T>> _systems = new List<ISystem<T>>();

        public ISystem<T> this[int index]
        {
            get { return _systems[index]; }
            set { _systems[index] = value; }
        }

        #endregion

        #region Initialisation

        /// <summary>
        /// Initialises a new instance of the <see cref="SequentialListSystem{T}"/> class.
        /// </summary>
        /// <param name="systems">The <see cref="ISystem{T}"/> instances.</param>
        public SequentialListSystem(IEnumerable<ISystem<T>> systems)
        {
            if (systems != null)
                AddRange(systems);
            IsEnabled = true;
        }

        /// <summary>
        /// Initialises a new instance of the <see cref="SequentialListSystem{T}"/> class.
        /// </summary>
        /// <param name="systems">The <see cref="ISystem{T}"/> instances.</param>
        public SequentialListSystem(params ISystem<T>[] systems)
            : this(systems as IEnumerable<ISystem<T>>)
        { }

        #endregion

        #region ISystem

        /// <summary>
        /// Gets or sets whether the current <see cref="SequentialListSystem{T}"/> instance should update or not.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Updates all the systems once sequentially.
        /// </summary>
        /// <param name="state">The state to use.</param>
        public void Update(T state)
        {
            if (IsEnabled)
            {
                foreach (ISystem<T> system in _systems)
                {
                    system.Update(state);
                }
            }
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes all the inner <see cref="ISystem{T}"/> instances.
        /// </summary>
        public void Dispose()
        {
            for (int i = _systems.Count - 1; i >= 0; --i)
            {
                _systems[i].Dispose();
            }
            Clear();
        }

        #endregion

        #region ICollection

        public int Count => _systems.Count;

        public bool IsReadOnly => ((ICollection<ISystem<T>>)_systems).IsReadOnly;

        public void Add(ISystem<T> item)
        {
            _systems.Add(item);
        }
        public void AddRange(IEnumerable<ISystem<T>> collection)
        {
            _systems.AddRange(collection);
        }

        public void Insert(int index, ISystem<T> item)
        {
            _systems.Insert(index, item);
        }
        public void InsertRange(int index, IEnumerable<ISystem<T>> collection)
        {
            _systems.InsertRange(index, collection);
        }

        public void Clear()
        {
            _systems.Clear();
        }

        public bool Contains(ISystem<T> item)
        {
            return _systems.Contains(item);
        }

        public void CopyTo(ISystem<T>[] array, int arrayIndex)
        {
            _systems.CopyTo(array, arrayIndex);
        }

        public bool Remove(ISystem<T> item)
        {
            return _systems.Remove(item);
        }

        public int IndexOf(ISystem<T> item)
        {
            return _systems.IndexOf(item);
        }

        public IEnumerator<ISystem<T>> GetEnumerator()
        {
            return new SystemEnumerator<T>(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new SystemEnumerator<T>(this);
        }

        #endregion
    }
}

