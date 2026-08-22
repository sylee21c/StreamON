using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StreamOn.Minigames.Runner
{
    public sealed class PlasticKnightmareBroadcastController : MonoBehaviour, IBroadcastGameSuspendHandler
    {
        [Header("Shared broadcast")]
        [SerializeField] private RunnerCampaignSettings settings;
        [SerializeField] private RunnerChatController chat;
        [SerializeField] private RunnerBroadcastSettlementView settlementView;
        [SerializeField] private TMP_Text remainingTimeText;

        [Header("Editable result balance")]
        [SerializeField, Min(1)] private int scorePerClearedDay = 450;
        [SerializeField, Min(1)] private int coinsPerScorePoint = 20;
        [SerializeField, Min(0)] private int followersPerClearedDay = 8;

        private RunnerCampaignSaveData save;
        private DayNightManager dayNight;
        private bool initialized;
        private bool waitingForNightResolution;
        private bool finishing;
        private float nextAutosave;

        private IEnumerator Start()
        {
            yield return null;
            if (settings == null)
            {
                Debug.LogError("Plastic Knightmare broadcast controller needs RunnerCampaignSettings.", this);
                yield break;
            }
            if (!RunnerCampaignSaveStore.TryLoad(settings, out save))
            {
                save = RunnerCampaignSaveStore.CreateNew(settings);
                RunnerCampaignSaveStore.Save(settings, save, true);
            }
            if (save.plasticKnightmare == null) save.plasticKnightmare = new PlasticKnightmareSaveData();
            RunnerBroadcastSessionStore.BeginOrResume(settings, save);
            dayNight = DayNightManager.Instance ?? FindFirstObjectByType<DayNightManager>();
            RestoreGameState();
            if (dayNight != null) dayNight.OnDayBegin += HandleDayBegin;
            GameOverUIController.NightFailed += HandleNightFailed;
            if (chat == null) chat = FindFirstObjectByType<RunnerChatController>();
            if (settlementView == null) settlementView = FindFirstObjectByType<RunnerBroadcastSettlementView>();
            chat?.BindExternalGame("Plastic Knightmare");
            chat?.ResumeExternalGame();
            chat?.React(RunnerChatEvent.RunStarted);
            initialized = true;
            nextAutosave = Time.unscaledTime + 5f;
            RefreshHud();
        }

        private void OnDestroy()
        {
            if (dayNight != null) dayNight.OnDayBegin -= HandleDayBegin;
            GameOverUIController.NightFailed -= HandleNightFailed;
        }

        private void Update()
        {
            if (!initialized || finishing || Time.timeScale <= 0f) return;
            if (RunnerBroadcastSessionStore.RemainingSeconds > 0f)
            {
                RunnerBroadcastSessionStore.Tick(Time.deltaTime);
                if (Time.unscaledTime >= nextAutosave && dayNight != null && dayNight.CurrentPhase == DayNightManager.Phase.Day)
                {
                    SaveGameState();
                    nextAutosave = Time.unscaledTime + 5f;
                }
            }
            if (RunnerBroadcastSessionStore.RemainingSeconds <= 0f) waitingForNightResolution = true;
            RefreshHud();
        }

        private void HandleDayBegin()
        {
            if (!initialized) return;
            SaveGameState();
            chat?.React(RunnerChatEvent.ObstacleCleared);
            if (waitingForNightResolution) FinishBroadcast();
        }

        private void HandleNightFailed()
        {
            if (!initialized) return;
            chat?.React(RunnerChatEvent.GameOver);
            if (waitingForNightResolution) FinishBroadcast();
            else SaveGameState();
        }

        public bool TrySuspendForGameSwitch()
        {
            if (!initialized) return true;
            if (waitingForNightResolution || RunnerBroadcastSessionStore.RemainingSeconds <= 0f)
            {
                RefreshHud();
                return false;
            }
            // Mid-night enemies are an attempt, not permanent base state. Switching games
            // returns to the same Day while keeping the base, inventory and upgrades.
            if (dayNight != null && dayNight.CurrentPhase == DayNightManager.Phase.Night)
                dayNight.ReturnToCurrentDayAfterGameOver();
            SaveGameState();
            RunnerBroadcastSessionStore.SaveProgress(settings);
            initialized = false;
            return true;
        }

        private void SaveGameState()
        {
            if (save == null) return;
            PlasticKnightmareSaveData state = save.plasticKnightmare ??= new PlasticKnightmareSaveData();
            state.initialized = true;
            state.day = dayNight != null ? dayNight.DayCount : Mathf.Max(1, state.day);
            state.coins = CoinWallet.Instance != null ? CoinWallet.Instance.Coins : state.coins;
            state.brickInventory.Clear();
            if (BrickInventory.Instance != null)
                foreach (KeyValuePair<string, int> pair in BrickInventory.Instance.CaptureCounts())
                    state.brickInventory.Add(new PlasticKnightmareInventoryEntry { id = pair.Key, count = pair.Value });
            state.companionInventory.Clear();
            if (CompanionInventory.Instance != null)
            {
                Dictionary<int, string> slots = CompanionInventory.Instance.CaptureSlots();
                foreach (KeyValuePair<string, int> pair in CompanionInventory.Instance.CaptureCounts())
                {
                    int slot = slots.FirstOrDefault(candidate => candidate.Value == pair.Key).Key;
                    if (!slots.ContainsKey(slot) || slots[slot] != pair.Key) slot = -1;
                    state.companionInventory.Add(new PlasticKnightmareInventoryEntry { id = pair.Key, count = pair.Value, slot = slot });
                }
            }
            BuildingModeController building = FindFirstObjectByType<BuildingModeController>();
            if (building != null)
            {
                state.placedBricks = building.CapturePlacedBricks();
                state.placedCompanions = building.CapturePlacedCompanions();
            }
            foreach (UpgradeShopItem upgrade in FindObjectsByType<UpgradeShopItem>(FindObjectsSortMode.None))
            {
                if (upgrade.Type == UpgradeShopItem.UpgradeType.AttackDamage) state.attackUpgradeLevel = upgrade.CurrentLevelIndex;
                else if (upgrade.Type == UpgradeShopItem.UpgradeType.MaxHealth) state.healthUpgradeLevel = upgrade.CurrentLevelIndex;
            }
            RunnerBroadcastSessionStore.ApplyToSave(save);
            RunnerCampaignSaveStore.Save(settings, save, true);
        }

        private void RestoreGameState()
        {
            PlasticKnightmareSaveData state = save.plasticKnightmare;
            if (!state.initialized) return;
            CoinWallet.EnsureExists();
            CoinWallet.Instance?.SetCoins(state.coins);
            BrickInventory.EnsureExists();
            BrickInventory.Instance?.RestoreCounts(state.brickInventory.Select(item =>
                new KeyValuePair<string, int>(item.id, item.count)));
            CompanionInventory.EnsureExists();
            CompanionInventory.Instance?.ClearAndRestore(
                state.companionInventory.Where(item => item.slot >= 0).Select(item => new KeyValuePair<int, string>(item.slot, item.id)),
                state.companionInventory.Select(item => new KeyValuePair<string, int>(item.id, item.count)));
            dayNight?.RestoreDay(state.day);
            foreach (UpgradeShopItem upgrade in FindObjectsByType<UpgradeShopItem>(FindObjectsSortMode.None))
                upgrade.RestoreLevel(upgrade.Type == UpgradeShopItem.UpgradeType.AttackDamage
                    ? state.attackUpgradeLevel : state.healthUpgradeLevel);
            BuildingModeController building = FindFirstObjectByType<BuildingModeController>();
            if (building != null)
            {
                building.RestorePlacedBricks(state.placedBricks);
                building.RestorePlacedCompanions(state.placedCompanions);
            }
        }

        private void FinishBroadcast()
        {
            if (finishing || save == null) return;
            finishing = true;
            SaveGameState();
            RunnerBroadcastSessionStore.End(settings, save);
            int clearedDays = Mathf.Max(0, (save.plasticKnightmare?.day ?? 1) - 1);
            int score = clearedDays * scorePerClearedDay + (CoinWallet.Instance != null ? CoinWallet.Instance.Coins / coinsPerScorePoint : 0);
            int subscriberDelta = clearedDays * followersPerClearedDay;
            save.subscribers = Mathf.Max(0, save.subscribers + subscriberDelta);
            save.bestBroadcastScore = Mathf.Max(save.bestBroadcastScore, score);
            save.broadcastPending = false;
            save.awaitingAdvance = true;
            RunnerCampaignSaveStore.Save(settings, save, true);
            chat?.React(RunnerChatEvent.BroadcastCompleted);
            RunnerSettlementDisplayData display = new RunnerSettlementDisplayData
            {
                gameTitle = "Plastic Knightmare", score = score,
                targetScore = settings.TargetScoreForDay(save.day), hitsTaken = 0,
                subscriberDelta = subscriberDelta, subscribersAfter = save.subscribers,
                mentalLevel = save.mentalLevel, cashAfter = save.cash
            };
            if (settlementView != null) settlementView.Show(display, ReturnToRoom, "다음 날");
            else ReturnToRoom();
        }

        private void ReturnToRoom() => SceneManager.LoadScene(settings.roomSceneName);

        private void RefreshHud()
        {
            if (remainingTimeText == null) return;
            int seconds = Mathf.CeilToInt(RunnerBroadcastSessionStore.RemainingSeconds);
            remainingTimeText.text = waitingForNightResolution
                ? "방송 종료 대기 · 이번 밤까지"
                : $"방송 {seconds / 60:00}:{seconds % 60:00}";
        }
    }
}
