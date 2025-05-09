﻿// <auto-generated>
//   This file is part of AI-Refactored, an open-source project focused on realistic AI behavior in Escape from Tarkov.
//   Licensed under the MIT License. See LICENSE in the repository root for more information.
//
//   THIS FILE IS SYSTEMATICALLY MANAGED.
//   Please follow strict StyleCop, ReSharper, and AI-Refactored code standards for all modifications.
// </auto-generated>

#nullable enable

namespace AIRefactored.AI.Navigation
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AIRefactored.AI.Core;
    using AIRefactored.Core;
    using AIRefactored.Runtime;
    using BepInEx.Logging;
    using Unity.AI.Navigation;
    using UnityEngine;
    using UnityEngine.AI;

    /// <summary>
    /// Dynamically scans valid AI navigation points including cover, rooftop, flank, and fallback zones.
    /// Headless-safe, memory-safe, and deferred for runtime performance.
    /// </summary>
    public static class NavPointBootstrapper
    {
        #region Constants

        private const float ForwardCoverCheckDistance = 4.0f;
        private const float MaxSampleHeight = 30.0f;
        private const float MinNavPointClearance = 1.6f;
        private const float RoofRaycastHeight = 12.0f;
        private const float ScanRadius = 80.0f;
        private const float ScanSpacing = 2.5f;
        private const float VerticalProbeMax = 24.0f;
        private const float VerticalStep = 2.0f;

        #endregion

        #region Fields

        private static readonly List<Vector3> BackgroundPending = new List<Vector3>(512);
        private static readonly Queue<Vector3> ScanQueue = new Queue<Vector3>(2048);
        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        private static Vector3 _center = Vector3.zero;
        private static bool _isRunning;
        private static bool _isTaskRunning;
        private static int _registered;

        #endregion

        #region Public API

        /// <summary>
        /// Queues all spatial points for scanning and begins NavPoint registration.
        /// </summary>
        /// <param name="mapId">The name of the current map (used for surface context).</param>
        public static void RegisterAll(string mapId)
        {
            if (_isRunning || !IsHostEnvironment())
            {
                Logger.LogWarning("[NavPointBootstrapper] Skipped — already running or non-host.");
                return;
            }

            _isRunning = true;
            _registered = 0;
            ScanQueue.Clear();
            BackgroundPending.Clear();

            NavMeshSurface? surface = Object.FindObjectOfType<NavMeshSurface>();
            if (surface == null)
            {
                Logger.LogWarning("[NavPointBootstrapper] No NavMeshSurface found.");
                _isRunning = false;
                return;
            }

            _center = surface.transform.position;
            float half = ScanRadius * 0.5f;

            for (float x = -half; x <= half; x += ScanSpacing)
            {
                for (float z = -half; z <= half; z += ScanSpacing)
                {
                    Vector3 basePos = _center + new Vector3(x, MaxSampleHeight, z);
                    ScanQueue.Enqueue(basePos);
                }
            }

            Logger.LogInfo("[NavPointBootstrapper] Queued " + ScanQueue.Count + " surface points.");

            if (!_isTaskRunning)
            {
                _isTaskRunning = true;
                Task.Run(PrequeueVerticalPoints);
            }
        }

        /// <summary>
        /// Processes queued spatial points to detect and register valid AI nav points.
        /// </summary>
        public static void Tick()
        {
            if (!_isRunning || !IsHostEnvironment())
            {
                return;
            }

            int maxPerFrame = FikaHeadlessDetector.IsHeadless ? 80 : 40;
            int processed = 0;

            while (ScanQueue.Count > 0 && processed++ < maxPerFrame)
            {
                Vector3 probe = ScanQueue.Dequeue();

                if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, MaxSampleHeight))
                {
                    continue;
                }

                Vector3 pos = hit.point;

                if (!NavMesh.SamplePosition(pos, out NavMeshHit navHit, 1.0f, NavMesh.AllAreas))
                {
                    continue;
                }

                if (Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.up, MinNavPointClearance))
                {
                    continue;
                }

                Vector3 final = navHit.position;
                float elevation = final.y - _center.y;

                bool isCover = IsCoverPoint(final);
                bool isIndoor = IsIndoorPoint(final);
                string tag = ClassifyNavPoint(elevation, isCover, isIndoor);

                NavPointRegistry.Register(final, isCover, tag, elevation, isIndoor);
                _registered++;
            }

            if (ScanQueue.Count == 0 && BackgroundPending.Count > 0)
            {
                for (int i = 0; i < BackgroundPending.Count; i++)
                {
                    ScanQueue.Enqueue(BackgroundPending[i]);
                }

                BackgroundPending.Clear();
                Logger.LogInfo("[NavPointBootstrapper] Queued vertical fallback points.");
            }

            if (ScanQueue.Count == 0)
            {
                _isRunning = false;
                Logger.LogInfo("[NavPointBootstrapper] ✅ Completed — " + _registered + " nav points registered.");
            }
        }

        #endregion

        #region Internal Methods

        private static void PrequeueVerticalPoints()
        {
            float half = ScanRadius * 0.5f;

            for (float x = -half; x <= half; x += ScanSpacing)
            {
                for (float z = -half; z <= half; z += ScanSpacing)
                {
                    for (float y = 5.0f; y <= VerticalProbeMax; y += VerticalStep)
                    {
                        BackgroundPending.Add(_center + new Vector3(x, y, z));
                    }
                }
            }

            _isTaskRunning = false;
        }

        private static bool IsCoverPoint(Vector3 pos)
        {
            Vector3 eye = pos + Vector3.up * 1.4f;

            for (float angle = -45.0f; angle <= 45.0f; angle += 15.0f)
            {
                Vector3 dir = Quaternion.Euler(0.0f, angle, 0.0f) * Vector3.forward;
                if (Physics.Raycast(eye, dir, ForwardCoverCheckDistance, AIRefactoredLayerMasks.HighPolyCollider))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsIndoorPoint(Vector3 pos)
        {
            return Physics.Raycast(pos + Vector3.up * 1.4f, Vector3.up, RoofRaycastHeight);
        }

        private static string ClassifyNavPoint(float elevation, bool isCover, bool isIndoor)
        {
            if (isIndoor)
            {
                return "indoor";
            }

            if (elevation > 6.0f)
            {
                return "roof";
            }

            return isCover ? "fallback" : "flank";
        }

        private static bool IsHostEnvironment()
        {
            return GameWorldHandler.IsLocalHost() || FikaHeadlessDetector.IsHeadless;
        }

        #endregion
    }
}
