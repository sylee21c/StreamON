using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using StreamOn.Platform;
using UnityEngine;

namespace StreamOn.Minigames.Runner
{
    [Serializable]
    public sealed class RunnerSaveSlotInfo
    {
        public int slot;
        public bool exists;
        public bool recoveredFromBackup;
        public bool corrupted;
        public int day;
        public int subscribers;
        public int mentalLevel;
        public int healthStat;
        public int bestBroadcastScore;
        public int broadcasterLevel;
        public string savedAtUtc;
    }

    [Serializable]
    public sealed class RunnerUserSettingsData
    {
        public int version = 3;
        public int activeSaveSlot = 1;
        public string leaderboardDisplayName;
        public float masterVolume = 1f;
        public float bgmVolume = 1f;
        public float sfxVolume = 1f;
        public bool aiChatEnabled = true;
        public int runnerHighScore;
        public List<BroadcastPendingLeaderboardSubmission> pendingLeaderboardSubmissions = new List<BroadcastPendingLeaderboardSubmission>();
        public List<BroadcastLeaderboardUploadState> leaderboardUploadStates = new List<BroadcastLeaderboardUploadState>();
    }

    [Serializable]
    public sealed class BroadcastPendingLeaderboardSubmission
    {
        public string boardId;
        public BroadcastGameId gameId;
        public bool followerBoard;
        public int score;
        public int clearedNight;
        public string displayName;
        public string achievedAtUtc;
    }

    [Serializable]
    public sealed class BroadcastLeaderboardUploadState
    {
        public string boardId;
        public int score;
        public string displayName;
        public bool hasUploadedValue;
    }

    public static class RunnerUserSettingsStore
    {
        private const string LegacyVolumeKey = "StreamOn.Settings.MasterVolume";
        private const string LegacyAiKey = "StreamOn.Settings.AiChat";
        private static RunnerUserSettingsData _cached;
        private static string SettingsDirectory => Path.Combine(Application.persistentDataPath, "Settings");
        private static string SettingsPath => Path.Combine(SettingsDirectory, "user_settings.json");
        private static string BackupPath => SettingsPath + ".bak";

        public static RunnerUserSettingsData Load(bool defaultAiEnabled = true)
        {
            if (_cached != null) return _cached;
            bool loadedPrimary = TryRead(SettingsPath, out _cached);
            bool loadedBackup = !loadedPrimary && TryRead(BackupPath, out _cached);
            if (!loadedPrimary && !loadedBackup)
            {
                _cached = new RunnerUserSettingsData
                {
                    masterVolume = PlayerPrefs.GetFloat(LegacyVolumeKey, AudioListener.volume),
                    aiChatEnabled = PlayerPrefs.GetInt(LegacyAiKey, defaultAiEnabled ? 1 : 0) != 0,
                    runnerHighScore = PlayerPrefs.GetInt("Runner.HighScore", 0)
                };
                Save(_cached);
            }
            else if (loadedBackup)
            {
                try { File.Copy(BackupPath, SettingsPath, true); } catch { }
            }
            _cached.activeSaveSlot = Mathf.Max(1, _cached.activeSaveSlot);
            _cached.masterVolume = Mathf.Clamp01(_cached.masterVolume);
            _cached.bgmVolume = Mathf.Clamp01(_cached.bgmVolume);
            _cached.sfxVolume = Mathf.Clamp01(_cached.sfxVolume);
            _cached.version = Mathf.Max(3, _cached.version);
            if (_cached.pendingLeaderboardSubmissions == null)
                _cached.pendingLeaderboardSubmissions = new List<BroadcastPendingLeaderboardSubmission>();
            if (_cached.leaderboardUploadStates == null)
                _cached.leaderboardUploadStates = new List<BroadcastLeaderboardUploadState>();
            return _cached;
        }

        /// <summary>
        /// The first name entered on the title screen becomes the public leaderboard name.
        /// Keeping it outside individual save slots makes every game board use one identity.
        /// </summary>
        public static string LockLeaderboardDisplayName(string requestedName, int maximumLength = 16)
        {
            RunnerUserSettingsData data = Load();
            if (!string.IsNullOrWhiteSpace(data.leaderboardDisplayName))
                return data.leaderboardDisplayName;

            string sanitized = SanitizeLeaderboardDisplayName(requestedName, maximumLength);
            if (string.IsNullOrWhiteSpace(sanitized)) return string.Empty;
            data.leaderboardDisplayName = sanitized;
            Save(data);
            return sanitized;
        }

        public static string LeaderboardDisplayName(string fallbackName, int maximumLength = 16)
        {
            RunnerUserSettingsData data = Load();
            return !string.IsNullOrWhiteSpace(data.leaderboardDisplayName)
                ? data.leaderboardDisplayName
                : SanitizeLeaderboardDisplayName(fallbackName, maximumLength);
        }

        private static string SanitizeLeaderboardDisplayName(string value, int maximumLength)
        {
            string trimmed = (value ?? string.Empty).Trim();
            int limit = Mathf.Clamp(maximumLength, 1, 32);
            return trimmed.Length <= limit ? trimmed : trimmed.Substring(0, limit);
        }

        public static void Save(RunnerUserSettingsData data)
        {
            if (data == null) return;
            _cached = data;
            if (WriteAtomic(SettingsDirectory, SettingsPath, BackupPath, JsonUtility.ToJson(data, true)))
                WebGLPlatformBridge.RequestFileSystemSync();
        }

        private static bool TryRead(string path, out RunnerUserSettingsData data)
        {
            data = null;
            try
            {
                if (!File.Exists(path)) return false;
                data = JsonUtility.FromJson<RunnerUserSettingsData>(File.ReadAllText(path));
                return data != null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("STREAM ON settings file could not be read: " + exception.Message);
                return false;
            }
        }

        internal static bool WriteAtomic(string directory, string path, string backupPath, string json)
        {
            string temporaryPath = path + ".tmp";
            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(temporaryPath, json);
                if (File.Exists(path)) File.Copy(path, backupPath, true);
                File.Copy(temporaryPath, path, true);
                File.Delete(temporaryPath);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("STREAM ON atomic save failed: " + exception.Message);
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
                return false;
            }
        }
    }

    /// <summary>
    /// WebGL에서도 브라우저의 persistentDataPath에 남는 최신 기록 전송 대기열입니다.
    /// 같은 리더보드의 오래된 요청은 보관하지 않고 항상 가장 최신 값 하나로 합칩니다.
    /// </summary>
    public static class BroadcastLeaderboardPendingStore
    {
        public static void QueueFromSave(RunnerCampaignSettings settings, RunnerCampaignSaveData save)
        {
            if (settings == null || save == null || !settings.useOnlineLeaderboard
                || !settings.automaticallySubmitLeaderboardRecords) return;

            Queue(settings, save, BroadcastGameId.Runner, false, save.bestRunnerGameScore, 0);
            Queue(settings, save, BroadcastGameId.TileArena, false, save.bestTileArenaGameScore, 0);
            Queue(settings, save, BroadcastGameId.PlasticKnightmare, false, save.bestPlasticGameScoreAtNight, save.bestPlasticNight);
            Queue(settings, save, BroadcastGameId.Runner, true, save.subscribers, 0);
        }

        public static List<BroadcastPendingLeaderboardSubmission> Snapshot()
        {
            RunnerUserSettingsData data = RunnerUserSettingsStore.Load();
            return data.pendingLeaderboardSubmissions != null
                ? new List<BroadcastPendingLeaderboardSubmission>(data.pendingLeaderboardSubmissions)
                : new List<BroadcastPendingLeaderboardSubmission>();
        }

        public static void MarkUploaded(BroadcastPendingLeaderboardSubmission uploaded)
        {
            if (uploaded == null || string.IsNullOrWhiteSpace(uploaded.boardId)) return;
            RunnerUserSettingsData data = RunnerUserSettingsStore.Load();
            data.pendingLeaderboardSubmissions ??= new List<BroadcastPendingLeaderboardSubmission>();
            data.leaderboardUploadStates ??= new List<BroadcastLeaderboardUploadState>();

            data.pendingLeaderboardSubmissions.RemoveAll(item => item != null
                && item.boardId == uploaded.boardId
                && item.score == uploaded.score
                && item.displayName == uploaded.displayName);
            BroadcastLeaderboardUploadState state = data.leaderboardUploadStates.Find(item => item != null && item.boardId == uploaded.boardId);
            if (state == null)
            {
                state = new BroadcastLeaderboardUploadState { boardId = uploaded.boardId };
                data.leaderboardUploadStates.Add(state);
            }
            state.score = uploaded.score;
            state.displayName = uploaded.displayName;
            state.hasUploadedValue = true;
            RunnerUserSettingsStore.Save(data);
        }

        private static void Queue(RunnerCampaignSettings settings, RunnerCampaignSaveData save,
            BroadcastGameId gameId, bool followerBoard, int score, int clearedNight)
        {
            string boardId = settings.LeaderboardId(gameId, followerBoard);
            if (string.IsNullOrWhiteSpace(boardId)) return;
            RunnerUserSettingsData data = RunnerUserSettingsStore.Load();
            data.pendingLeaderboardSubmissions ??= new List<BroadcastPendingLeaderboardSubmission>();
            data.leaderboardUploadStates ??= new List<BroadcastLeaderboardUploadState>();
            string fallbackName = string.IsNullOrWhiteSpace(save.streamerName) ? settings.defaultStreamerName : save.streamerName;
            string displayName = RunnerUserSettingsStore.LockLeaderboardDisplayName(fallbackName);
            BroadcastLeaderboardUploadState uploaded = data.leaderboardUploadStates.Find(item => item != null && item.boardId == boardId);
            if (uploaded != null && uploaded.hasUploadedValue && uploaded.score == score && uploaded.displayName == displayName) return;

            BroadcastPendingLeaderboardSubmission pending = data.pendingLeaderboardSubmissions.Find(item => item != null && item.boardId == boardId);
            if (pending != null && pending.score == Mathf.Max(0, score)
                && pending.clearedNight == Mathf.Max(0, clearedNight)
                && pending.displayName == displayName) return;
            if (pending == null)
            {
                pending = new BroadcastPendingLeaderboardSubmission { boardId = boardId };
                data.pendingLeaderboardSubmissions.Add(pending);
            }
            pending.gameId = gameId;
            pending.followerBoard = followerBoard;
            pending.score = Mathf.Max(0, score);
            pending.clearedNight = Mathf.Max(0, clearedNight);
            pending.displayName = displayName;
            pending.achievedAtUtc = save.savedAtUtc;

            int maximum = Mathf.Max(1, settings.maximumPendingLeaderboardSubmissions);
            while (data.pendingLeaderboardSubmissions.Count > maximum)
                data.pendingLeaderboardSubmissions.RemoveAt(0);
            RunnerUserSettingsStore.Save(data);
        }
    }

    public static class RunnerSaveSession
    {
        public static bool RequireSlotSelection { get; set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetForApplicationStart() => RequireSlotSelection = true;
    }

    public static class RunnerCampaignSaveStore
    {
        public const int CurrentVersion = 9;
        private const string LegacyMigrationKey = "StreamOn.Save.LegacyMigrated.v2";

        public static int ActiveSlot
        {
            get => RunnerUserSettingsStore.Load().activeSaveSlot;
            private set
            {
                RunnerUserSettingsData userSettings = RunnerUserSettingsStore.Load();
                userSettings.activeSaveSlot = value;
                RunnerUserSettingsStore.Save(userSettings);
            }
        }

        public static void SelectSlot(RunnerCampaignSettings settings, int slot)
        {
            ActiveSlot = Mathf.Clamp(slot, 1, Mathf.Max(1, settings != null ? settings.saveSlotCount : 3));
        }

        public static RunnerCampaignSaveData CreateNew(RunnerCampaignSettings settings, int slot = -1) => new RunnerCampaignSaveData
        {
            version = CurrentVersion,
            slot = slot > 0 ? slot : ActiveSlot,
            savedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            day = 1,
            subscribers = settings.startingSubscribers,
            streamerName = settings.defaultStreamerName,
            playerId = Guid.NewGuid().ToString("N"),
            mentalLevel = settings.startingMentalLevel,
            mentalExperience = 0,
            gameSkill = settings.startingGameSkill,
            gameSkillExperience = 0,
            talkingSkill = settings.startingTalkingSkill,
            talkingSkillExperience = 0,
            healthStat = settings.startingHealthStat,
            healthStatExperience = 0,
            broadcasterLevel = Mathf.Max(1, settings.startingBroadcasterLevel),
            unspentStatPoints = Mathf.Max(0, settings.startingStatPoints),
            cash = 0,
            pcLevel = 1,
            microphoneLevel = 1,
            fitnessLevel = 1,
            interiorLevel = 1,
            records = new List<RunnerCampaignDayRecord>()
        };

        public static IReadOnlyList<RunnerSaveSlotInfo> GetSlotInfos(RunnerCampaignSettings settings)
        {
            EnsureLegacyMigrated(settings);
            List<RunnerSaveSlotInfo> result = new List<RunnerSaveSlotInfo>();
            int count = Mathf.Max(1, settings != null ? settings.saveSlotCount : 3);
            for (int slot = 1; slot <= count; slot++) result.Add(GetSlotInfo(settings, slot));
            return result;
        }

        public static RunnerSaveSlotInfo GetSlotInfo(RunnerCampaignSettings settings, int slot)
        {
            RunnerSaveSlotInfo info = new RunnerSaveSlotInfo { slot = slot };
            if (TryLoadSlot(settings, slot, out RunnerCampaignSaveData data, out bool recovered, false))
            {
                info.exists = true;
                info.recoveredFromBackup = recovered;
                info.day = data.day;
                info.subscribers = data.subscribers;
                info.mentalLevel = data.mentalLevel;
                info.healthStat = data.healthStat;
                info.bestBroadcastScore = data.bestBroadcastScore;
                info.broadcasterLevel = data.broadcasterLevel;
                info.savedAtUtc = data.savedAtUtc;
            }
            else info.corrupted = File.Exists(SlotPath(settings, slot)) || File.Exists(BackupPath(settings, slot));
            return info;
        }

        public static bool TryLoad(RunnerCampaignSettings settings, out RunnerCampaignSaveData data)
        {
            if (RunnerBroadcastSessionStore.TryGetStagedSave(settings, out data)) return true;
            EnsureLegacyMigrated(settings);
            return TryLoadSlot(settings, ActiveSlot, out data, out _, true);
        }

        public static bool Save(RunnerCampaignSettings settings, RunnerCampaignSaveData data, bool force = false)
        {
            if (settings == null || data == null || (!force && !settings.enableAutomaticSave)) return false;
            int slot = Mathf.Clamp(ActiveSlot, 1, Mathf.Max(1, settings.saveSlotCount));
            data.version = CurrentVersion;
            data.slot = slot;
            data.savedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            if (RunnerBroadcastSessionStore.TryStageSave(settings, data)) return true;
            bool saved = RunnerUserSettingsStore.WriteAtomic(SaveDirectory(settings), SlotPath(settings, slot), BackupPath(settings, slot), JsonUtility.ToJson(data, true));
            if (saved) BroadcastLeaderboardPendingStore.QueueFromSave(settings, data);
            if (saved) WebGLPlatformBridge.RequestFileSystemSync();
            return saved;
        }

        public static void Delete(RunnerCampaignSettings settings, int slot = -1)
        {
            slot = slot > 0 ? slot : ActiveSlot;
            DeleteIfPresent(SlotPath(settings, slot));
            DeleteIfPresent(BackupPath(settings, slot));
            DeleteIfPresent(SlotPath(settings, slot) + ".tmp");
        }

        private static bool TryLoadSlot(RunnerCampaignSettings settings, int slot, out RunnerCampaignSaveData data, out bool recovered, bool logRecovery)
        {
            recovered = false;
            if (TryReadAndMigrate(settings, SlotPath(settings, slot), slot, out data)) return true;
            if (!TryReadAndMigrate(settings, BackupPath(settings, slot), slot, out data)) return false;
            recovered = true;
            try { File.Copy(BackupPath(settings, slot), SlotPath(settings, slot), true); } catch { }
            if (logRecovery) Debug.LogWarning($"STREAM ON save slot {slot} was restored from its backup.");
            return true;
        }

        private static bool TryReadAndMigrate(RunnerCampaignSettings settings, string path, int slot, out RunnerCampaignSaveData data)
        {
            data = null;
            try
            {
                if (!File.Exists(path)) return false;
                data = JsonUtility.FromJson<RunnerCampaignSaveData>(File.ReadAllText(path));
                if (data == null || data.day < 1 || data.version > CurrentVersion) { data = null; return false; }
                Migrate(settings, data, slot);
                return true;
            }
            catch { data = null; return false; }
        }

        private static void Migrate(RunnerCampaignSettings settings, RunnerCampaignSaveData data, int slot)
        {
            if (data.version < 2)
            {
                if (data.healthStat <= 0) data.healthStat = settings.startingHealthStat;
                data.version = 2;
            }
            if (data.version < 3) data.version = 3;
            if (data.version < 4)
            {
                int legacyGame = Mathf.Max(1, data.gameSkill);
                int legacyTalking = Mathf.Max(1, data.talkingSkill);
                int legacyHealth = Mathf.Max(1, data.healthStat);
                data.gameSkill = 1;
                data.talkingSkill = 1;
                data.healthStat = 1;
                data.gameSkillExperience = (legacyGame - 1) * 40;
                data.talkingSkillExperience = (legacyTalking - 1) * 40;
                data.healthStatExperience = (legacyHealth - 1) * 40;
                settings.AddStatExperience(ref data.gameSkill, ref data.gameSkillExperience, 0, settings.maximumGameSkill);
                settings.AddStatExperience(ref data.talkingSkill, ref data.talkingSkillExperience, 0, settings.maximumTalkingSkill);
                settings.AddStatExperience(ref data.healthStat, ref data.healthStatExperience, 0, settings.maximumHealthStat);
                data.version = 4;
            }
            if (data.version < 5)
            {
                data.cash = Math.Max(0L, data.lifetimeDonations);
                data.pcLevel = 1;
                data.microphoneLevel = 1;
                data.fitnessLevel = 1;
                data.interiorLevel = 1;
                data.version = 5;
            }
            if (data.version < 6)
            {
                // The old 0-100 consumable mental resource no longer affects
                // progression. Existing saves restart the new stat at Lv.1.
                data.mentalLevel = settings.startingMentalLevel;
                data.mentalExperience = 0;
                data.campaignFailed = false;
                data.version = 6;
            }
            if (data.version < 7)
            {
                data.broadcastSessionActive = false;
                data.broadcastSessionDurationSeconds = 0f;
                data.broadcastSessionRemainingSeconds = 0f;
                data.broadcastSessionElapsedSeconds = 0f;
                data.version = 7;
            }
            if (data.version < 8)
            {
                data.broadcasterLevel = Mathf.Max(1, settings.startingBroadcasterLevel);
                data.broadcasterExperience = 0;
                data.witRank = Mathf.Clamp(data.talkingSkill - settings.startingTalkingSkill, 0,
                    BroadcasterProgression.MaximumRank(settings, BroadcasterStatType.Wit));
                data.ComposureRank = Mathf.Clamp(data.mentalLevel - settings.startingMentalLevel, 0,
                    BroadcasterProgression.MaximumRank(settings, BroadcasterStatType.Composure));
                data.ControlRank = Mathf.Clamp(data.healthStat - settings.startingHealthStat, 0,
                    BroadcasterProgression.MaximumRank(settings, BroadcasterStatType.Control));
                int spent = data.witRank * (data.witRank + 1) / 2
                    + data.ComposureRank * (data.ComposureRank + 1) / 2
                    + data.ControlRank * (data.ControlRank + 1) / 2;
                data.unspentStatPoints = Mathf.Max(0, settings.startingStatPoints - spent);
                data.broadcastSessionActive = false;
                data.version = 8;
            }
            if (data.version < 9)
            {
                // v9 separates visible per-game records from the hidden broadcast
                // score used only for time bonuses.
                data.bestRunnerGameScore = 0;
                data.bestTileArenaGameScore = 0;
                data.bestPlasticGameScoreAtNight = 0;
                data.version = 9;
            }
            data.slot = slot;
            data.day = Mathf.Max(1, data.day);
            data.mentalLevel = Mathf.Clamp(data.mentalLevel, 1, settings.maximumMentalLevel);
            data.mentalExperience = Mathf.Max(0, data.mentalExperience);
            data.gameSkill = Mathf.Clamp(data.gameSkill, 1, settings.maximumGameSkill);
            data.talkingSkill = Mathf.Clamp(data.talkingSkill, 1, settings.maximumTalkingSkill);
            data.healthStat = Mathf.Clamp(data.healthStat, 1, settings.maximumHealthStat);
            if (string.IsNullOrWhiteSpace(data.streamerName)) data.streamerName = settings.defaultStreamerName;
            if (string.IsNullOrWhiteSpace(data.playerId)) data.playerId = Guid.NewGuid().ToString("N");
            data.broadcasterLevel = Mathf.Clamp(data.broadcasterLevel, 1, Mathf.Max(1, settings.maximumBroadcasterLevel));
            data.broadcasterExperience = Mathf.Max(0, data.broadcasterExperience);
            data.unspentStatPoints = Mathf.Max(0, data.unspentStatPoints);
            data.witRank = Mathf.Clamp(data.witRank, 0, BroadcasterProgression.MaximumRank(settings, BroadcasterStatType.Wit));
            data.ComposureRank = Mathf.Clamp(data.ComposureRank, 0, BroadcasterProgression.MaximumRank(settings, BroadcasterStatType.Composure));
            data.ControlRank = Mathf.Clamp(data.ControlRank, 0, BroadcasterProgression.MaximumRank(settings, BroadcasterStatType.Control));
            data.witInvestedPoints = NormalizeInvestedPoints(settings, data, BroadcasterStatType.Wit, data.witInvestedPoints);
            data.composureInvestedPoints = NormalizeInvestedPoints(settings, data, BroadcasterStatType.Composure, data.composureInvestedPoints);
            data.controlInvestedPoints = NormalizeInvestedPoints(settings, data, BroadcasterStatType.Control, data.controlInvestedPoints);
            data.unlockedManagerTier = Mathf.Max(0, data.unlockedManagerTier);
            data.hiredManagerTier = Mathf.Clamp(data.hiredManagerTier, 0, data.unlockedManagerTier);
            data.managerUsesRemaining = Mathf.Max(0, data.managerUsesRemaining);
            if (data.leaderboardRecords == null) data.leaderboardRecords = new List<BroadcastLeaderboardRecord>();
            data.pcLevel = Mathf.Clamp(data.pcLevel, 1, RunnerCampaignSettings.MaximumEquipmentLevel);
            data.microphoneLevel = Mathf.Clamp(data.microphoneLevel, 1, RunnerCampaignSettings.MaximumEquipmentLevel);
            data.fitnessLevel = Mathf.Clamp(data.fitnessLevel, 1, RunnerCampaignSettings.MaximumEquipmentLevel);
            data.interiorLevel = Mathf.Clamp(data.interiorLevel, 1, RunnerCampaignSettings.MaximumEquipmentLevel);
            if (data.gameSkill >= settings.maximumGameSkill) data.gameSkillExperience = 0;
            if (data.talkingSkill >= settings.maximumTalkingSkill) data.talkingSkillExperience = 0;
            if (data.healthStat >= settings.maximumHealthStat) data.healthStatExperience = 0;
            if (data.mentalLevel >= settings.maximumMentalLevel) data.mentalExperience = 0;
            data.campaignFailed = false;
            if (data.records == null) data.records = new List<RunnerCampaignDayRecord>();
        }

        private static int NormalizeInvestedPoints(RunnerCampaignSettings settings, RunnerCampaignSaveData data,
            BroadcasterStatType type, int invested)
        {
            int required = BroadcasterProgression.NextUpgradeCost(settings, data, type);
            return required <= 0 ? 0 : Mathf.Clamp(invested, 0, required - 1);
        }

        private static void EnsureLegacyMigrated(RunnerCampaignSettings settings)
        {
            if (settings == null || PlayerPrefs.GetInt(LegacyMigrationKey, 0) != 0) return;
            bool migrationResolved = true;
            if (!string.IsNullOrWhiteSpace(settings.playerPrefsSaveKey) && PlayerPrefs.HasKey(settings.playerPrefsSaveKey)
                && !File.Exists(SlotPath(settings, 1)))
            {
                migrationResolved = false;
                try
                {
                    RunnerCampaignSaveData legacy = JsonUtility.FromJson<RunnerCampaignSaveData>(PlayerPrefs.GetString(settings.playerPrefsSaveKey));
                    if (legacy != null && legacy.day >= 1)
                    {
                        int previousSlot = ActiveSlot;
                        SelectSlot(settings, 1);
                        Migrate(settings, legacy, 1);
                        migrationResolved = Save(settings, legacy, true);
                        SelectSlot(settings, previousSlot);
                        if (migrationResolved) Debug.Log("STREAM ON legacy PlayerPrefs campaign was migrated to save slot 1.");
                    }
                }
                catch (Exception exception) { Debug.LogWarning("STREAM ON legacy save migration failed: " + exception.Message); }
            }
            if (!migrationResolved) return;
            PlayerPrefs.SetInt(LegacyMigrationKey, 1);
            PlayerPrefs.Save();
        }

        private static string SaveDirectory(RunnerCampaignSettings settings) => Path.Combine(Application.persistentDataPath,
            string.IsNullOrWhiteSpace(settings != null ? settings.saveFolderName : null) ? "Saves" : settings.saveFolderName);
        private static string SlotPath(RunnerCampaignSettings settings, int slot) => Path.Combine(SaveDirectory(settings), $"campaign_slot_{slot}.json");
        private static string BackupPath(RunnerCampaignSettings settings, int slot) => SlotPath(settings, slot) + ".bak";
        private static void DeleteIfPresent(string path) { try { if (File.Exists(path)) File.Delete(path); } catch (Exception exception) { Debug.LogWarning("STREAM ON save delete failed: " + exception.Message); } }
    }
}
