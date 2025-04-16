using System;
using AIRefactored.AI;
using AIRefactored.Data;
using Comfort.Common;
using EFT;
using EFT.Bots;
using EFT.Game.Spawning;
using EFT.Interactive;
using UnityEngine;

namespace AIRefactored.Core
{
    public static class BotSpawnerWrapper
    {
        private static int _botIdCounter = 0;
        private static WildSpawnType? _pmcTeam = null;

        public static async void TrySpawn(BotType type, PersonalityType personality)
        {
            try
            {
                var gameWorld = GameWorldHandler.Get();
                if (gameWorld?.MainPlayer == null)
                {
                    Debug.LogWarning("[AI-Refactored] GameWorld or MainPlayer is null.");
                    return;
                }

                var markers = Singleton<Location>.Instance?.SpawnPointMarkers;
                if (markers == null || markers.Length == 0)
                {
                    Debug.LogWarning("[AI-Refactored] No spawn points found.");
                    return;
                }

                Vector3 playerPos = gameWorld.MainPlayer.Position;
                ISpawnPoint? closest = null;
                float minDist = float.MaxValue;

                foreach (var marker in markers)
                {
                    var point = marker?.SpawnPoint;
                    if (point == null)
                        continue;

                    float dist = Vector3.Distance(playerPos, point.Position);
                    if (dist > 25f && dist < minDist)
                    {
                        closest = point;
                        minDist = dist;
                    }
                }

                if (closest == null)
                {
                    Debug.LogWarning("[AI-Refactored] No valid spawn point selected.");
                    return;
                }

                var botSpawner = Singleton<BotSpawner>.Instance;
                var botCreator = Singleton<IBotCreator>.Instance;

                if (botSpawner == null || botCreator == null)
                {
                    Debug.LogError("[AI-Refactored] Missing BotSpawner or IBotCreator.");
                    return;
                }

                var spawnType = MapToWildSpawn(type);
                var profileId = $"ai_wrapper_{_botIdCounter++}_{type.ToString().ToLower()}";

                // Optional: pick a consistent PMC side
                if (type == BotType.Pmc && _pmcTeam == null)
                    _pmcTeam = UnityEngine.Random.value < 0.5f ? WildSpawnType.pmcUSEC : WildSpawnType.pmcBEAR;
                if (type == BotType.Pmc) spawnType = _pmcTeam.Value;

                var personalityProfile = BotPersonalityPresets.GenerateProfile(personality);
                BotRegistry.Register(profileId, personalityProfile);

                var spawnParams = new BotSpawnParams
                {
                    Id_spawn = closest.Id,
                    TriggerType = SpawnTriggerType.none,
                    ShallBeGroup = null
                };

                var config = new GClass663(
                    MapToPlayerSide(spawnType),
                    spawnType,
                    BotDifficulty.normal,
                    0f,
                    spawnParams
                );

                var data = await BotCreationDataClass.Create(config, botCreator, 1, botSpawner);
                botSpawner.TrySpawnFreeAndDelay(data, newWave: true);

                Debug.Log($"[AI-Refactored] Spawned {type} ({personality}) at {closest.Position}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AI-Refactored] Bot spawn failed: {ex}");
            }
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

        private static EPlayerSide MapToPlayerSide(WildSpawnType type)
        {
            return type switch
            {
                WildSpawnType.pmcUSEC => EPlayerSide.Usec,
                WildSpawnType.pmcBEAR => EPlayerSide.Bear,
                _ => EPlayerSide.Savage
            };
        }
    }
}
