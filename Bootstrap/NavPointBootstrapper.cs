#nullable enable

namespace AIRefactored.AI.Navigation
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using AIRefactored.Core;
    using AIRefactored.Runtime;

    using BepInEx.Logging;

    using Unity.AI.Navigation;

    using UnityEngine;
    using UnityEngine.AI;

    /// <summary>
    ///     Dynamically scans the world for valid AI navigation points including rooftops, high cover,
    ///     flanking routes, and fallback zones. All data is registered into the NavPointRegistry.
    ///     Headless-safe and frame-split for performance.
    /// </summary>
    public static class NavPointBootstrapper
    {
        private const float ForwardCoverCheckDistance = 4f;

        private const float MaxSampleHeight = 30f;

        private const float MinNavPointClearance = 1.6f;

        private const float RoofRaycastHeight = 12f;

        private const float ScanRadius = 80f;

        private const float ScanSpacing = 2.5f;

        private const float VerticalProbeMax = 24f;

        private const float VerticalStep = 2f;

        private static readonly List<Vector3> _backgroundPending = new(512);

        private static readonly Queue<Vector3> _scanQueue = new(2048);

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        private static Vector3 _center;

        private static bool _isRunning;

        private static int _registered;

        /// <summary>
        ///     Begins scanning nav points across the map centered on the first NavMeshSurface.
        /// </summary>
        public static void RegisterAll(string mapId)
        {
            if (_isRunning)
                return;

            _isRunning = true;
            _registered = 0;
            _scanQueue.Clear();
            _backgroundPending.Clear();

            var surface = object.FindObjectOfType<NavMeshSurface>();
            _center = surface != null ? surface.transform.position : Vector3.zero;

            var half = ScanRadius * 0.5f;
            for (var x = -half; x <= half; x += ScanSpacing)
            for (var z = -half; z <= half; z += ScanSpacing)
            {
                var basePos = _center + new Vector3(x, MaxSampleHeight, z);
                _scanQueue.Enqueue(basePos);
            }

            Logger.LogInfo($"[NavPointBootstrapper] Queued {_scanQueue.Count} surface points for scanning.");
            Task.Run(PrequeueVerticalPoints);
        }

        /// <summary>
        ///     Called every frame to incrementally process nav point scans.
        /// </summary>
        public static void Tick()
        {
            if (!_isRunning)
                return;

            var perFrameLimit = FikaHeadlessDetector.IsHeadless ? 80 : 40;
            var processed = 0;

            while (_scanQueue.Count > 0 && processed++ < perFrameLimit)
            {
                var basePos = _scanQueue.Dequeue();

                if (!Physics.Raycast(basePos, Vector3.down, out var hit, MaxSampleHeight))
                    continue;

                var pos = hit.point;

                if (!NavMesh.SamplePosition(pos, out var navHit, 1.0f, NavMesh.AllAreas))
                    continue;

                if (Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.up, MinNavPointClearance))
                    continue;

                var worldPos = navHit.position;
                var isCover = IsCoverPoint(worldPos);
                var isIndoor = Physics.Raycast(worldPos + Vector3.up * 1.4f, Vector3.up, RoofRaycastHeight);
                var elevation = worldPos.y - _center.y;

                var tag = ClassifyNavPoint(elevation, isCover, isIndoor);

                NavPointRegistry.Register(
                    worldPos,
                    isCover,
                    tag,
                    elevation,
                    isIndoor // Future: angle detection
                );

                _registered++;
            }

            if (_scanQueue.Count == 0 && _backgroundPending.Count > 0)
            {
                foreach (var point in _backgroundPending)
                    _scanQueue.Enqueue(point);

                _backgroundPending.Clear();
                Logger.LogInfo("[NavPointBootstrapper] Added vertical fallback scan layer.");
            }

            if (_scanQueue.Count == 0)
            {
                _isRunning = false;
                Logger.LogInfo($"[NavPointBootstrapper] Completed: {_registered} nav points registered.");
            }
        }

        private static string ClassifyNavPoint(float elevation, bool isCover, bool isIndoor)
        {
            if (isIndoor) return "indoor";
            if (elevation > 6f) return "roof";
            if (isCover) return "fallback";
            return "flank";
        }

        private static bool IsCoverPoint(Vector3 pos)
        {
            var eye = pos + Vector3.up * 1.4f;

            for (var angle = -45f; angle <= 45f; angle += 15f)
            {
                var dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                if (Physics.Raycast(eye, dir, out _, ForwardCoverCheckDistance, LayerMaskClass.HighPolyCollider))
                    return true;
            }

            return false;
        }

        private static void PrequeueVerticalPoints()
        {
            var half = ScanRadius * 0.5f;

            for (var x = -half; x <= half; x += ScanSpacing)
            for (var z = -half; z <= half; z += ScanSpacing)
            for (var height = 5f; height <= VerticalProbeMax; height += VerticalStep)
            {
                var pos = _center + new Vector3(x, height, z);
                _backgroundPending.Add(pos);
            }
        }
    }
}