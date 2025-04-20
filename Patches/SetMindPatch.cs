#nullable enable

using System;
using System.Reflection;
using HarmonyLib;
using EFT;
using UnityEngine;

namespace AIRefactored.Patches
{
    /// <summary>
    /// Prevents overwriting AIRefactored brains by intercepting BotOwner.SetMind.
    /// </summary>
    [HarmonyPatch(typeof(BotOwner))]
    public static class SetMindPatch
    {
        private static readonly MethodInfo? TargetMethod = typeof(BotOwner).GetMethod("SetMind", BindingFlags.Instance | BindingFlags.Public);

        [HarmonyPrefix]
        [HarmonyPatch("SetMind")]
        public static bool Prefix(BotOwner __instance)
        {
            try
            {
                var player = __instance.GetPlayer;
                if (player == null)
                    return true;

                if (player.GetComponent<AIRefactored.AI.Threads.BotBrain>() != null)
                {
                    AIRefactored.Plugin.LoggerInstance.LogWarning($"[AIRefactored] ⛔ Blocked brain overwrite on '{__instance.Profile?.Info?.Nickname ?? "Unknown"}'");
                    return false; // cancel the call
                }
            }
            catch (Exception ex)
            {
                AIRefactored.Plugin.LoggerInstance.LogError($"[AIRefactored] SetMindPatch error: {ex.Message}");
            }

            return true; // fallback to original
        }
    }
}
