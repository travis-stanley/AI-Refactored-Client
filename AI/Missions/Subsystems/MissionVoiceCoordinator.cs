#nullable enable

namespace AIRefactored.AI.Missions.Subsystems
{
    using System;

    using AIRefactored.Core;
    using AIRefactored.Runtime;

    using BepInEx.Logging;

    using EFT;

    /// <summary>
    ///     Handles voice lines for looting, extraction, and coordination.
    ///     VO routing is multiplayer-safe and avoids triggering on headless hosts.
    /// </summary>
    public sealed class MissionVoiceCoordinator
    {
        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        private readonly BotOwner _bot;

        public MissionVoiceCoordinator(BotOwner bot)
        {
            this._bot = bot;
        }

        /// <summary>
        ///     Plays extraction found voice line.
        /// </summary>
        public void OnExitLocated()
        {
            this.TrySay(EPhraseTrigger.ExitLocated);
        }

        /// <summary>
        ///     Plays loot acknowledgment voice line.
        /// </summary>
        public void OnLoot()
        {
            this.TrySay(EPhraseTrigger.OnLoot);
        }

        /// <summary>
        ///     Plays coordination or mission switch voice line.
        /// </summary>
        public void OnMissionSwitch()
        {
            this.TrySay(EPhraseTrigger.Cooperation);
        }

        private void TrySay(EPhraseTrigger phrase)
        {
            if (FikaHeadlessDetector.IsHeadless)
                return;

            try
            {
                this._bot.GetPlayer?.Say(phrase);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(
                    $"[MissionVoiceCoordinator] VO failed for bot '{this._bot.Profile?.Info?.Nickname ?? "Unknown"}': {ex.Message}");
            }
        }
    }
}