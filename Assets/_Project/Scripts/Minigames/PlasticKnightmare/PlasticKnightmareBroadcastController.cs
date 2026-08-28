using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace StreamOn.Minigames.Runner
{
    public sealed class PlasticKnightmareBroadcastController : MonoBehaviour, IBroadcastGameSuspendHandler
    {
        [Header("Scene-authored references")]
        public RunnerCampaignSettings settings;
        public RunnerBroadcastGrowthSettings growthSettings;
        public RunnerChatController chat;
        public RunnerDonationPopupController donationPopup;
        public RunnerWitInteractionController witInteraction;
        public RunnerBroadcastSettlementView settlementView;
        public TMP_Text phaseTimeText;
        public TMP_Text scoreText;
        public TMP_Text bestScoreText;
        public TMP_Text nightText;
        public Button startNightButton;
        public TMP_Text maintenanceButtonText;

        [Header("Inspector balance")]
        [Min(0f)] public float heatGainPerGhost = 0.25f;
        [FormerlySerializedAs("heatLossPerProtectedObjectHit")]
        [Tooltip("침대 피격 또는 플레이어/방벽/동료 등의 체력이 0이 될 때 적용되는 열기 변화")]
        public float heatLossPerProtectedObjectLoss = -0.8f;
        public float correctModerationHeat = 7f;
        public float wrongModerationHeat = -14f;
        public float fraternizationHeatPerTick = -1.4f;
        public float fraternizationResolvedHeat = 7f;
        [Min(0.1f)] public float eventHeatChangePerSecond = 3f;
        [Min(0.1f)] public float heatSampleIntervalSeconds = 0.25f;
        [Header("Endless score multipliers")]
        [Min(0.1f)] public float difficultyScoreMultiplierPerAssault = 0.08f;
        [Min(0.1f)] public float comboKeepSeconds = 3f;
        [Min(0f)] public float comboMultiplierPerKill = 0.05f;
        [Min(1f)] public float maximumComboMultiplier = 2.5f;
        [Min(0.1f)] public float fastKillThresholdSeconds = 4f;
        [Min(1f)] public float fastKillScoreMultiplier = 1.2f;
        [Header("Game over flow")]
        [Min(0f)] public float settlementDelayAfterGameOver = 2f;
        [Header("Preparation chat")]
        [Min(1f)] public float preparationIdleFirstReactionSeconds = 25f;
        [Min(1f)] public float preparationIdleRepeatSeconds = 18f;
        [Min(0.1f)] public float preparationActivitySampleInterval = 0.5f;
        [Min(0.1f)] public float bedDamageChatCooldown = 3f;

        public float Heat => _heat;
        public int RawGameScore => _rawScore;
        public int BroadcastScore => Mathf.FloorToInt(_broadcastScore);
        public float DaySecondsRemaining => _daySecondsRemaining;
        public int CurrentViewers => _currentViewers;
        public int PeakViewers => _peakViewers;
        public int ChattingViewers => growthSettings != null ? growthSettings.ChattersForViewers(_currentViewers) : 0;
        public bool CanShowWitInteraction => _initialized && !_finishing && _nightStarted;
        public bool MissionGameplayActive => _initialized && !_finishing && _nightStarted
            && _spawner != null && (_spawner.CurrentState == GhostSpawner.AssaultState.Combat
                || _spawner.CurrentState == GhostSpawner.AssaultState.ClearingRemaining);
        public int GhostsDefeated => _ghostsDefeated;
        public int CurrentCombo => Time.unscaledTime - _lastKillAt <= comboKeepSeconds ? _combo : 0;
        public int ActiveGhostCount => _spawner != null ? _spawner.ActiveGhostCount : 0;
        public float BedHealth => _bed != null ? _bed.CurrentHealth : 0f;
        public float PlayerHealth
        {
            get
            {
                PlayerController player = FindFirstObjectByType<PlayerController>();
                Damageable health = player != null ? player.GetComponent<Damageable>() : null;
                return health != null ? health.CurrentHealth : 0f;
            }
        }
        public int DamagedFacilityCount => _protectedObjects.Count(candidate => candidate != null
            && candidate != _bed && candidate.GetComponent<PlayerController>() == null
            && (candidate.IsDead || candidate.CurrentHealth < candidate.MaxHealth));

        private RunnerCampaignSaveData _save;
        private DayNightManager _dayNight;
        private GhostSpawner _spawner;
        private Damageable _bed;
        private readonly List<Damageable> _protectedObjects = new List<Damageable>();
        private readonly Dictionary<Damageable, Action> _protectedDeathHandlers = new Dictionary<Damageable, Action>();
        private float _lastBedHealth;
        private bool _initialized;
        private bool _nightStarted;
        private bool _finishing;
        private bool _gameOverPending;
        private int _night;
        private int _rawScore;
        private float _broadcastScore;
        private float _heat = 50f;
        private float _bufferedHeat;
        private float _daySecondsRemaining;
        private float _heatSampleTotal;
        private float _heatSampleSeconds;
        private float _nextHeatSampleAt;
        private int _ghostsDefeated;
        private float _nightStartedAt;
        private int _startingFollowers;
        private int _currentViewers;
        private int _peakViewers;
        private int _totalVisitors;
        private float _viewerSeconds;
        private float _nextViewerUpdateAt;
        private float _nextAmbientDonationAt;
        private float _nextDonationAt;
        private int _liveDonationWon;
        private float _socialFollowerPenalty;
        private int _moderationFollowerBonus;
        private int _webcamLevel = 1;
        private readonly RunnerBroadcastPerformanceMeter _performanceMeter = new RunnerBroadcastPerformanceMeter();
        private bool _cleanMistakeProtectionUsed;
        private bool _largePenaltyProtectionUsed;
        private float _nextMistakeClearAt;
        private int _combo;
        private float _lastKillAt = float.NegativeInfinity;
        private float _lastPreparationActivityAt;
        private float _nextPreparationIdleReactionAt;
        private float _nextPreparationActivitySampleAt;
        private float _nextBedDamageChatAt;
        private string _preparationActivityFingerprint = string.Empty;
        private string _cachedAffordablePurchases = string.Empty;
        private string _cachedAffordableCompanionNames = string.Empty;
        private string _cachedAffordableBrickNames = string.Empty;
        private string _cachedAffordableUpgradeNames = string.Empty;
        private string _cachedCompanionNames = string.Empty;
        private string _cachedCompanionRoster = string.Empty;

        private void Awake() => startNightButton?.onClick.AddListener(BeginNightEarly);

        private IEnumerator Start()
        {
            yield return null;
            if (settings == null)
            {
                Debug.LogError("Plastic Knightmare 방송 설정이 연결되지 않았습니다.", this);
                yield break;
            }
            if (!RunnerCampaignSaveStore.TryLoad(settings, out _save))
            {
                _save = RunnerCampaignSaveStore.CreateNew(settings);
                RunnerCampaignSaveStore.Save(settings, _save, true);
            }
            if (!RunnerBroadcastSessionStore.BeginOrResume(settings, _save, BroadcastGameId.PlasticKnightmare))
            {
                Debug.LogError("이미 다른 게임 방송이 진행 중입니다.", this);
                yield break;
            }
            _save.selectedBroadcastGame = BroadcastGameId.PlasticKnightmare.ToString();
            RunnerCampaignSaveStore.Save(settings, _save, true);
            _dayNight = DayNightManager.Instance ?? FindFirstObjectByType<DayNightManager>();
            ResetTransientRunState();
            _night = 1;
            _daySecondsRemaining = Mathf.Max(1f, settings.plasticDayPreparationSeconds);
            _startingFollowers = _save.subscribers;
            _webcamLevel = Mathf.Clamp(_save.interiorLevel, 1, RunnerCampaignSettings.MaximumEquipmentLevel);
            _heat = growthSettings != null ? growthSettings.startingHype : 50f;
            _currentViewers = Mathf.Max(0, Mathf.RoundToInt(settings.plasticStartingViewers
                + _startingFollowers * settings.ViewerRatioForWebcamLevel(_webcamLevel)));
            _peakViewers = _currentViewers;
            _totalVisitors = _currentViewers;
            _nextViewerUpdateAt = Time.unscaledTime;
            _performanceMeter.Reset(Time.unscaledTime);
            ScheduleNextAmbientDonation();
            if (_dayNight != null)
            {
                _dayNight.OnNightBegin += HandleNightBegin;
                _dayNight.OnDayBegin += HandleDayBegin;
            }
            _spawner = FindFirstObjectByType<GhostSpawner>();
            if (_spawner != null) _spawner.StateChanged += HandleAssaultStateChanged;
            GhostSpawner.GhostDefeatedDetailed += HandleGhostDefeated;
            GameOverUIController.NightFailed += HandleNightFailed;
            if (chat == null) chat = FindFirstObjectByType<RunnerChatController>();
            if (donationPopup == null) donationPopup = FindFirstObjectByType<RunnerDonationPopupController>();
            if (witInteraction == null) witInteraction = FindFirstObjectByType<RunnerWitInteractionController>();
            chat?.ConfigureCampaignSettings(settings);
            chat?.BindExternalGame("Plastic Knightmare");
            RunnerBroadcastHeatGauge.Show(_heat);
            _initialized = true;
            ResetPreparationActivityTracking();
            if (maintenanceButtonText != null) maintenanceButtonText.text = "스킵";
            RefreshHud();
            PushChatSnapshot(SharedChatGameState.Ready);
            chat?.React(RunnerChatEvent.PlasticPreparationStarted);
        }

        private void OnDestroy()
        {
            if (_dayNight != null)
            {
                _dayNight.OnNightBegin -= HandleNightBegin;
                _dayNight.OnDayBegin -= HandleDayBegin;
            }
            if (_spawner != null) _spawner.StateChanged -= HandleAssaultStateChanged;
            GhostSpawner.GhostDefeatedDetailed -= HandleGhostDefeated;
            GameOverUIController.NightFailed -= HandleNightFailed;
            UnsubscribeProtectedObjects();
        }

        private void Update()
        {
            if (!_initialized || _finishing || _gameOverPending || Time.timeScale <= 0f) return;
            UpdatePreparationChat();
            if (!_nightStarted)
            {
                _daySecondsRemaining = Mathf.Max(0f, _daySecondsRemaining - Time.unscaledDeltaTime);
                if (_daySecondsRemaining <= 0f) BeginNightEarly();
            }
            else
            {
                _viewerSeconds += _currentViewers * Time.unscaledDeltaTime;
                ComposureRankRule mental = CurrentComposureRule();
                float performanceStep = _performanceMeter.Tick(Time.unscaledTime, growthSettings,
                    mental != null ? mental.poorStateTickInterval : -1f,
                    mental != null ? mental.extraMistakesRequiredForPoorState : 0,
                    mental != null ? mental.neutralRecoveryTimeReduction : 0f);
                if (!Mathf.Approximately(performanceStep, 0f)) AddHeat(performanceStep, false);
                if (_performanceMeter.State != BroadcastPerformanceState.Good) _cleanMistakeProtectionUsed = false;
                ApplyBufferedHeat(Time.unscaledDeltaTime);
                if (Time.unscaledTime >= _nextHeatSampleAt)
                {
                    float sample = Mathf.Max(0.01f, heatSampleIntervalSeconds);
                    _heatSampleTotal += _heat * sample;
                    _heatSampleSeconds += sample;
                    _nextHeatSampleAt = Time.unscaledTime + sample;
                }
                TryAmbientDonation();
            }
            PushChatSnapshot(_nightStarted ? SharedChatGameState.Playing : SharedChatGameState.Ready);
            if (Time.unscaledTime >= _nextViewerUpdateAt) UpdateAudience();
            RunnerBroadcastHeatGauge.SetValue(_heat);
            RefreshHud();
        }

        public void BeginNightEarly()
        {
            if (!_initialized || _finishing) return;
            if (_nightStarted)
            {
                _spawner?.EndMaintenanceEarly();
                return;
            }
            if (_dayNight == null) return;
            _daySecondsRemaining = 0f;
            _dayNight.BeginNight();
        }

        private void HandleNightBegin()
        {
            if (_finishing) return;
            if (_nightStarted)
            {
                if (startNightButton != null) startNightButton.gameObject.SetActive(false);
                SubscribeNewProtectedObjects();
                PushChatSnapshot(SharedChatGameState.Playing);
                chat?.React(RunnerChatEvent.PlasticNightStarted);
                return;
            }
            _nightStarted = true;
            if (startNightButton != null) startNightButton.gameObject.SetActive(false);
            _nightStartedAt = Time.unscaledTime;
            _heat = Mathf.Clamp(_heat, settings.plasticNightStartingHeatMinimum, settings.plasticNightStartingHeatMaximum);
            _heatSampleTotal = 0f;
            _heatSampleSeconds = 0f;
            _nextHeatSampleAt = Time.unscaledTime;
            SubscribeProtectedObjects();
            UpdateAudience();
            chat?.ResumeExternalGame();
            chat?.React(RunnerChatEvent.PlasticNightStarted);
            PushChatSnapshot(SharedChatGameState.Playing);
        }

        private void HandleDayBegin()
        {
            ResetPreparationActivityTracking();
            PushChatSnapshot(SharedChatGameState.Playing);
            chat?.React(RunnerChatEvent.PlasticPreparationStarted);
        }

        private void HandleNightFailed()
        {
            if (!_initialized || _finishing || _gameOverPending) return;
            _gameOverPending = true;
            _spawner?.NotifyGameOver();
            int previousBest = _save != null ? _save.bestPlasticGameScoreAtNight : 0;
            GameOverUIController.ConfigureFinalResult(_rawScore, previousBest,
                _spawner != null ? _spawner.CurrentAssault : _night, _ghostsDefeated,
                _nightStarted ? Time.unscaledTime - _nightStartedAt : 0f);
            GameOverUIController.PrepareForBroadcastSettlement();
            chat?.React(RunnerChatEvent.GameOver);
            StartCoroutine(ShowSettlementAfterGameOver());
        }

        private IEnumerator ShowSettlementAfterGameOver()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, settlementDelayAfterGameOver));
            FinishBroadcast(false, _spawner != null ? _spawner.CurrentAssault : _night);
        }

        private void HandleGhostDefeated(GhostSpawner.GhostDefeatInfo defeat)
        {
            if (!_nightStarted || _finishing) return;
            _ghostsDefeated++;
            _night = Mathf.Max(1, defeat.Assault);
            _combo = Time.unscaledTime - _lastKillAt <= comboKeepSeconds ? _combo + 1 : 1;
            _lastKillAt = Time.unscaledTime;
            float difficulty = 1f + Mathf.Max(0, defeat.Assault - 1) * difficultyScoreMultiplierPerAssault;
            float combo = Mathf.Min(maximumComboMultiplier, 1f + Mathf.Max(0, _combo - 1) * comboMultiplierPerKill);
            float fastKill = defeat.LifetimeSeconds <= fastKillThresholdSeconds ? fastKillScoreMultiplier : 1f;
            AddFiniteScore(Mathf.RoundToInt(Mathf.Max(0, defeat.BaseScore) * difficulty * combo * fastKill), _heat);
            _performanceMeter.RecordSuccess(Time.unscaledTime, growthSettings);
            AddHeat(heatGainPerGhost, false);
            TryLiveDonation(settings.plasticGhostDonationChance, "방금 유령 잡는 거 좋았다");
            witInteraction?.NotifySafeMoment("Plastic Knightmare에서 방금 유령을 처치함", 3f);
            chat?.React(defeat.TierIndex > 0
                ? RunnerChatEvent.PlasticStrongGhostDefeated
                : RunnerChatEvent.EnemyDefeated);
        }

        private void HandleAssaultStateChanged(GhostSpawner.AssaultState state, int assault)
        {
            _night = Mathf.Max(1, assault);
            bool maintenance = state == GhostSpawner.AssaultState.Maintenance;
            if (startNightButton != null) startNightButton.gameObject.SetActive(maintenance);
            if (maintenanceButtonText != null) maintenanceButtonText.text = "스킵";
            if (state == GhostSpawner.AssaultState.Combat)
                SubscribeNewProtectedObjects();
            if (state == GhostSpawner.AssaultState.Combat && assault > 1)
                witInteraction?.NotifySafeMoment("다음 공세가 감지됨", 2f);
        }

        private PlasticNightScoreRule CurrentNightRule(int night)
        {
            PlasticNightScoreRule result = settings.plasticNightScoreRules?
                .Where(candidate => candidate != null && candidate.night <= night)
                .OrderBy(candidate => candidate.night).LastOrDefault();
            return result ?? new PlasticNightScoreRule();
        }

        private void AddFiniteScore(int rawPoints, float heat)
        {
            if (rawPoints <= 0) return;
            _rawScore += rawPoints;
            _broadcastScore += rawPoints * settings.HeatScoreMultiplier(heat);
        }

        private void AddHeat(float amount, bool applyMentalReduction = true)
        {
            if (amount < 0f && applyMentalReduction)
            {
                ComposureRankRule mental = CurrentComposureRule();
                float reduction = mental != null ? mental.ordinaryPenaltyReduction : 0f;
                if (!_largePenaltyProtectionUsed && Mathf.Abs(amount) >= settings.largeHeatPenaltyThreshold
                    && mental != null && mental.oncePerBroadcastLargePenaltyReduction > 0f)
                {
                    reduction = Mathf.Max(reduction, mental.oncePerBroadcastLargePenaltyReduction);
                    _largePenaltyProtectionUsed = true;
                }
                amount *= 1f - Mathf.Clamp01(reduction);
            }
            _bufferedHeat += amount;
            float maximum = growthSettings != null ? growthSettings.maximumBufferedHeatChange : 20f;
            _bufferedHeat = Mathf.Clamp(_bufferedHeat, -maximum, maximum);
        }

        private void ApplyBufferedHeat(float delta)
        {
            if (Mathf.Approximately(_bufferedHeat, 0f)) return;
            float change = Mathf.Sign(_bufferedHeat) * Mathf.Min(Mathf.Abs(_bufferedHeat), eventHeatChangePerSecond * delta);
            _heat = Mathf.Clamp(_heat + change, 0f, 100f);
            _bufferedHeat -= change;
        }

        public void ApplyMissionOutcome(float heatChange, int donationReward)
        {
            if (!_initialized || _finishing) return;
            AddHeat(heatChange);
            _liveDonationWon = Mathf.Min(settings != null ? settings.plasticDonationCapPerNight : int.MaxValue,
                _liveDonationWon + Mathf.Max(0, donationReward));
            RefreshChatScale();
        }

        public void OnModerationResult(bool correct)
        {
            AddHeat(correct ? correctModerationHeat : wrongModerationHeat, correct);
            if (correct) _moderationFollowerBonus++;
        }

        public void RemoveBannedViewer()
        {
            if (_currentViewers <= 0) return;
            _currentViewers--;
            RefreshChatScale();
            RefreshHud();
            PushChatSnapshot(_nightStarted ? SharedChatGameState.Playing : SharedChatGameState.Ready);
        }

        public void OnFraternizationTick()
        {
            AddHeat(fraternizationHeatPerTick);
            int leaving = growthSettings != null ? Mathf.Max(growthSettings.socialMinimumViewerLeave,
                Mathf.CeilToInt(_currentViewers * growthSettings.socialViewerLeaveFraction)) : 0;
            _currentViewers = Mathf.Max(0, _currentViewers - leaving);
            if (growthSettings != null) _socialFollowerPenalty += Mathf.Max(growthSettings.socialFollowerPenaltyMinimum,
                _startingFollowers * growthSettings.socialFollowerPenaltyPerFollower);
            RefreshChatScale();
        }
        public void OnFraternizationResolved(float responseSeconds)
        {
            AddHeat(fraternizationResolvedHeat, false);
            if (growthSettings == null) return;
            float quick = Mathf.InverseLerp(growthSettings.slowFraternizationResponseSeconds,
                growthSettings.quickFraternizationResponseSeconds, responseSeconds);
            int returning = Mathf.Max(growthSettings.socialMinimumViewerLeave, Mathf.CeilToInt(_currentViewers
                * Mathf.Lerp(growthSettings.socialResolutionViewerReturnMinimum,
                    growthSettings.socialResolutionViewerReturnMaximum, quick)));
            _currentViewers = Mathf.Min(settings.plasticMaximumViewers, _currentViewers + returning);
            _totalVisitors += returning;
            _peakViewers = Mathf.Max(_peakViewers, _currentViewers);
            _moderationFollowerBonus += Mathf.RoundToInt(Mathf.Lerp(0f,
                growthSettings.socialResolutionFollowerBonusMaximum, quick));
            RefreshChatScale();
        }

        public void ApplyWitInteraction(int quality)
        {
            if (!_initialized || growthSettings == null) return;
            if (quality >= 2)
            {
                WitRankRule wit = CurrentWitRule();
                ComposureRankRule mental = CurrentComposureRule();
                float perk = quality >= 5 ? (wit != null ? wit.comebackRewardMultiplier : 1f)
                    : quality >= 4 ? (wit != null ? wit.correctStreakRewardMultiplier : 1f)
                    : quality >= 3 ? (wit != null ? wit.advancedAnswerRewardMultiplier : 1f) : 1f;
                AddHeat(growthSettings.witSuccessHype * (1f + (wit != null ? wit.correctHeatGainBonus : 0f)) * perk, false);
                if (mental != null && mental.correctWitClearsRecentMistakes && Time.time >= _nextMistakeClearAt)
                {
                    _performanceMeter.ClearRecentMistakes();
                    _nextMistakeClearAt = Time.time + mental.mistakeClearCooldownSeconds;
                }
                TryLiveDonation(growthSettings.witSuccessDonationChance, "이런 받아치기 좋다 ㅋㅋ");
                chat?.React(RunnerChatEvent.WitReplySuccess);
            }
            else if (quality == 1)
            {
                AddHeat(growthSettings.witOkayHype, false);
                chat?.React(RunnerChatEvent.WitReplyOkay);
            }
            else
            {
                AddHeat(growthSettings.witAwkwardHype);
                chat?.React(RunnerChatEvent.WitReplyAwkward);
            }
        }

        private void SubscribeProtectedObjects()
        {
            UnsubscribeProtectedObjects();
            SubscribeNewProtectedObjects();
        }

        private void SubscribeNewProtectedObjects()
        {
            foreach (Damageable damageable in FindObjectsByType<Damageable>(FindObjectsSortMode.None))
            {
                if (damageable == null || damageable.GetComponent<GhostAI>() != null
                    || _protectedObjects.Contains(damageable)) continue;
                _protectedObjects.Add(damageable);
                if (damageable.CompareTag("Bed") || damageable.name == "Bed")
                {
                    _bed = damageable;
                    _lastBedHealth = damageable.CurrentHealth;
                    damageable.OnHealthChanged += HandleBedHealthChanged;
                    continue;
                }

                Damageable captured = damageable;
                Action deathHandler = () => HandleProtectedObjectDestroyed(captured);
                _protectedDeathHandlers[damageable] = deathHandler;
                damageable.OnDeath += deathHandler;
            }
        }

        private void UnsubscribeProtectedObjects()
        {
            if (_bed != null) _bed.OnHealthChanged -= HandleBedHealthChanged;
            foreach (KeyValuePair<Damageable, Action> pair in _protectedDeathHandlers)
                if (pair.Key != null) pair.Key.OnDeath -= pair.Value;
            _protectedDeathHandlers.Clear();
            _protectedObjects.Clear();
            _bed = null;
            _lastBedHealth = 0f;
        }

        private void HandleBedHealthChanged(float current, float maximum)
        {
            bool tookDamage = current < _lastBedHealth - 0.01f;
            _lastBedHealth = current;
            if (!_nightStarted || !tookDamage) return;
            RegisterProtectedObjectFailure();
            if (Time.unscaledTime >= _nextBedDamageChatAt)
            {
                _nextBedDamageChatAt = Time.unscaledTime + Mathf.Max(0.1f, bedDamageChatCooldown);
                chat?.React(RunnerChatEvent.PlasticBedDamaged);
            }
        }

        private void HandleProtectedObjectDestroyed(Damageable damageable)
        {
            if (!_nightStarted || damageable == null || damageable == _bed) return;
            RegisterProtectedObjectFailure();
            chat?.React(damageable.GetComponent<PlayerController>() != null
                ? RunnerChatEvent.PlasticPlayerStunned
                : RunnerChatEvent.PlasticDefenseDestroyed);
        }

        private void RegisterProtectedObjectFailure()
        {
            ComposureRankRule mental = CurrentComposureRule();
            if (mental != null && mental.protectsFirstMistakeAfterGoodPlay
                && _performanceMeter.State == BroadcastPerformanceState.Good && !_cleanMistakeProtectionUsed)
            {
                _cleanMistakeProtectionUsed = true;
                return;
            }
            _performanceMeter.RecordMistake(Time.unscaledTime, growthSettings);
            AddHeat(heatLossPerProtectedObjectLoss);
        }

        private ComposureRankRule CurrentComposureRule()
        {
            if (settings == null || _save == null) return null;
            return settings.ComposureRule(_save.ComposureRank);
        }

        private WitRankRule CurrentWitRule()
        {
            if (settings == null || _save == null) return null;
            return settings.WitRule(_save.witRank);
        }

        private void UpdateAudience()
        {
            if (growthSettings == null || settings == null) return;
            _nextViewerUpdateAt = Time.unscaledTime + Mathf.Max(.5f, growthSettings.viewerUpdateInterval);
            float target = settings.plasticStartingViewers
                + _startingFollowers * settings.ViewerRatioForWebcamLevel(_webcamLevel)
                + _heat * growthSettings.viewersPerHypePoint
                + Mathf.Max(0, _night - 1) * settings.plasticViewersPerNight;
            float heat01 = _heat / 100f;
            target *= Mathf.Lerp(growthSettings.viewerTargetMultiplierAtZeroHeat,
                growthSettings.viewerTargetMultiplierAtFullHeat, heat01);
            target *= UnityEngine.Random.Range(1f - growthSettings.viewerRandomVariation,
                1f + growthSettings.viewerRandomVariation);
            bool growing = target >= _currentViewers;
            float adjustment = growthSettings.viewerAdjustmentRate * (growing
                ? Mathf.Lerp(growthSettings.viewerGrowthRateMultiplierAtZeroHeat,
                    growthSettings.viewerGrowthRateMultiplierAtFullHeat, heat01)
                : Mathf.Lerp(growthSettings.viewerDeclineRateMultiplierAtZeroHeat,
                    growthSettings.viewerDeclineRateMultiplierAtFullHeat, heat01));
            int next = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(_currentViewers, target,
                Mathf.Clamp01(adjustment))), 0, settings.plasticMaximumViewers);
            if (next > _currentViewers) _totalVisitors += next - _currentViewers;
            _currentViewers = next;
            _peakViewers = Mathf.Max(_peakViewers, _currentViewers);
            RefreshChatScale();
        }

        private void RefreshChatScale()
        {
            if (chat == null || growthSettings == null) return;
            chat.ConfigureAudience(_currentViewers, ChattingViewers,
                growthSettings.ChatDelayMultiplierForViewers(_currentViewers),
                growthSettings.EventReactionChanceForViewers(_currentViewers)
                    * Mathf.Lerp(growthSettings.eventReactionMultiplierAtZeroHeat,
                        growthSettings.eventReactionMultiplierAtFullHeat, _heat / 100f),
                growthSettings.EventCooldownForViewers(_currentViewers), _heat);
        }

        private void TryLiveDonation(float chance, string message)
        {
            if (!_nightStarted || growthSettings == null || _currentViewers < growthSettings.minimumViewersForDonation
                || Time.time < _nextDonationAt || _liveDonationWon >= settings.plasticDonationCapPerNight) return;
            chance *= Mathf.Lerp(growthSettings.donationEventChanceMultiplierAtZeroHeat,
                growthSettings.donationEventChanceMultiplierAtFullHeat, _heat / 100f);
            if (UnityEngine.Random.value > chance) return;
            GrantLiveDonation(message);
        }

        private void TryAmbientDonation()
        {
            if (!_nightStarted || growthSettings == null || !growthSettings.enableAmbientDonations
                || Time.time < _nextAmbientDonationAt) return;
            if (_currentViewers >= growthSettings.minimumViewersForDonation && Time.time >= _nextDonationAt)
            {
                string[] messages = growthSettings.ambientDonationMessages;
                GrantLiveDonation(messages != null && messages.Length > 0
                    ? messages[UnityEngine.Random.Range(0, messages.Length)] : "계속 가보자");
            }
            else ScheduleNextAmbientDonation();
        }

        private void GrantLiveDonation(string message)
        {
            int room = Mathf.Max(0, settings.plasticDonationCapPerNight - _liveDonationWon);
            int amount = Mathf.Min(room, RunnerBroadcastAudienceController.RollDonationAmount(growthSettings, _heat));
            if (amount <= 0) return;
            _liveDonationWon += amount;
            _nextDonationAt = Time.time + growthSettings.liveDonationCooldown;
            ScheduleNextAmbientDonation();
            string donor = chat != null ? chat.PickDonationViewerNickname() : "익명의 시청자";
            bool isLargeDonation = amount >= growthSettings.largeDonationWon;
            donationPopup?.ShowDonation(donor, amount, message, isLargeDonation);
            chat?.OnDonationReceived(donor, amount, message, isLargeDonation);
        }

        private void ScheduleNextAmbientDonation()
        {
            if (growthSettings == null)
            {
                _nextAmbientDonationAt = float.PositiveInfinity;
                return;
            }
            float minimum = Mathf.Max(growthSettings.liveDonationCooldown, growthSettings.ambientDonationMinimumInterval);
            float maximum = Mathf.Max(minimum, growthSettings.ambientDonationMaximumInterval);
            float heatInterval = Mathf.Lerp(growthSettings.donationIntervalMultiplierAtZeroHeat,
                growthSettings.donationIntervalMultiplierAtFullHeat, _heat / 100f);
            _nextAmbientDonationAt = Time.time + UnityEngine.Random.Range(minimum, maximum) * heatInterval;
        }

        private void FinishBroadcast(bool cleared, int clearedNight)
        {
            if (_finishing || _save == null) return;
            _finishing = true;
            if (RunnerCampaignSaveStore.TryLoad(settings, out RunnerCampaignSaveData stagedSave)) _save = stagedSave;
            float averageHeat = _heatSampleSeconds > 0f ? _heatSampleTotal / _heatSampleSeconds : _heat;
            int survivingFacilities = _protectedObjects.Count(candidate => candidate != null && !candidate.IsDead
                && candidate != _bed && candidate.GetComponent<PlayerController>() == null);
            float bedRatio = _bed != null ? Mathf.Clamp01(_bed.CurrentHealth / Mathf.Max(1f, _bed.MaxHealth)) : 0f;
            PlasticNightScoreRule rule = CurrentNightRule(clearedNight);
            SaveGameState();
            int previousBestNight = _save.bestPlasticNight;
            int previousBestScore = _save.bestPlasticGameScoreAtNight;
            _save.bestPlasticNight = Mathf.Max(_save.bestPlasticNight, clearedNight);
            _save.bestPlasticBroadcastScoreAtNight = Mathf.Max(_save.bestPlasticBroadcastScoreAtNight, BroadcastScore);
            _save.bestPlasticGameScoreAtNight = Mathf.Max(_save.bestPlasticGameScoreAtNight, RawGameScore);
            _save.bestBroadcastScore = Mathf.Max(_save.bestBroadcastScore, BroadcastScore);

            float averageViewers = _heatSampleSeconds > 0f ? _viewerSeconds / _heatSampleSeconds : _currentViewers;
            float hostingRating = Mathf.Lerp(1f, 5f, averageHeat / 100f);
            int ratingTargetScore = Mathf.Max(1,
                Mathf.RoundToInt(rule.clearBonus * settings.plasticRatingScoreTargetMultiplier));
            float finalRating = RunnerBroadcastAudienceController.RatingFromBroadcastScore(BroadcastScore, ratingTargetScore);
            float conversion = growthSettings != null
                ? Mathf.Clamp(growthSettings.baseFollowConversion
                    + (finalRating - 1f) * growthSettings.followConversionPerRatingPoint
                    + growthSettings.completionFollowBonus, 0f, growthSettings.maximumFollowConversion)
                : 0f;
            int gainedFollowers = Mathf.RoundToInt(_totalVisitors * conversion)
                + settings.plasticFollowerGainPerClearedNight
                + Mathf.FloorToInt(averageHeat / Mathf.Max(1f, settings.plasticFollowerHeatBandSize)) * settings.plasticFollowersPerHeatBand
                + _moderationFollowerBonus;
            int lostFollowers = Mathf.CeilToInt(_socialFollowerPenalty);
            int followerDelta = gainedFollowers - lostFollowers;
            _save.subscribers = Mathf.Max(0, _save.subscribers + followerDelta);
            int performanceDonation = Mathf.Max(0, Mathf.RoundToInt(BroadcastScore * settings.plasticDonationWonPerBroadcastPoint));
            int donation = Mathf.Min(settings.plasticDonationCapPerNight, _liveDonationWon + performanceDonation);
            _save.cash += donation;
            _save.lifetimeDonations += donation;
            long managerSalary = BroadcasterProgression.ApplyManagerSalary(settings, _save);
            int experience = settings.broadcastCompletionExperience + settings.plasticNightClearExperience
                + Mathf.RoundToInt(finalRating * settings.broadcastRatingExperiencePerPoint)
                + ((RawGameScore > previousBestScore)
                    ? settings.newRecordExperience : 0);
            BroadcasterProgression.AddBroadcastExperience(settings, _save, experience);
            experience = _save.broadcastSessionExperienceEarned;
            int experienceAfter = _save.broadcasterExperience;
            BroadcasterProgression.ExperienceStateBeforeGain(settings, _save.broadcasterLevel, experienceAfter,
                experience, out int levelBefore, out int experienceBefore);
            _save.broadcastSessionActive = false;
            _save.broadcastSessionGameId = string.Empty;
            _save.broadcastPending = false;
            _save.awaitingAdvance = true;
            _save.broadcastSessionExperienceEarned = 0;
            RunnerCampaignSaveStore.Save(settings, _save, true);
            RunnerBroadcastSessionStore.Complete(settings, _save);

            RunnerBroadcastResult result = new RunnerBroadcastResult
            {
                startingFollowers = _startingFollowers,
                endingViewers = _currentViewers,
                peakViewers = _peakViewers,
                totalVisitors = _totalVisitors,
                averageViewers = averageViewers,
                chattingViewers = ChattingViewers,
                gameplayRating = finalRating,
                survivalRating = Mathf.Lerp(1f, 5f, Mathf.Clamp01(clearedNight / 10f)),
                safetyRating = Mathf.Lerp(1f, 5f, bedRatio),
                hostingRating = hostingRating,
                finalRating = finalRating,
                followConversionRate = conversion,
                followersGained = gainedFollowers,
                followersLost = lostFollowers,
                netFollowerChange = followerDelta,
                donationWon = donation
            };
            chat?.React(RunnerChatEvent.BroadcastCompleted);
            RunnerSettlementDisplayData display = new RunnerSettlementDisplayData
            {
                gameTitle = "Plastic Knightmare",
                score = BroadcastScore,
                rawGameScore = RawGameScore,
                broadcastScore = BroadcastScore,
                finalScore = BroadcastScore,
                previousBestScore = previousBestScore,
                isNewRecord = RawGameScore > previousBestScore,
                broadcastCompleted = true,
                targetScore = rule.clearBonus,
                enemiesDefeated = _ghostsDefeated,
                subscriberDelta = followerDelta,
                subscribersAfter = _save.subscribers,
                cashAfter = _save.cash,
                managerSalary = managerSalary,
                mentalLevel = _save.ComposureRank,
                experienceGained = experience,
                experienceBefore = experienceBefore,
                experienceAfter = experienceAfter,
                levelBefore = levelBefore,
                levelAfter = _save.broadcasterLevel,
                broadcastResult = result
            };
            if (settlementView != null) settlementView.Show(display, ReturnToRoom, "방으로 돌아가기");
            else ReturnToRoom();
        }

        public bool TrySuspendForGameSwitch() => !_initialized || _finishing;

        private void ResetPreparationActivityTracking()
        {
            float now = Time.unscaledTime;
            _lastPreparationActivityAt = now;
            _nextPreparationIdleReactionAt = now + Mathf.Max(1f, preparationIdleFirstReactionSeconds);
            _nextPreparationActivitySampleAt = now;
            _preparationActivityFingerprint = CapturePreparationActivityFingerprint();
            RefreshPreparationChatContext();
        }

        private void UpdatePreparationChat()
        {
            if (_dayNight == null || _dayNight.CurrentPhase != DayNightManager.Phase.Day) return;
            float now = Time.unscaledTime;
            if (now >= _nextPreparationActivitySampleAt)
            {
                _nextPreparationActivitySampleAt = now + Mathf.Max(0.1f, preparationActivitySampleInterval);
                string fingerprint = CapturePreparationActivityFingerprint();
                RefreshPreparationChatContext();
                if (!string.Equals(fingerprint, _preparationActivityFingerprint, StringComparison.Ordinal))
                {
                    _preparationActivityFingerprint = fingerprint;
                    _lastPreparationActivityAt = now;
                    _nextPreparationIdleReactionAt = now + Mathf.Max(1f, preparationIdleFirstReactionSeconds);
                }
            }

            if (now < _nextPreparationIdleReactionAt
                || now - _lastPreparationActivityAt < preparationIdleFirstReactionSeconds) return;
            PushChatSnapshot(_nightStarted ? SharedChatGameState.Playing : SharedChatGameState.Ready);
            chat?.React(RunnerChatEvent.PlasticPreparationIdle);
            _nextPreparationIdleReactionAt = now + Mathf.Max(1f, preparationIdleRepeatSeconds);
        }

        private string CapturePreparationActivityFingerprint()
        {
            int coins = CoinWallet.Instance != null ? CoinWallet.Instance.Coins : 0;
            string bricks = BrickInventory.Instance != null
                ? string.Join(",", BrickInventory.Instance.CaptureCounts().OrderBy(pair => pair.Key)
                    .Select(pair => pair.Key + ":" + pair.Value)) : string.Empty;
            string companions = CompanionInventory.Instance != null
                ? string.Join(",", CompanionInventory.Instance.CaptureCounts().OrderBy(pair => pair.Key)
                    .Select(pair => pair.Key + ":" + pair.Value)) : string.Empty;
            string upgrades = string.Join(",", FindObjectsByType<UpgradeShopItem>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).OrderBy(item => item.Type)
                .Select(item => item.Type + ":" + item.CurrentLevelIndex));
            BuildingModeController building = FindFirstObjectByType<BuildingModeController>();
            int placedBricks = building != null ? building.CapturePlacedBricks().Count : 0;
            int placedCompanions = building != null ? building.CapturePlacedCompanions().Count : 0;
            float totalFriendlyHealth = FindObjectsByType<Damageable>(FindObjectsSortMode.None)
                .Where(item => item != null && item.GetComponent<GhostAI>() == null)
                .Sum(item => item.CurrentHealth);
            return $"{coins}|{bricks}|{companions}|{upgrades}|{placedBricks}|{placedCompanions}|{totalFriendlyHealth:0.0}";
        }

        private void BuildPreparationChatContext(out string affordablePurchases,
            out string affordableCompanionNames, out string affordableBrickNames,
            out string affordableUpgradeNames, out string companionNames, out string companionRoster)
        {
            int coins = CoinWallet.Instance != null ? CoinWallet.Instance.Coins : 0;
            List<string> affordable = new List<string>();
            List<string> affordableCompanions = new List<string>();
            List<string> affordableBricks = new List<string>();
            List<string> affordableUpgrades = new List<string>();
            Dictionary<string, CompanionDefinition> definitions = new Dictionary<string, CompanionDefinition>();
            CompanionInventory inventory = CompanionInventory.Instance;

            foreach (CompanionShopItem item in FindObjectsByType<CompanionShopItem>(FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            {
                CompanionDefinition definition = item.Definition;
                if (definition == null || string.IsNullOrWhiteSpace(definition.companionId)
                    || string.IsNullOrWhiteSpace(definition.displayName)) continue;
                definitions[definition.companionId] = definition;
                bool hasSlot = inventory == null || inventory.GetSlotOfId(definition.companionId) >= 0
                    || inventory.FindFirstEmptySlot() >= 0;
                if (hasSlot && item.UnitPrice <= coins)
                {
                    affordableCompanions.Add(definition.displayName);
                    affordable.Add($"{definition.displayName} 1기 추가 구매({item.UnitPrice:N0})");
                }
            }

            foreach (BrickShopItem item in FindObjectsByType<BrickShopItem>(FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            {
                if (string.IsNullOrWhiteSpace(item.DisplayName) || item.UnitPrice > coins) continue;
                affordableBricks.Add(item.DisplayName);
                affordable.Add($"{item.DisplayName} 방벽 1개 구매({item.UnitPrice:N0})");
            }

            foreach (UpgradeShopItem item in FindObjectsByType<UpgradeShopItem>(FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            {
                if (item.IsMaxed || item.CurrentCost > coins) continue;
                affordableUpgrades.Add(item.DisplayName);
                affordable.Add($"{item.DisplayName}({item.CurrentCost:N0})");
            }

            Dictionary<string, int> roster = definitions.Values.ToDictionary(item => item.companionId,
                item => inventory != null ? inventory.GetCount(item.companionId) : 0);
            foreach (CompanionToy toy in FindObjectsByType<CompanionToy>(FindObjectsSortMode.None))
            {
                CompanionDefinition definition = toy != null ? toy.Definition : null;
                Damageable health = toy != null ? toy.GetComponent<Damageable>() : null;
                if (definition == null || health != null && health.IsDead) continue;
                definitions[definition.companionId] = definition;
                roster.TryGetValue(definition.companionId, out int count);
                roster[definition.companionId] = count + 1;
            }

            affordablePurchases = string.Join(" | ", affordable.Distinct());
            affordableCompanionNames = string.Join(" | ", affordableCompanions.Distinct());
            affordableBrickNames = string.Join(" | ", affordableBricks.Distinct());
            affordableUpgradeNames = string.Join(" | ", affordableUpgrades.Distinct());
            companionNames = string.Join(" | ", definitions.Values.Select(item => item.displayName).Distinct());
            companionRoster = string.Join(" | ", roster.Where(pair => pair.Value > 0)
                .Select(pair => definitions.TryGetValue(pair.Key, out CompanionDefinition definition)
                    ? $"{definition.displayName} {pair.Value}기" : $"{pair.Key} {pair.Value}기"));
        }

        private void RefreshPreparationChatContext()
        {
            BuildPreparationChatContext(out _cachedAffordablePurchases, out _cachedAffordableCompanionNames,
                out _cachedAffordableBrickNames, out _cachedAffordableUpgradeNames,
                out _cachedCompanionNames, out _cachedCompanionRoster);
        }

        private void PushChatSnapshot(SharedChatGameState state)
        {
            chat?.UpdateExternalGame(state, new RunnerChatSnapshot
            {
                score = _rawScore,
                highScore = _save != null ? _save.bestPlasticGameScoreAtNight : 0,
                campaignDay = _night,
                plasticPhase = !_nightStarted ? "낮 정비"
                    : _spawner == null ? "밤 전투"
                    : _spawner.CurrentState == GhostSpawner.AssaultState.Maintenance ? "짧은 정비"
                    : _spawner.CurrentState == GhostSpawner.AssaultState.ClearingRemaining ? "남은 유령 처리"
                    : "전투",
                plasticCoins = CoinWallet.Instance != null ? CoinWallet.Instance.Coins : 0,
                plasticPreparationIdleSeconds = _dayNight != null
                    && _dayNight.CurrentPhase == DayNightManager.Phase.Day
                    ? Mathf.Max(0f, Time.unscaledTime - _lastPreparationActivityAt) : 0f,
                plasticHasAffordablePurchase = !string.IsNullOrEmpty(_cachedAffordablePurchases),
                plasticAffordablePurchases = _cachedAffordablePurchases,
                plasticAffordableCompanionNames = _cachedAffordableCompanionNames,
                plasticAffordableBrickNames = _cachedAffordableBrickNames,
                plasticAffordableUpgradeNames = _cachedAffordableUpgradeNames,
                plasticCompanionNames = _cachedCompanionNames,
                plasticCompanionRoster = _cachedCompanionRoster,
                subscribers = _save != null ? _save.subscribers : 0,
                enemiesDefeated = _ghostsDefeated,
                elapsedSeconds = _nightStarted ? Time.unscaledTime - _nightStartedAt : 0f,
                broadcastSecondsRemaining = _nightStarted ? 0f : _daySecondsRemaining,
                broadcastDurationSeconds = settings != null ? settings.plasticDayPreparationSeconds : 0f,
                broadcastHype = _heat
            });
        }

        private void SaveGameState()
        {
            if (_save == null) return;
            PlasticKnightmareSaveData state = _save.plasticKnightmare ??= new PlasticKnightmareSaveData();
            // Run currency, inventory, placements and upgrades never persist between attempts.
            state.initialized = false;
            state.day = 1;
            state.coins = 0;
            (state.brickInventory ??= new List<PlasticKnightmareInventoryEntry>()).Clear();
            (state.companionInventory ??= new List<PlasticKnightmareInventoryEntry>()).Clear();
            (state.placedBricks ??= new List<PlasticKnightmarePlacedObject>()).Clear();
            (state.placedCompanions ??= new List<PlasticKnightmarePlacedObject>()).Clear();
            state.attackUpgradeLevel = 0;
            state.healthUpgradeLevel = 0;
        }

        private void ResetTransientRunState()
        {
            CoinWallet.EnsureExists();
            CoinWallet.Instance?.ResetForNewRun();
            BrickInventory.EnsureExists();
            BrickInventory.Instance?.RestoreCounts(Array.Empty<KeyValuePair<string, int>>());
            CompanionInventory.EnsureExists();
            CompanionInventory.Instance?.ClearAndRestore(Array.Empty<KeyValuePair<int, string>>(), Array.Empty<KeyValuePair<string, int>>());
            foreach (UpgradeShopItem upgrade in FindObjectsByType<UpgradeShopItem>(FindObjectsSortMode.None))
                upgrade.RestoreLevel(0);
            BuildingModeController building = FindFirstObjectByType<BuildingModeController>();
            if (building != null)
            {
                building.RestorePlacedBricks(Array.Empty<PlasticKnightmarePlacedObject>());
                building.RestorePlacedCompanions(Array.Empty<PlasticKnightmarePlacedObject>());
            }
        }

        private void RefreshHud()
        {
            if (scoreText != null) scoreText.text = $"현재 점수  {_rawScore:N0}";
            if (bestScoreText != null)
            {
                int savedBest = _save != null ? _save.bestPlasticGameScoreAtNight : 0;
                bestScoreText.text = $"최고 점수  {Mathf.Max(savedBest, _rawScore):N0}";
            }
        }

        private void ReturnToRoom() => SceneManager.LoadScene(settings.roomSceneName);
    }
}
