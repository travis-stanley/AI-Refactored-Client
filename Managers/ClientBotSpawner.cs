#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AIRefactored.AI;
using AIRefactored.AI.Optimization;
using AIRefactored.Core;
using AIRefactored.Data;
using Comfort.Common;
using EFT;
using EFT.Bots;
using EFT.Game.Spawning;
using EFT.Interactive;
using UnityEngine;
using WildSpawnType = EFT.WildSpawnType;

namespace AIRefactored.Managers
{
    public static class ClientBotSpawner
    {
        private static int _botIdCounter = 0;
        private static readonly Dictionary<BotType, string> _botTeams = new();
        private static WildSpawnType? _pmcTeam = null;

        public static async void SpawnBot(WavePreset preset)
        {
            try
            {
                var gameWorld = GameWorldHandler.Get();
                if (gameWorld?.MainPlayer == null)
                    return;

                ISpawnPoint? spawnPoint = FindValidSpawnPoint(gameWorld.MainPlayer.Position);
                if (spawnPoint == null)
                {
                    Debug.LogWarning("[AI-Refactored] No valid spawn point found.");
                    return;
                }

                if (preset.BotType == BotType.Pmc && _pmcTeam == null)
                    _pmcTeam = UnityEngine.Random.value < 0.5f ? WildSpawnType.pmcUSEC : WildSpawnType.pmcBEAR;

                WildSpawnType spawnType = MapToWildSpawn(preset.BotType);
                string teamId = GetOrGenerateTeamId(preset.BotType);

                var botSpawner = Singleton<BotSpawner>.Instantiated ? Singleton<BotSpawner>.Instance : null;
                if (botSpawner == null)
                {
                    Debug.LogError("[AI-Refactored] BotSpawner instance is missing.");
                    return;
                }

                var botCreator = Singleton<IBotCreator>.Instance;
                if (botCreator == null)
                {
                    Debug.LogError("[AI-Refactored] IBotCreator instance is missing.");
                    return;
                }

                string profileId = $"ai_{_botIdCounter++}_{preset.BotType.ToString().ToLower()}";

                var profile = BotPersonalityPresets.GenerateProfile(preset.Personality);
                BotRegistry.Register(profileId, profile);

                var spawnParams = new BotSpawnParams
                {
                    Id_spawn = spawnPoint.Id,
                    TriggerType = SpawnTriggerType.none,
                    ShallBeGroup = null
                };

                var botConfig = new GClass663(
                    MapToPlayerSide(spawnType),
                    spawnType,
                    BotDifficulty.normal,
                    0f,
                    spawnParams
                );

                // Hook post-spawn logic
                Singleton<BotSpawner>.Instance.OnBotCreated += OnBotCreated;

                var data = await BotCreationDataClass.Create(botConfig, botCreator, 1, botSpawner);

                if (data == null)
                {
                    Debug.LogError("[AI-Refactored] Failed to create bot spawn data.");
                    Singleton<BotSpawner>.Instance.OnBotCreated -= OnBotCreated;
                    return;
                }

                botSpawner.TrySpawnFreeAndDelay(data, newWave: true);

                Debug.Log($"[AI-Refactored] ✅ Spawned {spawnType} ({preset.Personality}) team={teamId} at {spawnPoint.Position}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AI-Refactored] ❌ Bot spawn failed: {ex}");
            }
        }

        private static void OnBotCreated(BotOwner owner)
        {
            try
            {
                if (owner?.GetPlayer == null)
                    return;

                var go = owner.GetPlayer.gameObject;
                var processor = go.AddComponent<BotAsyncProcessor>();
                processor.Initialize(owner);

#if UNITY_EDITOR
                Debug.Log($"[AI-Refactored] 🎯 Async personality setup triggered for {owner.Profile?.Info?.Nickname ?? "?"}");
#endif
            }
            finally
            {
                Singleton<BotSpawner>.Instance.OnBotCreated -= OnBotCreated;
            }
        }

        private static ISpawnPoint? FindValidSpawnPoint(Vector3 origin)
        {
            var markers = Singleton<Location>.Instance?.SpawnPointMarkers;
            if (markers == null || markers.Length == 0)
                return null;

            ISpawnPoint? closest = null;
            float minDist = float.MaxValue;

            for (int i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker?.SpawnPoint == null)
                    continue;

                float dist = Vector3.Distance(origin, marker.SpawnPoint.Position);
                if (dist > 20f && dist < minDist)
                {
                    minDist = dist;
                    closest = marker.SpawnPoint;
                }
            }

            return closest;
        }

        private static string GetOrGenerateTeamId(BotType type)
        {
            if (_botTeams.TryGetValue(type, out string id))
                return id;

            string newId = $"team_{type.ToString().ToLower()}_{UnityEngine.Random.Range(1000, 9999)}";
            _botTeams[type] = newId;
            return newId;
        }

        private static WildSpawnType MapToWildSpawn(BotType type)
        {
            return type switch
            {
                BotType.Pmc => _pmcTeam ?? WildSpawnType.pmcBEAR,
                BotType.Scav => WildSpawnType.assault,
                BotType.Raider => WildSpawnType.marksman,
                BotType.Follower => WildSpawnType.followerBully,
                BotType.Boss => WildSpawnType.bossKilla,
                _ => WildSpawnType.assault
            };
        }

        private static EPlayerSide MapToPlayerSide(WildSpawnType spawnType)
        {
            return spawnType switch
            {
                WildSpawnType.pmcBEAR => EPlayerSide.Bear,
                WildSpawnType.pmcUSEC => EPlayerSide.Usec,
                _ => EPlayerSide.Savage
            };
        }
    }
}
