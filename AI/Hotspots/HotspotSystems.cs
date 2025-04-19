#nullable enable

using AIRefactored.AI.Helpers;
using Comfort.Common;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Hotspots
{
    public class HotspotSystem : MonoBehaviour
    {
        private sealed class HotspotSession
        {
            private readonly BotOwner _bot;
            private readonly List<Vector3> _route;
            private readonly bool _isDefender;
            private int _index;
            private float _nextSwitchTime;
            private float _lastHitTime = -999f;

            private const float DefendRadius = 7f;
            private const float DamageCooldown = 6f;

            public HotspotSession(BotOwner bot, List<Vector3> route, bool isDefender)
            {
                _bot = bot;
                _route = route;
                _isDefender = isDefender;
                _index = 0;
                _nextSwitchTime = Time.time + GetSwitchInterval();

                if (_bot.GetPlayer?.HealthController is HealthControllerClass health)
                    health.ApplyDamageEvent += OnDamaged;
            }

            private void OnDamaged(EBodyPart part, float damage, DamageInfoStruct info)
            {
                _lastHitTime = Time.time;
            }

            private float GetSwitchInterval()
            {
                var profile = BotRegistry.Get(_bot.ProfileId);
                if (profile == null)
                    return 120f;

                return profile.Personality switch
                {
                    PersonalityType.Cautious => 160f,
                    PersonalityType.TeamPlayer => 100f,
                    PersonalityType.Strategic => 90f,
                    PersonalityType.Explorer => 75f,
                    PersonalityType.Dumb => 45f,
                    _ => 120f,
                };
            }

            public void Update()
            {
                if (_bot == null || _bot.IsDead || _route.Count == 0 || _bot.GetPlayer?.IsYourPlayer == true)
                    return;

                if (_bot.Memory?.GoalEnemy != null)
                {
                    _bot.Sprint(true);
                    return;
                }

                // Pause movement briefly if bot recently took damage
                if (Time.time - _lastHitTime < DamageCooldown)
                    return;

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

                // Optional: Talk trigger
                // if (Random.value < 0.15f) _bot.BotTalk?.TrySay(EPhraseTrigger.GoForward);
            }

            private void MoveNext()
            {
                _index = (_index + 1) % _route.Count;
            }
        }

        private readonly Dictionary<BotOwner, HotspotSession> _sessions = new();
        private static readonly List<BotOwner> _botList = new();

        private void Start()
        {
            HotspotLoader.LoadAll();
        }

        private void Update()
        {
            var controller = Singleton<BotsController>.Instance;
            if (controller?.Bots?.BotOwners == null)
                return;

            _botList.Clear();
            _botList.AddRange(controller.Bots.BotOwners);

            for (int i = 0; i < _botList.Count; i++)
            {
                var bot = _botList[i];
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

        private HotspotSession? AssignHotspotRoute(BotOwner bot)
        {
            var role = bot.Profile?.Info?.Settings?.Role ?? WildSpawnType.assault;
            var set = HotspotLoader.GetHotspotsForCurrentMap(role);

            if (set == null || set.Points.Count == 0)
                return null;

            var profile = BotRegistry.Get(bot.ProfileId);

            bool defendOnly = profile?.Personality switch
            {
                PersonalityType.Camper => true,
                PersonalityType.Cautious => UnityEngine.Random.value < 0.5f,
                _ => false
            };

            if (defendOnly)
            {
                Vector3 defendPoint = set.Points[Random.Range(0, set.Points.Count)];
                return new HotspotSession(bot, new List<Vector3> { defendPoint }, true);
            }

            int pointCount = Random.Range(2, 4);
            var route = new List<Vector3>(pointCount);
            var usedIndices = new HashSet<int>();

            while (route.Count < pointCount && usedIndices.Count < set.Points.Count)
            {
                int index = Random.Range(0, set.Points.Count);
                if (usedIndices.Add(index))
                    route.Add(set.Points[index]);
            }

            return new HotspotSession(bot, route, false);
        }

        public static Vector3 GetRandomHotspot(BotOwner bot)
        {
            var role = bot.Profile?.Info?.Settings?.Role ?? WildSpawnType.assault;
            string map = Singleton<GameWorld>.Instance?.LocationId?.ToLowerInvariant() ?? "unknown";

            var set = HotspotLoader.GetHotspotsForCurrentMap(role);
            if (set == null || set.Points.Count == 0)
            {
                Debug.LogWarning($"[AIRefactored] No hotspots found for {map} ({role})");
                return bot.Position + Random.insideUnitSphere * 5f;
            }

            return set.Points[Random.Range(0, set.Points.Count)];
        }
    }
}
