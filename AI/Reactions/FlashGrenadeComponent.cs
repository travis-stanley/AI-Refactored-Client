﻿// <auto-generated>
//   This file is part of AI-Refactored, an open-source project focused on realistic AI behavior in Escape from Tarkov.
//   Licensed under the MIT License. See LICENSE in the repository root for more information.
//
//   THIS FILE IS SYSTEMATICALLY MANAGED.
//   Please follow strict StyleCop, ReSharper, and AI-Refactored code standards for all modifications.
// </auto-generated>

#nullable enable

namespace AIRefactored.AI.Reactions
{
    using System.Collections.Generic;
    using AIRefactored.AI.Core;
    using AIRefactored.AI.Helpers;
    using AIRefactored.Core;
    using EFT;
    using UnityEngine;

    /// <summary>
    /// Detects sudden bright light exposure from flashlights or flashbangs.
    /// Simulates temporary blindness and optionally applies suppression/fallback.
    /// </summary>
    public sealed class FlashGrenadeComponent
    {
        #region Constants

        private const float BaseBlindDuration = 4.5f;
        private const float TriggerScoreThreshold = 0.35f;

        #endregion

        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private bool _isBlinded;
        private float _lastFlashTime = -999f;

        #endregion

        #region Public API

        /// <summary>
        /// Initializes the flash component with the bot's runtime cache.
        /// </summary>
        /// <param name="cache">The bot component cache.</param>
        public void Initialize(BotComponentCache cache)
        {
            this._cache = cache;
            this._bot = cache.Bot;
        }

        /// <summary>
        /// Returns true if the bot is still considered blinded.
        /// </summary>
        /// <returns>True if blinded; otherwise, false.</returns>
        public bool IsFlashed()
        {
            return this._isBlinded;
        }

        /// <summary>
        /// Forces the bot into a blind state, optionally triggering suppression logic from a known source.
        /// </summary>
        /// <param name="duration">Duration of blindness.</param>
        /// <param name="source">Optional world position of flash source.</param>
        public void ForceBlind(float duration = BaseBlindDuration, Vector3? source = default)
        {
            if (this._bot == null || this._bot.IsDead)
            {
                return;
            }

            this._lastFlashTime = Time.time;
            this._isBlinded = true;

            if (source.HasValue)
            {
                Player? player = EFTPlayerUtil.ResolvePlayer(this._bot);
                if (player != null)
                {
                    BotSuppressionHelper.TrySuppressBot(player, source.Value);
                }
            }
        }

        /// <summary>
        /// Evaluates exposure to light and clears blindness after recovery.
        /// </summary>
        /// <param name="time">The current time in seconds.</param>
        public void Tick(float time)
        {
            if (this._bot == null || this._bot.IsDead)
            {
                return;
            }

            Player? player = EFTPlayerUtil.ResolvePlayer(this._bot);
            if (player == null || !player.IsAI || player.IsYourPlayer)
            {
                return;
            }

            this.CheckForFlashlightExposure();

            if (this._isBlinded && time - this._lastFlashTime > this.GetBlindRecoveryTime())
            {
                this._isBlinded = false;
            }
        }

        #endregion

        #region Private Methods

        private void CheckForFlashlightExposure()
        {
            if (this._cache == null || this._bot == null)
            {
                return;
            }

            Transform? head = BotCacheUtility.Head(this._cache);
            if (head == null)
            {
                return;
            }

            for (int i = 0; i < FlashlightRegistry.GetLastKnownFlashlightPositions().Count; i++)
            {
                Light? light;
                if (FlashlightRegistry.IsExposingBot(head, out light) && light != null)
                {
                    float score = FlashLightUtils.CalculateFlashScore(light.transform, head, 20f);
                    if (score >= TriggerScoreThreshold)
                    {
                        this._lastFlashTime = Time.time;
                        this._isBlinded = true;

                        Player? player = EFTPlayerUtil.ResolvePlayer(this._bot);
                        if (player != null)
                        {
                            BotSuppressionHelper.TrySuppressBot(player, light.transform.position);
                        }

                        break;
                    }
                }
            }
        }

        private float GetBlindRecoveryTime()
        {
            float composure = 1f;

            if (this._cache != null && this._cache.PanicHandler != null)
            {
                composure = this._cache.PanicHandler.GetComposureLevel();
            }

            return Mathf.Lerp(2f, BaseBlindDuration, 1f - composure);
        }

        #endregion
    }
}
