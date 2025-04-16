#nullable enable

using UnityEngine;
using EFT;
using AIRefactored.AI.Core;
using AIRefactored.AI.Reactions;
using AIRefactored.AI.Helpers;

namespace AIRefactored.AI.Perception
{
    public class BotVisionSystem : MonoBehaviour
    {
        private BotOwner? _bot;
        private BotComponentCache? _cache;

        private float _nextVisionCheck = 0f;
        private const float VisionCheckInterval = 0.35f;
        private const float FieldOfViewAngle = 110f;
        private const float MaxSightDistance = 100f;
        private const float MinSightDistance = 15f;
        private const float DarknessThreshold = 0.15f;

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            _cache = GetComponent<BotComponentCache>();
        }

        private void Update()
        {
            if (_bot == null || _cache == null || Time.time < _nextVisionCheck)
                return;

            _nextVisionCheck = Time.time + VisionCheckInterval;

            float ambientLight = RenderSettings.ambientLight.grayscale;
            float suppressionPenalty = _cache.Suppression?.IsSuppressed() == true ? 0.6f : 0f;
            float blindPenalty = _cache.FlashGrenade?.IsFlashed() == true ? 0.8f : 0f;

            float cautionMod = 1f;
            if (_cache.TryGetComponent(out AIRefactoredBotOwner? aiOwner) && aiOwner.PersonalityProfile != null)
            {
                cautionMod = Mathf.Lerp(0.7f, 1.3f, 1f - aiOwner.PersonalityProfile.Caution);
            }

            float totalPenalty = Mathf.Clamp01(suppressionPenalty + blindPenalty);
            float baseRange = Mathf.Lerp(MaxSightDistance, MinSightDistance, 1f - ambientLight);
            float adjustedRange = baseRange * cautionMod * (1f - totalPenalty);

            _bot.LookSensor.ClearVisibleDist = Mathf.Clamp(adjustedRange, 10f, MaxSightDistance);

            if (blindPenalty >= 0.75f || suppressionPenalty >= 0.6f)
                return;

            ScanForTargets(adjustedRange, ambientLight);
        }

        private void ScanForTargets(float range, float ambientLight)
        {
            if (_bot?.EnemiesController == null || _bot.BotsGroup == null)
                return;

            Vector3 botEye = _bot.Position + Vector3.up * 1.5f;
            Vector3 botFwd = _bot.LookDirection;

            Player[] players = GameObject.FindObjectsOfType<Player>();
            for (int i = 0; i < players.Length; i++)
            {
                var player = players[i];
                if (player == null || player.ProfileId == _bot.ProfileId || _bot.EnemiesController.IsEnemy(player))
                    continue;

                Vector3 toTarget = player.Position - botEye;
                float angle = Vector3.Angle(botFwd, toTarget.normalized);
                if (angle > FieldOfViewAngle * 0.5f)
                    continue;

                if (Physics.Raycast(botEye, toTarget.normalized, out RaycastHit hit, range))
                {
                    if (hit.transform != player.Transform.Original)
                        continue;

                    if (!CanSeeClearly(hit.point, ambientLight))
                        continue;

                    bool added = _bot.BotsGroup.AddEnemy(player, EBotEnemyCause.addPlayer);
                    if (added)
                    {
#if UNITY_EDITOR
                        Debug.Log($"[AIRefactored-Vision] {_bot.Profile?.Info?.Nickname} spotted {player.Profile?.Info?.Nickname}");
#endif
                    }
                }
            }
        }

        private bool CanSeeClearly(Vector3 target, float ambient)
        {
            if (ambient < DarknessThreshold)
                return false;

            Vector3 origin = _bot!.Position + Vector3.up * 1.5f;
            Vector3 dir = (target - origin).normalized;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, MaxSightDistance))
            {
                string tag = hit.collider.tag.ToLowerInvariant();
                string mat = hit.collider.sharedMaterial?.name.ToLowerInvariant() ?? "";

                if (tag.Contains("wall") || tag.Contains("glass") || mat.Contains("metal") || mat.Contains("concrete"))
                    return false;

                if (tag.Contains("bush") || mat.Contains("foliage") || mat.Contains("leaf"))
                    return false;

                return true;
            }

            return false;
        }
    }
}
