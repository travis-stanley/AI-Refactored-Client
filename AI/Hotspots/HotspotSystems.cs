#nullable enable

using System;
using System.Collections.Generic;
using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.Core;
using Comfort.Common;
using EFT;
using EFT.Bots;
using UnityEngine;

namespace AIRefactored.AI.Hotspots
{
    public class HotspotSystem : MonoBehaviour
    {
        #region Session

        private sealed class HotspotSession
        {
            private readonly BotOwner _bot;
            private readonly List<Vector3> _route;
            private readonly bool _isDefender;

            private int _index;
            private float _nextSwitchTime;
            private float _lastHitTime = -999f;

            private const float DefendRadius = 7f;

            public HotspotSession(BotOwner bot, List<Vector3> route, bool isDefender)
            {
                _bot = bot;
                _route = new List<Vector3>(route);
                _isDefender = isDefender;
                _index = 0;
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
                var profile = id != null ? BotRegistry.Get(id) : null;

                return profile?.Personality switch
                {
                    PersonalityType.Cautious => 160f,
                    PersonalityType.TeamPlayer => 100f,
                    PersonalityType.Strategic => 90f,
                    PersonalityType.Explorer => 75f,
                    PersonalityType.Dumb => 45f,
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

                Vector3 target = _route[_index];
                float dist = Vector3.Distance(_bot.Position, target);

                if (_isDefender)
                {
                    if (dist > DefendRadius)
                        BotMovementHelper.SmoothMoveTo(_bot, target);
                    return;
                }

                if (Time.time >= _nextSwitchTime || dist < 2f)
                {
                    MoveNext();
                    _nextSwitchTime = Time.time + GetSwitchInterval();
                }

                BotMovementHelper.SmoothMoveTo(_bot, _route[_index]);
            }

            private void MoveNext()
            {
                _index = (_index + 1) % _route.Count;
            }
        }

        #endregion

        #region State

        private readonly Dictionary<BotOwner, HotspotSession> _sessions = new();
        private List<BotOwner>? _botList;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            HotspotLoader.LoadAll();
        }

        private void Update()
        {
            var controller = Singleton<BotsController>.Instance;
            if (controller == null || controller.Bots == null || controller.Bots.BotOwners == null)
                return;

            _botList = new List<BotOwner>(controller.Bots.BotOwners);
            for (int i = 0; i < _botList.Count; i++)
            {
                BotOwner bot = _botList[i];
                if (bot == null || bot.IsDead || bot.AIData == null || bot.GetPlayer?.IsYourPlayer == true)
                    continue;

                if (!_sessions.TryGetValue(bot, out var session))
                {
                    session = AssignHotspotRoute(bot);
                    if (session != null)
                        _sessions[bot] = session;
                }

                session?.Update();
            }
        }

        #endregion

        #region Assignment Logic

        private HotspotSession? AssignHotspotRoute(BotOwner bot)
        {
            var role = bot.Profile?.Info?.Settings?.Role ?? WildSpawnType.assault;
            var set = HotspotLoader.GetHotspotsForCurrentMap(role);
            if (set == null || set.Points.Count == 0)
                return null;

            var id = bot.Profile?.Id;
            var profile = id != null ? BotRegistry.Get(id) : null;

            bool defendOnly = profile?.Personality switch
            {
                PersonalityType.Camper => true,
                PersonalityType.Cautious => UnityEngine.Random.value < 0.5f,
                _ => false
            };

            if (defendOnly)
            {
                var defendPoint = set.Points[UnityEngine.Random.Range(0, set.Points.Count)];
                return new HotspotSession(bot, new List<Vector3> { defendPoint }, true);
            }

            int pointCount = UnityEngine.Random.Range(2, 4);
            var route = new List<Vector3>();
            var used = new HashSet<int>();

            while (route.Count < pointCount && used.Count < set.Points.Count)
            {
                int i = UnityEngine.Random.Range(0, set.Points.Count);
                if (used.Add(i))
                    route.Add(set.Points[i]);
            }

            return new HotspotSession(bot, route, false);
        }

        #endregion

        #region External API

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
