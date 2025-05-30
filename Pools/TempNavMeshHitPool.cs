﻿// <auto-generated>
//   AI-Refactored: TempNavMeshHitPool.cs (Beyond Diamond, Zero-Alloc & Parity Edition, June 2025)
//   Bulletproof pooling for NavMeshHit[] arrays for high-performance AI navigation/fallback logic.
//   Thread-safe, teardown/reload safe, fully AI-Refactored compliant.
//   MIT License.
// </auto-generated>

namespace AIRefactored.Pools
{
    using System;
    using System.Collections.Generic;
    using UnityEngine.AI;

    /// <summary>
    /// Pool for reusable <see cref="NavMeshHit"/> arrays used in navigation and fallback systems.
    /// </summary>
    public static class TempNavMeshHitPool
    {
        private static readonly Dictionary<int, Stack<NavMeshHit[]>> PoolBySize = new Dictionary<int, Stack<NavMeshHit[]>>(16);
        private static readonly object SyncRoot = new object();

        static TempNavMeshHitPool()
        {
            try
            {
                AppDomain.CurrentDomain.DomainUnload += (_, __) => ClearAll();
            }
            catch { }
        }

        /// <summary>
        /// Rents a pooled <see cref="NavMeshHit"/> array of the specified size.
        /// </summary>
        /// <param name="size">Requested array size.</param>
        /// <returns>Reusable NavMeshHit array.</returns>
        public static NavMeshHit[] Rent(int size)
        {
            if (size <= 0)
                size = 1;

            lock (SyncRoot)
            {
                if (PoolBySize.TryGetValue(size, out Stack<NavMeshHit[]> stack) && stack.Count > 0)
                    return stack.Pop();
            }

            return new NavMeshHit[size];
        }

        /// <summary>
        /// Returns a <see cref="NavMeshHit"/> array to the pool.
        /// </summary>
        /// <param name="array">Array to return.</param>
        public static void Return(NavMeshHit[] array)
        {
            if (array == null || array.Length == 0)
                return;

            lock (SyncRoot)
            {
                if (!PoolBySize.TryGetValue(array.Length, out Stack<NavMeshHit[]> stack))
                {
                    stack = new Stack<NavMeshHit[]>(8);
                    PoolBySize[array.Length] = stack;
                }

                stack.Push(array);
            }
        }

        /// <summary>
        /// Prewarms the pool with reusable <see cref="NavMeshHit"/> arrays.
        /// </summary>
        /// <param name="size">Length of each array.</param>
        /// <param name="count">Number of arrays to cache.</param>
        public static void Prewarm(int size, int count)
        {
            if (size <= 0 || count <= 0)
                return;

            lock (SyncRoot)
            {
                if (!PoolBySize.TryGetValue(size, out Stack<NavMeshHit[]> stack))
                {
                    stack = new Stack<NavMeshHit[]>(count);
                    PoolBySize[size] = stack;
                }

                for (int i = 0; i < count; i++)
                    stack.Push(new NavMeshHit[size]);
            }
        }

        /// <summary>
        /// Clears all pooled <see cref="NavMeshHit"/> arrays and resets internal pool state.
        /// </summary>
        public static void ClearAll()
        {
            lock (SyncRoot)
            {
                foreach (var stack in PoolBySize.Values)
                    stack.Clear();

                PoolBySize.Clear();
            }
        }
    }
}
