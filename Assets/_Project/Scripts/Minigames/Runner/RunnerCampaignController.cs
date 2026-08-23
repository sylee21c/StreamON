using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StreamOn.Minigames.Runner
{
    public sealed class RunnerCampaignController : MonoBehaviour
    {
        [Header("Editable Campaign Data")]
        [Tooltip("진행 방식, 행동 목록, 정산 공식, 저장 옵션을 담은 설정 에셋")]
        [SerializeField] private RunnerCampaignSettings settings;

        private readonly List<RunnerCampaignDayRecord> _records = new List<RunnerCampaignDayRecord>();
        private RunnerGameManager _gameManager;
        private TMP_FontAsset _font;
        private GameObject _preparationPanel;
        private GameObject _settlementPanel;
        private GameObject _recordsPanel;
        private GameObject _endingPanel;
        private TMP_Text _statusText;
        private TMP_Text _preparationTitle;
        private TMP_Text _preparationStats;
        private TMP_Text _settlementTitle;
        private TMP_Text _settlementBody;
        private TMP_Text _settlementButtonLabel;
        private TMP_Text _recordsText;
        private TMP_Text _endingTitle;
        private TMP_Text _endingBody;
        private TMP_Text _newGameButtonLabel;

        private int _day;
        private int _subscribers;
        private int _mentalLevel;
        private int _mentalExperience;
        private int _gameSkill;
        private int _gameSkillExperience;
        private int _talkingSkill;
        private int _talkingSkillExperience;
        private int _healthStat;
        private int _healthStatExperience;
        private int _bestBroadcastScore;
        private long _lifetimeDonations;
        private long _cash;
        private int _pcLevel = 1;
        private int _microphoneLevel = 1;
        private int _fitnessLevel = 1;
        private int _interiorLevel = 1;
        private string _selectedActionName = string.Empty;
        private string _selectedBroadcastGame = string.Empty;
        private bool _initialized;
        private bool _resultPending;
        private bool _broadcastPending;
        private bool _newGameConfirmationArmed;
        private Coroutine _newGameConfirmationRoutine;
        private RunnerBroadcastSettlementView _settlementView;

        public bool IsActive => _initialized;
        public bool IsEndless => settings != null && settings.IsEndless;
        public int Day => _day;
        public int MaximumDays => IsEndless || settings == null ? 0 : settings.fixedMaximumDays;
        public int Subscribers => _subscribers;
        public int Followers => _subscribers;
        public int MentalLevel => _mentalLevel;
        public int GameSkill => _gameSkill;
        public int TalkingSkill => _talkingSkill;
        public int HealthStat => _healthStat;
        public int BestBroadcastScore => _bestBroadcastScore;
        public long LifetimeDonations => _lifetimeDonations;
        public long Cash => _cash;
        public RunnerBroadcastGrowthSettings GrowthSettings => settings != null ? settings.broadcastGrowthSettings : null;
        public RunnerCampaignSettings Settings => settings;
        public string LastSelectedAction => _selectedActionName;
        public int CurrentTargetScore => settings != null ? settings.TargetScoreForDay(_day) : 0;
        public IReadOnlyList<RunnerCampaignDayRecord> Records => _records;

        public bool ManualSave()
        {
            if (!_initialized) return false;
            return RunnerCampaignSaveStore.Save(settings, BuildSaveData(false), true);
        }

        public void Initialize(RunnerGameManager gameManager)
        {
            if (_initialized) return;
            if (settings == null)
            {
                Debug.LogError("STREAM ON campaign settings asset is not assigned.", this);
                enabled = false;
                return;
            }

            _gameManager = gameManager;
            _settlementView = FindFirstObjectByType<RunnerBroadcastSettlementView>();
            if (_settlementView == null)
                Debug.LogError("방송 정산 UI가 씬에 연결되지 않았습니다. 런타임 UI는 자동 생성하지 않습니다.", this);
            _initialized = true;
            if (!TryLoadCampaign()) StartNewCampaign(true);
        }

        public void HandleRunEnded()
        {
            if (!_initialized || _resultPending) return;
            _resultPending = true;

            int target = CurrentTargetScore;
            int finalBroadcastScore = _gameManager.FinalBroadcastScore;
            bool succeeded = finalBroadcastScore >= target;
            int scoreContribution = finalBroadcastScore / Mathf.Max(1, settings.scorePerSubscriber);
            int effectiveTalkingLevel = Mathf.Min(_talkingSkill, settings.maximumEffectiveTalkingLevel);
            RunnerBroadcastResult broadcastResult = _gameManager.BroadcastResult;
            int subscriberDelta;
            if (broadcastResult != null)
            {
                subscriberDelta = broadcastResult.netFollowerChange;
            }
            else if (succeeded)
            {
                subscriberDelta = settings.successBaseSubscriberGain + scoreContribution
                    + _gameManager.EnemiesDefeated * settings.subscriberGainPerEnemy
                    - _gameManager.HitsTaken * settings.subscriberPenaltyPerHit
                    + effectiveTalkingLevel * settings.subscriberGainPerTalkingLevel;
            }
            else
            {
                int mitigatedLoss = settings.failureBaseSubscriberLoss
                    + Mathf.Min(_day, settings.maximumFailureScalingDay) * settings.failureSubscriberLossIncreasePerDay
                    - scoreContribution - effectiveTalkingLevel * 2;
                subscriberDelta = -Mathf.Max(4, mitigatedLoss);
            }

            float mentalDelta = 0f;
            _subscribers = Mathf.Max(0, _subscribers + subscriberDelta);
            if (broadcastResult != null)
            {
                _lifetimeDonations += broadcastResult.donationWon;
                _cash += broadcastResult.donationWon;
            }
            int previousBest = RunnerCampaignSaveStore.TryLoad(settings, out RunnerCampaignSaveData recordSave)
                ? recordSave.bestRunnerGameScore : 0;
            _bestBroadcastScore = Mathf.Max(_bestBroadcastScore, finalBroadcastScore);
            _broadcastPending = false;

            _records.Add(new RunnerCampaignDayRecord
            {
                day = _day,
                selectedAction = _selectedActionName,
                score = finalBroadcastScore,
                targetScore = target,
                succeeded = succeeded,
                enemiesDefeated = _gameManager.EnemiesDefeated,
                hitsTaken = _gameManager.HitsTaken,
                subscriberDelta = subscriberDelta,
                mentalDelta = mentalDelta,
                subscribersAfter = _subscribers,
                mentalAfter = _mentalLevel,
                broadcastRating = broadcastResult != null ? broadcastResult.finalRating : 0f,
                peakViewers = broadcastResult != null ? broadcastResult.peakViewers : 0,
                averageViewers = broadcastResult != null ? broadcastResult.averageViewers : 0f,
                totalVisitors = broadcastResult != null ? broadcastResult.totalVisitors : 0,
                donationWon = broadcastResult != null ? broadcastResult.donationWon : 0
            });
            TrimRecords();
            SaveCampaign(true);
            int experienceGained = 0;
            int broadcasterLevelAfter = 1;
            if (RunnerCampaignSaveStore.TryLoad(settings, out RunnerCampaignSaveData progressionSave))
            {
                progressionSave.bestRunnerBroadcastScore = Mathf.Max(progressionSave.bestRunnerBroadcastScore, finalBroadcastScore);
                progressionSave.bestRunnerGameScore = Mathf.Max(progressionSave.bestRunnerGameScore, _gameManager.FinalRawGameScore);
                int experience = settings.broadcastCompletionExperience
                    + Mathf.RoundToInt((broadcastResult != null ? broadcastResult.finalRating : 0f) * settings.broadcastRatingExperiencePerPoint)
                    + (_gameManager.FinalRawGameScore > previousBest ? settings.newRecordExperience : 0);
                experienceGained = BroadcasterProgression.AddBroadcastExperience(settings, progressionSave, experience);
                progressionSave.hiredManagerTier = 0;
                progressionSave.managerUsesRemaining = 0;
                progressionSave.broadcastSessionExperienceEarned = 0;
                broadcasterLevelAfter = progressionSave.broadcasterLevel;
                RunnerCampaignSaveStore.Save(settings, progressionSave, true);
            }
            RunnerBroadcastSessionStore.Complete(settings);
            RefreshStatus();
            _gameManager.NotifyChat(RunnerChatEvent.CampaignSettlement);
            StartCoroutine(ShowResultAfterDelay(succeeded, target, subscriberDelta, mentalDelta, broadcastResult,
                previousBest, experienceGained, broadcasterLevelAfter));
        }

        private IEnumerator ShowResultAfterDelay(bool succeeded, int target, int subscriberDelta, float mentalDelta,
            RunnerBroadcastResult result, int previousBest, int experienceGained, int broadcasterLevelAfter)
        {
            int finalBroadcastScore = _gameManager.FinalBroadcastScore;
            yield return new WaitForSecondsRealtime(settings.resultDelay);
            if (_settlementView != null)
            {
                _settlementView.Show(new RunnerSettlementDisplayData
                {
                    gameTitle = "러너",
                    score = _gameManager.FinalRawGameScore,
                    rawGameScore = _gameManager.FinalRawGameScore,
                    broadcastScore = finalBroadcastScore,
                    previousBestScore = previousBest,
                    isNewRecord = _gameManager.FinalRawGameScore > previousBest,
                    broadcastCompleted = true,
                    experienceGained = experienceGained,
                    levelAfter = broadcasterLevelAfter,
                    targetScore = target,
                    enemiesDefeated = _gameManager.EnemiesDefeated,
                    hitsTaken = _gameManager.HitsTaken,
                    subscriberDelta = subscriberDelta,
                    subscribersAfter = _subscribers,
                    mentalLevel = _mentalLevel,
                    cashAfter = _cash,
                    broadcastResult = result
                }, ContinueAfterSettlement, "다음 날");
                yield break;
            }
            _settlementTitle.text = succeeded ? "오늘 방송 성공!" : "오늘 방송은 아쉬웠다";
            _settlementTitle.color = succeeded ? new Color(0.40f, 0.90f, 0.82f) : new Color(1f, 0.47f, 0.48f);
            string dashboard = $"[방송 성적]\n점수  {_gameManager.Score:N0} / 목표 {target:N0}    적 처치 {_gameManager.EnemiesDefeated}    피격 {_gameManager.HitsTaken}";
            _settlementBody.text = dashboard;
            if (result != null)
            {
                yield return new WaitForSecondsRealtime(0.28f);
                dashboard += $"\n\n[시청자]\n총 방문 {result.totalVisitors:N0}    평균 {result.averageViewers:0.0}    최고 {result.peakViewers:N0}    종료 {result.endingViewers:N0}";
                _settlementBody.text = dashboard;
                yield return new WaitForSecondsRealtime(0.28f);
                dashboard += $"\n\n[방송 평가 {RatingGrade(result.finalRating)}]\n플레이 {result.gameplayRating:0.0}    생존 {result.survivalRating:0.0}    안정성 {result.safetyRating:0.0}    진행 {result.hostingRating:0.0}\n최종 평점  {result.finalRating:0.0} / 5.0";
                _settlementBody.text = dashboard;
                yield return new WaitForSecondsRealtime(0.28f);
                dashboard += $"\n\n[성장 및 수익]\n팔로워 {Signed(result.followersGained)} / 이탈 -{result.followersLost} / 순변화 {Signed(subscriberDelta)}    전환율 {result.followConversionRate * 100f:0.0}%\n후원금 +{result.donationWon:N0}원    누적 {_lifetimeDonations:N0}원\n현재 팔로워 {_subscribers:N0}    멘탈 Lv.{_mentalLevel}";
                _settlementBody.text = dashboard;
            }
            else
            {
                dashboard += $"\n\n팔로워 {Signed(subscriberDelta)}\n현재 팔로워 {_subscribers:N0}    멘탈 Lv.{_mentalLevel}";
                _settlementBody.text = dashboard;
            }
            _settlementButtonLabel.text = !IsEndless && _day >= settings.fixedMaximumDays
                ? "최종 결과 보기" : "다음 날";
            ShowOnly(_settlementPanel);
        }

        private void SelectAction(RunnerCampaignActionDefinition action)
        {
            if (action == null) return;
            _selectedActionName = action.displayName;
            settings.AddStatExperience(ref _gameSkill, ref _gameSkillExperience, action.gameSkillDelta, settings.maximumGameSkill);
            settings.AddStatExperience(ref _talkingSkill, ref _talkingSkillExperience, action.talkingSkillDelta, settings.maximumTalkingSkill);
            settings.AddStatExperience(ref _healthStat, ref _healthStatExperience, action.healthStatDelta, settings.maximumHealthStat);
            settings.AddStatExperience(ref _mentalLevel, ref _mentalExperience,
                action.mentalExperienceDelta, settings.maximumMentalLevel);
            _subscribers = Mathf.Max(settings.minimumSubscribersToStartBroadcast, _subscribers + action.subscriberDelta);
            _broadcastPending = true;
            SaveCampaign(false);
            StartBroadcast();
        }

        private void StartBroadcast()
        {
            _resultPending = false;
            HideAllPanels();
            SetStatusVisible(false);
            _gameManager.ConfigureCampaignRun(_day, _gameSkill, _healthStat,
                settings.BroadcastSecondsForHealth(_healthStat, _fitnessLevel), settings.gameOverTimePenaltySeconds,
                _pcLevel, _microphoneLevel, _interiorLevel);
            _gameManager.BeginRun();
            _gameManager.NotifyChat(RunnerChatEvent.CampaignActionSelected);
        }

        private void ContinueAfterSettlement()
        {
            if (!IsEndless && _day >= settings.fixedMaximumDays)
            {
                ShowEnding(true);
                return;
            }

            if (settings.useThreeDimensionalRoomFlow)
            {
                LoadRoomScene();
                return;
            }
            _day++;
            SaveCampaign(false);
            ShowPreparation();
        }

        private void StartNewCampaign(bool clearPreviousSave)
        {
            StopAllCoroutines();
            if (clearPreviousSave) DeleteSave();
            _day = 1;
            _subscribers = settings.startingSubscribers;
            _mentalLevel = settings.startingMentalLevel;
            _mentalExperience = 0;
            _gameSkill = settings.startingGameSkill;
            _gameSkillExperience = 0;
            _talkingSkill = settings.startingTalkingSkill;
            _talkingSkillExperience = 0;
            _healthStat = settings.startingHealthStat;
            _healthStatExperience = 0;
            _bestBroadcastScore = 0;
            _lifetimeDonations = 0;
            _cash = 0;
            _pcLevel = _microphoneLevel = _fitnessLevel = _interiorLevel = 1;
            _selectedActionName = string.Empty;
            _selectedBroadcastGame = string.Empty;
            _records.Clear();
            _resultPending = false;
            _broadcastPending = false;
            _newGameConfirmationArmed = false;
            SaveCampaign(false);
            if (settings.useThreeDimensionalRoomFlow) LoadRoomScene();
            else ShowPreparation();
        }

        private void ShowPreparation()
        {
            SetStatusVisible(true);
            _gameManager.PrepareCampaignDay();
            _preparationTitle.text = IsEndless
                ? $"DAY {_day}  방송 준비"
                : $"DAY {_day} / {settings.fixedMaximumDays}  방송 준비";
            _preparationStats.text =
                $"팔로워  {_subscribers:N0}명     보유금 {_cash:N0}원\n" +
                $"게임 Lv.{_gameSkill}     소통 Lv.{_talkingSkill}     체력 Lv.{_healthStat}     멘탈 Lv.{_mentalLevel}\n" +
                $"오늘 목표  {CurrentTargetScore:N0}     캠페인 최고  {_bestBroadcastScore:N0}";
            RefreshStatus();
            ResetNewGameConfirmation();
            ShowOnly(_preparationPanel);
            _gameManager.NotifyChat(RunnerChatEvent.CampaignDayStarted);
        }

        private void ShowEnding(bool cleared)
        {
            SetStatusVisible(true);
            _endingTitle.text = cleared ? "캠페인 목표 달성!" : "캠페인 종료";
            _endingTitle.color = cleared ? new Color(0.40f, 0.90f, 0.82f) : new Color(1f, 0.40f, 0.44f);
            _endingBody.text = cleared
                ? $"최종 DAY  {_day}\n최종 팔로워  {_subscribers:N0}명\n누적 후원금 {_lifetimeDonations:N0}원\n캠페인 최고 점수  {_bestBroadcastScore:N0}"
                : $"DAY {_day}에서 종료\n최종 팔로워  {_subscribers:N0}명\n멘탈 Lv.{_mentalLevel}\n최고 점수  {_bestBroadcastScore:N0}";
            ShowOnly(_endingPanel);
            _gameManager.NotifyChat(cleared ? RunnerChatEvent.CampaignClear : RunnerChatEvent.CampaignFailed);
        }

        private void ShowRecords()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("캠페인 최고 점수  ").Append(_bestBroadcastScore.ToString("N0")).AppendLine();
            builder.Append("완료한 방송  ").Append(_records.Count).AppendLine("회\n");
            if (_records.Count == 0)
            {
                builder.Append("아직 완료한 방송이 없습니다.");
            }
            else
            {
                foreach (RunnerCampaignDayRecord record in _records.Skip(Mathf.Max(0, _records.Count - 9)).Reverse())
                {
                    builder.Append(record.succeeded ? "성공 " : "실패 ")
                        .Append("DAY ").Append(record.day)
                        .Append("  ").Append(record.score.ToString("N0")).Append("/").Append(record.targetScore.ToString("N0"))
                        .Append("  ").Append(string.IsNullOrWhiteSpace(record.selectedAction) ? "행동 없음" : record.selectedAction)
                        .Append("  구독 ").Append(Signed(record.subscriberDelta)).AppendLine();
                }
            }
            _recordsText.text = builder.ToString().Replace("구독 ", "팔로워 ");
            ShowOnly(_recordsPanel);
        }

        private void ReturnToPreparation() => ShowOnly(_preparationPanel);

        private void RequestNewCampaign()
        {
            if (!_newGameConfirmationArmed)
            {
                _newGameConfirmationArmed = true;
                _newGameButtonLabel.text = "정말 초기화? 한 번 더 클릭";
                if (_newGameConfirmationRoutine != null) StopCoroutine(_newGameConfirmationRoutine);
                _newGameConfirmationRoutine = StartCoroutine(CancelNewGameConfirmation());
                return;
            }
            StartNewCampaign(true);
        }

        private IEnumerator CancelNewGameConfirmation()
        {
            yield return new WaitForSecondsRealtime(4f);
            ResetNewGameConfirmation();
        }

        private void ResetNewGameConfirmation()
        {
            _newGameConfirmationArmed = false;
            _newGameConfirmationRoutine = null;
            if (_newGameButtonLabel != null) _newGameButtonLabel.text = "새 캠페인";
        }

        private void RefreshStatus()
        {
            if (_statusText == null) return;
            string dayText = IsEndless ? $"DAY {_day}" : $"DAY {_day}/{settings.fixedMaximumDays}";
            _statusText.text = $"{dayText}    팔로워 {_subscribers:N0}    보유금 {_cash:N0}원    목표 {CurrentTargetScore:N0}    게임 Lv.{_gameSkill}    소통 Lv.{_talkingSkill}    체력 Lv.{_healthStat}    멘탈 Lv.{_mentalLevel}";
        }

        private void SetStatusVisible(bool visible)
        {
            if (_statusText != null && _statusText.transform.parent != null)
                _statusText.transform.parent.gameObject.SetActive(visible);
        }

        private bool TryLoadCampaign()
        {
            if (!RunnerCampaignSaveStore.TryLoad(settings, out RunnerCampaignSaveData data)) return false;
            try
            {
                _day = data.day;
                _subscribers = data.subscribers;
                _mentalLevel = Mathf.Clamp(data.mentalLevel, 1, settings.maximumMentalLevel);
                _mentalExperience = Mathf.Max(0, data.mentalExperience);
                _gameSkill = Mathf.Clamp(data.gameSkill, 1, settings.maximumGameSkill);
                _gameSkillExperience = Mathf.Max(0, data.gameSkillExperience);
                _talkingSkill = Mathf.Clamp(data.talkingSkill, 1, settings.maximumTalkingSkill);
                _talkingSkillExperience = Mathf.Max(0, data.talkingSkillExperience);
                _healthStat = Mathf.Clamp(data.healthStat > 0 ? data.healthStat : settings.startingHealthStat, 1, settings.maximumHealthStat);
                _healthStatExperience = Mathf.Max(0, data.healthStatExperience);
                _bestBroadcastScore = data.bestBroadcastScore;
                _lifetimeDonations = data.lifetimeDonations;
                _cash = data.cash;
                _pcLevel = Mathf.Clamp(data.pcLevel, 1, 3);
                _microphoneLevel = Mathf.Clamp(data.microphoneLevel, 1, 3);
                _fitnessLevel = Mathf.Clamp(data.fitnessLevel, 1, 3);
                _interiorLevel = Mathf.Clamp(data.interiorLevel, 1, 3);
                _broadcastPending = data.broadcastPending;
                _selectedActionName = data.selectedAction ?? string.Empty;
                _selectedBroadcastGame = data.selectedBroadcastGame ?? string.Empty;
                _records.Clear();
                if (data.records != null) _records.AddRange(data.records.Where(record => record != null));

                if (!IsEndless && data.awaitingAdvance && _day >= settings.fixedMaximumDays)
                {
                    _gameManager.PrepareCampaignDay();
                    RefreshStatus();
                    ShowEnding(true);
                }
                else
                {
                    if (data.awaitingAdvance)
                    {
                        _day++;
                        _broadcastPending = false;
                        SaveCampaign(false);
                    }
                    if (_broadcastPending) StartBroadcast();
                    else if (settings.useThreeDimensionalRoomFlow) LoadRoomScene();
                    else ShowPreparation();
                }
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("STREAM ON campaign save could not be loaded: " + exception.Message, this);
                return false;
            }
        }

        private void SaveCampaign(bool awaitingAdvance)
        {
            RunnerCampaignSaveStore.Save(settings, BuildSaveData(awaitingAdvance));
        }

        private RunnerCampaignSaveData BuildSaveData(bool awaitingAdvance)
        {
            if (!RunnerCampaignSaveStore.TryLoad(settings, out RunnerCampaignSaveData data))
                data = RunnerCampaignSaveStore.CreateNew(settings);
            data.day = _day;
            data.subscribers = _subscribers;
            data.mentalLevel = _mentalLevel;
            data.mentalExperience = _mentalExperience;
            data.gameSkill = _gameSkill;
            data.gameSkillExperience = _gameSkillExperience;
            data.talkingSkill = _talkingSkill;
            data.talkingSkillExperience = _talkingSkillExperience;
            data.healthStat = _healthStat;
            data.healthStatExperience = _healthStatExperience;
            data.bestBroadcastScore = _bestBroadcastScore;
            data.lifetimeDonations = _lifetimeDonations;
            data.cash = _cash;
            data.pcLevel = _pcLevel;
            data.microphoneLevel = _microphoneLevel;
            data.fitnessLevel = _fitnessLevel;
            data.interiorLevel = _interiorLevel;
            data.campaignFailed = false;
            data.awaitingAdvance = awaitingAdvance;
            data.broadcastPending = _broadcastPending;
            data.selectedAction = _selectedActionName;
            data.selectedBroadcastGame = _selectedBroadcastGame;
            data.records = new List<RunnerCampaignDayRecord>(_records);
            RunnerBroadcastSessionStore.ApplyToSave(data);
            return data;
        }

        private void DeleteSave() => RunnerCampaignSaveStore.Delete(settings);

        private void LoadRoomScene()
        {
            if (!string.IsNullOrWhiteSpace(settings.roomSceneName)) SceneManager.LoadScene(settings.roomSceneName);
        }

        private void TrimRecords()
        {
            int excess = _records.Count - settings.maximumStoredDayRecords;
            if (excess > 0) _records.RemoveRange(0, excess);
        }

        private void BuildInterface()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("STREAM ON campaign UI could not find a Canvas.", this);
                return;
            }

            _font = canvas.GetComponentsInChildren<TMP_Text>(true).Select(text => text.font).FirstOrDefault(font => font != null);
            GameObject root = new GameObject("Campaign UI", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            Stretch(root.GetComponent<RectTransform>());

            RectTransform status = CreateImage("Campaign Status", root.transform, new Color(0.04f, 0.05f, 0.08f, 0.92f));
            Place(status, new Vector2(0.5f, 1f), new Vector2(900f, 42f), new Vector2(-150f, -121f));
            _statusText = CreateText("Status", status, string.Empty, 17, TextAlignmentOptions.Center, new Vector2(870f, 36f), Vector2.zero);

            _preparationPanel = CreateModal("Preparation Panel", root.transform);
            _preparationTitle = CreateText("Title", _preparationPanel.transform, string.Empty, 31, TextAlignmentOptions.Center, new Vector2(620f, 50f), new Vector2(0f, 232f));
            _preparationTitle.color = new Color(0.40f, 0.90f, 0.82f);
            _preparationStats = CreateText("Stats", _preparationPanel.transform, string.Empty, 19, TextAlignmentOptions.Center, new Vector2(620f, 95f), new Vector2(0f, 158f));
            CreateText("Guide", _preparationPanel.transform, "낮 행동을 선택하면 바로 방송을 시작합니다.", 17, TextAlignmentOptions.Center, new Vector2(620f, 32f), new Vector2(0f, 100f));
            BuildActionList(_preparationPanel.transform);
            CreateButton("Records Button", _preparationPanel.transform, "방송 기록", new Color(0.24f, 0.30f, 0.43f), new Vector2(250f, 50f), new Vector2(-140f, -236f), ShowRecords);
            Button newGame = CreateButton("New Campaign Button", _preparationPanel.transform, "새 캠페인", new Color(0.55f, 0.25f, 0.30f), new Vector2(250f, 50f), new Vector2(140f, -236f), RequestNewCampaign);
            _newGameButtonLabel = newGame.GetComponentInChildren<TMP_Text>();

            _settlementPanel = CreateModal("Settlement Panel", root.transform);
            _settlementTitle = CreateText("Title", _settlementPanel.transform, string.Empty, 32, TextAlignmentOptions.Center, new Vector2(620f, 55f), new Vector2(0f, 190f));
            _settlementBody = CreateText("Result", _settlementPanel.transform, string.Empty, 16, TextAlignmentOptions.Center, new Vector2(640f, 390f), new Vector2(0f, 20f));
            Button continueButton = CreateButton("Continue Button", _settlementPanel.transform, "다음 날", new Color(0.18f, 0.72f, 0.64f), new Vector2(300f, 62f), new Vector2(0f, -210f), ContinueAfterSettlement);
            _settlementButtonLabel = continueButton.GetComponentInChildren<TMP_Text>();

            _recordsPanel = CreateModal("Records Panel", root.transform);
            TMP_Text recordsTitle = CreateText("Title", _recordsPanel.transform, "방송 기록", 32, TextAlignmentOptions.Center, new Vector2(620f, 55f), new Vector2(0f, 230f));
            recordsTitle.color = new Color(0.40f, 0.90f, 0.82f);
            _recordsText = CreateText("Records", _recordsPanel.transform, string.Empty, 17, TextAlignmentOptions.TopLeft, new Vector2(590f, 390f), new Vector2(0f, 0f));
            CreateButton("Back Button", _recordsPanel.transform, "돌아가기", new Color(0.24f, 0.30f, 0.43f), new Vector2(280f, 55f), new Vector2(0f, -235f), ReturnToPreparation);

            _endingPanel = CreateModal("Ending Panel", root.transform);
            _endingTitle = CreateText("Title", _endingPanel.transform, string.Empty, 34, TextAlignmentOptions.Center, new Vector2(620f, 65f), new Vector2(0f, 170f));
            _endingBody = CreateText("Ending", _endingPanel.transform, string.Empty, 22, TextAlignmentOptions.Center, new Vector2(620f, 300f), new Vector2(0f, 10f));
            CreateButton("Restart Button", _endingPanel.transform, "새 캠페인 시작", new Color(0.18f, 0.72f, 0.64f), new Vector2(300f, 62f), new Vector2(0f, -210f), () => StartNewCampaign(true));
            HideAllPanels();
        }

        private void BuildActionList(Transform parent)
        {
            RectTransform viewport = CreateImage("Action Viewport", parent, new Color(0.025f, 0.032f, 0.055f, 0.72f));
            Place(viewport, new Vector2(0.5f, 0.5f), new Vector2(560f, 285f), new Vector2(0f, -62f));
            Mask mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            GameObject contentObject = new GameObject("Actions", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport, false);
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 9f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;

            foreach (RunnerCampaignActionDefinition action in settings.dayActions.Where(action => action != null))
            {
                RunnerCampaignActionDefinition captured = action;
                Button button = CreateButton(action.id, content, action.displayName + "  |  " + action.description,
                    action.buttonColor, new Vector2(520f, 58f), Vector2.zero, () => SelectAction(captured));
                LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 58f;
                element.minHeight = 58f;
            }
        }

        private GameObject CreateModal(string name, Transform parent)
        {
            RectTransform panel = CreateImage(name, parent, new Color(0.035f, 0.045f, 0.075f, 0.985f));
            Place(panel, new Vector2(0.5f, 0.5f), new Vector2(700f, 580f), new Vector2(-150f, -4f));
            return panel.gameObject;
        }

        private Button CreateButton(string name, Transform parent, string label, Color color, Vector2 size, Vector2 position, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = CreateImage(name, parent, color);
            Place(rect, new Vector2(0.5f, 0.5f), size, position);
            Button button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.25f);
            button.colors = colors;
            CreateText("Label", rect, label, 18, TextAlignmentOptions.Center, size - new Vector2(20f, 8f), Vector2.zero);
            button.onClick.AddListener(action);
            return button;
        }

        private TMP_Text CreateText(string name, Transform parent, string value, float size, TextAlignmentOptions alignment, Vector2 dimensions, Vector2 position)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
            text.font = _font;
            text.fontSize = size;
            text.text = value;
            text.color = Color.white;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            Place(text.rectTransform, new Vector2(0.5f, 0.5f), dimensions, position);
            return text;
        }

        private static RectTransform CreateImage(string name, Transform parent, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            Image image = obj.GetComponent<Image>();
            image.color = color;
            return obj.GetComponent<RectTransform>();
        }

        private void ShowOnly(GameObject panel)
        {
            _preparationPanel.SetActive(panel == _preparationPanel);
            _settlementPanel.SetActive(panel == _settlementPanel);
            _recordsPanel.SetActive(panel == _recordsPanel);
            _endingPanel.SetActive(panel == _endingPanel);
        }

        private void HideAllPanels()
        {
            if (_preparationPanel != null) _preparationPanel.SetActive(false);
            if (_settlementPanel != null) _settlementPanel.SetActive(false);
            if (_recordsPanel != null) _recordsPanel.SetActive(false);
            if (_endingPanel != null) _endingPanel.SetActive(false);
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static string Signed(int value) => value >= 0 ? "+" + value : value.ToString();
        private static string Signed(float value) => value >= 0f ? "+" + value.ToString("0") : value.ToString("0");
        private static string RatingGrade(float rating) => rating >= 4.6f ? "S" : rating >= 4f ? "A" : rating >= 3.2f ? "B" : rating >= 2.4f ? "C" : "D";
    }
}
