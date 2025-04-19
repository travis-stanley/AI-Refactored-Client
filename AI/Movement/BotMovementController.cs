#nullable enable

using EFT;
using UnityEngine;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Core;
using AIRefactored.AI; // Needed for BotPersonalityProfile and LeanPreference

namespace AIRefactored.AI.Movement
{
    public class BotMovementController : MonoBehaviour
    {
        private BotOwner? _bot;
        private BotComponentCache? _cache;

        private const float CornerScanInterval = 1.2f;
        private const float ScanDistance = 2.5f;
        private const float ScanRadius = 0.25f;

        private const float LookSmoothSpeed = 6f;
        private const float InertiaWeight = 8f;
        private const float MinMoveThreshold = 0.05f;
        private const float LeanCooldown = 1.5f;

        private float _nextScanTime = 0f;
        private Vector3 _lastVelocity = Vector3.zero;

        private bool _isStrafingRight = true;
        private float _strafeTimer = 0f;
        private float _nextLeanAllowed = 0f;

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            _cache = GetComponent<BotComponentCache>();
        }

        public void Tick(float deltaTime)
        {
            if (_bot == null || _bot.IsDead)
                return;

            var player = _bot.GetPlayer;
            if (player == null || !player.IsAI || player.IsYourPlayer)
                return;

            float now = Time.time;

            if (now >= _nextScanTime)
            {
                ScanAhead();
                _nextScanTime = now + CornerScanInterval;
            }

            if (_bot.Mover != null)
            {
                Vector3 lookTarget = _bot.Mover.LastTargetPoint(1.0f);
                SmoothLookTo(lookTarget, deltaTime);
            }

            ApplyInertia(deltaTime);

            if (_bot.Memory?.GoalEnemy != null && _bot.WeaponManager?.IsReady == true)
            {
                CombatStrafe(deltaTime);
                TryCombatLean();
            }
        }

        private void SmoothLookTo(Vector3 target, float deltaTime)
        {
            Vector3 direction = target - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;

            float angle = Vector3.Angle(transform.forward, direction);
            if (_cache?.Tilt?._coreTilt == true && angle > 80f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, LookSmoothSpeed * deltaTime);
        }

        private void ApplyInertia(float deltaTime)
        {
            if (_bot?.Mover == null) return;

            Vector3 targetPoint = _bot.Mover.LastTargetPoint(1.0f);
            Vector3 direction = targetPoint - _bot.Position;
            direction.y = 0f;

            if (direction.magnitude < MinMoveThreshold)
                return;

            float moveSpeed = 1.65f;
            Vector3 desiredVelocity = direction.normalized * moveSpeed;

            _lastVelocity = Vector3.Lerp(_lastVelocity, desiredVelocity, InertiaWeight * deltaTime);
            _bot.GetPlayer?.CharacterController?.Move(_lastVelocity * deltaTime, deltaTime);
        }

        private void CombatStrafe(float deltaTime)
        {
            if (_bot == null || _bot.Mover == null)
                return;

            _strafeTimer -= deltaTime;
            if (_strafeTimer <= 0f)
            {
                _isStrafingRight = UnityEngine.Random.value > 0.5f;
                _strafeTimer = UnityEngine.Random.Range(0.4f, 0.8f);
            }

            Vector3 strafeDir = _isStrafingRight ? transform.right : -transform.right;
            float strafeSpeed = 1.25f;

            _bot.GetPlayer?.CharacterController?.Move(strafeDir * strafeSpeed * deltaTime, deltaTime);
        }

        private void TryCombatLean()
        {
            if (_bot == null || _cache == null || _cache.Tilt == null)
                return;

            if (Time.time < _nextLeanAllowed)
                return;

            var personality = _cache.AIRefactoredBotOwner?.PersonalityProfile;
            if (personality == null || personality.LeaningStyle == LeanPreference.Never)
                return;

            var memory = _bot.Memory;
            if (memory?.GoalEnemy == null)
                return;

            Vector3 enemyPos = memory.GoalEnemy.CurrPosition;
            Vector3 toEnemy = enemyPos - _bot.Position;

            Vector3 origin = _bot.Position + Vector3.up * 1.5f;
            Vector3 left = -transform.right;
            Vector3 right = transform.right;

            float checkDist = 1.5f;
            bool wallLeft = Physics.Raycast(origin, left, checkDist);
            bool wallRight = Physics.Raycast(origin, right, checkDist);

            var cover = memory.BotCurrentCoverInfo?.LastCover;
            bool inCover = cover != null;

            // Conservative bots require wall or cover
            if (personality.LeaningStyle == LeanPreference.Conservative && !inCover && !wallLeft && !wallRight)
                return;

            if (inCover)
            {
                Vector3 coverToBot = _bot.Position - cover!.Position;
                float side = Vector3.Dot(coverToBot.normalized, transform.right);
                _cache.Tilt.Set(side > 0f ? BotTiltType.right : BotTiltType.left);
            }
            else if (wallLeft && !wallRight)
            {
                _cache.Tilt.Set(BotTiltType.right);
            }
            else if (wallRight && !wallLeft)
            {
                _cache.Tilt.Set(BotTiltType.left);
            }
            else
            {
                float dot = Vector3.Dot(toEnemy.normalized, transform.right);
                _cache.Tilt.Set(dot > 0f ? BotTiltType.right : BotTiltType.left);
            }

            _nextLeanAllowed = Time.time + LeanCooldown;
            Debug.Log($"[AIRefactored-Movement] {BotName()} leaned for combat.");
        }

        private void ScanAhead()
        {
            if (_bot == null || _bot.Mover == null)
                return;

            Vector3 origin = _bot.Position + Vector3.up * 1.5f;
            Vector3 forward = _bot.LookDirection;

            if (Physics.SphereCast(origin, ScanRadius, forward, out RaycastHit hit, ScanDistance))
            {
                if (hit.collider != null)
                {
                    _bot.BotTalk?.TrySay(EPhraseTrigger.Look);
                }
            }
        }

        private string BotName()
        {
            return _bot?.Profile?.Info?.Nickname ?? "UnknownBot";
        }
    }
}
