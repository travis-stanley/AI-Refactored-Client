#nullable enable

using MissionType = AIRefactored.AI.Missions.BotMissionController.MissionType;

namespace AIRefactored.AI.Missions.Subsystems
{
    using System;

    using AIRefactored.AI.Core;
    using AIRefactored.AI.Groups;
    using AIRefactored.AI.Helpers;
    using AIRefactored.AI.Looting;
    using AIRefactored.Runtime;

    using BepInEx.Logging;

    using EFT;

    /// <summary>
    ///     Dynamically switches bot mission type based on context:
    ///     panic, aggression, squad cohesion, loot opportunity, etc.
    /// </summary>
    public sealed class MissionSwitcher
    {
        private const float SwitchCooldown = 10f;

        private readonly BotOwner _bot;

        private readonly BotComponentCache _cache;

        private readonly BotGroupSyncCoordinator? _group;

        private readonly ManualLogSource _log;

        private readonly BotLootScanner? _lootScanner;

        private readonly BotPersonalityProfile _profile;

        private float _lastSwitchTime;

        public MissionSwitcher(BotOwner bot, BotComponentCache cache)
        {
            this._bot = bot;
            this._cache = cache;
            this._profile = BotRegistry.Get(bot.Profile.Id);
            this._lootScanner = cache.LootScanner;
            this._group = BotCacheUtility.GetGroupSync(cache);
            this._log = AIRefactoredController.Logger;
        }

        /// <summary>
        ///     Evaluates and switches mission based on current combat and squad context.
        /// </summary>
        public void Evaluate(
            ref MissionType currentMission,
            float time,
            Action switchToFight,
            Action resumeQuesting,
            Func<bool> isGroupAligned)
        {
            if (time - this._lastSwitchTime < SwitchCooldown)
                return;

            // Switch to Fight if under fire and personality is aggressive
            if (this._bot.Memory?.IsUnderFire == true && this._profile.AggressionLevel > 0.6f
                                                      && currentMission != MissionType.Fight)
            {
                this._log.LogInfo(
                    $"[MissionSwitcher] {this._bot.Profile.Info.Nickname} escalating to Fight (under fire + aggressive)");
                this._lastSwitchTime = time;
                currentMission = MissionType.Fight;
                switchToFight.Invoke();
                return;
            }

            // Opportunistically switch to Loot if personality prefers it and a high-value loot point exists
            if (currentMission == MissionType.Quest && this._profile.PreferredMission == MissionBias.Loot
                                                    && this._lootScanner?.GetHighestValueLootPoint() != null)
            {
                this._log.LogInfo(
                    $"[MissionSwitcher] {this._bot.Profile.Info.Nickname} switching to Loot (loot point nearby)");
                this._lastSwitchTime = time;
                currentMission = MissionType.Loot;
                return;
            }

            // Squad is scattered — fallback to Quest if currently fighting
            if (currentMission == MissionType.Fight && !isGroupAligned())
            {
                this._log.LogInfo(
                    $"[MissionSwitcher] {this._bot.Profile.Info.Nickname} falling back to Quest (squad separation)");
                this._lastSwitchTime = time;
                currentMission = MissionType.Quest;
                resumeQuesting.Invoke();
            }
        }
    }
}