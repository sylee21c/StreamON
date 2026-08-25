using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using StreamOn.Minigames.TileArena;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StreamOn.Minigames.Runner
{
    public enum BroadcastMissionGame { Runner, TileArena, PlasticKnightmare }
    public enum BroadcastMissionDifficulty { Easy, Normal, Hard }
    public enum BroadcastMissionType
    {
        RunnerNoDamageScore, RunnerNoDamageTime, RunnerClearObstaclesNoDamage,
        RunnerAvoidRobots, RunnerNoAttackScore,
        TileCollectBlueTimed, TileNoDamageTime, TileClearPatternNoDamage,
        TileCollectWithoutJump, TileClearPatternsNoDamage,
        PlasticBedNoDamageTime, PlasticDefeatGhostsTimed, PlasticCombo,
        PlasticPlayerNoDamageTime
    }

    [Serializable]
    public sealed class BroadcastMissionRule
    {
        public string id = "mission";
        public BroadcastMissionGame game;
        public BroadcastMissionDifficulty difficulty;
        public BroadcastMissionType type;
        public string title = "미션";
        [Min(1f)] public float target = 1f;
        [Tooltip("0이면 시간 제한이 없습니다.")]
        [Min(0f)] public float durationSeconds;
        [Min(0f)] public float successHeat = 10f;
        [Min(1f)] public float donationMultiplier = 1.5f;
        [Header("Plastic Knightmare 조건")]
        [Min(0)] public int minimumActiveGhosts;
        [Min(0)] public int minimumDamagedFacilities;
    }

    /// <summary>Three games share this scene-authored popup and mission scheduler.</summary>
    public sealed class BroadcastMissionEventController : MonoBehaviour
    {
        [Header("Scene-authored UI")]
        public CanvasGroup canvasGroup;
        public TMP_Text headerText;
        public TMP_Text missionText;
        public TMP_Text progressText;
        public TMP_Text rewardText;
        public Image timerBackground;
        public Image timerFill;

        [Header("Mission pool (all values editable)")]
        public List<BroadcastMissionRule> missions = new List<BroadcastMissionRule>();

        [Header("Occurrence")]
        [Min(0f)] public float firstMissionDelayMinimum = 25f;
        [Min(0f)] public float firstMissionDelayMaximum = 45f;
        [Min(0.1f)] public float chanceCheckInterval = 8f;
        [Range(0f, 1f)] public float chancePerCheck = 0.20f;
        [Min(0f)] public float missionCooldown = 45f;
        [Min(0)] public int maximumMissionsPerBroadcast = 3;

        [Header("Reward / failure")]
        [Min(0)] public int estimatedAverageDonation = 3000;
        [Min(0f)] public float wrongWitHeatPenalty = 8f;
        [Min(0f)] public float easyPenaltyMultiplier = 0.5f;
        [Min(0f)] public float normalPenaltyMultiplier = 0.8f;
        [Min(0f)] public float hardPenaltyMultiplier = 1.1f;
        [Range(0f, 1f)] public float nearMissThreshold = 0.9f;
        [Range(0f, 1f)] public float closeFailThreshold = 0.7f;
        [Range(0f, 1f)] public float poorFailThreshold = 0.3f;
        [Min(0f)] public float nearMissPenaltyMultiplier = 0.35f;
        [Min(0f)] public float closeFailPenaltyMultiplier = 0.65f;
        [Min(0f)] public float ordinaryFailPenaltyMultiplier = 1f;
        [Min(0f)] public float poorFailPenaltyMultiplier = 1.2f;

        [Header("Presentation")]
        [Min(0.01f)] public float fadeSeconds = 0.18f;
        [Min(0.1f)] public float resultVisibleSeconds = 2.2f;
        public Color activeColor = new Color32(255, 190, 59, 255);
        public Color successColor = new Color32(74, 224, 139, 255);
        public Color failureColor = new Color32(255, 91, 105, 255);

        private RunnerGameManager _runner;
        private RunnerBroadcastAudienceController _runnerAudience;
        private TileArenaController _tile;
        private TileArenaChatAdapter _tileAudience;
        private PlasticKnightmareBroadcastController _plastic;
        private RunnerChatController _chat;
        private RunnerDonationPopupController _donation;
        private RunnerWitInteractionController _wit;
        private BroadcastMissionRule _active;
        private Coroutine _presentation;
        private float _nextCheckAt;
        private float _startedAt;
        private int _missionsStarted;
        private bool _sawGameplay;
        private Snapshot _start;

        public bool IsActive => _active != null;

        private void Awake()
        {
            ResolveTargets();
            HideImmediate();
            ScheduleFirstCheck();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            ResolveTargets();
            bool gameplay = IsGameplayActive();
            if (gameplay && !_sawGameplay)
            {
                _sawGameplay = true;
                _missionsStarted = 0;
                ScheduleFirstCheck();
            }

            if (_active != null)
            {
                EvaluateActiveMission(gameplay);
                return;
            }
            if (!gameplay) return;
            if (_presentation != null || _missionsStarted >= maximumMissionsPerBroadcast
                || Time.unscaledTime < _nextCheckAt) return;

            _nextCheckAt = Time.unscaledTime + Mathf.Max(0.1f, chanceCheckInterval);
            if ((_donation != null && _donation.IsShowing) || (_wit != null && _wit.IsShowing)) return;
            if (UnityEngine.Random.value <= chancePerCheck) TryBeginMission();
        }

        private void ResolveTargets()
        {
            if (_runner == null) _runner = FindFirstObjectByType<RunnerGameManager>();
            if (_runnerAudience == null) _runnerAudience = FindFirstObjectByType<RunnerBroadcastAudienceController>();
            if (_tile == null) _tile = FindFirstObjectByType<TileArenaController>();
            if (_tileAudience == null) _tileAudience = FindFirstObjectByType<TileArenaChatAdapter>();
            if (_plastic == null) _plastic = FindFirstObjectByType<PlasticKnightmareBroadcastController>();
            if (_chat == null) _chat = FindFirstObjectByType<RunnerChatController>();
            if (_donation == null) _donation = FindFirstObjectByType<RunnerDonationPopupController>();
            if (_wit == null) _wit = FindFirstObjectByType<RunnerWitInteractionController>();
        }

        private bool IsGameplayActive()
        {
            if (_runner != null) return _runner.State == RunnerGameState.Playing;
            if (_tile != null) return _tile.IsRunning;
            return _plastic != null && _plastic.MissionGameplayActive;
        }

        private BroadcastMissionGame CurrentGame()
        {
            if (_runner != null) return BroadcastMissionGame.Runner;
            if (_tile != null) return BroadcastMissionGame.TileArena;
            return BroadcastMissionGame.PlasticKnightmare;
        }

        private void ScheduleFirstCheck()
        {
            float minimum = Mathf.Max(0f, firstMissionDelayMinimum);
            float maximum = Mathf.Max(minimum, firstMissionDelayMaximum);
            _nextCheckAt = Time.unscaledTime + UnityEngine.Random.Range(minimum, maximum);
        }

        private bool TryBeginMission()
        {
            BroadcastMissionGame game = CurrentGame();
            List<BroadcastMissionRule> candidates = missions.Where(rule => rule != null && rule.game == game && IsEligible(rule)).ToList();
            if (candidates.Count == 0) return false;
            _active = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            _startedAt = Time.unscaledTime;
            _start = Capture();
            _missionsStarted++;
            transform.SetAsLastSibling();
            SetTimerVisible(_active.durationSeconds > 0f);
            SetColor(activeColor);
            if (headerText != null) headerText.text = DifficultyLabel(_active.difficulty) + " 돌발 미션";
            if (missionText != null) missionText.text = _active.title;
            int reward = RewardAmount(_active);
            if (rewardText != null) rewardText.text = $"성공 보상  +{reward:N0}원";
            RefreshProgress(0f);
            if (_presentation != null) StopCoroutine(_presentation);
            _presentation = StartCoroutine(FadeTo(1f));
            _chat?.ReactMissionEvent(RunnerChatEvent.MissionStarted, 1, 2);
            return true;
        }

        [ContextMenu("Debug/Start Eligible Mission Now")]
        public void DebugStartEligibleMissionNow()
        {
            ResolveTargets();
            if (_active == null && IsGameplayActive()) TryBeginMission();
        }

        private bool IsEligible(BroadcastMissionRule rule)
        {
            switch (rule.type)
            {
                case BroadcastMissionType.RunnerAvoidRobots: return _runner != null && _runner.HasActiveEnemy;
                case BroadcastMissionType.TileCollectBlueTimed:
                case BroadcastMissionType.TileCollectWithoutJump:
                    return _tile != null && !_tile.IsTransitioning && _tile.BlueTilesRemaining >= Mathf.CeilToInt(rule.target);
                case BroadcastMissionType.TileClearPatternNoDamage:
                    return _tile != null && !_tile.IsTransitioning && _tile.BlueTilesRemaining > 0;
                case BroadcastMissionType.PlasticBedNoDamageTime:
                    return _plastic != null && _plastic.BedHealth > 0f
                        && _plastic.ActiveGhostCount >= rule.minimumActiveGhosts
                        && _plastic.DamagedFacilityCount >= rule.minimumDamagedFacilities;
                case BroadcastMissionType.PlasticDefeatGhostsTimed:
                    return _plastic != null && _plastic.ActiveGhostCount >= Mathf.CeilToInt(rule.target);
                case BroadcastMissionType.PlasticCombo:
                    return _plastic != null && _plastic.CurrentCombo < Mathf.CeilToInt(rule.target);
                case BroadcastMissionType.PlasticPlayerNoDamageTime:
                    return _plastic != null && _plastic.PlayerHealth > 0f;
                default: return true;
            }
        }

        private void EvaluateActiveMission(bool gameplay)
        {
            Snapshot now = Capture();
            float elapsed = Time.unscaledTime - _startedAt;
            float progress = Progress(now, elapsed);
            RefreshProgress(progress);

            if (HasFailed(now)) { Finish(false, progress); return; }
            if (HasSucceeded(now, elapsed)) { Finish(true, 1f); return; }
            if (_active.durationSeconds > 0f && elapsed >= _active.durationSeconds)
            {
                Finish(false, progress);
                return;
            }
            // Scene/game state invalidation is not a player failure.
            if (!gameplay) CancelWithoutPenalty();
        }

        private bool HasFailed(Snapshot now)
        {
            switch (_active.type)
            {
                case BroadcastMissionType.RunnerNoDamageScore:
                case BroadcastMissionType.RunnerNoDamageTime:
                case BroadcastMissionType.RunnerClearObstaclesNoDamage:
                case BroadcastMissionType.RunnerAvoidRobots:
                    return now.hits > _start.hits;
                case BroadcastMissionType.RunnerNoAttackScore:
                    return now.attacks > _start.attacks;
                case BroadcastMissionType.TileNoDamageTime:
                case BroadcastMissionType.TileClearPatternNoDamage:
                case BroadcastMissionType.TileClearPatternsNoDamage:
                    return now.hits > _start.hits;
                case BroadcastMissionType.TileCollectWithoutJump:
                    return now.jumps > _start.jumps;
                case BroadcastMissionType.PlasticBedNoDamageTime:
                    return now.bedHealth < _start.bedHealth;
                case BroadcastMissionType.PlasticPlayerNoDamageTime:
                    return now.playerHealth < _start.playerHealth;
                default: return false;
            }
        }

        private bool HasSucceeded(Snapshot now, float elapsed)
        {
            float target = Mathf.Max(1f, _active.target);
            switch (_active.type)
            {
                case BroadcastMissionType.RunnerNoDamageScore:
                case BroadcastMissionType.RunnerNoAttackScore: return now.score - _start.score >= target;
                case BroadcastMissionType.RunnerNoDamageTime:
                case BroadcastMissionType.TileNoDamageTime:
                case BroadcastMissionType.PlasticBedNoDamageTime:
                case BroadcastMissionType.PlasticPlayerNoDamageTime: return elapsed >= _active.durationSeconds;
                case BroadcastMissionType.RunnerClearObstaclesNoDamage: return now.obstacles - _start.obstacles >= target;
                case BroadcastMissionType.RunnerAvoidRobots: return now.robots - _start.robots >= target;
                case BroadcastMissionType.TileCollectBlueTimed:
                case BroadcastMissionType.TileCollectWithoutJump: return now.pickups - _start.pickups >= target;
                case BroadcastMissionType.TileClearPatternNoDamage:
                case BroadcastMissionType.TileClearPatternsNoDamage: return now.patterns - _start.patterns >= target;
                case BroadcastMissionType.PlasticDefeatGhostsTimed: return now.kills - _start.kills >= target;
                case BroadcastMissionType.PlasticCombo: return now.combo >= target;
                default: return false;
            }
        }

        private float Progress(Snapshot now, float elapsed)
        {
            float target = Mathf.Max(1f, _active.target);
            switch (_active.type)
            {
                case BroadcastMissionType.RunnerNoDamageScore:
                case BroadcastMissionType.RunnerNoAttackScore: return Mathf.Clamp01((now.score - _start.score) / target);
                case BroadcastMissionType.RunnerClearObstaclesNoDamage: return Mathf.Clamp01((now.obstacles - _start.obstacles) / target);
                case BroadcastMissionType.RunnerAvoidRobots: return Mathf.Clamp01((now.robots - _start.robots) / target);
                case BroadcastMissionType.TileCollectBlueTimed:
                case BroadcastMissionType.TileCollectWithoutJump: return Mathf.Clamp01((now.pickups - _start.pickups) / target);
                case BroadcastMissionType.TileClearPatternNoDamage:
                case BroadcastMissionType.TileClearPatternsNoDamage: return Mathf.Clamp01((now.patterns - _start.patterns) / target);
                case BroadcastMissionType.PlasticDefeatGhostsTimed: return Mathf.Clamp01((now.kills - _start.kills) / target);
                case BroadcastMissionType.PlasticCombo: return Mathf.Clamp01(now.combo / target);
                default: return _active.durationSeconds > 0f ? Mathf.Clamp01(elapsed / _active.durationSeconds) : 0f;
            }
        }

        private Snapshot Capture()
        {
            if (_runner != null) return new Snapshot
            {
                score = _runner.Score, hits = _runner.HitsTaken, obstacles = _runner.ObstaclesCleared,
                robots = _runner.EnemiesAvoided, attacks = _runner.AttacksPerformed
            };
            if (_tile != null) return new Snapshot
            {
                score = _tile.Score, hits = _tile.HitsTaken, pickups = _tile.BlueTilesCollected,
                patterns = _tile.PatternsCleared, jumps = _tile.JumpsPerformed
            };
            return _plastic != null ? new Snapshot
            {
                hits = 0, kills = _plastic.GhostsDefeated, combo = _plastic.CurrentCombo,
                bedHealth = _plastic.BedHealth, playerHealth = _plastic.PlayerHealth
            } : default;
        }

        private void Finish(bool success, float progress)
        {
            BroadcastMissionRule completed = _active;
            _active = null;
            _nextCheckAt = Time.unscaledTime + Mathf.Max(0f, missionCooldown);
            if (success)
            {
                int reward = RewardAmount(completed);
                ApplyOutcome(completed.successHeat, reward);
                if (headerText != null) headerText.text = "미션 성공";
                if (progressText != null) progressText.text = "달성!";
                SetColor(successColor);
                _chat?.ReactMissionEvent(RunnerChatEvent.MissionSuccess, 2, 4);
            }
            else
            {
                float penalty = FailurePenalty(completed, progress);
                ApplyOutcome(-penalty, 0);
                if (headerText != null) headerText.text = progress >= nearMissThreshold ? "아쉽게 실패" : "미션 실패";
                if (progressText != null) progressText.text = $"달성도  {Mathf.RoundToInt(progress * 100f)}%";
                if (rewardText != null) rewardText.text = $"열기 -{penalty:0.#}";
                SetColor(failureColor);
                _chat?.ReactMissionEvent(progress >= nearMissThreshold ? RunnerChatEvent.MissionNearMiss
                    : progress < poorFailThreshold ? RunnerChatEvent.MissionFailedBadly : RunnerChatEvent.MissionFailed, 2, 4);
            }
            if (_presentation != null) StopCoroutine(_presentation);
            _presentation = StartCoroutine(HideAfterResult());
        }

        private void CancelWithoutPenalty()
        {
            _active = null;
            _nextCheckAt = Time.unscaledTime + Mathf.Max(0f, missionCooldown);
            if (_presentation != null) StopCoroutine(_presentation);
            _presentation = StartCoroutine(FadeTo(0f));
        }

        private void ApplyOutcome(float heat, int donation)
        {
            if (_runnerAudience != null) _runnerAudience.ApplyMissionOutcome(heat, donation);
            else if (_tileAudience != null) _tileAudience.ApplyMissionOutcome(heat, donation);
            else _plastic?.ApplyMissionOutcome(heat, donation);
        }

        private int RewardAmount(BroadcastMissionRule rule)
        {
            int raw = Mathf.RoundToInt(estimatedAverageDonation * Mathf.Max(1f, rule.donationMultiplier));
            return Mathf.Max(0, Mathf.RoundToInt(raw / 100f) * 100);
        }

        private float FailurePenalty(BroadcastMissionRule rule, float progress)
        {
            float difficulty = rule.difficulty == BroadcastMissionDifficulty.Easy ? easyPenaltyMultiplier
                : rule.difficulty == BroadcastMissionDifficulty.Normal ? normalPenaltyMultiplier : hardPenaltyMultiplier;
            float progressMultiplier = progress >= nearMissThreshold ? nearMissPenaltyMultiplier
                : progress >= closeFailThreshold ? closeFailPenaltyMultiplier
                : progress < poorFailThreshold ? poorFailPenaltyMultiplier : ordinaryFailPenaltyMultiplier;
            return wrongWitHeatPenalty * difficulty * progressMultiplier;
        }

        private void RefreshProgress(float progress)
        {
            if (_active == null) return;
            if (progressText != null) progressText.text = $"진행도  {Mathf.RoundToInt(progress * 100f)}%";
            if (_active.durationSeconds > 0f && timerFill != null)
                timerFill.fillAmount = Mathf.Clamp01(1f - (Time.unscaledTime - _startedAt) / _active.durationSeconds);
        }

        private IEnumerator HideAfterResult()
        {
            float elapsed = 0f;
            while (elapsed < resultVisibleSeconds)
            {
                if (Time.timeScale > 0f) elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return FadeTo(0f);
        }

        private IEnumerator FadeTo(float target)
        {
            if (canvasGroup == null) { _presentation = null; yield break; }
            float from = canvasGroup.alpha;
            float elapsed = 0f;
            while (elapsed < Mathf.Max(0.01f, fadeSeconds))
            {
                if (Time.timeScale > 0f) elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, target, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeSeconds)));
                yield return null;
            }
            canvasGroup.alpha = target;
            _presentation = null;
        }

        private void HideImmediate()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        private void SetTimerVisible(bool visible)
        {
            if (timerBackground != null) timerBackground.gameObject.SetActive(visible);
            if (timerFill != null) { timerFill.gameObject.SetActive(visible); timerFill.fillAmount = 1f; }
        }

        private void SetColor(Color color)
        {
            if (headerText != null) headerText.color = color;
            if (timerFill != null) timerFill.color = color;
        }

        private static string DifficultyLabel(BroadcastMissionDifficulty difficulty) => difficulty switch
        {
            BroadcastMissionDifficulty.Easy => "쉬운",
            BroadcastMissionDifficulty.Normal => "보통",
            _ => "어려운"
        };

        private struct Snapshot
        {
            public float score, bedHealth, playerHealth;
            public int hits, obstacles, robots, attacks, pickups, patterns, jumps, kills, combo;
        }
    }
}
