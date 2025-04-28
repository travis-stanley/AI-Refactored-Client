#nullable enable

namespace AIRefactored.AI.Hotspots
{
    using System.Collections.Generic;

    using AIRefactored.AI.Core;
    using AIRefactored.AI.Helpers;
    using AIRefactored.Core;
    using AIRefactored.Runtime;

    using BepInEx.Logging;

    using Comfort.Common;

    using EFT;

    using UnityEngine;

    /// <summary>
    ///     Drives bot navigation between tactical hotspots on a map.
    ///     Each bot maintains its own route or defense zone and adapts behavior based on combat state.
    /// </summary>
    public class HotspotSystem
    {
        private static readonly List<BotOwner> _botList = new(64);

        private static readonly ManualLogSource _log = AIRefactoredController.Logger;

        private readonly Dictionary<BotOwner, HotspotSession> _sessions = new(64);

        public static Vector3 GetRandomHotspotPosition(BotOwner bot)
        {
            return HotspotRegistry.GetRandomHotspot().Position;
        }

        public void Initialize()
        {
            this._sessions.Clear();
            HotspotRegistry.Initialize(GameWorldHandler.GetCurrentMapName());
        }

        public void Tick()
        {
            var controller = Singleton<BotsController>.Instance;
            if (controller?.Bots?.BotOwners == null)
                return;

            _botList.Clear();
            _botList.AddRange(controller.Bots.BotOwners);

            for (var i = 0; i < _botList.Count; i++)
            {
                var bot = _botList[i];
                if (bot == null || bot.IsDead || bot.GetPlayer?.IsYourPlayer == true)
                    continue;

                if (!this._sessions.TryGetValue(bot, out var session))
                {
                    session = this.AssignHotspotRoute(bot);
                    if (session != null) this._sessions[bot] = session;
                }

                session?.Tick();
            }
        }

        private HotspotSession? AssignHotspotRoute(BotOwner bot)
        {
            var profile = BotRegistry.Get(bot.ProfileId);
            var map = GameWorldHandler.GetCurrentMapName();
            var all = HotspotRegistry.GetAll();

            if (all.Count == 0)
            {
                _log.LogWarning($"[HotspotSystem] ❌ No hotspots found for {map}");
                return null;
            }

            var nearby = HotspotRegistry.QueryNearby(bot.Position, 150f);
            if (nearby.Count == 0)
                nearby = new List<HotspotRegistry.Hotspot>(all);

            var defendOnly = profile?.Personality switch
                {
                    PersonalityType.Camper => true,
                    PersonalityType.Cautious => Random.value < 0.5f,
                    _ => false
                };

            if (defendOnly)
            {
                var defend = nearby[Random.Range(0, nearby.Count)];
                return new HotspotSession(bot, new List<HotspotRegistry.Hotspot> { defend }, true);
            }

            var routeLen = Random.Range(2, 4);
            List<HotspotRegistry.Hotspot> route = new(routeLen);
            HashSet<int> used = new();

            while (route.Count < routeLen && used.Count < nearby.Count)
            {
                var i = Random.Range(0, nearby.Count);
                if (used.Add(i))
                    route.Add(nearby[i]);
            }

            return new HotspotSession(bot, route, false);
        }

        private sealed class HotspotSession
        {
            private const float BaseDefendRadius = 7f;

            private const float DamageCooldown = 6f;

            private readonly BotOwner _bot;

            private readonly BotComponentCache? _cache;

            private readonly bool _isDefender;

            private readonly string _mapKey;

            private readonly List<HotspotRegistry.Hotspot> _route;

            private int _index;

            private float _lastHitTime = -999f;

            private float _lastRouteStartTime;

            private float _nextSwitchTime;

            public HotspotSession(BotOwner bot, List<HotspotRegistry.Hotspot> route, bool isDefender)
            {
                this._bot = bot;
                this._route = route;
                this._isDefender = isDefender;
                this._mapKey = GameWorldHandler.GetCurrentMapName();
                this._cache = BotCacheUtility.GetCache(bot);
                this._index = 0;
                this._lastRouteStartTime = Time.time;

                if (this._bot.GetPlayer?.HealthController is HealthControllerClass health)
                    health.ApplyDamageEvent += this.OnDamaged;

                this._nextSwitchTime = Time.time + this.GetSwitchInterval();
            }

            public void Tick()
            {
                if (this._bot.IsDead || this._route.Count == 0 || this._bot.GetPlayer?.IsYourPlayer == true)
                    return;

                if (this._bot.Memory?.GoalEnemy != null)
                {
                    this._bot.Sprint(true);
                    return;
                }

                if (Time.time - this._lastHitTime < DamageCooldown)
                    return;

                var target = this._route[this._index].Position;

                if (this._isDefender)
                {
                    var dist = Vector3.Distance(this._bot.Position, target);
                    var composure = this._cache?.PanicHandler?.GetComposureLevel() ?? 1f;
                    var defendRadius = BaseDefendRadius * Mathf.Clamp(1f + (1f - composure), 1f, 2f);

                    if (dist > defendRadius)
                        BotMovementHelper.SmoothMoveTo(this._bot, target);
                }
                else
                {
                    var distToTarget = Vector3.Distance(this._bot.Position, target);
                    if (Time.time >= this._nextSwitchTime || distToTarget < 2f)
                    {
                        this._index = (this._index + 1) % this._route.Count;
                        this._nextSwitchTime = Time.time + this.GetSwitchInterval();
                    }

                    BotMovementHelper.SmoothMoveTo(this._bot, this.AddJitterTo(target));
                }
            }

            private Vector3 AddJitterTo(Vector3 target)
            {
                var profile = this._cache?.AIRefactoredBotOwner?.PersonalityProfile;
                if (profile == null)
                    return target;

                var jitter = Vector3.zero;
                var chaos = profile.ChaosFactor;

                if (profile.IsFrenzied)
                    jitter = Random.insideUnitSphere * 2.5f;
                else if (profile.IsSilentHunter)
                    jitter = Random.insideUnitSphere * 0.25f;
                else if (chaos > 0.4f)
                    jitter = new Vector3(Mathf.Sin(Time.time), 0f, Mathf.Cos(Time.time)) * chaos;

                jitter.y = 0f;
                return target + jitter;
            }

            private float GetSwitchInterval()
            {
                var profile = BotRegistry.Get(this._bot.ProfileId);
                var baseTime = profile?.Personality switch
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

            private void OnDamaged(EBodyPart part, float damage, DamageInfoStruct info)
            {
                this._lastHitTime = Time.time;
                this._cache?.PanicHandler?.TriggerPanic();
            }
        }
    }
}