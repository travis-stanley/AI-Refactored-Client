#nullable enable

namespace AIRefactored.AI.Helpers
{
    using System.Collections.Generic;

    using AIRefactored.AI.Combat;
    using AIRefactored.AI.Core;
    using AIRefactored.AI.Groups;
    using AIRefactored.AI.Missions;

    using EFT;

    using UnityEngine;

    public static class BotCacheUtility
    {
        private static readonly Dictionary<BotOwner, BotComponentCache> CacheRegistry = new(64);

        private static readonly Dictionary<string, BotComponentCache> ProfileIdLookup = new(64);

        public static IEnumerable<BotComponentCache> AllActiveBots()
        {
            foreach (var kv in CacheRegistry)
                if (kv.Key != null && !kv.Key.IsDead)
                    yield return kv.Value;
        }

        public static void DumpCache()
        {
            Debug.Log($"[BotCacheUtility] Dumping {CacheRegistry.Count} bot caches:");

            foreach (var kv in CacheRegistry)
            {
                var bot = kv.Key;
                var cache = kv.Value;
                Debug.Log($" → {GetBotName(cache)}, Pos={bot.Position}, Alive={!bot.IsDead}");
            }
        }

        public static string GetBotName(BotComponentCache? cache)
        {
            if (cache?.Bot?.Profile?.Info == null)
                return "Unknown";

            return $"{cache.Bot.Profile.Info.Nickname} ({cache.Bot.Profile.Side})";
        }

        public static BotComponentCache? GetCache(BotOwner bot)
        {
            return CacheRegistry.TryGetValue(bot, out var cache) ? cache : null;
        }

        public static BotComponentCache? GetCache(Player player)
        {
            return player?.AIData?.BotOwner is BotOwner bot ? GetCache(bot) : null;
        }

        public static BotComponentCache? GetCache(string profileId)
        {
            return string.IsNullOrEmpty(profileId) ? null :
                   ProfileIdLookup.TryGetValue(profileId, out var cache) ? cache : null;
        }

        public static BotComponentCache? GetClosestBot(Vector3 origin, float maxDistance = 40f)
        {
            BotComponentCache? closest = null;
            var minDistSq = maxDistance * maxDistance;

            foreach (var pair in CacheRegistry)
            {
                var bot = pair.Key;
                if (bot == null || bot.IsDead) continue;

                var distSq = (bot.Position - origin).sqrMagnitude;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    closest = pair.Value;
                }
            }

            return closest;
        }

        public static Transform? GetFootTransform(BotComponentCache cache)
        {
            return cache?.Bot?.MainParts?.TryGetValue(BodyPartType.leftLeg, out var part) == true
                       ? part?._transform?.Original
                       : null;
        }

        public static BotGroupSyncCoordinator? GetGroupSync(BotComponentCache cache)
        {
            return cache.GroupSync ?? cache.GroupBehavior?.GroupSync;
        }

        public static Transform? GetLookTransform(BotComponentCache cache)
        {
            return cache?.Bot?.Fireport?.Original;
        }

        public static BotMissionController? GetMissionController(BotComponentCache cache)
        {
            return cache.AIRefactoredBotOwner?.MissionController;
        }

        public static BotPersonalityProfile? GetPersonality(BotComponentCache cache)
        {
            return cache?.Bot?.ProfileId != null ? BotRegistry.TryGet(cache.Bot.ProfileId) : null;
        }

        public static string GetStance(BotComponentCache cache)
        {
            var pose = cache?.PoseController;
            if (pose == null) return "Unknown";

            var level = pose.GetPoseLevel();
            if (level < 25f) return "Prone";
            if (level < 75f) return "Crouching";
            return "Standing";
        }

        public static Transform? GetWeaponTransform(BotComponentCache cache)
        {
            return cache?.Bot?.Fireport?.Original;
        }

        public static Transform? Head(BotComponentCache cache)
        {
            return cache?.Bot?.MainParts?.TryGetValue(BodyPartType.head, out var part) == true
                       ? part?._transform?.Original
                       : null;
        }

        public static bool IsFollower(BotComponentCache cache)
        {
            return !IsLeader(cache);
        }

        public static bool IsLeader(BotComponentCache cache)
        {
            var group = cache.Bot?.BotsGroup;
            return group != null && group.MembersCount > 0 && group.Member(0)?.ProfileId == cache.Bot?.ProfileId;
        }

        public static void Register(BotOwner bot, BotComponentCache cache)
        {
            if (bot == null || cache == null)
                return;

            CacheRegistry[bot] = cache;

            if (!string.IsNullOrEmpty(bot.ProfileId))
                ProfileIdLookup[bot.ProfileId] = cache;

            BotTeamTracker.RegisterFromBot(bot);
            GroupMissionCoordinator.RegisterFromBot(bot);
        }

        public static bool TryGetPanicComponent(BotComponentCache cache, out BotPanicHandler? panic)
        {
            panic = cache?.PanicHandler;
            return panic != null;
        }

        public static void Unregister(BotOwner bot)
        {
            if (bot == null) return;

            CacheRegistry.Remove(bot);
            if (!string.IsNullOrEmpty(bot.ProfileId))
                ProfileIdLookup.Remove(bot.ProfileId);

            BotTeamTracker.Unregister(bot);
        }
    }
}