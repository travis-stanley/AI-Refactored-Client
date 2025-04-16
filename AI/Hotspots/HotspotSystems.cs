#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using AIRefactored.Core;
using Comfort.Common;
using EFT;
using Newtonsoft.Json;
using UnityEngine;

namespace AIRefactored.AI.Hotspots
{
    public class HotspotSystem : MonoBehaviour
    {
        private class HotspotData
        {
            public string name = string.Empty;
            public List<Vector3> positions = new();
        }

        private class HotspotSession
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

                if (_bot.GetPlayer != null)
                {
                    _bot.GetPlayer.HealthController.ApplyDamageEvent += OnDamaged;
                }
            }

            private float GetSwitchInterval()
            {
                var id = _bot.Profile?.Id;
                var personality = id != null ? BotRegistry.Get(id) : null;

                if (personality != null)
                {
                    return personality.Personality switch
                    {
                        PersonalityType.Cautious => 180f,
                        PersonalityType.Dumb => 60f,
                        PersonalityType.Strategic => 90f,
                        _ => 120f
                    };
                }

                return 120f;
            }

            private void OnDamaged(EBodyPart part, float dmg, DamageInfoStruct info)
            {
                _lastHitTime = Time.time;
            }

            public void Update()
            {
                if (_bot == null || _bot.IsDead || _route.Count == 0)
                    return;

                Vector3 currentTarget = _route[_hotspotIndex].positions[_positionIndex];
                float dist = Vector3.Distance(_bot.Position, currentTarget);

                if (_bot.Memory?.GoalEnemy != null)
                {
                    _bot.Sprint(true);
                    return;
                }

                if (_isGuardian)
                {
                    if (dist > _defendRadius)
                        _bot.GoToPoint(currentTarget, false);

                    return; // Guardians stay near their hotspot
                }

                // Wanderer patrol logic
                if (Time.time >= _nextSwitchTime || dist < 2f)
                {
                    MoveNext();
                    _nextSwitchTime = Time.time + GetSwitchInterval();
                }

                _bot.GoToPoint(CurrentTarget(), false);
            }

            private Vector3 CurrentTarget()
            {
                return _route[_hotspotIndex].positions[_positionIndex];
            }

            private void MoveNext()
            {
                _positionIndex++;
                if (_positionIndex >= _route[_hotspotIndex].positions.Count)
                {
                    _positionIndex = 0;
                    _hotspotIndex++;
                    if (_hotspotIndex >= _route.Count)
                        _hotspotIndex = 0;
                }
            }
        }

        private readonly Dictionary<string, List<HotspotData>> _hotspotsByMap = new();
        private readonly Dictionary<BotOwner, HotspotSession> _botSessions = new();

        private const string HOTSPOT_FOLDER = "hotspots/";

        private void Start()
        {
            LoadAllHotspots();
        }

        private void Update()
        {
            var bots = Singleton<BotsController>.Instance?.Bots?.BotOwners;
            if (bots == null) return;

            foreach (var bot in bots)
            {
                if (bot == null || bot.IsDead || bot.AIData == null)
                    continue;

                if (!_botSessions.ContainsKey(bot))
                {
                    var session = AssignHotspot(bot);
                    if (session != null)
                        _botSessions[bot] = session;
                }

                _botSessions[bot]?.Update();
            }
        }

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

            // Wanderer or PMC logic
            int routeLength = UnityEngine.Random.Range(1, 3);
            List<HotspotData> route = new();
            HashSet<int> used = new();

            while (route.Count < routeLength && used.Count < hotspots.Count)
            {
                int i = UnityEngine.Random.Range(0, hotspots.Count);
                if (!used.Contains(i))
                {
                    used.Add(i);
                    route.Add(hotspots[i]);
                }
            }

            return new HotspotSession(bot, route, false);
        }

        private void LoadAllHotspots()
        {
            string fullPath = Path.Combine(Application.dataPath, HOTSPOT_FOLDER);
            if (!Directory.Exists(fullPath))
                fullPath = Path.Combine("BepInEx", "plugins", "AIRefactored", HOTSPOT_FOLDER);

            if (!Directory.Exists(fullPath))
            {
                Debug.LogWarning($"[AIRefactored] Hotspot folder not found: {fullPath}");
                return;
            }

            string[] files = Directory.GetFiles(fullPath, "*.json");
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    string json = File.ReadAllText(files[i]);
                    var data = JsonConvert.DeserializeObject<Dictionary<string, List<HotspotData>>>(json);
                    if (data == null)
                        continue;

                    foreach (var kvp in data)
                        _hotspotsByMap[kvp.Key] = kvp.Value;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AIRefactored] Failed to load hotspot file {files[i]}: {ex.Message}");
                }
            }
        }

        public static Vector3 GetRandomHotspot(BotOwner bot)
        {
            var role = bot.Profile?.Info?.Settings?.Role ?? WildSpawnType.assault;
            var map = Singleton<GameWorld>.Instance?.LocationId?.ToLowerInvariant() ?? "unknown";

            var hotspots = HotspotLoader.GetHotspotsForCurrentMap(role);
            if (hotspots == null || hotspots.Points.Count == 0)
            {
                Debug.LogWarning($"[HotspotSystem] No hotspots found for {map} ({role})");
                return bot.Position + UnityEngine.Random.insideUnitSphere * 5f;
            }

            int index = UnityEngine.Random.Range(0, hotspots.Points.Count);
            return hotspots.Points[index];
        }
    }
}
