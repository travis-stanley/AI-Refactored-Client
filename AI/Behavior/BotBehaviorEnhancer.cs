#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Groups;
using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;

namespace AIRefactored.AI.Behavior
{
    /// <summary>
    /// Handles enhancements to core bot behavior, specifically door interaction logic.
    /// This class delegates execution logic externally (e.g., via BotBrain or BotMissionSystem).
    /// </summary>
    public class BotBehaviorEnhancer
    {
        #region Fields

        private BotComponentCache? _cache;
        private BotOwner? _bot;
        private BotGroupSyncCoordinator? _groupSync;
        private AIRefactoredBotOwner? _owner;
        private BotPersonalityProfile? _profile;
        private BotDoorOpener? _doorOpener;

        private static ManualLogSource Logger => AIRefactoredController.Logger;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the behavior enhancer using cached components.
        /// Sets up door opener and internal references.
        /// </summary>
        /// <param name="cache">Cached bot component references.</param>
        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot;
            _owner = cache.AIRefactoredBotOwner;
            _profile = _owner?.PersonalityProfile;

            if (_bot?.GetPlayer != null)
            {
                _groupSync = _bot.GetPlayer.GetComponent<BotGroupSyncCoordinator>();
                _doorOpener = new BotDoorOpener(_bot);
            }

            Logger.LogDebug($"[BehaviorEnhancer] Initialized for bot '{GetBotName()}'");
        }

        #endregion

        #region Runtime Update

        /// <summary>
        /// Per-frame entry point for logic execution.
        /// Handles reactive door logic and panic-safe logic.
        /// </summary>
        /// <param name="time">Current world time (Time.time).</param>
        /// <returns>True if the bot can continue its normal behavior; false if door logic is blocking.</returns>
        public bool Tick(float time)
        {
            // Safety checks for inactive bots or invalid references
            if (_bot == null || _profile == null || _bot.GetPlayer?.HealthController?.IsAlive != true || !_bot.GetPlayer.IsAI)
                return true;

            // Don't interact with doors while panicking
            if (_cache?.PanicHandler?.IsPanicking == true)
                return true;

            // Block movement while waiting on door interaction to finish
            if (_doorOpener != null && !_doorOpener.Update())
            {
                Logger.LogDebug($"[BehaviorEnhancer] Bot '{GetBotName()}' is waiting for door interaction.");
                return false;
            }

            return true;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Returns the bot’s display name or "Unknown" if not available.
        /// </summary>
        private string GetBotName()
        {
            return _bot?.Profile?.Info?.Nickname ?? "Unknown";
        }

        #endregion
    }
}
