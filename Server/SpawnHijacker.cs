#nullable enable

using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using EFT.Game.Spawning;
using EFT;
using UnityEngine;
using AIRefactored.Runtime;
using AIRefactored.Spawning;
using Comfort.Common;

namespace AIRefactored.Server
{
    [BepInPlugin("com.spock.airefactored.server", "AI-Refactored Server", "1.0.0")]
    public class SpawnHijacker : BaseUnityPlugin
    {
        internal static ManualLogSource Log = null!;
        private static Harmony _harmony = null!;
        private const string HarmonyId = "com.spock.airefactored.server";

        private void Awake()
        {
            Log = Logger;
            _harmony = new Harmony(HarmonyId);

            try
            {
                AIRefactoredController.Initialize(Logger);
                PatchSpawnHooks();

                Log.LogInfo("[AIRefactored:Server] ✅ Server spawn shunt initialized.");
            }
            catch (Exception ex)
            {
                Log.LogError($"[AIRefactored:Server] ❌ Initialization failed:\n{ex}");
            }
        }

        private void PatchSpawnHooks()
        {
            try
            {
                _harmony.PatchAll(typeof(SpawnHijacker));
                Log.LogInfo("[AIRefactored:Server] ✅ Spawn system patches applied.");
            }
            catch (Exception ex)
            {
                Log.LogError($"[AIRefactored:Server] ❌ Failed to patch spawn systems:\n{ex}");
            }
        }

        [HarmonyPatch(typeof(SpawnSystemClass), nameof(SpawnSystemClass.SelectAISpawnPoints))]
        [HarmonyPrefix]
        private static bool BlockAISpawnPoints(ref Vector3 __result, BotSpawnParams spawnParams)
        {
            var group = spawnParams.ShallBeGroup;

            string groupId = group?.Group.ToString() ?? string.Empty;
            bool isPmc = spawnParams.ShallBeGroup?.IsBossSetted == true; // heuristic
            int count = group?.StartCount ?? 3;

            if (!string.IsNullOrEmpty(groupId))
            {
                SpawnZoneRedirector.ReserveGroupSpawn(groupId, count, isPmc);
            }

            var fallback = SpawnZoneRedirector.GetRedirectedSpawn(groupId, isPmc);
            __result = fallback?.Position ?? GetRadiusFallback(Vector3.zero);
            Log.LogDebug($"[AIRefactored:Server] ⛔ AI group spawn redirected → {__result} (Group: {groupId})");
            return false;
        }

        [HarmonyPatch(typeof(SpawnSystemClass), nameof(SpawnSystemClass.SelectSpawnPoint))]
        [HarmonyPrefix]
        private static bool BlockPlayerSpawns(ref Vector3 __result)
        {
            string groupId = TryGetFikaGroupId();
            int groupSize = TryGetFikaGroupSize(groupId);

            if (!string.IsNullOrEmpty(groupId))
            {
                SpawnZoneRedirector.ReserveGroupSpawn(groupId, groupSize, isPmc: true);
            }

            var fallback = SpawnZoneRedirector.GetRedirectedSpawn(groupId, isPmc: true);
            __result = fallback?.Position ?? GetRadiusFallback(Vector3.zero);
            Log.LogDebug($"[AIRefactored:Server] ⛔ Player spawn redirected → {__result} (Group: {groupId})");
            return false;
        }

        [HarmonyPatch(typeof(SpawnSystemClass), nameof(SpawnSystemClass.SelectPlayerSavageSpawn))]
        [HarmonyPrefix]
        private static bool BlockScavSpawns(ref Vector3 __result)
        {
            var fallback = SpawnZoneRedirector.GetRandomAvailableSpawn(groupId: null, isPmc: false);
            __result = fallback?.Position ?? GetRadiusFallback(Vector3.zero);
            Log.LogDebug($"[AIRefactored:Server] ⛔ Scav spawn redirected → {__result}");
            return false;
        }

        [HarmonyPatch(typeof(SpawnSystemClass), nameof(SpawnSystemClass.ValidateSpawnPosition))]
        [HarmonyPrefix]
        private static bool SkipValidation(ref bool __result)
        {
            __result = true;
            Log.LogDebug("[AIRefactored:Server] ✅ Skipping vanilla spawn validation.");
            return false;
        }

        private static Vector3 GetRadiusFallback(Vector3 center, float radius = 30f)
        {
            Vector2 circle = UnityEngine.Random.insideUnitCircle * radius;
            return center + new Vector3(circle.x, 0f, circle.y);
        }

        private static string TryGetFikaGroupId()
        {
            try
            {
                var main = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance?.MainPlayer : null;
                return main?.Profile?.Info?.GroupId ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static int TryGetFikaGroupSize(string groupId)
        {
            if (string.IsNullOrEmpty(groupId)) return 1;

            var players = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance?.AllAlivePlayersList : null;
            if (players == null) return 1;

            int count = 0;
            foreach (var p in players)
            {
                if (p?.Profile?.Info?.GroupId == groupId)
                    count++;
            }

            return Mathf.Clamp(count, 1, 10);
        }
    }
}
