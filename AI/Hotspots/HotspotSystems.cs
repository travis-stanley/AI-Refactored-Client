#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Optimization;
using AIRefactored.Core;
using AIRefactored.Runtime;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Hotspots
{
    /// <summary>
    /// Handles per-bot hotspot routing and memory of visited tactical areas.
    /// Provides realistic patrol loops, defensive logic, and escalation triggers.
    /// </summary>
    public class HotspotSystem
    {
        #region Nested

        private sealed class HotspotSession
        {
            private readonly BotOwner _bot;
            private readonly BotComponentCache? _cache;
            private readonly List<Vector3> _route;
            private readonly bool _isDefender;
            private readonly string _mapKey;

            private int _index;
            private float _nextSwitchTime;
            private float _lastHitTime = -999f;
            private float _lastRouteStartTime;

            private const float BaseDefendRadius = 7f;
            private const float DamageCooldown = 6f;

            public HotspotSession(BotOwner bot, List<Vector3> route, bool isDefender)
            {
                _bot = bot;
                _route = route;
                _isDefender = isDefender;
                _cache = BotCacheUtility.GetCache(bot);
                _index = 0;
                _mapKey = GameWorldHandler.GetCurrentMapName();
                _lastRouteStartTime = Time.time;

                if (_bot.GetPlayer?.HealthController is HealthControllerClass health)
                    health.ApplyDamageEvent += OnDamaged;

                _nextSwitchTime = Time.time + GetSwitchInterval();
                HotspotMemory.MarkVisited(_mapKey, _route[0]);
            }

            private void OnDamaged(EBodyPart part, float damage, DamageInfoStruct info)
            {
                _lastHitTime = Time.time;
                _cache?.PanicHandler?.TriggerPanic();
            }

            private float GetSwitchInterval()
            {
                var profile = BotRegistry.Get(_bot.ProfileId);
                if (profile == null)
                    return 120f;

                float baseTime = profile.Personality switch
                {
                    PersonalityType.Cautious => 160f,
                    PersonalityType.TeamPlayer => 100f,
                    PersonalityType.Strategic => 90f,
                    PersonalityType.Explorer => 75f,
                    PersonalityType.Dumb => 45f,
                    _ => 120f,
                };

                return baseTime * Mathf.Clamp01(1f + profile.ChaosFactor * 0.6f);
            }

            public void Tick()
            {
                if (_bot == null || _bot.IsDead || _route.Count == 0 || _bot.GetPlayer?.IsYourPlayer == true)
                    return;

                if (_bot.Memory?.GoalEnemy != null)
                {
                    _bot.Sprint(true);
                    return;
                }

                if (Time.time - _lastHitTime < DamageCooldown)
                    return;

                if (_isDefender)
                {
                    float dist = Vector3.Distance(_bot.Position, _route[0]);
                    float defendRadius = BaseDefendRadius;
                    float composure = _cache?.PanicHandler?.GetComposureLevel() ?? 1f;
                    defendRadius *= Mathf.Clamp(1f + (1f - composure), 1f, 2f);

                    if (dist > defendRadius)
                        BotMovementHelper.SmoothMoveTo(_bot, _route[0]);

                    return;
                }

                Vector3 target = _route[_index];
                float distToTarget = Vector3.Distance(_bot.Position, target);

                if (Time.time >= _nextSwitchTime || distToTarget < 2f)
                {
                    _index = (_index + 1) % _route.Count;
                    _nextSwitchTime = Time.time + GetSwitchInterval();

                    HotspotMemory.MarkVisited(_mapKey, _route[_index]);

                    float visits = HotspotMemory.GetVisitCount(_mapKey, _route[_index]);
                    if (visits >= 3 && Time.time - _lastRouteStartTime < 300f)
                        AIOptimizationManager.TriggerEscalation(_bot);
                }

                BotMovementHelper.SmoothMoveTo(_bot, AddJitterTo(target));
            }

            private Vector3 AddJitterTo(Vector3 target)
            {
                var profile = _cache?.AIRefactoredBotOwner?.PersonalityProfile;
                if (profile == null)
                    return target;

                Vector3 jitter = Vector3.zero;
                float chaos = profile.ChaosFactor;

                if (profile.IsFrenzied)
                    jitter = Random.insideUnitSphere * 2.5f;
                else if (profile.IsSilentHunter)
                    jitter = Random.insideUnitSphere * 0.25f;
                else if (chaos > 0.4f)
                    jitter = new Vector3(Mathf.Sin(Time.time), 0f, Mathf.Cos(Time.time)) * chaos;

                jitter.y = 0f;
                return target + jitter;
            }
        }

        #endregion

        #region Fields

        private readonly Dictionary<BotOwner, HotspotSession> _sessions = new();
        private static readonly List<BotOwner> _botList = new();
        private static readonly ManualLogSource _log = AIRefactoredController.Logger;

        #endregion

        #region Public API

        /// <summary>
        /// Clears old route sessions on startup.
        /// </summary>
        public void Initialize()
        {
            _sessions.Clear();
        }

        /// <summary>
        /// Called every tick to manage bot hotspot behavior.
        /// </summary>
        public void Tick()
        {
            var controller = Singleton<BotsController>.Instance;
            if (controller?.Bots?.BotOwners == null)
                return;

            _botList.Clear();
            _botList.AddRange(controller.Bots.BotOwners);

            for (int i = 0; i < _botList.Count; i++)
            {
                var bot = _botList[i];
                if (bot == null || bot.IsDead || bot.GetPlayer?.IsYourPlayer == true)
                    continue;

                if (!_sessions.TryGetValue(bot, out var session))
                {
                    session = AssignHotspotRoute(bot);
                    if (session != null)
                        _sessions[bot] = session;
                }

                session?.Tick();
            }
        }

        /// <summary>
        /// Picks a random hotspot location for utility use.
        /// </summary>
        public static Vector3 GetRandomHotspot(BotOwner bot)
        {
            var list = HotspotLoader.GetAllHotspotsRaw();
            return list.Count > 0
                ? list[Random.Range(0, list.Count)]
                : bot.Position + Random.insideUnitSphere * 5f;
        }

        #endregion

        #region Internals

        private HotspotSession? AssignHotspotRoute(BotOwner bot)
        {
            var profile = BotRegistry.Get(bot.ProfileId);
            var key = GameWorldHandler.GetCurrentMapName();

            var raw = HotspotLoader.GetAllHotspotsRaw();
            var all = new List<Vector3>(raw);
            if (all.Count == 0)
            {
                _log.LogWarning($"[AIRefactored] No hotspots found for map: {key}");
                return null;
            }

            bool defendOnly = profile?.Personality switch
            {
                PersonalityType.Camper => true,
                PersonalityType.Cautious => Random.value < 0.5f,
                _ => false
            };

            var available = new List<Vector3>(all);
            available.RemoveAll(p => HotspotMemory.WasRecentlyVisited(key, p, 300f));
            if (available.Count == 0)
                available = all;

            if (defendOnly)
            {
                Vector3 defend = available[Random.Range(0, available.Count)];
                return new HotspotSession(bot, new List<Vector3> { defend }, true);
            }

            int routeLen = Random.Range(2, 4);
            var route = new List<Vector3>(routeLen);
            var used = new HashSet<int>();

            while (route.Count < routeLen && used.Count < available.Count)
            {
                int i = Random.Range(0, available.Count);
                if (used.Add(i))
                    route.Add(available[i]);
            }

            return new HotspotSession(bot, route, false);
        }

        #endregion
    }

    /// <summary>
    /// Stores recent hotspot visits per map to avoid repetition.
    /// </summary>
    internal static class HotspotMemory
    {
        private static readonly Dictionary<string, Dictionary<Vector3, float>> Visited = new();

        public static void MarkVisited(string map, Vector3 pos)
        {
            if (!Visited.TryGetValue(map, out var dict))
            {
                dict = new Dictionary<Vector3, float>();
                Visited[map] = dict;
            }

            dict[pos] = Time.time;
        }

        public static bool WasRecentlyVisited(string map, Vector3 pos, float cooldown)
        {
            return Visited.TryGetValue(map, out var dict)
                && dict.TryGetValue(pos, out float time)
                && Time.time - time < cooldown;
        }

        public static float GetVisitCount(string map, Vector3 pos)
        {
            if (!Visited.TryGetValue(map, out var dict))
                return 0;

            return dict.ContainsKey(pos) ? 1 : 0;
        }
    }
}
