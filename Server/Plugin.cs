#nullable enable

using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using EFT.Game.Spawning;
using UnityEngine;
using AIRefactored.Runtime;

namespace AIRefactored.Server
{
    [BepInPlugin("com.spock.airefactored.server", "AI-Refactored Server", "1.0.0")]
    public class SpawnHijacker : BaseUnityPlugin
    {
        internal static ManualLogSource Log = null!;
        private static Harmony _harmony = null!;

        private void Awake()
        {
            Log = Logger;
            _harmony = new Harmony("com.spock.airefactored.server");

            try
            {
                AIRefactoredController.Initialize(Logger);
                PatchSpawnHooks();

                Log.LogInfo("[AIRefactored:Server] ✅ Server shunt initialized.");
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
                Log.LogInfo("[AIRefactored:Server] ✅ Patched spawn systems.");
            }
            catch (Exception ex)
            {
                Log.LogError($"[AIRefactored:Server] ❌ Failed to patch spawn systems:\n{ex}");
            }
        }

        [HarmonyPatch(typeof(SpawnSystemClass), nameof(SpawnSystemClass.SelectAISpawnPoints))]
        [HarmonyPrefix]
        private static bool PreventAISpawnPoints()
        {
            Log.LogDebug("[AIRefactored:Server] Intercepted AI spawn point selection.");
            return false; // Block default logic
        }

        [HarmonyPatch(typeof(SpawnSystemClass), nameof(SpawnSystemClass.SelectSpawnPoint))]
        [HarmonyPrefix]
        private static bool PreventPlayerSpawnPoints()
        {
            Log.LogDebug("[AIRefactored:Server] Intercepted player spawn point selection.");
            return false; // Block default logic
        }

        [HarmonyPatch(typeof(SpawnSystemClass), nameof(SpawnSystemClass.SelectPlayerSavageSpawn))]
        [HarmonyPrefix]
        private static bool PreventSavageSpawn()
        {
            Log.LogDebug("[AIRefactored:Server] Intercepted scav/savage spawn point selection.");
            return false; // Block default logic
        }

        [HarmonyPatch(typeof(SpawnSystemClass), nameof(SpawnSystemClass.ValidateSpawnPosition))]
        [HarmonyPrefix]
        private static bool PreventPositionValidation(ref bool __result)
        {
            __result = true; // Skip validation — host handles this
            return false;
        }
    }
}
