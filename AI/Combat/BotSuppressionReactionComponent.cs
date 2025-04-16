#nullable enable

using UnityEngine;
using EFT;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Simulates bot suppression reaction — evasive movement and flinch response under fire.
    /// </summary>
    public class BotSuppressionReactionComponent : MonoBehaviour
    {
        public BotOwner Bot { get; private set; } = null!;

        private float suppressionStartTime;
        private const float SuppressionDuration = 2.0f;

        private bool isSuppressed = false;

        private void Awake()
        {
            Bot = GetComponent<BotOwner>();
        }

        private void Update()
        {
            if (isSuppressed && Time.time - suppressionStartTime > SuppressionDuration)
            {
                isSuppressed = false;
            }
        }

        /// <summary>
        /// Triggers suppression movement and directional retreat.
        /// </summary>
        public void TriggerSuppression(Vector3? from = null)
        {
            if (isSuppressed)
                return;

            suppressionStartTime = Time.time;
            isSuppressed = true;

            if (Bot == null)
                return;

            if (from.HasValue)
            {
                Vector3 dir = (Bot.Position - from.Value).normalized;
                Vector3 evade = Bot.Position + dir * 6f;

                if (Physics.Raycast(Bot.Position, dir, out var hit, 6f))
                {
                    evade = hit.point - dir * 1f;
                }

                Bot.Sprint(true);
                Bot.GoToPoint(evade, slowAtTheEnd: false);

            }
        }

        /// <summary>
        /// External suppression interface.
        /// </summary>
        public void ReactToSuppression(Vector3 source)
        {
            TriggerSuppression(source);
        }

        /// <summary>
        /// Returns whether the bot is currently suppressed.
        /// </summary>
        public bool IsSuppressed()
        {
            return isSuppressed;
        }
    }
}
