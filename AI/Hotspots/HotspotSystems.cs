#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using AIRefactored.Core;
using Comfort.Common;
using EFT;
using Newtonsoft.Json;
using UnityEngine;
using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;

namespace AIRefactored.AI.Hotspots
{
    /// <summary>
    /// Handles dynamic patrol, defense, and mission routing based on map-defined hotspots.
    /// Assigned to each bot during runtime to drive naturalistic movement and behavior loops.
    /// </summary>
    public class HotspotSystem : MonoBehaviour
    {
        #region Internal Session Class

        private sealed class HotspotSession
        {
            private readonly BotOwner _bot;
            private readonly List<HotspotData> _route;
            private readonly bool _isGuardian;
            private readonly float _defendRadius = 8f;

            private int _hotspotIndex;
            private int _positionIndex;
            private float _nextSwitchTime;
            private float _lastHitTime = -999f;

            public HotspotSession(BotOwner bot, List<HotspotData> route, bool isGuardian)
            {
                _bot = bot;
                _route = route;
                _isGuardian = isGuardian;
                _hotspotIndex = 0;
                _positionIndex = 0;
                _nextSwitchTime = Time.time + GetSwitchInterval();

                if (_bot.GetPlayer?.HealthController is HealthControllerClass controller)
                {
                    controller.ApplyDamageEvent += OnDamaged;
                }
            }

            private void OnDamaged(EBodyPart part, float damage, DamageInfoStruct info)
            {
                _lastHitTime = Time.time;
            }

            private float GetSwitchInterval()
            {
                var id = _bot.Profile?.Id;
                var personality = id != null ? BotRegistry.Get(id) : null;

                return personality?.Personality switch
                {
                    PersonalityType.Cautious => 180f,
                    PersonalityType.Strategic => 90f,
                    PersonalityType.Dumb => 60f,
                    _ => 120f
                };
            }

            public void Update()
            {
                if (_bot == null || _bot.IsDead || _route.Count == 0)
                    return;

                if (_bot.GetPlayer?.IsYourPlayer == true)
                    return;

                if (_bot.Memory?.GoalEnemy != null)
                {
                    _bot.Sprint(true);
                    return;
                }

                Vector3 target = CurrentTarget();
                float dist = Vector3.Distance(_bot.Position, target);

                if (_isGuardian)
                {
                    if (dist > _defendRadius)
                        BotMovementHelper.SmoothMoveTo(_bot, target);
                    return;
                }

                if (Time.time >= _nextSwitchTime || dist < 2f)
                {
                    MoveNext();
                    _nextSwitchTime = Time.time + GetSwitchInterval();
                }

                BotMovementHelper.SmoothMoveTo(_bot, CurrentTarget());
            }

            private Vector3 CurrentTarget() => _route[_hotspotIndex].positions[_positionIndex];

            private void MoveNext()
            {
                _positionIndex++;
                if (_positionIndex >= _route[_hotspotIndex].positions.Count)
                {
                    _positionIndex = 0;
                    _hotspotIndex = (_hotspotIndex + 1) % _route.Count;
                }
            }
        }

        private class HotspotData
        {
            public string name = string.Empty;
            public List<Vector3> positions = new();
        }

        #endregion

        #region State

        private const string HOTSPOT_FOLDER = "hotspots/";
        private readonly Dictionary<string, List<HotspotData>> _hotspotsByMap = new();
        private readonly Dictionary<BotOwner, HotspotSession> _botSessions = new();

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            LoadAllHotspots();
        }

        private void Update()
        {
            var bots = Singleton<BotsController>.Instance?.Bots?.BotOwners;
            if (bots == null)
                return;

            foreach (var bot in bots)
            {
                if (bot == null || bot.IsDead || bot.AIData == null)
                    continue;

                if (bot.GetPlayer != null && bot.GetPlayer.IsYourPlayer)
                    continue;

                if (!_botSessions.TryGetValue(bot, out var session))
                {
                    session = AssignHotspot(bot);
                    if (session != null)
                        _botSessions[bot] = session;
                }

                session?.Update();
            }
        }

        #endregion

        #region Assignment Logic

        /// <summary>
        /// Assigns a route of one or more hotspots to the specified bot.
        /// </summary>
        private HotspotSession? AssignHotspot(BotOwner bot)
        {
            if (!Singleton<GameWorld>.Instantiated || Singleton<GameWorld>.Instance == null)
                return null;

            string map = Singleton<GameWorld>.Instance.LocationId.ToLowerInvariant();

            if (!_hotspotsByMap.TryGetValue(map, out var hotspots) || hotspots.Count == 0)
                return null;

            var side = bot.Profile?.Info?.Side;
            bool isScav = side == EPlayerSide.Savage;
            bool isGuardian = isScav && UnityEngine.Random.value < 0.4f;

            if (isGuardian)
            {
                var defend = hotspots[UnityEngine.Random.Range(0, hotspots.Count)];
                return new HotspotSession(bot, new List<HotspotData> { defend }, true);
            }

            int routeLength = UnityEngine.Random.Range(1, 3);
            var route = new List<HotspotData>();
            var used = new HashSet<int>();

            while (route.Count < routeLength && used.Count < hotspots.Count)
            {
                int i = UnityEngine.Random.Range(0, hotspots.Count);
                if (used.Add(i))
                    route.Add(hotspots[i]);
            }

            return new HotspotSession(bot, route, false);
        }

        #endregion

        #region File Loader

        /// <summary>
        /// Loads all hotspot JSON files from the disk into memory.
        /// </summary>
        private void LoadAllHotspots()
        {
            string basePath = Path.Combine(Application.dataPath, HOTSPOT_FOLDER);
            if (!Directory.Exists(basePath))
                basePath = Path.Combine("BepInEx", "plugins", "AIRefactored", HOTSPOT_FOLDER);

            if (!Directory.Exists(basePath))
            {
                Debug.LogWarning($"[AIRefactored] Hotspot folder not found: {basePath}");
                return;
            }

            string[] files = Directory.GetFiles(basePath, "*.json");
            foreach (var file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var data = JsonConvert.DeserializeObject<Dictionary<string, List<HotspotData>>>(json);

                    if (data == null)
                        continue;

                    foreach (var kvp in data)
                    {
                        if (!_hotspotsByMap.ContainsKey(kvp.Key))
                            _hotspotsByMap[kvp.Key] = new List<HotspotData>();

                        _hotspotsByMap[kvp.Key].AddRange(kvp.Value);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AIRefactored] Failed to load hotspot file '{file}': {ex.Message}");
                }
            }
        }

        #endregion

        #region External API

        /// <summary>
        /// Returns a random hotspot location for the current map, based on bot role.
        /// </summary>
        public static Vector3 GetRandomHotspot(BotOwner bot)
        {
            var role = bot.Profile?.Info?.Settings?.Role ?? WildSpawnType.assault;
            string map = Singleton<GameWorld>.Instance?.LocationId?.ToLowerInvariant() ?? "unknown";

            var set = HotspotLoader.GetHotspotsForCurrentMap(role);
            if (set == null || set.Points.Count == 0)
            {
                Debug.LogWarning($"[AIRefactored] No hotspots found for {map} ({role})");
                return bot.Position + UnityEngine.Random.insideUnitSphere * 5f;
            }

            return set.Points[UnityEngine.Random.Range(0, set.Points.Count)];
        }

        #endregion
    }
}
