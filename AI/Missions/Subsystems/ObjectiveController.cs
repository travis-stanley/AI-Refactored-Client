#nullable enable

using MissionType = AIRefactored.AI.Missions.BotMissionController.MissionType;

namespace AIRefactored.AI.Missions.Subsystems
{
    using System;
    using System.Collections.Generic;

    using AIRefactored.AI.Core;
    using AIRefactored.AI.Helpers;
    using AIRefactored.AI.Hotspots;
    using AIRefactored.AI.Looting;

    using EFT;

    using UnityEngine;

    using Random = System.Random;

    /// <summary>
    ///     Handles all routing, objective assignment, quest queueing, and re-pathing logic
    ///     for active missions (Loot, Fight, Quest). Fully integrated with squad pathing.
    /// </summary>
    public sealed class ObjectiveController
    {
        private readonly BotOwner _bot;

        private readonly BotComponentCache _cache;

        private readonly BotLootScanner? _lootScanner;

        private readonly Queue<Vector3> _questRoute = new();

        private readonly Random _rng = new();

        public ObjectiveController(BotOwner bot, BotComponentCache cache)
        {
            this._bot = bot;
            this._cache = cache;
            this._lootScanner = cache.LootScanner;
        }

        /// <summary>Current world position the bot is routing toward.</summary>
        public Vector3 CurrentObjective { get; private set; }

        /// <summary>
        ///     Called when an objective is reached. Re-queues or updates based on mission.
        /// </summary>
        public void OnObjectiveReached(MissionType type)
        {
            Vector3 next;

            switch (type)
            {
                case MissionType.Quest:
                    if (this._questRoute.Count == 0) this.PopulateQuestRoute();

                    next = this.GetNextQuestObjective();
                    break;

                case MissionType.Fight:
                    next = this.GetFightZone();
                    break;

                case MissionType.Loot:
                    next = this.GetLootObjective();
                    break;

                default:
                    next = this._bot.Position;
                    break;
            }

            this.CurrentObjective = this.ApplyPathOffset(next);
            BotMovementHelper.SmoothMoveTo(this._bot, this.CurrentObjective);
        }

        /// <summary>
        ///     Resumes quest routing after fallback or combat.
        /// </summary>
        public void ResumeQuesting()
        {
            if (this._questRoute.Count == 0) this.PopulateQuestRoute();

            if (this._questRoute.Count > 0)
            {
                var next = this.GetNextQuestObjective();
                this.CurrentObjective = this.ApplyPathOffset(next);
                BotMovementHelper.SmoothMoveTo(this._bot, this.CurrentObjective);
            }
        }

        /// <summary>
        ///     Assigns the first objective based on mission type.
        /// </summary>
        public void SetInitialObjective(MissionType type)
        {
            var target = type switch
                {
                    MissionType.Quest => this.GetNextQuestObjective(),
                    MissionType.Loot => this.GetLootObjective(),
                    MissionType.Fight => this.GetFightZone(),
                    _ => this._bot.Position
                };

            this.CurrentObjective = this.ApplyPathOffset(target);
            BotMovementHelper.SmoothMoveTo(this._bot, this.CurrentObjective);
        }

        private Vector3 ApplyPathOffset(Vector3 target)
        {
            return this._cache.Pathing?.ApplyOffsetTo(target) ?? target;
        }

        private Vector3 GetFightZone()
        {
            var zones = GameObject.FindObjectsOfType<BotZone>();
            return zones.Length > 0 ? zones[this._rng.Next(0, zones.Length)].transform.position : this._bot.Position;
        }

        private Vector3 GetLootObjective()
        {
            return this._lootScanner?.GetHighestValueLootPoint() ?? this._bot.Position;
        }

        private Vector3 GetNextQuestObjective()
        {
            return this._questRoute.Count > 0 ? this._questRoute.Dequeue() : this._bot.Position;
        }

        private void PopulateQuestRoute()
        {
            this._questRoute.Clear();
            var origin = this._bot.Position;

            Predicate<HotspotRegistry.Hotspot> directionFilter = (HotspotRegistry.Hotspot h) =>
                Vector3.Dot((h.Position - origin).normalized, this._bot.LookDirection.normalized) > 0.25f;

            var filtered = HotspotRegistry.QueryNearby(origin, 100f, directionFilter);
            if (filtered.Count == 0)
                return;

            var count = UnityEngine.Random.Range(2, 4);
            HashSet<int> used = new();

            while (this._questRoute.Count < count && used.Count < filtered.Count)
            {
                var i = UnityEngine.Random.Range(0, filtered.Count);
                if (used.Add(i)) this._questRoute.Enqueue(filtered[i].Position);
            }
        }
    }
}