#nullable enable

using AIRefactored.Core;
using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Missions.Subsystems
{
    /// <summary>
    /// Handles voice lines for looting, extraction, and coordination.
    /// Supports multiplayer-safe VO routing.
    /// </summary>
    public sealed class MissionVoiceCoordinator
    {
        private readonly BotOwner _bot;
        private static readonly ManualLogSource _log = AIRefactoredController.Logger;

        public MissionVoiceCoordinator(BotOwner bot)
        {
            _bot = bot;
        }

        public void OnLoot()
        {
            TrySay(EPhraseTrigger.OnLoot);
        }

        public void OnExitLocated()
        {
            TrySay(EPhraseTrigger.ExitLocated);
        }

        public void OnMissionSwitch()
        {
            TrySay(EPhraseTrigger.Cooperation);
        }

        private void TrySay(EPhraseTrigger phrase)
        {
            try
            {
                if (!FikaHeadlessDetector.IsHeadless)
                    _bot.GetPlayer?.Say(phrase);
            }
            catch (System.Exception ex)
            {
                _log.LogWarning($"[MissionVoiceCoordinator] VO failed: {ex.Message}");
            }
        }
    }
}
