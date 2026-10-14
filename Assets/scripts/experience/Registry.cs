// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// Every component of one type that exists, so nothing has to search the scene for it (CS-342). Entries join in
    /// <c>Awake</c> and leave in <c>OnDestroy</c>, which keeps a component that is only disabled (narration under the
    /// system menu) findable; <see cref="Active"/> then yields those on an active GameObject, the same set as
    /// <c>FindObjectsByType</c> with inactive objects excluded. Iterating <see cref="Active"/> allocates nothing.
    /// </summary>
    public static class Registry<T> where T : Component
    {
        private static readonly List<T> Items = new List<T>();

        public static void Add(T item)
        {
            if (item != null && !Items.Contains(item))
            {
                Items.Add(item);
            }
        }

        public static void Remove(T item) => Items.Remove(item);

        public static ActiveSet Active => default;

        /// <summary>The first registered entry on an active GameObject, or null.</summary>
        public static T First
        {
            get
            {
                foreach (var item in Active)
                {
                    return item;
                }

                return null;
            }
        }

        public readonly struct ActiveSet
        {
            public Enumerator GetEnumerator() => new Enumerator(-1);
        }

        public struct Enumerator
        {
            private int _index;

            public Enumerator(int start) => _index = start;

            public T Current => Items[_index];

            public bool MoveNext()
            {
                while (++_index < Items.Count)
                {
                    var item = Items[_index];
                    if (item != null && item.gameObject.activeInHierarchy)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
