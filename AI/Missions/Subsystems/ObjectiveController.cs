#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Hotspots;
using AIRefactored.AI.Looting;
using EFT;
using EFT.Interactive;
using System;
using System.Collections.Generic;
using UnityEngine;
using MissionType = AIRefactored.AI.Missions.BotMissionController.MissionType;

namespace AIRefactored.AI.Missions.Subsystems
{
    /// <summary>
    /// Handles all routing, objective assignment, quest queueing, and re-pathing logic
    /// for active missions (Loot, Fight, Quest).
    /// </summary>
    public sealed class ObjectiveController
    {
        private readonly BotOwner _bot;
        private readonly BotComponentCache _cache;
        private readonly BotLootScanner? _lootScanner;

        private readonly Queue<Vector3> _questRoute = new();
        private readonly System.Random _rng = new();

        public Vector3 CurrentObjective { get; private set; }

        public ObjectiveController(BotOwner bot, BotComponentCache cache)
        {
            _bot = bot;
            _cache = cache;
            _lootScanner = cache.LootScanner;
        }

        /// <summary>
        /// Assigns the first objective based on mission type.
        /// </summary>
        public void SetInitialObjective(MissionType type)
        {
            Vector3 target = type switch
            {
                MissionType.Quest => GetNextQuestObjective(),
                MissionType.Loot => GetLootObjective(),
                MissionType.Fight => GetFightZone(),
                _ => _bot.Position
            };

            CurrentObjective = _cache.Pathing?.ApplyOffsetTo(target) ?? target;
            BotMovementHelper.SmoothMoveTo(_bot, CurrentObjective);
        }

        /// <summary>
        /// Called when an objective is reached. Automatically assigns a new one.
        /// </summary>
        public void OnObjectiveReached(MissionType type)
        {
            switch (type)
            {
                case MissionType.Quest:
                    if (_questRoute.Count == 0)
                        PopulateQuestRoute();

                    if (_questRoute.Count > 0)
                    {
                        Vector3 next = GetNextQuestObjective();
                        CurrentObjective = _cache.Pathing?.ApplyOffsetTo(next) ?? next;
                        BotMovementHelper.SmoothMoveTo(_bot, CurrentObjective);
                    }
                    break;

                case MissionType.Fight:
                    CurrentObjective = _cache.Pathing?.ApplyOffsetTo(GetFightZone()) ?? GetFightZone();
                    BotMovementHelper.SmoothMoveTo(_bot, CurrentObjective);
                    break;

                case MissionType.Loot:
                    Vector3 loot = GetLootObjective();
                    CurrentObjective = _cache.Pathing?.ApplyOffsetTo(loot) ?? loot;
                    BotMovementHelper.SmoothMoveTo(_bot, CurrentObjective);
                    break;
            }
        }

        /// <summary>
        /// Manually resumes questing path (e.g., after pause or fallback).
        /// </summary>
        public void ResumeQuesting()
        {
            if (_questRoute.Count == 0)
                PopulateQuestRoute();

            if (_questRoute.Count > 0)
            {
                Vector3 next = GetNextQuestObjective();
                CurrentObjective = _cache.Pathing?.ApplyOffsetTo(next) ?? next;
                BotMovementHelper.SmoothMoveTo(_bot, CurrentObjective);
            }
        }

        private Vector3 GetNextQuestObjective() =>
            _questRoute.Count > 0 ? _questRoute.Dequeue() : _bot.Position;

        private Vector3 GetLootObjective() =>
            _lootScanner?.GetHighestValueLootPoint() ?? _bot.Position;

        private Vector3 GetFightZone()
        {
            var zones = GameObject.FindObjectsOfType<BotZone>();
            return zones.Length > 0
                ? zones[_rng.Next(0, zones.Length)].transform.position
                : _bot.Position;
        }

        private void PopulateQuestRoute()
        {
            _questRoute.Clear();
            Vector3 origin = _bot.Position;

            var directionFilter = new Predicate<HotspotRegistry.Hotspot>(h =>
                Vector3.Dot((h.Position - origin).normalized, _bot.LookDirection.normalized) > 0.25f);

            var filtered = HotspotRegistry.QueryNearby(origin, 100f, directionFilter);
            if (filtered.Count == 0) return;

            int count = UnityEngine.Random.Range(2, 4);
            var used = new HashSet<int>();

            while (_questRoute.Count < count && used.Count < filtered.Count)
            {
                int i = UnityEngine.Random.Range(0, filtered.Count);
                if (used.Add(i))
                    _questRoute.Enqueue(filtered[i].Position);
            }
        }
    }
}
