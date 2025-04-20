#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Memory;
using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Listens for nearby sounds like gunfire or movement and registers them with bot memory.
    /// Adjusted by hearing damage and auditory range.
    /// </summary>
    public class BotHearingSystem
    {
        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private float _baseRange = 35f;

        private static readonly ManualLogSource _log = AIRefactoredController.Logger;
        private static readonly bool _debug = false;

        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot;
        }

        /// <summary>
        /// Called from BotBrain.Tick() to check for new sound sources.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_bot == null || _bot.IsDead || _bot.GetPlayer?.IsYourPlayer == true)
                return;

            Vector3 myPos = _bot.Position;
            List<Player> players = BotMemoryStore.GetNearbyPlayers(myPos);

            float deafnessScale = _cache?.HearingDamage?.VolumeModifier ?? 1f;
            float maxRange = _baseRange * deafnessScale;

            foreach (var player in players)
            {
                if (player == null || player == _bot.GetPlayer || (!player.IsAI && !player.IsYourPlayer))
                    continue;

                float dist = Vector3.Distance(myPos, player.Position);
                if (dist > maxRange)
                    continue;

                bool heard = BotSoundUtils.DidFireRecently(player) || BotSoundUtils.DidStepRecently(player);
                if (!heard)
                    continue;

                _cache?.RegisterHeardSound(player.Position);

                if (_debug)
                {
                    string botName = BotName();
                    string target = player.Profile?.Info?.Nickname ?? "unknown";
                    _log.LogDebug($"[BotHearing] {botName} heard {target} at {dist:F1}m.");
                }
            }
        }

        private string BotName()
        {
            return _bot?.Profile?.Info?.Nickname ?? "Bot";
        }
    }
}
