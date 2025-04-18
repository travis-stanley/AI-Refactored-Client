#nullable enable

using AIRefactored.AI.Components;
using AIRefactored.AI.Core;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Simulates auditory perception for bots, including sound source evaluation,
    /// loudness scoring, occlusion, and reaction to nearby player noise.
    /// </summary>
    public class BotHearingSystem : MonoBehaviour
    {
        #region Constants

        private const float MaxBaseHearing = 60f;
        private const float SprintLoudness = 1.0f;
        private const float WalkLoudness = 0.6f;
        private const float CrouchLoudness = 0.3f;
        private const float FireLoudness = 1.25f;
        private static readonly int PlayerLayerMask = LayerMask.GetMask("Player");

        #endregion

        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private AIRefactoredBotOwner? _owner;
        private HearingDamageComponent? _hearing;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            _cache = GetComponent<BotComponentCache>();
            _owner = GetComponent<AIRefactoredBotOwner>();
            _hearing = GetComponent<HearingDamageComponent>();
        }

        #endregion

        #region Tick Interface

        /// <summary>
        /// Tick-based auditory evaluation, called from BotBrain at 3Hz.
        /// </summary>
        public void Tick(float time)
        {
            if (_bot == null || _cache == null || _owner == null || _bot.IsDead)
                return;

            if (_bot.GetPlayer?.IsYourPlayer == true)
                return;

            EvaluateNearbySounds();
        }

        #endregion

        #region Sound Evaluation

        private void EvaluateNearbySounds()
        {
            float caution = _owner.PersonalityProfile?.Caution ?? 0.5f;
            float effectiveRadius = MaxBaseHearing * Mathf.Lerp(0.5f, 1.5f, caution);

            Collider[] hits = Physics.OverlapSphere(_bot.Position, effectiveRadius, PlayerLayerMask);

            for (int i = 0; i < hits.Length; i++)
            {
                Player player = hits[i].GetComponent<Player>();
                if (player == null || player.ProfileId == _bot.ProfileId || !player.HealthController.IsAlive)
                    continue;

                if (!player.IsAI && player.IsYourPlayer)
                    continue;

                float loudness = EstimateLoudness(player);
                float distance = Vector3.Distance(_bot.Position, player.Position);

                if (loudness <= 0.1f)
                    continue;

                if (!player.IsAI && distance < 50f)
                    loudness = Mathf.Max(loudness, FireLoudness);

                if (loudness >= 1f)
                    TryApplyDeafness(distance);

                float perceived = loudness * Mathf.Clamp01(1f - (distance / effectiveRadius));

                if (!HasClearPath(player.Position, out float occlusionMod))
                    perceived *= occlusionMod;

                if (_hearing != null && _hearing.IsDeafened)
                    perceived *= Mathf.Lerp(0.1f, 0.5f, _hearing.Deafness);

                if (perceived > 0.35f)
                {
                    _cache.RegisterHeardSound(player.Position);
                    _bot.BotsGroup?.LastSoundsController?.AddNeutralSound(player, player.Position);
                    HandleDetectedNoise(player.Position);
                    break;
                }
            }
        }

        #endregion

        #region Loudness & Occlusion

        private float EstimateLoudness(Player player)
        {
            string? state = player.MovementContext?.CurrentState?.GetType().Name;

            if (string.IsNullOrEmpty(state)) return 0f;
            if (state.Contains("Sprint")) return SprintLoudness;
            if (state.Contains("Walk")) return WalkLoudness;
            if (state.Contains("Crouch")) return CrouchLoudness;

            return 0f;
        }

        private void TryApplyDeafness(float distance)
        {
            if (_hearing == null || distance > 30f)
                return;

            float scaled = Mathf.Clamp01(1f - (distance / 30f));
            if (scaled <= 0.2f) return;

            float intensity = scaled;
            float duration = Mathf.Lerp(1f, 8f, scaled);

            var earpiece = _bot?.Profile?.Inventory?.Equipment?
                .GetSlot(EFT.InventoryLogic.EquipmentSlot.Earpiece)?.ContainedItem;

            if (earpiece != null)
            {
                intensity *= 0.3f;
                duration *= 0.4f;
            }

            _hearing.ApplyDeafness(intensity, duration);
        }

        private bool HasClearPath(Vector3 source, out float occlusionModifier)
        {
            occlusionModifier = 1f;

            if (Physics.Linecast(source, _bot!.Position, out RaycastHit hit))
            {
                if (hit.collider != null && hit.collider.gameObject != _bot.GetPlayer?.gameObject)
                {
                    occlusionModifier = 0.25f;
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region Reaction

        private void HandleDetectedNoise(Vector3 position)
        {
            if (_bot?.Memory.GoalEnemy != null)
                return;

            Vector3 direction = (position - _bot.Position).normalized;
            Vector3 moveTo = _bot.Position + direction * 3f;

            _bot.GoToPoint(moveTo, slowAtTheEnd: false);
            _bot.BotTalk?.TrySay(EPhraseTrigger.OnEnemyShot);
        }

        #endregion
    }
}
