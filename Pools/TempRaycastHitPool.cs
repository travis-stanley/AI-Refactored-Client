﻿// <auto-generated>
//   AI-Refactored: TempRaycastHitPool.cs (Beyond Diamond, Zero-Alloc & Parity Edition, June 2025)
//   Bulletproof pooling for RaycastHit[] arrays for high-performance AI vision/suppression/occlusion logic.
//   Thread-safe, teardown/reload safe, AI-Refactored compliant.
//   MIT License.
// </auto-generated>

namespace AIRefactored.Pools
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Centralized pool for <see cref="RaycastHit"/> array reuse in vision, suppression, and occlusion logic.
    /// </summary>
    public static class TempRaycastHitPool
    {
        private static readonly Dictionary<int, Stack<RaycastHit[]>> PoolBySize = new Dictionary<int, Stack<RaycastHit[]>>(16);
        private static readonly object SyncRoot = new object();

        static TempRaycastHitPool()
        {
            try
            {
                AppDomain.CurrentDomain.DomainUnload += (_, __) => ClearAll();
            }
            catch { }
        }

        /// <summary>
        /// Rents a reusable <see cref="RaycastHit"/> array of the given size.
        /// </summary>
        /// <param name="size">Minimum array length.</param>
        /// <returns>Reusable array of RaycastHit.</returns>
        public static RaycastHit[] Rent(int size)
        {
            if (size <= 0)
                size = 1;

            lock (SyncRoot)
            {
                if (PoolBySize.TryGetValue(size, out Stack<RaycastHit[]> stack) && stack.Count > 0)
                    return stack.Pop();
            }

            return new RaycastHit[size];
        }

        /// <summary>
        /// Returns a <see cref="RaycastHit"/> array to the pool.
        /// </summary>
        /// <param name="array">Array to return.</param>
        public static void Return(RaycastHit[] array)
        {
            if (array == null || array.Length == 0)
                return;

            lock (SyncRoot)
            {
                if (!PoolBySize.TryGetValue(array.Length, out Stack<RaycastHit[]> stack))
                {
                    stack = new Stack<RaycastHit[]>(8);
                    PoolBySize[array.Length] = stack;
                }

                stack.Push(array);
            }
        }

        /// <summary>
        /// Pre-warms the pool with reusable <see cref="RaycastHit"/> arrays.
        /// </summary>
        /// <param name="size">Length of each array.</param>
        /// <param name="count">Number of arrays to allocate.</param>
        public static void Prewarm(int size, int count)
        {
            if (size <= 0 || count <= 0)
                return;

            lock (SyncRoot)
            {
                if (!PoolBySize.TryGetValue(size, out Stack<RaycastHit[]> stack))
                {
                    stack = new Stack<RaycastHit[]>(count);
                    PoolBySize[size] = stack;
                }

                for (int i = 0; i < count; i++)
                    stack.Push(new RaycastHit[size]);
            }
        }

        /// <summary>
        /// Clears all pooled raycast buffers.
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
