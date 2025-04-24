#nullable enable

using AIRefactored.Core;
using AIRefactored.Runtime;
using BepInEx.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace AIRefactored.AI.Navigation
{
    /// <summary>
    /// Dynamically scans the world for valid AI navigation points including rooftops, high cover,
    /// flanking routes, and fallback zones. All data is registered into the NavPointRegistry.
    /// Headless-safe and frame-split for performance.
    /// </summary>
    public static class NavPointBootstrapper
    {
        #region Configuration

        private const float ScanRadius = 80f;
        private const float ScanSpacing = 2.5f;
        private const float MaxSampleHeight = 30f;
        private const float VerticalProbeMax = 24f;
        private const float VerticalStep = 2f;
        private const float MinNavPointClearance = 1.6f;
        private const float ForwardCoverCheckDistance = 4f;
        private const float RoofRaycastHeight = 12f;

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        private static readonly Queue<Vector3> _scanQueue = new(2048);
        private static readonly List<Vector3> _backgroundPending = new(512);

        private static bool _isRunning;
        private static int _registered;
        private static Vector3 _center;

        #endregion

        #region Public Entry

        /// <summary>
        /// Begins scanning nav points across the map centered on the first NavMeshSurface.
        /// </summary>
        public static void RegisterAll(string mapId)
        {
            if (_isRunning)
                return;

            _isRunning = true;
            _registered = 0;
            _scanQueue.Clear();
            _backgroundPending.Clear();

            var surface = Object.FindObjectOfType<NavMeshSurface>();
            _center = surface != null ? surface.transform.position : Vector3.zero;

            float half = ScanRadius * 0.5f;
            for (float x = -half; x <= half; x += ScanSpacing)
            {
                for (float z = -half; z <= half; z += ScanSpacing)
                {
                    Vector3 basePos = _center + new Vector3(x, MaxSampleHeight, z);
                    _scanQueue.Enqueue(basePos);
                }
            }

            Logger.LogInfo($"[NavPointBootstrapper] 📍 Queued {_scanQueue.Count} surface points for scanning.");
            Task.Run(PrequeueVerticalPoints);
        }

        #endregion

        #region Tick Processing

        /// <summary>
        /// Called every frame to incrementally process nav point scans.
        /// </summary>
        public static void Tick()
        {
            if (!_isRunning)
                return;

            int perFrameLimit = FikaHeadlessDetector.IsHeadless ? 80 : 40;
            int processed = 0;

            while (_scanQueue.Count > 0 && processed++ < perFrameLimit)
            {
                Vector3 basePos = _scanQueue.Dequeue();

                if (!Physics.Raycast(basePos, Vector3.down, out RaycastHit hit, MaxSampleHeight))
                    continue;

                Vector3 pos = hit.point;

                if (!NavMesh.SamplePosition(pos, out var navHit, 1.0f, NavMesh.AllAreas))
                    continue;

                if (Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.up, MinNavPointClearance))
                    continue;

                Vector3 worldPos = navHit.position;
                bool isCover = IsCoverPoint(worldPos);
                bool isIndoor = Physics.Raycast(worldPos + Vector3.up * 1.4f, Vector3.up, RoofRaycastHeight);
                float elevation = worldPos.y - _center.y;

                string tag = ClassifyNavPoint(elevation, isCover, isIndoor);
                NavPointRegistry.Register(worldPos, isCover, tag, elevation, isIndoor);
                _registered++;
            }

            if (_scanQueue.Count == 0 && _backgroundPending.Count > 0)
            {
                foreach (Vector3 point in _backgroundPending)
                    _scanQueue.Enqueue(point);

                _backgroundPending.Clear();
                Logger.LogInfo("[NavPointBootstrapper] 🧠 Added vertical fallback scan layer.");
            }

            if (_scanQueue.Count == 0)
            {
                _isRunning = false;
                Logger.LogInfo($"[NavPointBootstrapper] ✅ Registered {_registered} nav points.");
            }
        }

        #endregion

        #region Background Vertical Scan

        /// <summary>
        /// Adds additional points to scan from above to catch rooftops and fallback vertical paths.
        /// </summary>
        private static void PrequeueVerticalPoints()
        {
            float half = ScanRadius * 0.5f;

            for (float x = -half; x <= half; x += ScanSpacing)
            {
                for (float z = -half; z <= half; z += ScanSpacing)
                {
                    for (float height = 5f; height <= VerticalProbeMax; height += VerticalStep)
                    {
                        Vector3 pos = _center + new Vector3(x, height, z);
                        _backgroundPending.Add(pos);
                    }
                }
            }
        }

        #endregion

        #region Cover and Tag Classification

        /// <summary>
        /// Checks surrounding angles for forward-facing raycast collisions to classify a cover point.
        /// </summary>
        private static bool IsCoverPoint(Vector3 pos)
        {
            Vector3 eye = pos + Vector3.up * 1.4f;

            for (float angle = -45f; angle <= 45f; angle += 15f)
            {
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                if (Physics.Raycast(eye, dir, out _, ForwardCoverCheckDistance, LayerMaskClass.HighPolyCollider))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Classifies the nav point into a tagged type: indoor, roof, fallback, flank.
        /// </summary>
        private static string ClassifyNavPoint(float elevation, bool isCover, bool isIndoor)
        {
            if (isIndoor) return "indoor";
            if (elevation > 6f) return "roof";
            if (isCover) return "fallback";
            return "flank";
        }

        #endregion
    }
}
