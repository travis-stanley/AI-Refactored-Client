﻿// <auto-generated>
//   AI-Refactored: TempHashSetPool.cs (Beyond Diamond, Zero-Alloc & Parity Edition, June 2025)
//   Bulletproof generic HashSet<T> pooling for high-performance AI/world logic.
//   Thread-safe, teardown/reload safe, and compliant with all AI-Refactored pooling architecture.
//   MIT License.
// </auto-generated>

namespace AIRefactored.Pools
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Centralized pool for temporary <see cref="HashSet{T}"/> reuse.
    /// Prevents GC churn in high-frequency logic paths.
    /// </summary>
    public static class TempHashSetPool
    {
        private static readonly Dictionary<Type, Stack<object>> PoolByType = new Dictionary<Type, Stack<object>>(128);
        private static readonly object SyncRoot = new object();

        static TempHashSetPool()
        {
            try
            {
                AppDomain.CurrentDomain.DomainUnload += (_, __) => ClearAll();
            }
            catch { }
        }

        /// <summary>
        /// Retrieves a pooled hash set of the given type, or allocates a new one if none are available.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <returns>A cleared, reusable hash set.</returns>
        public static HashSet<T> Rent<T>()
        {
            lock (SyncRoot)
            {
                if (PoolByType.TryGetValue(typeof(T), out Stack<object> stack) && stack.Count > 0)
                    return (HashSet<T>)stack.Pop();
            }

            return new HashSet<T>();
        }

        /// <summary>
        /// Returns a hash set to the pool after clearing it.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="set">The hash set to return.</param>
        public static void Return<T>(HashSet<T> set)
        {
            if (set == null)
                return;

            set.Clear();

            lock (SyncRoot)
            {
                if (!PoolByType.TryGetValue(typeof(T), out Stack<object> stack))
                {
                    stack = new Stack<object>(32);
                    PoolByType[typeof(T)] = stack;
                }

                stack.Push(set);
            }
        }

        /// <summary>
        /// Pre-warms the pool for a specific element type.
        /// </summary>
        /// <typeparam name="T">Element type to prewarm.</typeparam>
        /// <param name="count">How many instances to preload.</param>
        public static void Prewarm<T>(int count)
        {
            if (count <= 0)
                return;

            lock (SyncRoot)
            {
                if (!PoolByType.TryGetValue(typeof(T), out Stack<object> stack))
                {
                    stack = new Stack<object>(count);
                    PoolByType[typeof(T)] = stack;
                }

                for (int i = 0; i < count; i++)
                    stack.Push(new HashSet<T>());
            }
        }

        /// <summary>
        /// Clears all pooled instances across all types.
        /// </summary>
        public static void ClearAll()
        {
            lock (SyncRoot)
            {
                foreach (var stack in PoolByType.Values)
                    stack.Clear();

                PoolByType.Clear();
            }
        }
    }
}
