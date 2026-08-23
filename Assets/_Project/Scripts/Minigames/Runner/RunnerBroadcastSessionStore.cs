using System;
using UnityEngine;

namespace StreamOn.Minigames.Runner
{
    // Runtime-only state; every balance value is supplied by RunnerCampaignSettings.
    public static class RunnerBroadcastSessionStore
    {
        public static bool IsActive { get; private set; }
        public static BroadcastGameId GameId { get; private set; }
        public static float DurationSeconds { get; private set; }
        public static float RemainingSeconds { get; private set; }
        public static float ElapsedSeconds { get; private set; }
        public static int RawScore { get; private set; }
        public static int BroadcastScore => Mathf.FloorToInt(_broadcastScore);
        public static float GrantedBonusSeconds { get; private set; }
        public static bool OpenGameSelectionOnRoomLoad { get; set; }

        public static event Action<float, int> TimeBonusGranted;
        public static event Action<int, int> ScoreChanged;

        private static RunnerCampaignSettings _settings;
        private static BroadcastGameRule _rule;
        private static float _broadcastScore;
        private static float _nextAutosaveAt;
        private static int _nextBonusIndex;
        private static RunnerCampaignSaveData _stagedSave;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForApplicationStart() => ResetRuntime();

        public static void BeginOrResume(RunnerCampaignSettings settings, RunnerCampaignSaveData save) =>
            BeginOrResume(settings, save, BroadcastGameId.Runner);

        public static bool BeginOrResume(RunnerCampaignSettings settings, RunnerCampaignSaveData save, BroadcastGameId gameId)
        {
            if (settings == null || save == null) return false;
            if (IsActive) return _settings == settings && GameId == gameId;

            _settings = settings;
            _rule = settings.GameRule(gameId);
            GameId = gameId;
            IsActive = true;
            DurationSeconds = Mathf.Max(1f, _rule.baseDurationSeconds);
            RemainingSeconds = DurationSeconds;
            ElapsedSeconds = 0f;
            RawScore = 0;
            _broadcastScore = 0f;
            GrantedBonusSeconds = 0f;
            _nextBonusIndex = 0;
            _nextAutosaveAt = Time.unscaledTime + Mathf.Max(0.25f, settings.sessionAutosaveIntervalSeconds);
            ClearSessionFields(save);
            _stagedSave = CloneSave(save);
            ApplyToSave(_stagedSave);
            return true;
        }

        public static void Tick(float unscaledDeltaTime)
        {
            if (!IsActive || unscaledDeltaTime <= 0f) return;
            ElapsedSeconds += unscaledDeltaTime;
            RemainingSeconds = Mathf.Max(0f, RemainingSeconds - unscaledDeltaTime);
            if (_settings != null && Time.unscaledTime >= _nextAutosaveAt)
            {
                SaveProgress(_settings);
                _nextAutosaveAt = Time.unscaledTime + Mathf.Max(0.25f, _settings.sessionAutosaveIntervalSeconds);
            }
        }

        public static int AddRawPoints(int rawPoints, float heat)
        {
            if (!IsActive || rawPoints <= 0) return 0;
            int before = BroadcastScore;
            RawScore += rawPoints;
            _broadcastScore += rawPoints * (_settings != null ? _settings.HeatScoreMultiplier(heat) : 1f);
            TryGrantTimeBonuses();
            ScoreChanged?.Invoke(RawScore, BroadcastScore);
            return BroadcastScore - before;
        }

        private static void TryGrantTimeBonuses()
        {
            if (_rule?.timeBonuses == null || GameId == BroadcastGameId.PlasticKnightmare) return;
            while (_nextBonusIndex < _rule.timeBonuses.Count)
            {
                BroadcastTimeBonusRule bonus = _rule.timeBonuses[_nextBonusIndex];
                if (bonus == null) { _nextBonusIndex++; continue; }
                if (BroadcastScore < bonus.broadcastScoreThreshold) break;
                float grant = Mathf.Min(Mathf.Max(0f, bonus.bonusSeconds), Mathf.Max(0f, _rule.maximumBonusSeconds - GrantedBonusSeconds));
                _nextBonusIndex++;
                if (grant <= 0f) continue;
                GrantedBonusSeconds += grant;
                RemainingSeconds += grant;
                TimeBonusGranted?.Invoke(grant, BroadcastScore);
            }
        }

        public static void ApplyPenalty(float seconds)
        {
            if (IsActive) RemainingSeconds = Mathf.Max(0f, RemainingSeconds - Mathf.Max(0f, seconds));
        }

        public static void SaveProgress(RunnerCampaignSettings settings)
        {
            if (settings == null || !RunnerCampaignSaveStore.TryLoad(settings, out RunnerCampaignSaveData save)) return;
            ApplyToSave(save);
            RunnerCampaignSaveStore.Save(settings, save, true);
        }

        public static bool TryGetStagedSave(RunnerCampaignSettings settings, out RunnerCampaignSaveData save)
        {
            save = null;
            if (!IsActive || _settings != settings || _stagedSave == null) return false;
            save = CloneSave(_stagedSave);
            return save != null;
        }

        public static bool TryStageSave(RunnerCampaignSettings settings, RunnerCampaignSaveData save)
        {
            if (!IsActive || _settings != settings || save == null) return false;
            _stagedSave = CloneSave(save);
            ApplyToSave(_stagedSave);
            return true;
        }

        public static void ApplyToSave(RunnerCampaignSaveData save)
        {
            if (save == null) return;
            save.broadcastSessionActive = IsActive;
            save.broadcastSessionGameId = GameId.ToString();
            save.broadcastSessionDurationSeconds = DurationSeconds;
            save.broadcastSessionRemainingSeconds = RemainingSeconds;
            save.broadcastSessionElapsedSeconds = ElapsedSeconds;
            save.broadcastSessionRawScore = RawScore;
            save.broadcastSessionScore = BroadcastScore;
            save.broadcastSessionGrantedBonusSeconds = GrantedBonusSeconds;
            save.broadcastSessionNextBonusIndex = _nextBonusIndex;
        }

        public static void End(RunnerCampaignSettings settings, RunnerCampaignSaveData save = null)
        {
            if (save != null) ClearSessionFields(save);
            ResetRuntime();
        }

        public static bool Complete(RunnerCampaignSettings settings, RunnerCampaignSaveData save = null)
        {
            RunnerCampaignSaveData completed = save != null ? CloneSave(save) : CloneSave(_stagedSave);
            if (completed == null || settings == null) return false;
            ClearSessionFields(completed);
            if (save != null) ClearSessionFields(save);
            ResetRuntime();
            return RunnerCampaignSaveStore.Save(settings, completed, true);
        }

        private static void ClearSessionFields(RunnerCampaignSaveData save)
        {
            if (save == null) return;
            save.broadcastSessionActive = false;
            save.broadcastSessionGameId = string.Empty;
            save.broadcastSessionDurationSeconds = save.broadcastSessionRemainingSeconds = save.broadcastSessionElapsedSeconds = 0f;
            save.broadcastSessionRawScore = save.broadcastSessionScore = 0;
            save.broadcastSessionGrantedBonusSeconds = 0f;
            save.broadcastSessionNextBonusIndex = 0;
        }

        private static RunnerCampaignSaveData CloneSave(RunnerCampaignSaveData save)
        {
            return save == null ? null : JsonUtility.FromJson<RunnerCampaignSaveData>(JsonUtility.ToJson(save));
        }

        private static void ClearRuntimeValues()
        {
            DurationSeconds = RemainingSeconds = ElapsedSeconds = GrantedBonusSeconds = 0f;
            RawScore = 0;
            _broadcastScore = 0f;
            _nextBonusIndex = 0;
        }

        public static void ResetRuntime()
        {
            IsActive = false;
            ClearRuntimeValues();
            OpenGameSelectionOnRoomLoad = false;
            _settings = null;
            _rule = null;
            _stagedSave = null;
        }
    }
}
