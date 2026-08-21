using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
        public float mental;
        public int healthStat;
        public int bestBroadcastScore;
        public string savedAtUtc;
    }

    [Serializable]
    public sealed class RunnerUserSettingsData
    {
        public int version = 1;
        public int activeSaveSlot = 1;
        public float masterVolume = 1f;
        public bool aiChatEnabled = true;
        public int runnerHighScore;
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
            return _cached;
        }

        public static void Save(RunnerUserSettingsData data)
        {
            if (data == null) return;
            _cached = data;
            WriteAtomic(SettingsDirectory, SettingsPath, BackupPath, JsonUtility.ToJson(data, true));
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

    public static class RunnerSaveSession
    {
        public static bool RequireSlotSelection { get; set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetForApplicationStart() => RequireSlotSelection = true;
    }

    public static class RunnerCampaignSaveStore
    {
        public const int CurrentVersion = 4;
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
            mental = settings.startingMental,
            gameSkill = settings.startingGameSkill,
            gameSkillExperience = 0,
            talkingSkill = settings.startingTalkingSkill,
            talkingSkillExperience = 0,
            healthStat = settings.startingHealthStat,
            healthStatExperience = 0,
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
                info.mental = data.mental;
                info.healthStat = data.healthStat;
                info.bestBroadcastScore = data.bestBroadcastScore;
                info.savedAtUtc = data.savedAtUtc;
            }
            else info.corrupted = File.Exists(SlotPath(settings, slot)) || File.Exists(BackupPath(settings, slot));
            return info;
        }

        public static bool TryLoad(RunnerCampaignSettings settings, out RunnerCampaignSaveData data)
        {
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
            return RunnerUserSettingsStore.WriteAtomic(SaveDirectory(settings), SlotPath(settings, slot), BackupPath(settings, slot), JsonUtility.ToJson(data, true));
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
            data.slot = slot;
            data.day = Mathf.Max(1, data.day);
            data.mental = Mathf.Clamp(data.mental, 0f, settings.maximumMental);
            data.gameSkill = Mathf.Clamp(data.gameSkill, 1, settings.maximumGameSkill);
            data.talkingSkill = Mathf.Clamp(data.talkingSkill, 1, settings.maximumTalkingSkill);
            data.healthStat = Mathf.Clamp(data.healthStat, 1, settings.maximumHealthStat);
            if (data.gameSkill >= settings.maximumGameSkill) data.gameSkillExperience = 0;
            if (data.talkingSkill >= settings.maximumTalkingSkill) data.talkingSkillExperience = 0;
            if (data.healthStat >= settings.maximumHealthStat) data.healthStatExperience = 0;
            if (data.records == null) data.records = new List<RunnerCampaignDayRecord>();
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
