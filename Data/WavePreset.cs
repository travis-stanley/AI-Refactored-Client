using System;
using System.Collections.Generic;
using AIRefactored.AI;
using AIRefactored.Core;

namespace AIRefactored.Data
{
    public enum BotType
    {
        Pmc,
        Scav,
        Raider,
        Follower,
        Boss
    }

    public class WavePreset
    {
        public float SpawnTime;
        public BotType BotType;
        public int Count;
        public PersonalityType Personality;

        public WavePreset(float spawnTime, BotType botType, int count, PersonalityType personality)
        {
            SpawnTime = spawnTime;
            BotType = botType;
            Count = count;
            Personality = personality;
        }
    }

    public static class WavePresetLibrary
    {
        public static List<WavePreset> GetCurrentMapPreset()
        {
            string mapName = GameWorldHandler.GetCurrentMapName().ToLowerInvariant();

            return mapName switch
            {
                "bigmap" => CustomsPreset(),
                "factory4_day" => FactoryDayPreset(),
                "factory4_night" => FactoryNightPreset(),
                "woods" => WoodsPreset(),
                "shoreline" => ShorelinePreset(),
                "interchange" => InterchangePreset(),
                "rezervbase" => ReservePreset(),
                "laboratory" => LabsPreset(),
                "lighthouse" => LighthousePreset(),
                "tarkovstreets" => StreetsPreset(),
                "sandbox" or "sandbox_high" => SandboxPreset(),
                "develop" => DevPreset(),
                _ => new List<WavePreset>()
            };
        }

        private static List<WavePreset> CustomsPreset() => new()
        {
            new WavePreset(30f, BotType.Pmc, 3, PersonalityType.Cautious),
            new WavePreset(60f, BotType.Scav, 5, PersonalityType.Aggressive),
            new WavePreset(120f, BotType.Pmc, 2, PersonalityType.Balanced)
        };

        private static List<WavePreset> FactoryDayPreset() => new()
        {
            new WavePreset(20f, BotType.Scav, 3, PersonalityType.Defensive),
            new WavePreset(50f, BotType.Pmc, 2, PersonalityType.Aggressive)
        };

        private static List<WavePreset> FactoryNightPreset() => new()
        {
            new WavePreset(25f, BotType.Scav, 4, PersonalityType.Cautious),
            new WavePreset(70f, BotType.Raider, 2, PersonalityType.Aggressive)
        };

        private static List<WavePreset> WoodsPreset() => new()
        {
            new WavePreset(40f, BotType.Scav, 4, PersonalityType.Defensive),
            new WavePreset(90f, BotType.Pmc, 3, PersonalityType.Balanced)
        };

        private static List<WavePreset> ShorelinePreset() => new()
        {
            new WavePreset(35f, BotType.Scav, 5, PersonalityType.Cautious),
            new WavePreset(80f, BotType.Pmc, 2, PersonalityType.Aggressive)
        };

        private static List<WavePreset> InterchangePreset() => new()
        {
            new WavePreset(30f, BotType.Scav, 4, PersonalityType.Balanced),
            new WavePreset(100f, BotType.Pmc, 3, PersonalityType.Cautious)
        };

        private static List<WavePreset> ReservePreset() => new()
        {
            new WavePreset(45f, BotType.Scav, 6, PersonalityType.Defensive),
            new WavePreset(110f, BotType.Pmc, 3, PersonalityType.Aggressive)
        };

        private static List<WavePreset> LabsPreset() => new()
        {
            new WavePreset(20f, BotType.Raider, 4, PersonalityType.Aggressive),
            new WavePreset(80f, BotType.Raider, 3, PersonalityType.Cautious)
        };

        private static List<WavePreset> LighthousePreset() => new()
        {
            new WavePreset(50f, BotType.Scav, 5, PersonalityType.Cautious),
            new WavePreset(100f, BotType.Pmc, 3, PersonalityType.Balanced)
        };

        private static List<WavePreset> StreetsPreset() => new()
        {
            new WavePreset(40f, BotType.Scav, 5, PersonalityType.Aggressive),
            new WavePreset(90f, BotType.Pmc, 3, PersonalityType.Defensive)
        };

        private static List<WavePreset> SandboxPreset() => new()
        {
            new WavePreset(25f, BotType.Scav, 2, PersonalityType.Cautious),
            new WavePreset(60f, BotType.Pmc, 1, PersonalityType.Aggressive)
        };

        private static List<WavePreset> DevPreset() => new()
        {
            new WavePreset(10f, BotType.Scav, 1, PersonalityType.Balanced)
        };
    }
}
