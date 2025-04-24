#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
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
    /// Drives bot navigation between tactical hotspots on a map.
    /// Each bot maintains its own route or defense zone and adapts behavior based on combat state.
    /// </summary>
    public class HotspotSystem
    {
        #region Inner Session

        private sealed class HotspotSession
        {
            private readonly BotOwner _bot;
            private readonly BotComponentCache? _cache;
            private readonly List<HotspotRegistry.Hotspot> _route;
            private readonly bool _isDefender;
            private readonly string _mapKey;

            private int _index;
            private float _nextSwitchTime;
            private float _lastHitTime = -999f;
            private float _lastRouteStartTime;

            private const float BaseDefendRadius = 7f;
            private const float DamageCooldown = 6f;

            public HotspotSession(BotOwner bot, List<HotspotRegistry.Hotspot> route, bool isDefender)
            {
                _bot = bot;
                _route = route;
                _isDefender = isDefender;
                _mapKey = GameWorldHandler.GetCurrentMapName();
                _cache = BotCacheUtility.GetCache(bot);
                _index = 0;
                _lastRouteStartTime = Time.time;

                if (_bot.GetPlayer?.HealthController is HealthControllerClass health)
                    health.ApplyDamageEvent += OnDamaged;

                _nextSwitchTime = Time.time + GetSwitchInterval();
            }

            private void OnDamaged(EBodyPart part, float damage, DamageInfoStruct info)
            {
                _lastHitTime = Time.time;
                _cache?.PanicHandler?.TriggerPanic();
            }

            private float GetSwitchInterval()
            {
                var profile = BotRegistry.Get(_bot.ProfileId);
                float baseTime = profile?.Personality switch
                {
                    PersonalityType.Cautious => 160f,
                    PersonalityType.TeamPlayer => 100f,
                    PersonalityType.Strategic => 90f,
                    PersonalityType.Explorer => 75f,
                    PersonalityType.Dumb => 45f,
                    _ => 120f
                };

                return baseTime * Mathf.Clamp01(1f + (profile?.ChaosFactor ?? 0f) * 0.6f);
            }

            public void Tick()
            {
                if (_bot.IsDead || _route.Count == 0 || _bot.GetPlayer?.IsYourPlayer == true)
                    return;

                if (_bot.Memory?.GoalEnemy != null)
                {
                    _bot.Sprint(true);
                    return;
                }

                if (Time.time - _lastHitTime < DamageCooldown)
                    return;

                Vector3 target = _route[_index].Position;

                if (_isDefender)
                {
                    float dist = Vector3.Distance(_bot.Position, target);
                    float composure = _cache?.PanicHandler?.GetComposureLevel() ?? 1f;
                    float defendRadius = BaseDefendRadius * Mathf.Clamp(1f + (1f - composure), 1f, 2f);

                    if (dist > defendRadius)
                        BotMovementHelper.SmoothMoveTo(_bot, target);
                }
                else
                {
                    float distToTarget = Vector3.Distance(_bot.Position, target);
                    if (Time.time >= _nextSwitchTime || distToTarget < 2f)
                    {
                        _index = (_index + 1) % _route.Count;
                        _nextSwitchTime = Time.time + GetSwitchInterval();
                    }

                    BotMovementHelper.SmoothMoveTo(_bot, AddJitterTo(target));
                }
            }

            private Vector3 AddJitterTo(Vector3 target)
            {
                var profile = _cache?.AIRefactoredBotOwner?.PersonalityProfile;
                if (profile == null)
                    return target;

                Vector3 jitter = Vector3.zero;
                float chaos = profile.ChaosFactor;

                if (profile.IsFrenzied)
                    jitter = UnityEngine.Random.insideUnitSphere * 2.5f;
                else if (profile.IsSilentHunter)
                    jitter = UnityEngine.Random.insideUnitSphere * 0.25f;
                else if (chaos > 0.4f)
                    jitter = new Vector3(Mathf.Sin(Time.time), 0f, Mathf.Cos(Time.time)) * chaos;

                jitter.y = 0f;
                return target + jitter;
            }
        }

        #endregion

        #region Fields

        private readonly Dictionary<BotOwner, HotspotSession> _sessions = new Dictionary<BotOwner, HotspotSession>(64);
        private static readonly List<BotOwner> _botList = new List<BotOwner>(64);
        private static readonly ManualLogSource _log = AIRefactoredController.Logger;

        #endregion

        #region Lifecycle

        public void Initialize()
        {
            _sessions.Clear();
            HotspotRegistry.Initialize(GameWorldHandler.GetCurrentMapName());
        }

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

        #endregion

        #region Route Assignment

        private HotspotSession? AssignHotspotRoute(BotOwner bot)
        {
            var profile = BotRegistry.Get(bot.ProfileId);
            string map = GameWorldHandler.GetCurrentMapName();
            var all = HotspotRegistry.GetAll();

            if (all.Count == 0)
            {
                _log.LogWarning($"[HotspotSystem] ❌ No hotspots found for {map}");
                return null;
            }

            List<HotspotRegistry.Hotspot> nearby = HotspotRegistry.QueryNearby(bot.Position, 150f);
            if (nearby.Count == 0)
                nearby = new List<HotspotRegistry.Hotspot>(all);

            bool defendOnly = profile?.Personality switch
            {
                PersonalityType.Camper => true,
                PersonalityType.Cautious => UnityEngine.Random.value < 0.5f,
                _ => false
            };

            if (defendOnly)
            {
                var defend = nearby[UnityEngine.Random.Range(0, nearby.Count)];
                return new HotspotSession(bot, new List<HotspotRegistry.Hotspot> { defend }, true);
            }

            int routeLen = UnityEngine.Random.Range(2, 4);
            List<HotspotRegistry.Hotspot> route = new List<HotspotRegistry.Hotspot>(routeLen);
            HashSet<int> used = new HashSet<int>();

            while (route.Count < routeLen && used.Count < nearby.Count)
            {
                int i = UnityEngine.Random.Range(0, nearby.Count);
                if (used.Add(i))
                    route.Add(nearby[i]);
            }

            return new HotspotSession(bot, route, false);
        }

        #endregion

        #region Utility

        public static Vector3 GetRandomHotspotPosition(BotOwner bot)
        {
            return HotspotRegistry.GetRandomHotspot().Position;
        }

        #endregion
    }
}
