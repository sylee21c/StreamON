using UnityEngine;

namespace StreamOn.Minigames.Runner
{
    /// <summary>
    /// One streamer-day broadcast shared by every minigame scene. The state is
    /// mirrored into the campaign save so changing games (or restarting the
    /// application) never grants a fresh timer.
    /// </summary>
    public static class RunnerBroadcastSessionStore
    {
        public static bool IsActive { get; private set; }
        public static float DurationSeconds { get; private set; }
        public static float RemainingSeconds { get; private set; }
        public static float ElapsedSeconds { get; private set; }
        public static bool OpenGameSelectionOnRoomLoad { get; set; }

        private static RunnerCampaignSettings _settings;
        private static float _nextAutosaveAt;

        public static void BeginOrResume(RunnerCampaignSettings settings, RunnerCampaignSaveData save)
        {
            if (settings == null || save == null) return;
            _settings = settings;
            if (save.broadcastSessionActive)
            {
                IsActive = true;
                DurationSeconds = Mathf.Max(1f, save.broadcastSessionDurationSeconds);
                RemainingSeconds = Mathf.Clamp(save.broadcastSessionRemainingSeconds, 0f, DurationSeconds);
                ElapsedSeconds = Mathf.Max(0f, save.broadcastSessionElapsedSeconds);
            }
            else
            {
                IsActive = true;
                DurationSeconds = settings.BroadcastSecondsForHealth(save.healthStat, save.fitnessLevel);
                RemainingSeconds = DurationSeconds;
                ElapsedSeconds = 0f;
            }
            _nextAutosaveAt = Time.unscaledTime + 5f;
            ApplyToSave(save);
            RunnerCampaignSaveStore.Save(settings, save, true);
        }

        public static void Tick(float deltaTime)
        {
            if (!IsActive || deltaTime <= 0f) return;
            ElapsedSeconds += deltaTime;
            RemainingSeconds = Mathf.Max(0f, RemainingSeconds - deltaTime);
            if (_settings != null && Time.unscaledTime >= _nextAutosaveAt)
            {
                SaveProgress(_settings);
                _nextAutosaveAt = Time.unscaledTime + 5f;
            }
        }

        public static void ApplyPenalty(float seconds)
        {
            if (!IsActive) return;
            RemainingSeconds = Mathf.Max(0f, RemainingSeconds - Mathf.Max(0f, seconds));
        }

        public static void SaveProgress(RunnerCampaignSettings settings)
        {
            if (settings == null || !RunnerCampaignSaveStore.TryLoad(settings, out RunnerCampaignSaveData save)) return;
            ApplyToSave(save);
            RunnerCampaignSaveStore.Save(settings, save, true);
        }

        public static void ApplyToSave(RunnerCampaignSaveData save)
        {
            if (save == null) return;
            save.broadcastSessionActive = IsActive;
            save.broadcastSessionDurationSeconds = DurationSeconds;
            save.broadcastSessionRemainingSeconds = RemainingSeconds;
            save.broadcastSessionElapsedSeconds = ElapsedSeconds;
        }

        public static void End(RunnerCampaignSettings settings, RunnerCampaignSaveData save = null)
        {
            IsActive = false;
            OpenGameSelectionOnRoomLoad = false;
            if (save == null && settings != null) RunnerCampaignSaveStore.TryLoad(settings, out save);
            if (save != null)
            {
                save.broadcastSessionActive = false;
                save.broadcastSessionDurationSeconds = 0f;
                save.broadcastSessionRemainingSeconds = 0f;
                save.broadcastSessionElapsedSeconds = 0f;
                if (settings != null) RunnerCampaignSaveStore.Save(settings, save, true);
            }
            DurationSeconds = RemainingSeconds = ElapsedSeconds = 0f;
        }

        public static void ResetRuntime()
        {
            IsActive = false;
            DurationSeconds = RemainingSeconds = ElapsedSeconds = 0f;
            OpenGameSelectionOnRoomLoad = false;
            _settings = null;
        }
    }
}
