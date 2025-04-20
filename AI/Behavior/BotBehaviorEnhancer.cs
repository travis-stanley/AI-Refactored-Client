#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Groups;
using EFT;

namespace AIRefactored.AI.Behavior
{
    /// <summary>
    /// Enhances bot behavior with reactive logic: door opening.
    /// All decision execution is delegated externally (e.g. by BotBrain or BotMissionSystem).
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

        #endregion

        #region Initialization

        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot;
            _owner = cache.AIRefactoredBotOwner;
            _profile = _owner?.PersonalityProfile;
            _groupSync = _bot?.GetPlayer?.GetComponent<BotGroupSyncCoordinator>();

            if (_bot != null)
                _doorOpener = new BotDoorOpener(_bot);
        }

        #endregion

        #region Tick Entry

        /// <summary>
        /// Called each frame to update door logic. Returns true if bot is clear to proceed.
        /// </summary>
        public bool Tick(float time)
        {
            if (_bot == null || _profile == null || _bot.GetPlayer?.HealthController?.IsAlive != true)
                return true;

            if (!_bot.GetPlayer.IsAI)
                return true;

            // Check door interaction logic
            if (_doorOpener != null && !_doorOpener.Update())
                return false;

            return true;
        }

        #endregion
    }
}
