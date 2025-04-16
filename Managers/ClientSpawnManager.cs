using System.Collections.Generic;
using UnityEngine;
using AIRefactored.AI;
using AIRefactored.Core;
using AIRefactored.Data;
using EFT;

namespace AIRefactored.Managers
{
    /// <summary>
    /// Controls spawn timing and dispatches bot waves at runtime. Host-only logic.
    /// </summary>
    public class ClientSpawnManager : MonoBehaviour
    {
        private float waveTimer;
        private int waveIndex;
        private bool isHost;
        private bool initialized;

        private List<WavePreset>? wavePresets;

        private void Start()
        {
            isHost = HostDetector.IsHost();

            if (!isHost)
            {
                Debug.Log("[AI-Refactored] Skipping spawn logic: not host.");
                Destroy(this);
                return;
            }

            Debug.Log("[AI-Refactored] ClientSpawnManager initialized for host.");
            wavePresets = WavePresetLibrary.GetCurrentMapPreset();
            waveTimer = 0f;
            waveIndex = 0;
        }

        private void Update()
        {
            if (!initialized)
            {
                if (Time.timeSinceLevelLoad < 3f || wavePresets == null || wavePresets.Count == 0)
                    return;

                Debug.Log("[AI-Refactored] Bot wave logic initialized.");
                initialized = true;
            }

            waveTimer += Time.deltaTime;

            if (waveIndex >= wavePresets.Count)
                return;

            WavePreset currentWave = wavePresets[waveIndex];

            if (waveTimer >= currentWave.SpawnTime)
            {
                SpawnWave(currentWave);
                waveIndex++;
            }
        }

        private void SpawnWave(WavePreset wave)
        {
            Debug.Log($"[AI-Refactored] Spawning wave: {wave.BotType} x{wave.Count} ({wave.Personality})");

            for (int i = 0; i < wave.Count; i++)
            {
                BotSpawnerWrapper.TrySpawn(wave.BotType, wave.Personality);
            }
        }
    }
}
