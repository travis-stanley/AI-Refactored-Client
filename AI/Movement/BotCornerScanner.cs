#nullable enable

namespace AIRefactored.AI.Movement
{
    using System;

    using AIRefactored.AI.Core;

    using EFT;

    using UnityEngine;
    using UnityEngine.AI;

    /// <summary>
    ///     Scans for corners and ledges to trigger tactical lean and movement pauses.
    ///     Aggressive bots peek faster and lean sooner. Defensive bots avoid ledges and corners.
    /// </summary>
    public class BotCornerScanner
    {
        private const float BasePauseDuration = 0.4f;

        private const float BaseWallCheckDistance = 1.5f;

        private const float EdgeCheckDistance = 1.25f;

        private const float EdgeRaySpacing = 0.25f;

        private const float MinFallHeight = 2.2f;

        private const float PrepCrouchTime = 0.75f;

        private const float WallAngleThreshold = 0.7f;

        private const float WallCheckHeight = 1.5f;

        private static readonly LayerMask CoverCheckMask = LayerMask.GetMask(
            "HighPolyCollider",
            "Terrain",
            "LowPolyCollider",
            "DoorLowPolyCollider");

        private BotOwner? _bot;

        private BotComponentCache? _cache;

        private float _pauseUntil;

        private float _prepCrouchUntil;

        private BotPersonalityProfile? _profile;

        public void Initialize(BotComponentCache cache)
        {
            this._cache = cache ?? throw new ArgumentNullException(nameof(cache));
            this._bot = cache.Bot ?? throw new InvalidOperationException("Bot is null in cache.");
            this._profile = cache.AIRefactoredBotOwner?.PersonalityProfile
                            ?? throw new InvalidOperationException("Missing personality profile.");
        }

        public void Tick(float time)
        {
            if (!this.IsEligible(time))
                return;

            if (this.IsApproachingEdge())
            {
                this._cache?.Tilt?.Stop();
                this.PauseMovement(time);
                return;
            }

            if (this.TryCornerPeekWithCrouch(time))
                return;

            this.ResetLean(time);
        }

        private bool AttemptCrouch(float time)
        {
            if (this._cache?.PoseController is { } pose && pose.GetPoseLevel() > 30f)
            {
                pose.SetCrouch();
                this._prepCrouchUntil = time + PrepCrouchTime;
                return true;
            }

            return false;
        }

        private bool CheckWall(Vector3 origin, Vector3 direction, float distance)
        {
            return Physics.Raycast(origin, direction, out var hit, distance, CoverCheckMask)
                   && Vector3.Dot(hit.normal, direction) < WallAngleThreshold;
        }

        private bool IsApproachingEdge()
        {
            if (this._bot == null)
                return false;

            var start = this._bot.Position + Vector3.up * 0.2f;
            var forward = this._bot.Transform.forward;
            var right = this._bot.Transform.right;
            var rayCount = Mathf.CeilToInt(EdgeRaySpacing * 2f / EdgeRaySpacing) + 1;

            for (var i = 0; i < rayCount; i++)
            {
                var offset = (i - (rayCount - 1) / 2f) * EdgeRaySpacing;
                var origin = start + right * offset + forward * EdgeCheckDistance;

                if (!Physics.Raycast(origin, Vector3.down, MinFallHeight, CoverCheckMask))
                    if (!NavMesh.SamplePosition(origin + Vector3.down * MinFallHeight, out _, 1.0f, NavMesh.AllAreas))
                        return true;
            }

            return false;
        }

        private bool IsEligible(float time)
        {
            return this._bot is { IsDead: false, Mover: not null } && time >= this._pauseUntil
                                                                   && time >= this._prepCrouchUntil
                                                                   && this._bot.Memory?.GoalEnemy == null;
        }

        private void PauseMovement(float time)
        {
            if (this._bot == null || this._profile == null)
                return;

            var duration = BasePauseDuration * Mathf.Clamp(0.5f + this._profile.Caution, 0.4f, 2.0f);
            this._bot.Mover?.MovementPause(duration);
            this._pauseUntil = time + duration;
        }

        private void ResetLean(float time)
        {
            var tilt = this._cache?.Tilt;
            if (tilt != null && tilt._coreTilt)
            {
                tilt.tiltOff = time - 1f;
                tilt.ManualUpdate();
            }
        }

        private void TriggerLean(BotTiltType side, float time)
        {
            this._cache?.Tilt?.Set(side);
            this.PauseMovement(time);
        }

        private bool TryCornerPeekWithCrouch(float time)
        {
            if (this._bot == null || this._profile == null)
                return false;

            var origin = this._bot.Position + Vector3.up * WallCheckHeight;
            var right = this._bot.Transform.right;
            var left = -right;
            var scanDistance = BaseWallCheckDistance + (1f - this._profile.Caution) * 0.5f;

            if (this.CheckWall(origin, left, scanDistance))
            {
                if (this.AttemptCrouch(time)) return true;
                this.TriggerLean(BotTiltType.left, time);
                return true;
            }

            if (this.CheckWall(origin, right, scanDistance))
            {
                if (this.AttemptCrouch(time)) return true;
                this.TriggerLean(BotTiltType.right, time);
                return true;
            }

            return false;
        }
    }
}