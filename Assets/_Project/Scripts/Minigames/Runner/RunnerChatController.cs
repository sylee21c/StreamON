using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace StreamOn.Minigames.Runner
{
    public enum RunnerChatEvent
    {
        RunStarted, PlayerJumped, PlayerRolled, ObstacleCleared, EnemyDefeated,
        AttackMissed, PlayerHit, LowHealth, GameOver, NewHighScore, QuietMoment,
        PostGameDiscussion, IdleChat, CampaignDayStarted, CampaignGameTraining,
        CampaignTalkingTraining, CampaignRest, CampaignActionSelected, CampaignSettlement, CampaignClear, CampaignFailed,
        BroadcastCompleted, DonationReceived, WitPrompt, WitReplySuccess, WitReplyOkay, WitReplyAwkward,
        TileArenaStarted, TileArenaJumped, TileArenaPickup,
        TileArenaStageCleared, TileArenaPlayerHit, TileArenaLowLives, TileArenaGameOver,
        ChatConflict, ChatFraternization
    }

    public enum SharedChatGameState { Ready, Playing, GameOver }

    public sealed class RunnerChatController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private RunnerGameManager gameManager;
        [SerializeField] private TMP_Text[] messageSlots;
        [Header("Manager (scene-authored UI)")]
        [SerializeField] private RunnerCampaignSettings campaignSettings;
        [SerializeField] private TMP_Text managerStatusText;

        [Header("Chat Typography")]
        [Tooltip("All text inside the shared game-chat panel is rebound to this font at runtime.")]
        [SerializeField] private TMP_FontAsset chatFont;

        [Header("Display Timing")]
        [SerializeField] private float minimumDelay = 0.2f;
        [SerializeField] private float maximumDelay = 0.6f;
        [SerializeField, Min(0f)] private float farewellInitialDelay = .8f;
        [SerializeField, Min(0f)] private float farewellRepeatDelay = .95f;
        [SerializeField] private float[] postGameReactionDelays = { .9f, 1.7f, 2.8f };
        [SerializeField, Range(0f, 1f)] private float minimumHitReactionChance = .45f;
        [SerializeField, Range(0f, 1f)] private float minimumEnemyDefeatReactionChance = .30f;
        [SerializeField, Range(.05f, 1f)] private float minimumAudienceDelayMultiplier = .2f;

        [Header("AI Chat Connection")]
        [SerializeField] private bool useAiChat = true;
        [Tooltip("Editor/desktop development only. Never put an API key in a WebGL build.")]
        [SerializeField] private string endpoint = "https://api.openai.com/v1/responses";
        [Tooltip("WebGL uses this server-side relay instead of calling OpenAI directly. Relative URLs such as /api/stream-on-chat are allowed.")]
        [SerializeField] private string webProxyEndpoint = "";
        [SerializeField] private string model = "gpt-5.6-luna";
        [SerializeField] private string apiKeyEnvironmentVariable = "OPENAI_API_KEY";
        [Tooltip("Turn this off only when endpoint points to your own authenticated proxy.")]
        [SerializeField] private bool requireApiKey = true;
        [SerializeField, Min(0.1f)] private float eventBatchWindow = 0.65f;
        [SerializeField, Min(0.5f)] private float minimumApiInterval = 2.5f;
        [SerializeField, Min(2f)] private float ambientMinimumDelay = 9f;
        [SerializeField, Min(2f)] private float ambientMaximumDelay = 15f;
        [SerializeField, Min(2f)] private float postGameAmbientMinimumDelay = 7f;
        [SerializeField, Min(2f)] private float postGameAmbientMaximumDelay = 12f;
        [SerializeField, Min(2f)] private float idleAmbientMinimumDelay = 11f;
        [SerializeField, Min(2f)] private float idleAmbientMaximumDelay = 18f;

        [Header("Persona Roster")]
        [Tooltip("Custom assets add a persona, or override a built-in persona when their IDs match.")]
        [SerializeField] private RunnerViewerPersona[] customPersonas;
        [SerializeField] private bool replaceBuiltInPersonas;
        [Tooltip("한 번의 플레이에 접속할 서로 다른 페르소나 유형 수")]
        [SerializeField, Range(1, 20)] private int minimumActivePersonas = 4;
        [SerializeField, Range(1, 20)] private int maximumActivePersonas = 6;
        [Tooltip("선택된 페르소나 유형 하나당 생성할 개별 시청자 수")]
        [SerializeField, Range(2, 6)] private int minimumViewersPerPersona = 2;
        [SerializeField, Range(2, 6)] private int maximumViewersPerPersona = 3;
        [SerializeField, Range(4, 80)] private int maximumActiveViewers = 40;
        [Tooltip("매 플레이에 분탕 또는 논쟁형 페르소나를 최소 한 유형 포함")]
        [SerializeField] private bool ensureConflictPersona = true;

        [Header("Social Event Balance")]
        [SerializeField, Min(1)] private int socialEventMinimumViewers = 3;
        [SerializeField, Min(1)] private int socialEventMinimumChatters = 3;
        [SerializeField, Range(0f, 1f)] private float conflictChanceAtZeroHeat = 0.48f;
        [SerializeField, Range(0f, 1f)] private float conflictChanceAtFullHeat = 0.01f;
        [SerializeField, Min(0.01f)] private float conflictHeatCurvePower = 0.65f;
        [SerializeField, Range(0f, 1f)] private float conflictTargetsStreamerChance = 0.32f;
        [SerializeField, Range(0f, 1f)] private float fraternizationChanceAtZeroHeat = 0.34f;
        [SerializeField, Range(0f, 1f)] private float fraternizationChanceAtFullHeat = 0.005f;
        [SerializeField, Min(0.01f)] private float fraternizationHeatCurvePower = 0.72f;
        [SerializeField, Range(0f, 1f)] private float thirdFraternizerChance = 0.48f;
        [SerializeField, Min(0f)] private float socialReplyMinimumSeconds = 3f;
        [SerializeField, Min(0f)] private float socialReplyMaximumSeconds = 5f;
        [SerializeField, Min(0f)] private float socialOpeningDelayMinimumSeconds = 13f;
        [SerializeField, Min(0f)] private float socialOpeningDelayMaximumSeconds = 16f;
        [SerializeField, Min(0)] private int wrongBanReactionMinimumCount = 3;
        [SerializeField, Min(0)] private int wrongBanReactionMaximumCount = 5;
        [SerializeField, Min(0f)] private float wrongBanReactionMinimumDelay = .35f;
        [SerializeField, Min(0f)] private float wrongBanReactionMaximumDelay = .9f;

        private sealed class RenderedChatLine
        {
            public string viewerId;
            public string rendered;
        }

        private readonly Queue<RenderedChatLine> _pending = new Queue<RenderedChatLine>();
        private readonly Queue<RenderedChatLine> _visible = new Queue<RenderedChatLine>();
        private readonly Queue<string> _recentChatContext = new Queue<string>();
        private readonly Queue<RunnerChatEvent> _aiEvents = new Queue<RunnerChatEvent>();
        private readonly List<RunnerViewerData> _activeViewers = new List<RunnerViewerData>();
        private readonly Dictionary<string, Queue<string>> _recentMessagesByViewer = new Dictionary<string, Queue<string>>();
        private readonly Dictionary<string, string> _lastMessageByViewer = new Dictionary<string, string>();
        private readonly HashSet<string> _bannedViewers = new HashSet<string>();
        private Coroutine _displayPump;
        private Coroutine _aiPump;
        private Coroutine _postGamePump;
        private Coroutine _conflictPump;
        private Coroutine _socialDialoguePump;
        private TMP_Text _titleText;
        private float _nextAmbientAt;
        private bool _loggedAiUnavailable;
        private bool _loggedAiConnected;
        private int _runGeneration;
        private float _aiRetryAfter;
        private float _nextAiRequestAt;
        private string _connectionMode = "LOCAL";
        private int _audienceViewerCount;
        private int _chattingViewerCount = 1;
        private float _audienceDelayMultiplier = 1f;
        private float _eventReactionChance = 1f;
        private float _eventReactionCooldown;
        private float _broadcastHeat = 50f;
        private float _nextEventReactionAt;
        private int _lastLoggedViewerCount = -1;
        private int _lastLoggedChatterCount = -1;
        private bool _externalGameBound;
        private string _externalGameTitle = "게임";
        private SharedChatGameState _externalGameState = SharedChatGameState.Ready;
        private RunnerChatSnapshot _externalSnapshot;
        private string _lastDonationNickname;
        private int _lastDonationAmount;
        private string _lastDonationMessage;
        private bool _lastDonationIsLarge;
        private bool _conflictActive;
        private RunnerViewerData _troublemaker;
        private RunnerViewerData _conflictTarget;
        private string _conflictTargetMessage;
        private bool _conflictTargetsStreamer;
        private bool _fraternizationActive;
        private readonly List<RunnerViewerData> _fraternizers = new List<RunnerViewerData>();
        private readonly HashSet<string> _fraternizationOffenders = new HashSet<string>();
        private RunnerViewerData _pendingFraternizer;
        private Coroutine _fraternizationPump;
        private float _socialEventStartedAt;
        private Coroutine _managerRoutine;

        private void Awake()
        {
            useAiChat = RunnerUserSettingsStore.Load(useAiChat).aiChatEnabled;
            if (gameManager == null) gameManager = FindFirstObjectByType<RunnerGameManager>();
            if (campaignSettings != null && RunnerCampaignSaveStore.TryLoad(campaignSettings, out RunnerCampaignSaveData managerSave))
                RefreshManagerStatus(managerSave, BroadcasterProgression.HiredManager(campaignSettings, managerSave));
            EnsureSlots();
            ApplyChatFont();
            _titleText = GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.name == "Title");
            SetConnectionLabel("LOCAL");
            SelectActiveViewers();
        }

        private void ApplyChatFont()
        {
            if (chatFont == null) return;
            foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
                text.font = chatFont;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || Time.unscaledTime < _nextAmbientAt) return;
            if (IsPlayingNow() && !IsSocialEventActive() && (TryStartConflict() || TryStartFraternization()))
            {
                ScheduleNextAmbient(ambientMinimumDelay, ambientMaximumDelay);
                return;
            }
            RunnerChatEvent ambientEvent;
            if (gameManager != null)
            {
                switch (gameManager.State)
                {
                    case RunnerGameState.GameOver:
                        ambientEvent = RunnerChatEvent.PostGameDiscussion;
                        ScheduleNextAmbient(postGameAmbientMinimumDelay, postGameAmbientMaximumDelay);
                        break;
                    case RunnerGameState.Ready:
                        ambientEvent = RunnerChatEvent.IdleChat;
                        ScheduleNextAmbient(idleAmbientMinimumDelay, idleAmbientMaximumDelay);
                        break;
                    default:
                        ambientEvent = RunnerChatEvent.QuietMoment;
                        ScheduleNextAmbient(ambientMinimumDelay, ambientMaximumDelay);
                        break;
                }
            }
            else if (_externalGameBound)
            {
                switch (_externalGameState)
                {
                    case SharedChatGameState.GameOver:
                        ambientEvent = RunnerChatEvent.PostGameDiscussion;
                        ScheduleNextAmbient(postGameAmbientMinimumDelay, postGameAmbientMaximumDelay);
                        break;
                    case SharedChatGameState.Ready:
                        ambientEvent = RunnerChatEvent.IdleChat;
                        ScheduleNextAmbient(idleAmbientMinimumDelay, idleAmbientMaximumDelay);
                        break;
                    default:
                        ambientEvent = RunnerChatEvent.QuietMoment;
                        ScheduleNextAmbient(ambientMinimumDelay, ambientMaximumDelay);
                        break;
                }
            }
            else return;
            React(ambientEvent);
        }

        public void BindExternalGame(string gameTitle)
        {
            gameManager = null;
            _externalGameBound = true;
            _externalGameTitle = string.IsNullOrWhiteSpace(gameTitle) ? "게임" : gameTitle.Trim();
            _externalGameState = SharedChatGameState.Ready;
            _externalSnapshot = new RunnerChatSnapshot { gameTitle = _externalGameTitle, gameState = _externalGameState.ToString() };
            ScheduleNextAmbient(idleAmbientMinimumDelay, idleAmbientMaximumDelay);
        }

        public void UpdateExternalGame(SharedChatGameState state, RunnerChatSnapshot snapshot)
        {
            _externalGameBound = true;
            _externalGameState = state;
            _externalSnapshot = snapshot ?? new RunnerChatSnapshot();
            _externalSnapshot.gameTitle = _externalGameTitle;
            _externalSnapshot.gameState = state.ToString();
        }

        public void ResumeExternalGame()
        {
            _externalGameBound = true;
            _externalGameState = SharedChatGameState.Playing;
            if (_postGamePump != null)
            {
                StopCoroutine(_postGamePump);
                _postGamePump = null;
            }
            ScheduleNextAmbient(ambientMinimumDelay, ambientMaximumDelay);
        }

        public void BeginExternalGameOverChat(bool isNewHighScore)
        {
            if (_postGamePump != null) StopCoroutine(_postGamePump);
            if (isNewHighScore) React(RunnerChatEvent.NewHighScore);
            ScheduleNextAmbient(postGameAmbientMinimumDelay, postGameAmbientMaximumDelay);
            _postGamePump = StartCoroutine(PumpPostGameReactions(_runGeneration));
        }

        public void React(RunnerChatEvent chatEvent)
        {
            if (IsSocialEventActive() && chatEvent != RunnerChatEvent.ChatConflict
                && chatEvent != RunnerChatEvent.ChatFraternization
                && IsGameplayReaction(chatEvent) && UnityEngine.Random.value < 0.82f) return;
            if (!ShouldReactToLiveEvent(chatEvent)) return;

            if (!CanUseAi(out string apiKey))
            {
                SetConnectionLabel("LOCAL");
                EnqueueLocal(chatEvent);
                return;
            }
            SetConnectionLabel("CONNECTING");
            if (!_loggedAiConnected)
            {
                Debug.Log("STREAM ON AI chat: API key detected. Sending a Responses API request.");
                _loggedAiConnected = true;
            }
            _aiEvents.Enqueue(chatEvent);
            if (_aiPump == null) _aiPump = StartCoroutine(PumpAi(apiKey, _runGeneration));
        }

        public bool AiEnabled => useAiChat;

        public IEnumerator GenerateWitInteraction(string situation, IReadOnlyCollection<string> recentPrompts,
            Action<RunnerGeneratedWitPrompt> onComplete)
        {
            if (!CanUseAi(out string apiKey))
            {
                onComplete?.Invoke(null);
                yield break;
            }
            RunnerChatSnapshot snapshot = gameManager != null
                ? gameManager.CreateChatSnapshot(situation)
                : _externalSnapshot ?? new RunnerChatSnapshot { gameTitle = _externalGameTitle };
            snapshot.events = situation;
            snapshot.recentMessages = string.Join(" | ", _recentChatContext);
            RunnerGeneratedWitPrompt generated = null;
            OpenAiRunnerChatClient client = new OpenAiRunnerChatClient(ActiveAiEndpoint(), model, apiKey);
            yield return client.GenerateWit(snapshot, recentPrompts, value => generated = value, _ => { });
            onComplete?.Invoke(generated);
        }

        public string PickDonationViewerNickname()
        {
            IReadOnlyList<RunnerViewerData> speaking = SpeakingViewers();
            if (speaking.Count == 0) speaking = _activeViewers;
            return speaking.Count > 0 ? speaking[UnityEngine.Random.Range(0, speaking.Count)].nickname : "익명의 시청자";
        }

        public void OnDonationReceived(string donorNickname, int amount, string donationMessage = null, bool isLarge = false)
        {
            _lastDonationNickname = donorNickname ?? string.Empty;
            _lastDonationAmount = Mathf.Max(0, amount);
            _lastDonationMessage = donationMessage ?? string.Empty;
            _lastDonationIsLarge = isLarge;
            _recentChatContext.Enqueue($"{donorNickname}님이 {amount:N0}원을 후원함");
            while (_recentChatContext.Count > 6) _recentChatContext.Dequeue();
            React(RunnerChatEvent.DonationReceived);
        }

        public void ConfigureAudience(int currentViewers, int chattingViewers, float delayMultiplier,
            float eventReactionChance, float eventReactionCooldown, float broadcastHeat = 50f)
        {
            _audienceViewerCount = Mathf.Max(0, currentViewers);
            _chattingViewerCount = Mathf.Clamp(chattingViewers, 0, Mathf.Min(_audienceViewerCount, _activeViewers.Count));
            _audienceDelayMultiplier = Mathf.Clamp(delayMultiplier, minimumAudienceDelayMultiplier, 1f);
            _eventReactionChance = Mathf.Clamp01(eventReactionChance);
            _eventReactionCooldown = Mathf.Max(0f, eventReactionCooldown);
            _broadcastHeat = Mathf.Clamp(broadcastHeat, 0f, 100f);
            if (_lastLoggedViewerCount != _audienceViewerCount || _lastLoggedChatterCount != _chattingViewerCount)
            {
                Debug.Log($"STREAM ON audience: {_audienceViewerCount:N0} viewers, {_chattingViewerCount:N0} active chatters.", this);
                _lastLoggedViewerCount = _audienceViewerCount;
                _lastLoggedChatterCount = _chattingViewerCount;
            }
            RefreshTitle();
        }

        public void SetAiEnabled(bool enabled)
        {
            useAiChat = enabled;
            RunnerUserSettingsData userSettings = RunnerUserSettingsStore.Load(enabled);
            userSettings.aiChatEnabled = enabled;
            RunnerUserSettingsStore.Save(userSettings);
            SetConnectionLabel(enabled ? "CONNECTING" : "LOCAL");
        }

        public void ResetChat()
        {
            _runGeneration++;
            _pending.Clear();
            _visible.Clear();
            _recentChatContext.Clear();
            _recentMessagesByViewer.Clear();
            _lastMessageByViewer.Clear();
            _bannedViewers.Clear();
            _aiEvents.Clear();
            if (_displayPump != null) StopCoroutine(_displayPump);
            if (_aiPump != null) StopCoroutine(_aiPump);
            if (_postGamePump != null) StopCoroutine(_postGamePump);
            if (_conflictPump != null) StopCoroutine(_conflictPump);
            if (_socialDialoguePump != null) StopCoroutine(_socialDialoguePump);
            if (_fraternizationPump != null) StopCoroutine(_fraternizationPump);
            _displayPump = null;
            _aiPump = null;
            _postGamePump = null;
            _conflictPump = null;
            _socialDialoguePump = null;
            _conflictActive = false;
            _troublemaker = null;
            _conflictTarget = null;
            _conflictTargetsStreamer = false;
            _fraternizationActive = false;
            _fraternizers.Clear();
            _fraternizationOffenders.Clear();
            _pendingFraternizer = null;
            _fraternizationPump = null;
            _nextEventReactionAt = 0f;
            _lastDonationNickname = string.Empty;
            _lastDonationAmount = 0;
            _lastDonationMessage = string.Empty;
            _lastDonationIsLarge = false;
            SelectActiveViewers();
            ScheduleNextAmbient(ambientMinimumDelay, ambientMaximumDelay);
            RefreshSlots();
        }

        public void BeginGameOverChat(bool isNewHighScore)
        {
            BeginRunEndedChat(isNewHighScore, false);
        }

        public void ResumeRunChat()
        {
            if (_postGamePump != null) StopCoroutine(_postGamePump);
            _postGamePump = null;
            ScheduleNextAmbient(ambientMinimumDelay, ambientMaximumDelay);
            React(RunnerChatEvent.RunStarted);
        }

        public void BeginRunEndedChat(bool isNewHighScore, bool completedTimeLimit)
        {
            if (_postGamePump != null) StopCoroutine(_postGamePump);
            if (completedTimeLimit)
            {
                BeginBroadcastEndingChat();
                if (isNewHighScore) React(RunnerChatEvent.NewHighScore);
                return;
            }
            React(RunnerChatEvent.GameOver);
            if (isNewHighScore) React(RunnerChatEvent.NewHighScore);
            ScheduleNextAmbient(postGameAmbientMinimumDelay, postGameAmbientMaximumDelay);
            _postGamePump = StartCoroutine(PumpPostGameReactions(_runGeneration));
        }

        public void BeginBroadcastEndingChat()
        {
            if (_postGamePump != null) StopCoroutine(_postGamePump);
            StopActiveSocialEvent();
            React(RunnerChatEvent.BroadcastCompleted);
            _postGamePump = StartCoroutine(PumpBroadcastFarewells(_runGeneration));
        }

        private void StopActiveSocialEvent()
        {
            _conflictActive = false;
            _fraternizationActive = false;
            _fraternizers.Clear();
            _fraternizationOffenders.Clear();
            _pendingFraternizer = null;
            if (_conflictPump != null) StopCoroutine(_conflictPump);
            if (_fraternizationPump != null) StopCoroutine(_fraternizationPump);
            if (_socialDialoguePump != null) StopCoroutine(_socialDialoguePump);
            _conflictPump = null;
            _fraternizationPump = null;
            _socialDialoguePump = null;
            RefreshTitle();
        }

        private IEnumerator PumpBroadcastFarewells(int generation)
        {
            yield return new WaitForSecondsRealtime(farewellInitialDelay);
            if (generation != _runGeneration) yield break;
            React(RunnerChatEvent.BroadcastCompleted);
            yield return new WaitForSecondsRealtime(farewellRepeatDelay);
            if (generation != _runGeneration) yield break;
            React(RunnerChatEvent.BroadcastCompleted);
            yield return new WaitForSecondsRealtime(farewellRepeatDelay);
            if (generation != _runGeneration) yield break;
            React(RunnerChatEvent.BroadcastCompleted);
            _postGamePump = null;
        }

        private IEnumerator PumpPostGameReactions(int generation)
        {
            float[] delays = postGameReactionDelays != null && postGameReactionDelays.Length > 0
                ? postGameReactionDelays : new[] { 1f };
            foreach (float delay in delays)
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(0f, delay));
                if (!IsCurrentGameOver(generation)) yield break;
                React(RunnerChatEvent.PostGameDiscussion);
            }
            _postGamePump = null;
        }

        private bool IsCurrentGameOver(int generation) => generation == _runGeneration
            && (gameManager != null ? gameManager.State == RunnerGameState.GameOver
                : _externalGameBound && _externalGameState == SharedChatGameState.GameOver);

        private void ScheduleNextAmbient(float minimum, float maximum) =>
            _nextAmbientAt = Time.unscaledTime + UnityEngine.Random.Range(minimum, maximum) * _audienceDelayMultiplier;

        private IEnumerator PumpAi(string apiKey, int generation)
        {
            while (_aiEvents.Count > 0 && generation == _runGeneration)
            {
                float wait = Mathf.Max(eventBatchWindow, _nextAiRequestAt - Time.unscaledTime);
                if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
                List<RunnerChatEvent> events = new List<RunnerChatEvent>();
                while (_aiEvents.Count > 0) events.Add(_aiEvents.Dequeue());
                bool conflictRequest = events.Contains(RunnerChatEvent.ChatConflict);
                bool fraternizationRequest = events.Contains(RunnerChatEvent.ChatFraternization);
                bool socialRequest = conflictRequest || fraternizationRequest;
                string eventText = BuildEventText(events);
                RunnerChatSnapshot snapshot = gameManager != null
                    ? gameManager.CreateChatSnapshot(eventText)
                    : _externalSnapshot ?? new RunnerChatSnapshot { gameTitle = _externalGameTitle };
                snapshot.events = eventText;
                snapshot.recentMessages = string.Join(" | ", _recentChatContext);
                snapshot.conflictActive = _conflictActive;
                snapshot.conflictTroublemakerId = _troublemaker?.viewerId ?? string.Empty;
                snapshot.conflictTroublemakerNickname = _troublemaker?.nickname ?? string.Empty;
                snapshot.conflictTargetId = _conflictTarget?.viewerId ?? string.Empty;
                snapshot.conflictTargetNickname = _conflictTarget?.nickname ?? string.Empty;
                snapshot.conflictTargetMessage = _conflictTargetMessage ?? string.Empty;
                snapshot.conflictTargetsStreamer = _conflictTargetsStreamer;
                snapshot.fraternizationActive = _fraternizationActive;
                snapshot.socialViewer1Id = _fraternizers.Count > 0 ? _fraternizers[0].viewerId : string.Empty;
                snapshot.socialViewer1Nickname = _fraternizers.Count > 0 ? _fraternizers[0].nickname : string.Empty;
                snapshot.socialViewer2Id = _fraternizers.Count > 1 ? _fraternizers[1].viewerId : string.Empty;
                snapshot.socialViewer2Nickname = _fraternizers.Count > 1 ? _fraternizers[1].nickname : string.Empty;
                snapshot.socialViewer3Id = _fraternizers.Count > 2 ? _fraternizers[2].viewerId : string.Empty;
                snapshot.socialViewer3Nickname = _fraternizers.Count > 2 ? _fraternizers[2].nickname : string.Empty;
                if (events.Contains(RunnerChatEvent.DonationReceived))
                {
                    snapshot.lastDonationNickname = _lastDonationNickname;
                    snapshot.lastDonationAmount = _lastDonationAmount;
                    snapshot.lastDonationMessage = _lastDonationMessage;
                    snapshot.lastDonationIsLarge = _lastDonationIsLarge;
                }

                RunnerGeneratedChatBatch generated = null;
                string failure = null;
                OpenAiRunnerChatClient client = new OpenAiRunnerChatClient(ActiveAiEndpoint(), model, apiKey);
                _nextAiRequestAt = Time.unscaledTime + minimumApiInterval;
                IReadOnlyList<RunnerViewerData> speakingViewers = IsSocialEventActive() ? SocialEventViewers() : SpeakingViewers();
                if (speakingViewers.Count == 0) continue;
                yield return client.Generate(speakingViewers, snapshot, value => generated = value, error => failure = error);

                if (generation != _runGeneration) yield break;
                if (socialRequest && !IsSocialEventActive()) continue;
                if (generated?.messages != null)
                {
                    if (socialRequest)
                    {
                        if (_socialDialoguePump != null) StopCoroutine(_socialDialoguePump);
                        _socialDialoguePump = StartCoroutine(PumpGeneratedSocialDialogue(generated.messages, generation));
                        SetConnectionLabel("AI");
                        continue;
                    }
                    int accepted = 0;
                    foreach (RunnerGeneratedChat message in generated.messages)
                    {
                        RunnerViewerData viewer = _activeViewers.FirstOrDefault(item => item.viewerId == message.speakerId);
                        if (viewer == null || _bannedViewers.Contains(viewer.viewerId)
                            || !TrySanitize(message.message, out string safeMessage)) continue;
                        if (EnqueueRendered(viewer, safeMessage)) accepted++;
                    }
                    if (accepted > 0)
                    {
                        SetConnectionLabel("AI");
                        Debug.Log($"STREAM ON AI chat: received {accepted} generated message(s).");
                        continue;
                    }
                    failure = "No generated message matched an active persona.";
                }

                if (!_loggedAiUnavailable)
                {
                    Debug.LogWarning("STREAM ON AI chat fell back to local messages. " + failure);
                    _loggedAiUnavailable = true;
                }
                _aiRetryAfter = Time.unscaledTime + 30f;
                SetConnectionLabel("LOCAL");
                foreach (RunnerChatEvent chatEvent in events.Take(2)) EnqueueLocal(chatEvent);
            }
            _aiPump = null;
        }

        private bool CanUseAi(out string apiKey)
        {
            apiKey = ReadApiKey();
            bool available = useAiChat && !string.IsNullOrWhiteSpace(ActiveAiEndpoint()) && !string.IsNullOrWhiteSpace(model)
                && (!RequiresClientApiKey() || !string.IsNullOrWhiteSpace(apiKey))
                && Time.unscaledTime >= _aiRetryAfter;
            if (!available && useAiChat && !_loggedAiUnavailable)
            {
                Debug.Log(IsWebPlayer()
                    ? "STREAM ON AI chat is using local fallback. Set the Web Proxy Endpoint before building for WebGL."
                    : "STREAM ON AI chat is using local fallback. Set " + apiKeyEnvironmentVariable
                        + " and restart Unity to enable the API connection.");
                _loggedAiUnavailable = true;
            }
            return available;
        }

        private string ReadApiKey()
        {
            if (!RequiresClientApiKey()) return string.Empty;
            if (string.IsNullOrWhiteSpace(apiKeyEnvironmentVariable)) return string.Empty;
            string value = Environment.GetEnvironmentVariable(apiKeyEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(value)) return value;

            // Unity Hub can retain an old process environment even after the user-level
            // variable is created. Read the Windows user store directly as a fallback.
            try
            {
                value = Environment.GetEnvironmentVariable(apiKeyEnvironmentVariable, EnvironmentVariableTarget.User);
            }
            catch (PlatformNotSupportedException)
            {
                value = string.Empty;
            }
            return value ?? string.Empty;
        }

        private string ActiveAiEndpoint() => IsWebPlayer() ? webProxyEndpoint : endpoint;

        private bool RequiresClientApiKey() => !IsWebPlayer() && requireApiKey;

        private static bool IsWebPlayer()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }

        private void SetConnectionLabel(string mode)
        {
            _connectionMode = mode;
            RefreshTitle();
        }

        private void RefreshTitle()
        {
            if (_titleText == null) return;
            if (_conflictActive)
                _titleText.text = $"채팅  ·  {_connectionMode}\n현재 시청자 {_audienceViewerCount:N0}명  ·  <color=#FF665F>분탕 유저를 클릭해 밴</color>";
            else _titleText.text = $"채팅  ·  {_connectionMode}\n현재 시청자 {_audienceViewerCount:N0}명";
        }

        private bool IsSocialEventActive() => _conflictActive || _fraternizationActive;

        private bool IsPlayingNow() => gameManager != null
            ? gameManager.State == RunnerGameState.Playing
            : _externalGameBound && _externalGameState == SharedChatGameState.Playing;

        private bool TryStartConflict()
        {
            if (_audienceViewerCount < socialEventMinimumViewers || _chattingViewerCount < socialEventMinimumChatters) return false;
            float chance = Mathf.Lerp(conflictChanceAtZeroHeat, conflictChanceAtFullHeat,
                Mathf.Pow(Mathf.Clamp01(_broadcastHeat / 100f), conflictHeatCurvePower));
            if (UnityEngine.Random.value > chance) return false;

            RunnerViewerData[] troublemakers = _activeViewers.Where(viewer => !_bannedViewers.Contains(viewer.viewerId)
                && IsConflictViewer(viewer)).ToArray();
            if (troublemakers.Length == 0) return false;
            _troublemaker = troublemakers[UnityEngine.Random.Range(0, troublemakers.Length)];

            _conflictTargetsStreamer = UnityEngine.Random.value < conflictTargetsStreamerChance;
            if (!_conflictTargetsStreamer)
            {
                RunnerViewerData[] targets = SpeakingViewers().Where(viewer => viewer.viewerId != _troublemaker.viewerId
                    && !IsConflictViewer(viewer) && _lastMessageByViewer.ContainsKey(viewer.viewerId)).ToArray();
                if (targets.Length > 0)
                {
                    _conflictTarget = targets[UnityEngine.Random.Range(0, targets.Length)];
                    _conflictTargetMessage = _lastMessageByViewer[_conflictTarget.viewerId];
                }
                else _conflictTargetsStreamer = true;
            }
            if (_conflictTargetsStreamer)
            {
                _conflictTarget = null;
                _conflictTargetMessage = "방금 플레이";
            }
            _conflictActive = true;
            RefreshTitle();
            EnqueueRendered(_troublemaker, _conflictTargetsStreamer
                ? "아니 이걸 왜 맞음? 이 정도면 겜 접어야지 ㅋㅋ"
                : $"@{_conflictTarget.nickname} {ShortConflictFragment(_conflictTargetMessage)}가 그렇게 어렵냐? 겜안분 티내네 ㅋㅋ");
            React(RunnerChatEvent.ChatConflict);
            if (_conflictPump != null) StopCoroutine(_conflictPump);
            _conflictPump = StartCoroutine(PumpConflictFollowups(_runGeneration));
            TryScheduleManager(false);
            return true;
        }

        private bool TryStartFraternization()
        {
            if (_audienceViewerCount < socialEventMinimumViewers || _chattingViewerCount < socialEventMinimumChatters) return false;
            float chance = Mathf.Lerp(fraternizationChanceAtZeroHeat, fraternizationChanceAtFullHeat,
                Mathf.Pow(Mathf.Clamp01(_broadcastHeat / 100f), fraternizationHeatCurvePower));
            if (UnityEngine.Random.value > chance) return false;
            RunnerViewerData[] candidates = SpeakingViewers().Where(viewer => !IsConflictViewer(viewer)).ToArray();
            if (candidates.Length < 2) return false;
            candidates = candidates.OrderBy(_ => UnityEngine.Random.value).ToArray();
            _fraternizers.Clear();
            _fraternizationOffenders.Clear();
            _fraternizers.Add(candidates[0]);
            _fraternizers.Add(candidates[1]);
            _pendingFraternizer = candidates.Length >= 3 && UnityEngine.Random.value < thirdFraternizerChance ? candidates[2] : null;
            _fraternizationActive = true;
            _socialEventStartedAt = Time.unscaledTime;
            MarkFraternizationOffender(_fraternizers[0]);
            RefreshTitle();
            EnqueueRendered(_fraternizers[0], $"@{_fraternizers[1].nickname} 오늘도 오셨네요 ㅋㅋ");
            React(RunnerChatEvent.ChatFraternization);
            if (_fraternizationPump != null) StopCoroutine(_fraternizationPump);
            _fraternizationPump = StartCoroutine(PumpFraternizationFollowups(_runGeneration));
            TryScheduleManager(true);
            return true;
        }

        private void TryScheduleManager(bool fraternization)
        {
            if (campaignSettings == null || !RunnerCampaignSaveStore.TryLoad(campaignSettings, out RunnerCampaignSaveData save)) return;
            ManagerTierRule manager = BroadcasterProgression.HiredManager(campaignSettings, save);
            if (manager == null || save.managerUsesRemaining <= 0 || (fraternization && !manager.handlesFraternization)) return;
            if (_managerRoutine != null) StopCoroutine(_managerRoutine);
            float delayMultiplier = 1f - Mathf.Max(0, save.pcLevel - 1) * campaignSettings.managerDelayReductionPerPcUpgrade;
            _managerRoutine = StartCoroutine(ManagerHandleRoutine(manager, fraternization, Mathf.Max(0f, delayMultiplier)));
            RefreshManagerStatus(save, manager);
        }

        private IEnumerator ManagerHandleRoutine(ManagerTierRule manager, bool fraternization, float delayMultiplier)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, manager.handlingDelaySeconds * delayMultiplier));
            _managerRoutine = null;
            if (fraternization ? !_fraternizationActive : !_conflictActive) yield break;
            if (!RunnerCampaignSaveStore.TryLoad(campaignSettings, out RunnerCampaignSaveData save) || save.managerUsesRemaining <= 0) yield break;
            save.managerUsesRemaining--;
            RunnerCampaignSaveStore.Save(campaignSettings, save, true);
            if (fraternization)
            {
                string[] offenders = _fraternizationOffenders.Take(Mathf.Max(0, _fraternizationOffenders.Count - 1)).ToArray();
                foreach (string viewerId in offenders) ManagerBan(viewerId);
                _fraternizationActive = false;
                _fraternizationOffenders.Clear();
                _fraternizers.Clear();
                if (_fraternizationPump != null) StopCoroutine(_fraternizationPump);
                _fraternizationPump = null;
            }
            else
            {
                ManagerBan(_troublemaker?.viewerId);
                _conflictActive = false;
                if (_conflictPump != null) StopCoroutine(_conflictPump);
                _conflictPump = null;
            }
            EnqueueSystemMessage($"{manager.displayName}가 채팅을 정리했습니다.");
            RefreshTitle();
            RefreshManagerStatus(save, manager);
        }

        private void ManagerBan(string viewerId)
        {
            if (string.IsNullOrWhiteSpace(viewerId) || _bannedViewers.Contains(viewerId)) return;
            RunnerViewerData viewer = _activeViewers.FirstOrDefault(item => item.viewerId == viewerId);
            _bannedViewers.Add(viewerId);
            RemoveViewerChatHistory(viewerId);
            if (viewer != null) EnqueueSystemMessage($"{viewer.nickname} 님이 강제 퇴장되었습니다.");
        }

        private void RefreshManagerStatus(RunnerCampaignSaveData save, ManagerTierRule manager)
        {
            if (managerStatusText == null) return;
            managerStatusText.text = manager == null ? "매니저 없음" : $"{manager.displayName} · 남은 처리 {save.managerUsesRemaining}회";
        }

        private static bool IsConflictViewer(RunnerViewerData viewer) => viewer != null
            && (viewer.personaId == "baiter" || viewer.personaId == "chat_fighter");

        private static bool IsGameplayReaction(RunnerChatEvent chatEvent) => chatEvent == RunnerChatEvent.PlayerJumped
            || chatEvent == RunnerChatEvent.PlayerRolled || chatEvent == RunnerChatEvent.ObstacleCleared
            || chatEvent == RunnerChatEvent.EnemyDefeated || chatEvent == RunnerChatEvent.AttackMissed
            || chatEvent == RunnerChatEvent.PlayerHit || chatEvent == RunnerChatEvent.LowHealth
            || chatEvent == RunnerChatEvent.TileArenaJumped || chatEvent == RunnerChatEvent.TileArenaPickup
            || chatEvent == RunnerChatEvent.TileArenaStageCleared || chatEvent == RunnerChatEvent.TileArenaPlayerHit
            || chatEvent == RunnerChatEvent.TileArenaLowLives;

        private void EnqueueConflictOpening()
        {
            if (!_conflictActive || _troublemaker == null) return;
            if (_socialDialoguePump != null) StopCoroutine(_socialDialoguePump);
            _socialDialoguePump = StartCoroutine(PumpLocalConflictOpening(_runGeneration));
        }

        private IEnumerator PumpLocalConflictOpening(int generation)
        {
            yield return SocialReplyDelay();
            if (!_conflictActive || generation != _runGeneration) yield break;
            if (_conflictTargetsStreamer) EnqueueConflictBystander($"@{_troublemaker.nickname} 싫으면 보지마", false);
            else if (_conflictTarget != null) EnqueueRendered(_conflictTarget, $"@{_troublemaker.nickname} 니가 해보던가 입만 살았네");
            yield return SocialReplyDelay();
            if (!_conflictActive || generation != _runGeneration) yield break;
            EnqueueRendered(_troublemaker, _conflictTargetsStreamer
                ? "못한 걸 못한다 하지 그럼 뭐라함?"
                : $"@{_conflictTarget.nickname} 내가 님보단 잘함 꼬우면 닉 까셈");
            yield return SocialReplyDelay();
            if (!_conflictActive || generation != _runGeneration) yield break;
            EnqueueConflictBystander("밴 좀 해주세요 방 분위기 망치네", true);
            _socialDialoguePump = null;
        }

        private IEnumerator PumpGeneratedSocialDialogue(RunnerGeneratedChat[] messages, int generation)
        {
            foreach (RunnerGeneratedChat message in messages)
            {
                yield return SocialReplyDelay();
                if (generation != _runGeneration || !IsSocialEventActive()) yield break;
                RunnerViewerData viewer = _activeViewers.FirstOrDefault(item => item.viewerId == message.speakerId);
                if (viewer == null || _bannedViewers.Contains(viewer.viewerId)
                    || !TrySanitize(message.message, out string safeMessage)) continue;
                if (_fraternizationActive && _fraternizers.Contains(viewer) && safeMessage.Contains("@"))
                    MarkFraternizationOffender(viewer);
                EnqueueRendered(viewer, safeMessage);
            }
            _socialDialoguePump = null;
        }

        private void EnqueueFraternizationOpening()
        {
            if (!_fraternizationActive || _fraternizers.Count < 2) return;
            if (_socialDialoguePump != null) StopCoroutine(_socialDialoguePump);
            _socialDialoguePump = StartCoroutine(PumpLocalFraternizationOpening(_runGeneration));
        }

        private IEnumerator PumpLocalFraternizationOpening(int generation)
        {
            yield return SocialReplyDelay();
            if (!_fraternizationActive || generation != _runGeneration || _fraternizers.Count < 2) yield break;
            MarkFraternizationOffender(_fraternizers[1]);
            EnqueueRendered(_fraternizers[1], $"@{_fraternizers[0].nickname} ㅎㅇㅎㅇ 오늘도 있네");
            yield return SocialReplyDelay();
            if (!_fraternizationActive || generation != _runGeneration || _fraternizers.Count < 2) yield break;
            EnqueueRendered(_fraternizers[0], $"@{_fraternizers[1].nickname} 어제 그 얘기 어떻게 됐어요?");
            yield return SocialReplyDelay();
            if (!_fraternizationActive || generation != _runGeneration) yield break;
            EnqueueSocialBystander("둘이 따로 연락하면 안됨?", false);
            _socialDialoguePump = null;
        }

        private IEnumerator PumpFraternizationFollowups(int generation)
        {
            int turn = 0;
            yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(socialOpeningDelayMinimumSeconds,
                Mathf.Max(socialOpeningDelayMinimumSeconds, socialOpeningDelayMaximumSeconds)));
            while (_fraternizationActive && generation == _runGeneration)
            {
                yield return SocialReplyDelay();
                if (!_fraternizationActive || generation != _runGeneration) break;
                ApplyFraternizationTick();
                if (_pendingFraternizer != null && turn == 1)
                {
                    RunnerViewerData joining = _pendingFraternizer;
                    _pendingFraternizer = null;
                    _fraternizers.Add(joining);
                    MarkFraternizationOffender(joining);
                    EnqueueRendered(joining, $"@{_fraternizers[0].nickname} 저도 기억나요 ㅋㅋ 어제도 봄");
                    RefreshTitle();
                }
                else if (_fraternizers.Count >= 2 && turn % 3 != 2)
                {
                    RunnerViewerData speaker = _fraternizers[turn % _fraternizers.Count];
                    RunnerViewerData target = _fraternizers[(turn + 1) % _fraternizers.Count];
                    MarkFraternizationOffender(speaker);
                    string[] lines = { "오늘도 늦게까지 봄?", "어제 그 사람 또 옴?", "저번에 말한 거 봤어요?", "아 그때 개웃겼는데 ㅋㅋ" };
                    EnqueueRendered(speaker, $"@{target.nickname} {lines[UnityEngine.Random.Range(0, lines.Length)]}");
                }
                else
                {
                    string[] reactions = { "친목 좀 그만", "또 시작이네", "둘이 개인톡해", "여기 단톡방임?", "보기 싫다 진짜", "친목 밴 안함?", "겜 얘기는 아무도 안하네" };
                    EnqueueSocialBystander(reactions[UnityEngine.Random.Range(0, reactions.Length)], false);
                }
                turn++;
            }
            _fraternizationPump = null;
        }

        private void EnqueueSocialBystander(string message, bool includeParticipants)
        {
            RunnerViewerData[] candidates = SpeakingViewers().Where(viewer => includeParticipants
                || !_fraternizers.Contains(viewer)).ToArray();
            if (candidates.Length > 0) EnqueueRendered(candidates[UnityEngine.Random.Range(0, candidates.Length)], message);
        }

        private void MarkFraternizationOffender(RunnerViewerData viewer)
        {
            if (viewer != null && _fraternizationOffenders.Add(viewer.viewerId)) RefreshTitle();
        }

        private WaitForSecondsRealtime SocialReplyDelay() =>
            new WaitForSecondsRealtime(UnityEngine.Random.Range(socialReplyMinimumSeconds,
                Mathf.Max(socialReplyMinimumSeconds, socialReplyMaximumSeconds)));

        private IEnumerator PumpConflictFollowups(int generation)
        {
            int turn = 0;
            yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(socialOpeningDelayMinimumSeconds,
                Mathf.Max(socialOpeningDelayMinimumSeconds, socialOpeningDelayMaximumSeconds)));
            while (_conflictActive && generation == _runGeneration)
            {
                yield return SocialReplyDelay();
                if (!_conflictActive || generation != _runGeneration) break;
                switch (turn++ % 4)
                {
                    case 0:
                        if (_conflictTargetsStreamer) EnqueueConflictBystander($"@{_troublemaker.nickname} 니 방송 가서 해", false);
                        else EnqueueRendered(_conflictTarget, $"@{_troublemaker.nickname} 말로는 누가 못함 ㅋㅋ");
                        break;
                    case 1:
                        EnqueueRendered(_troublemaker, _conflictTargetsStreamer
                            ? "팬들 몰려와서 쉴드치는 거 봐라 ㅋㅋ"
                            : $"@{_conflictTarget.nickname} 긁혔네 ㅋㅋㅋ");
                        break;
                    case 2:
                        EnqueueConflictBystander("채팅창 진짜 난리났네 ㅋㅋ", false);
                        break;
                    default:
                        EnqueueConflictBystander($"@{_troublemaker.nickname} 그만 좀 해라", true);
                        break;
                }
            }
            _conflictPump = null;
        }

        private void EnqueueConflictBystander(string message, bool preferPeacekeeper)
        {
            RunnerViewerData[] candidates = SpeakingViewers().Where(viewer => viewer != _troublemaker
                && viewer != _conflictTarget && (!preferPeacekeeper || viewer.personaId == "peacekeeper")).ToArray();
            if (candidates.Length == 0)
                candidates = SpeakingViewers().Where(viewer => viewer != _troublemaker && viewer != _conflictTarget).ToArray();
            if (candidates.Length > 0) EnqueueRendered(candidates[UnityEngine.Random.Range(0, candidates.Length)], message);
        }

        private static string ShortConflictFragment(string message)
        {
            string value = (message ?? "방금 한 말").Trim();
            if (value.Length > 14) value = value.Substring(0, 14).Trim();
            return string.IsNullOrWhiteSpace(value) ? "방금 한 말" : value;
        }

        private void OnViewerClicked(string viewerId)
        {
            if (string.IsNullOrWhiteSpace(viewerId) || _bannedViewers.Contains(viewerId)) return;
            RunnerViewerData clickedViewer = _activeViewers.FirstOrDefault(viewer => viewer.viewerId == viewerId);
            if (clickedViewer == null) return;
            bool conflictCorrect = _conflictActive && _troublemaker != null && viewerId == _troublemaker.viewerId;
            bool fraternizationCorrect = _fraternizationActive && _fraternizationOffenders.Contains(viewerId);
            bool correct = conflictCorrect || fraternizationCorrect;
            _bannedViewers.Add(viewerId);
            RemoveViewerChatHistory(viewerId);
            EnqueueSystemMessage($"{clickedViewer.nickname} 님이 강제 퇴장되었습니다.");

            if (!correct)
            {
                ApplyModerationResult(false);
                if (_conflictTarget != null && viewerId == _conflictTarget.viewerId)
                {
                    _conflictTarget = null;
                    _conflictTargetsStreamer = true;
                }
                StartCoroutine(PumpWrongBanReactions(_runGeneration));
                return;
            }

            if (campaignSettings != null && RunnerCampaignSaveStore.TryLoad(campaignSettings, out RunnerCampaignSaveData experienceSave))
            {
                BroadcasterProgression.AddBroadcastExperience(campaignSettings, experienceSave, campaignSettings.correctModerationExperience);
                RunnerCampaignSaveStore.Save(campaignSettings, experienceSave, true);
            }

            if (fraternizationCorrect)
            {
                _fraternizers.RemoveAll(viewer => viewer.viewerId == viewerId);
                _fraternizationOffenders.Remove(viewerId);
                if (_fraternizationOffenders.Count <= 1)
                {
                    _fraternizationActive = false;
                    _pendingFraternizer = null;
                    if (_fraternizationPump != null) StopCoroutine(_fraternizationPump);
                    _fraternizationPump = null;
                    _fraternizers.Clear();
                    ApplyFraternizationResolved(Time.unscaledTime - _socialEventStartedAt);
                    EnqueueSocialBystander("정리됐네 굿", false);
                }
                else EnqueueSocialBystander("남은 사람도 정리해야지", false);
                RefreshTitle();
                return;
            }

            _conflictActive = false;
            if (_conflictPump != null) StopCoroutine(_conflictPump);
            _conflictPump = null;
            ApplyModerationResult(true);
            EnqueueConflictBystander("밴 굿", false);
            EnqueueConflictBystander("드디어 조용해지겠네", false);
            RefreshTitle();
        }

        private IEnumerator PumpWrongBanReactions(int generation)
        {
            string[] reactions =
            {
                "?", "??", "뭐함?", "아니 왜?", "뭐 잘못했음?", "아니 무슨 일인데", "갑자기?", "뭐하냐?",
                "왜 밴함?", "예?", "아니 쟤가 뭘했다고", "잘못 누른거임?", "뭔데 갑자기", "멀쩡한 사람 왜 자름",
                "에반데", "???", "아니 이유라도 말해", "방금 뭐임?", "왜저래", "이건 좀..."
            };
            int minimum = Mathf.Max(0, wrongBanReactionMinimumCount);
            int maximum = Mathf.Max(minimum, wrongBanReactionMaximumCount);
            int count = UnityEngine.Random.Range(minimum, maximum + 1);
            for (int i = 0; i < count; i++)
            {
                yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(wrongBanReactionMinimumDelay,
                    Mathf.Max(wrongBanReactionMinimumDelay, wrongBanReactionMaximumDelay)));
                if (generation != _runGeneration) yield break;
                EnqueueConflictBystander(reactions[UnityEngine.Random.Range(0, reactions.Length)], false);
            }
        }

        private void RemoveViewerChatHistory(string viewerId)
        {
            RunnerViewerData viewer = _activeViewers.FirstOrDefault(item => item.viewerId == viewerId);
            RenderedChatLine[] pending = _pending.Where(line => line.viewerId != viewerId).ToArray();
            RenderedChatLine[] visible = _visible.Where(line => line.viewerId != viewerId).ToArray();
            string[] context = _recentChatContext.Where(line => viewer == null
                || !line.StartsWith(viewer.nickname + ": ", StringComparison.Ordinal)).ToArray();
            _pending.Clear();
            _visible.Clear();
            _recentChatContext.Clear();
            foreach (RenderedChatLine line in pending) _pending.Enqueue(line);
            foreach (RenderedChatLine line in visible) _visible.Enqueue(line);
            foreach (string line in context) _recentChatContext.Enqueue(line);
            _recentMessagesByViewer.Remove(viewerId);
            _lastMessageByViewer.Remove(viewerId);
            RefreshSlots();
        }

        private void EnqueueSystemMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            _pending.Enqueue(new RenderedChatLine
            {
                viewerId = string.Empty,
                rendered = $"<color=#AEB5C0>{message}</color>"
            });
            if (_displayPump == null) _displayPump = StartCoroutine(PumpDisplay());
        }

        private void ApplyModerationResult(bool correct)
        {
            RunnerBroadcastAudienceController runnerAudience = FindFirstObjectByType<RunnerBroadcastAudienceController>();
            if (runnerAudience != null) runnerAudience.OnModerationResult(correct);
            else
            {
                StreamOn.Minigames.TileArena.TileArenaChatAdapter tileAudience =
                    FindFirstObjectByType<StreamOn.Minigames.TileArena.TileArenaChatAdapter>();
                if (tileAudience != null) tileAudience.OnModerationResult(correct);
                else FindFirstObjectByType<PlasticKnightmareBroadcastController>()?.OnModerationResult(correct);
            }
        }

        private void ApplyFraternizationTick()
        {
            RunnerBroadcastAudienceController runnerAudience = FindFirstObjectByType<RunnerBroadcastAudienceController>();
            if (runnerAudience != null) runnerAudience.OnFraternizationTick();
            else
            {
                StreamOn.Minigames.TileArena.TileArenaChatAdapter tile = FindFirstObjectByType<StreamOn.Minigames.TileArena.TileArenaChatAdapter>();
                if (tile != null) tile.OnFraternizationTick();
                else FindFirstObjectByType<PlasticKnightmareBroadcastController>()?.OnFraternizationTick();
            }
        }

        private void ApplyFraternizationResolved(float responseSeconds)
        {
            RunnerBroadcastAudienceController runnerAudience = FindFirstObjectByType<RunnerBroadcastAudienceController>();
            if (runnerAudience != null) runnerAudience.OnFraternizationResolved(responseSeconds);
            else
            {
                StreamOn.Minigames.TileArena.TileArenaChatAdapter tile = FindFirstObjectByType<StreamOn.Minigames.TileArena.TileArenaChatAdapter>();
                if (tile != null) tile.OnFraternizationResolved(responseSeconds);
                else FindFirstObjectByType<PlasticKnightmareBroadcastController>()?.OnFraternizationResolved(responseSeconds);
            }
        }

        private bool ShouldReactToLiveEvent(RunnerChatEvent chatEvent)
        {
            bool playing = gameManager != null
                ? gameManager.State == RunnerGameState.Playing
                : _externalGameBound && _externalGameState == SharedChatGameState.Playing;
            if (!playing) return true;

            float eventWeight;
            bool bypassCooldown = false;
            switch (chatEvent)
            {
                case RunnerChatEvent.RunStarted:
                case RunnerChatEvent.GameOver:
                case RunnerChatEvent.BroadcastCompleted:
                case RunnerChatEvent.NewHighScore:
                case RunnerChatEvent.TileArenaStarted:
                case RunnerChatEvent.TileArenaGameOver:
                case RunnerChatEvent.ChatConflict:
                case RunnerChatEvent.ChatFraternization:
                    return true;
                case RunnerChatEvent.PlayerJumped:
                case RunnerChatEvent.TileArenaJumped:
                    eventWeight = 0.12f;
                    break;
                case RunnerChatEvent.TileArenaPickup:
                    eventWeight = 0.24f;
                    break;
                case RunnerChatEvent.TileArenaStageCleared:
                    eventWeight = 0.9f;
                    break;
                case RunnerChatEvent.PlayerRolled:
                    eventWeight = 0.45f;
                    break;
                case RunnerChatEvent.ObstacleCleared:
                    eventWeight = 0.32f;
                    break;
                case RunnerChatEvent.EnemyDefeated:
                    eventWeight = 0.85f;
                    break;
                case RunnerChatEvent.AttackMissed:
                    eventWeight = 0.28f;
                    break;
                case RunnerChatEvent.PlayerHit:
                case RunnerChatEvent.TileArenaPlayerHit:
                    eventWeight = 1f;
                    break;
                case RunnerChatEvent.LowHealth:
                case RunnerChatEvent.TileArenaLowLives:
                    eventWeight = 1f;
                    bypassCooldown = true;
                    break;
                default:
                    // Quiet/idle/post-game chat already uses the audience-scaled scheduler.
                    return true;
            }

            if (_chattingViewerCount <= 0) return false;
            if (!bypassCooldown && Time.unscaledTime < _nextEventReactionAt) return false;

            float chance = Mathf.Clamp01(_eventReactionChance * eventWeight);
            if (chatEvent == RunnerChatEvent.PlayerHit || chatEvent == RunnerChatEvent.TileArenaPlayerHit)
                chance = Mathf.Max(chance, minimumHitReactionChance);
            if (chatEvent == RunnerChatEvent.EnemyDefeated) chance = Mathf.Max(chance, minimumEnemyDefeatReactionChance);
            if (UnityEngine.Random.value > chance) return false;

            _nextEventReactionAt = Time.unscaledTime + _eventReactionCooldown;
            return true;
        }

        private void SelectActiveViewers()
        {
            List<RunnerViewerPersonaData> pool = replaceBuiltInPersonas
                ? new List<RunnerViewerPersonaData>()
                : RunnerDefaultPersonas.Create();
            if (customPersonas != null)
            {
                foreach (RunnerViewerPersona asset in customPersonas.Where(item => item != null && item.Definition != null))
                {
                    RunnerViewerPersonaData custom = asset.Definition.Copy();
                    int existing = pool.FindIndex(item => item.id == custom.id);
                    if (existing >= 0) pool[existing] = custom;
                    else pool.Add(custom);
                }
            }
            if (pool.Count == 0) pool = RunnerDefaultPersonas.Create();
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int swap = UnityEngine.Random.Range(0, i + 1);
                (pool[i], pool[swap]) = (pool[swap], pool[i]);
            }
            int lower = Mathf.Clamp(minimumActivePersonas, 1, pool.Count);
            int upper = Mathf.Clamp(maximumActivePersonas, lower, pool.Count);
            List<RunnerViewerPersonaData> selectedPersonas = pool.Take(UnityEngine.Random.Range(lower, upper + 1)).ToList();
            if (ensureConflictPersona && !selectedPersonas.Any(IsConflictPersona))
            {
                RunnerViewerPersonaData conflict = pool.FirstOrDefault(IsConflictPersona);
                if (conflict != null && selectedPersonas.Count > 0) selectedPersonas[selectedPersonas.Count - 1] = conflict;
            }
            _activeViewers.Clear();
            HashSet<string> usedNicknames = new HashSet<string>();
            Dictionary<string, int> personaOccurrences = new Dictionary<string, int>();
            for (int i = 0; i < maximumActiveViewers; i++)
            {
                RunnerViewerPersonaData persona = selectedPersonas[i % selectedPersonas.Count];
                personaOccurrences.TryGetValue(persona.id, out int occurrence);
                _activeViewers.Add(RunnerViewerFactory.Create(persona, occurrence, usedNicknames));
                personaOccurrences[persona.id] = occurrence + 1;
            }
            Debug.Log($"STREAM ON chat roster: {_activeViewers.Count} viewers from {selectedPersonas.Count} persona types.", this);
        }

        private static bool IsConflictPersona(RunnerViewerPersonaData persona) =>
            persona != null && (persona.id == "baiter" || persona.id == "chat_fighter");

        private void EnqueueLocal(RunnerChatEvent chatEvent)
        {
            if (SpeakingViewers().Count == 0) return;
            if (chatEvent == RunnerChatEvent.ChatConflict)
            {
                EnqueueConflictOpening();
                return;
            }
            if (chatEvent == RunnerChatEvent.ChatFraternization)
            {
                EnqueueFraternizationOpening();
                return;
            }
            string[] pool;
            if (chatEvent == RunnerChatEvent.DonationReceived)
            {
                string amount = _lastDonationAmount.ToString("N0") + "원";
                pool = _lastDonationIsLarge
                    ? new[] { "와", "???", "ㅁㅊ", "미친", amount + " ㄷㄷㄷㄷ", "와 " + amount + "...." }
                    : new[] { "오", "도네 ㄷㄷ", "ㅋㅋㅋㅋㅋ", "?", "도네 뭐야" };
            }
            else if (_broadcastHeat <= 30f && IsAmbientMoodEvent(chatEvent))
                pool = new[] { "ㄴㅈ", "?", "...", "뭐함?", "개노잼", "예?", "이건 좀...", "왤케 조용함" };
            else if (_broadcastHeat >= 70f && IsAmbientMoodEvent(chatEvent))
                pool = new[] { "오", "가자", "좋은데?", "ㄱㄱ", "ㄱㄱㄱ", "ㅋㅋㅋㅋㅋ", "이대로만", "가보자" };
            else pool = chatEvent switch
            {
                RunnerChatEvent.RunStarted => new[] { "왔냐", "가보자고", "오늘 몇 점 봄?", "오 시작했네" },
                RunnerChatEvent.PlayerJumped => new[] { "오", "방금 좀 쫄았다", "점프는 잘하네", "이건 깔끔" },
                RunnerChatEvent.PlayerRolled => new[] { "C 굿", "오 이건 잘함", "살짝 늦은 거 아님?", "휴" },
                RunnerChatEvent.ObstacleCleared => new[] { "이걸 사네", "어우", "아슬아슬 ㅋㅋ", "계속가" },
                RunnerChatEvent.EnemyDefeated => new[] { "오 잡았네", "이건 좀 멋있었다", "한방이네", "클립각?" },
                RunnerChatEvent.AttackMissed => new[] { "누구 때림?", "허공컷 ㅋㅋ", "아 너무 빨랐어", "못본척함" },
                RunnerChatEvent.PlayerHit => new[] { "아니", "?", "???", "ㅋㅋㅋㅋㅋㅋ", "아니 이걸?", "뭐함?", "에반데", "개못하네..." },
                RunnerChatEvent.LowHealth => new[] { "아 제발", "?", "좀만 더", "에반데", "못보겠다", "제발..." },
                RunnerChatEvent.GameOver => new[] { "아니 뭐하냐", "?", "ㅋㅋㅋㅋㅋㅋㅋㅋ", "개못하네...", "아니 이걸?", "예?", "...", "뭐함?" },
                RunnerChatEvent.BroadcastCompleted => new[] { "ㅈㅈ", "바이바이", "수고했다", "수고했어요", "다음에 봐요~", "담방에 봐" },
                RunnerChatEvent.WitPrompt => new[] { "대답해봐 ㅋㅋ", "채팅 읽었냐", "이건 뭐라 할 건데", "해명해" },
                RunnerChatEvent.WitReplySuccess => new[] { "ㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋ", "ㅋㅋㅋㅋㅋ", "아 ㅋㅋ", "미쳤네ㅋㅋ" },
                RunnerChatEvent.WitReplyOkay => new[] { "음....", "예?", "하하", "?", "이건 좀..." },
                RunnerChatEvent.WitReplyAwkward => new[] { "ㄴㅈ", "개노잼", "?", "음....", "예?", "하하", "이건 좀...", "아..." },
                RunnerChatEvent.NewHighScore => BuildLocalHighScorePool(),
                RunnerChatEvent.QuietMoment => new[] { "오", "가자", "좋은데?", "ㄱㄱ", "ㄱㄱㄱ", "좀만 더", "이대로만" },
                RunnerChatEvent.PostGameDiscussion => new[] { "그래서 다시함?", "아까 그거만 안맞았어도", "이번 판은 좀 아깝다", "R 안누름?", "한판 더 ㄱ" },
                RunnerChatEvent.IdleChat => new[] { "ㄴㅈ", "왤케 조용함", "...", "뭐함?", "자나", "언제 시작함" },
                RunnerChatEvent.CampaignDayStarted => new[] { "오늘도 왔네", "이번엔 뭐 고름?", "멘탈 괜찮냐", "가보자" },
                RunnerChatEvent.CampaignGameTraining => new[] { "연습했으면 보여줘", "오늘은 좀 다르냐", "게임 훈련 골랐네", "바로 방송 ㄱ" },
                RunnerChatEvent.CampaignTalkingTraining => new[] { "오늘 말 많아지나", "소통 방송 온다", "겜은 안해도 됨?", "입은 풀렸고" },
                RunnerChatEvent.CampaignRest => new[] { "쉬는 것도 중요하지", "멘탈 충전했네", "푹 쉬었으면 잘해라", "휴식 굿" },
                RunnerChatEvent.CampaignActionSelected => new[] { "오늘 선택은 그거네", "바로 방송 ㄱ", "효과 있나 보자", "이번 판 가보자" },
                RunnerChatEvent.CampaignSettlement => new[] { "팔로워 얼마나 늘었냐", "이번 판 결과 나왔네", "다음 날 가나", "후원 들어왔네" },
                RunnerChatEvent.CampaignClear => new[] { "7일 생존했다", "이걸 깨네", "다음 방송도 켜", "완주 ㅊㅊ" },
                RunnerChatEvent.CampaignFailed => new[] { "아 방송 끝남?", "다시 하면 되지", "멘탈 나갔네", "다음 회차 ㄱ" },
                RunnerChatEvent.TileArenaStarted => new[] { "이거 시작했네", "파란거 먹으면 됨?", "이번엔 얼마나 버팀?", "가보자" },
                RunnerChatEvent.TileArenaJumped => new[] { "오 점프", "방금 위험했다", "타이밍 굿", "그걸 넘네" },
                RunnerChatEvent.TileArenaPickup => new[] { "파랑 냠", "하나 더", "점수 좋고", "저거까지 먹자" },
                RunnerChatEvent.TileArenaStageCleared => new[] { "오 다먹었다", "다음거 뭐냐", "이걸 다 먹네", "패턴 바뀐다" },
                RunnerChatEvent.TileArenaPlayerHit => new[] { "아니", "?", "???", "ㅋㅋㅋㅋㅋㅋ", "아니 이걸?", "뭐함?", "에반데", "개못하네..." },
                RunnerChatEvent.TileArenaLowLives => new[] { "아 제발", "?", "좀만 더", "에반데", "제발...", "ㄱㄱ" },
                RunnerChatEvent.TileArenaGameOver => new[] { "아니 뭐하냐", "?", "ㅋㅋㅋㅋㅋㅋㅋㅋ", "아니 이걸?", "예?", "...", "뭐함?" },
                _ => new[] { "아깝네", "다시 ㄱ", "뭐 예상했음", "끝?" }
            };
            for (int attempt = 0; attempt < Mathf.Min(8, pool.Length * 2); attempt++)
            {
                RunnerViewerData viewer = PickLocalPersona(chatEvent);
                string message = pool[UnityEngine.Random.Range(0, pool.Length)];
                if (EnqueueRendered(viewer, message)) return;
            }
        }

        private static bool IsAmbientMoodEvent(RunnerChatEvent chatEvent) => chatEvent == RunnerChatEvent.QuietMoment
            || chatEvent == RunnerChatEvent.IdleChat || chatEvent == RunnerChatEvent.PostGameDiscussion;

        private string[] BuildLocalHighScorePool()
        {
            int score = gameManager != null ? gameManager.Score : Mathf.Max(0, _externalSnapshot?.score ?? 0);
            int nextThousand = (score / 1000 + 1) * 1000;
            return new[] { "ㅅㅅ", "나이스", "오", "가보자", "가즈아", $"{nextThousand}점 가자" };
        }

        private RunnerViewerData PickLocalPersona(RunnerChatEvent chatEvent)
        {
            string[] preferred = chatEvent switch
            {
                RunnerChatEvent.PlayerHit => new[] { "teaser", "baiter", "chat_fighter", "worrier", "loyal_fan", "coach" },
                RunnerChatEvent.LowHealth => new[] { "worrier", "loyal_fan", "teaser", "baiter" },
                RunnerChatEvent.EnemyDefeated => new[] { "clipper", "coach", "lurker" },
                RunnerChatEvent.AttackMissed => new[] { "teaser", "baiter", "chat_fighter", "coach", "clipper" },
                RunnerChatEvent.NewHighScore => new[] { "clipper", "loyal_fan", "lurker", "skeptic" },
                RunnerChatEvent.GameOver => new[] { "loyal_fan", "teaser", "baiter", "chat_fighter", "lurker" },
                RunnerChatEvent.TileArenaPlayerHit => new[] { "teaser", "baiter", "chat_fighter", "worrier", "coach" },
                RunnerChatEvent.TileArenaLowLives => new[] { "worrier", "loyal_fan", "teaser", "baiter" },
                RunnerChatEvent.TileArenaStageCleared => new[] { "clipper", "coach", "loyal_fan", "lurker" },
                RunnerChatEvent.TileArenaGameOver => new[] { "loyal_fan", "teaser", "baiter", "chat_fighter", "lurker" },
                RunnerChatEvent.DonationReceived => new[] { "loyal_fan", "casual", "teaser", "lurker" },
                RunnerChatEvent.WitPrompt => new[] { "teaser", "baiter", "chat_fighter", "new_viewer", "casual" },
                RunnerChatEvent.WitReplySuccess => new[] { "teaser", "clipper", "loyal_fan", "casual" },
                RunnerChatEvent.WitReplyAwkward => new[] { "baiter", "chat_fighter", "teaser", "skeptic" },
                RunnerChatEvent.PostGameDiscussion => new[] { "baiter", "chat_fighter", "peacekeeper", "loyal_fan", "teaser", "casual" },
                RunnerChatEvent.IdleChat => new[] { "casual", "new_viewer", "loyal_fan", "lurker" },
                _ => Array.Empty<string>()
            };
            IReadOnlyList<RunnerViewerData> speaking = SpeakingViewers();
            RunnerViewerData[] preferredViewers = speaking.Where(item => preferred.Contains(item.personaId)).ToArray();
            if (preferredViewers.Length > 0) return preferredViewers[UnityEngine.Random.Range(0, preferredViewers.Length)];
            return speaking[UnityEngine.Random.Range(0, speaking.Count)];
        }

        private IReadOnlyList<RunnerViewerData> SpeakingViewers() => _activeViewers
            .Where(viewer => !_bannedViewers.Contains(viewer.viewerId))
            .Take(Mathf.Clamp(_chattingViewerCount, 0, _activeViewers.Count)).ToArray();

        private IReadOnlyList<RunnerViewerData> ConflictViewers()
        {
            List<RunnerViewerData> viewers = SpeakingViewers().ToList();
            if (_troublemaker != null && !viewers.Contains(_troublemaker)) viewers.Add(_troublemaker);
            if (_conflictTarget != null && !viewers.Contains(_conflictTarget)) viewers.Add(_conflictTarget);
            return viewers;
        }

        private IReadOnlyList<RunnerViewerData> SocialEventViewers()
        {
            if (_conflictActive) return ConflictViewers();
            List<RunnerViewerData> viewers = SpeakingViewers().ToList();
            foreach (RunnerViewerData fraternizer in _fraternizers)
                if (fraternizer != null && !viewers.Contains(fraternizer)) viewers.Add(fraternizer);
            return viewers;
        }

        private bool EnqueueRendered(RunnerViewerData viewer, string message)
        {
            if (viewer == null || string.IsNullOrWhiteSpace(message) || _pending.Count >= 12) return false;
            string normalized = NormalizeDuplicateKey(message);
            if (!_recentMessagesByViewer.TryGetValue(viewer.viewerId, out Queue<string> recent))
            {
                recent = new Queue<string>();
                _recentMessagesByViewer[viewer.viewerId] = recent;
            }
            if (recent.Contains(normalized)) return false;
            recent.Enqueue(normalized);
            while (recent.Count > 8) recent.Dequeue();
            _lastMessageByViewer[viewer.viewerId] = message;
            string color = ColorUtility.ToHtmlStringRGB(viewer.nameColor);
            _pending.Enqueue(new RenderedChatLine
            {
                viewerId = viewer.viewerId,
                rendered = $"<link=ban><color=#{color}>{viewer.nickname}</color></link>  <color=#DFE2EA>{message}</color>"
            });
            _recentChatContext.Enqueue(viewer.nickname + ": " + message);
            while (_recentChatContext.Count > 6) _recentChatContext.Dequeue();
            if (_displayPump == null) _displayPump = StartCoroutine(PumpDisplay());
            return true;
        }

        private static string NormalizeDuplicateKey(string value) => new string((value ?? string.Empty)
            .Where(character => !char.IsWhiteSpace(character)).ToArray()).Trim().ToLowerInvariant();

        private IEnumerator PumpDisplay()
        {
            while (_pending.Count > 0)
            {
                yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(minimumDelay, maximumDelay) * _audienceDelayMultiplier);
                _visible.Enqueue(_pending.Dequeue());
                while (_visible.Count > messageSlots.Length) _visible.Dequeue();
                RefreshSlots();
            }
            _displayPump = null;
        }

        private static bool TrySanitize(string value, out string sanitized)
        {
            sanitized = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ")
                .Replace("<", string.Empty).Replace(">", string.Empty).Trim();
            if (sanitized.Length > 50) sanitized = sanitized.Substring(0, 50).Trim();
            return sanitized.Length > 0;
        }

        private static string EventLabel(RunnerChatEvent chatEvent) => chatEvent switch
        {
            RunnerChatEvent.RunStarted => "방송 시작",
            RunnerChatEvent.PlayerJumped => "플레이어 점프",
            RunnerChatEvent.PlayerRolled => "플레이어 구르기",
            RunnerChatEvent.ObstacleCleared => "장애물 통과",
            RunnerChatEvent.EnemyDefeated => "적 처치",
            RunnerChatEvent.AttackMissed => "헛공격",
            RunnerChatEvent.PlayerHit => "플레이어 피격",
            RunnerChatEvent.LowHealth => "HP 1 저체력",
            RunnerChatEvent.GameOver => "게임 오버",
            RunnerChatEvent.BroadcastCompleted => "제한 시간을 모두 버티고 방송을 정상 종료함",
            RunnerChatEvent.DonationReceived => "시청자가 플레이를 보고 실시간 후원을 보냄",
            RunnerChatEvent.WitPrompt => "시청자가 스트리머에게 질문이나 장난을 걸어 답변을 기다리는 중",
            RunnerChatEvent.WitReplySuccess => "스트리머가 시청자 말을 재치 있게 받아쳐 채팅 반응이 좋아짐",
            RunnerChatEvent.WitReplyOkay => "스트리머가 시청자에게 무난하게 답변함",
            RunnerChatEvent.WitReplyAwkward => "스트리머의 답변이 어색해 채팅이 잠시 싸해짐",
            RunnerChatEvent.NewHighScore => "신기록",
            RunnerChatEvent.PostGameDiscussion => "게임 오버 후 결과 화면에서 시청자들이 방금 판을 이야기하는 중",
            RunnerChatEvent.IdleChat => "게임을 플레이하지 않는 대기 화면에서 채팅하는 중",
            RunnerChatEvent.CampaignDayStarted => "새로운 방송 날짜의 준비 화면이 시작됨",
            RunnerChatEvent.CampaignGameTraining => "플레이어가 낮 행동으로 게임 훈련을 선택함",
            RunnerChatEvent.CampaignTalkingTraining => "플레이어가 낮 행동으로 소통 연습을 선택함",
            RunnerChatEvent.CampaignRest => "플레이어가 낮 행동으로 휴식을 선택함",
            RunnerChatEvent.CampaignActionSelected => "플레이어가 설정 데이터에 정의된 낮 행동을 선택함",
            RunnerChatEvent.CampaignSettlement => "오늘 방송의 시청자, 평점, 팔로워, 후원금 정산 결과가 나옴",
            RunnerChatEvent.CampaignClear => "7일 캠페인 생존에 성공함",
            RunnerChatEvent.CampaignFailed => "멘탈이 바닥나 방송 활동을 이어갈 수 없게 됨",
            RunnerChatEvent.TileArenaStarted => "타일 아레나 게임 시작",
            RunnerChatEvent.TileArenaJumped => "타일 아레나에서 빨간 타일을 피하려고 점프함",
            RunnerChatEvent.TileArenaPickup => "파란 타일을 획득해 점수가 오름",
            RunnerChatEvent.TileArenaStageCleared => "파란 타일을 모두 먹어 새 무작위 패턴으로 교체됨. 누적 단계 진행이 아님",
            RunnerChatEvent.TileArenaPlayerHit => "빨간 위험 타일에 닿아 목숨을 잃음",
            RunnerChatEvent.TileArenaLowLives => "타일 아레나에서 남은 목숨이 얼마 없음",
            RunnerChatEvent.TileArenaGameOver => "타일 아레나 게임 오버",
            RunnerChatEvent.ChatConflict => "분탕 유저가 다른 시청자의 직전 채팅을 지목해 시비를 걸고 실제 채팅 분쟁이 시작됨",
            RunnerChatEvent.ChatFraternization => "일부 시청자들이 서로 닉네임을 부르며 방송과 무관한 친분 대화를 시작함",
            _ => "특별한 사건 없이 게임 플레이 중"
        };

        private static string BuildEventText(IReadOnlyCollection<RunnerChatEvent> events)
        {
            bool gameOverWithNewRecord = events.Contains(RunnerChatEvent.GameOver)
                && events.Contains(RunnerChatEvent.NewHighScore);
            IEnumerable<string> labels = events
                .Where(chatEvent => !gameOverWithNewRecord
                    || chatEvent != RunnerChatEvent.GameOver && chatEvent != RunnerChatEvent.NewHighScore)
                .Select(EventLabel);
            if (gameOverWithNewRecord)
                labels = labels.Append("이번 런 종료와 함께 새 최고 기록이 확정됨");
            return string.Join(", ", labels.Distinct());
        }

        private void RefreshSlots()
        {
            EnsureSlots();
            RenderedChatLine[] values = _visible.ToArray();
            int empty = messageSlots.Length - values.Length;
            for (int i = 0; i < messageSlots.Length; i++)
            {
                messageSlots[i].raycastTarget = true;
                RunnerChatLineClickTarget clickTarget = messageSlots[i].GetComponent<RunnerChatLineClickTarget>();
                if (clickTarget == null) clickTarget = messageSlots[i].gameObject.AddComponent<RunnerChatLineClickTarget>();
                if (i < empty)
                {
                    messageSlots[i].text = string.Empty;
                    clickTarget.Bind(null, null);
                }
                else
                {
                    RenderedChatLine line = values[i - empty];
                    messageSlots[i].text = line.rendered;
                    clickTarget.Bind(line.viewerId, OnViewerClicked);
                }
            }
        }

        private void EnsureSlots()
        {
            if (messageSlots != null && messageSlots.Length > 0 && messageSlots.All(slot => slot != null)) return;
            messageSlots = GetComponentsInChildren<TMP_Text>(true).Where(text => text.name.StartsWith("Message ")).OrderBy(text => text.name).ToArray();
            foreach (TMP_Text slot in messageSlots) slot.raycastTarget = true;
        }

        private void OnValidate()
        {
            maximumDelay = Mathf.Max(minimumDelay, maximumDelay);
            ambientMaximumDelay = Mathf.Max(ambientMinimumDelay, ambientMaximumDelay);
            postGameAmbientMaximumDelay = Mathf.Max(postGameAmbientMinimumDelay, postGameAmbientMaximumDelay);
            idleAmbientMaximumDelay = Mathf.Max(idleAmbientMinimumDelay, idleAmbientMaximumDelay);
            maximumActivePersonas = Mathf.Max(minimumActivePersonas, maximumActivePersonas);
            maximumViewersPerPersona = Mathf.Max(minimumViewersPerPersona, maximumViewersPerPersona);
            maximumActiveViewers = Mathf.Max(maximumActiveViewers, minimumActivePersonas * minimumViewersPerPersona);
        }
    }

    public sealed class RunnerChatLineClickTarget : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler,
        IPointerExitHandler, IPointerMoveHandler
    {
        private TMP_Text _text;
        private string _viewerId;
        private Action<string> _onClick;
        private FontStyles _baseStyle;

        public void Bind(string viewerId, Action<string> onClick)
        {
            if (_text == null) _text = GetComponent<TMP_Text>();
            _viewerId = viewerId;
            _onClick = onClick;
            if (_text != null)
            {
                _baseStyle = _text.fontStyle & ~FontStyles.Underline;
                _text.fontStyle = _baseStyle;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && IsOverNickname(eventData)
                && !string.IsNullOrWhiteSpace(_viewerId))
            {
                RunnerChatBanTooltip.Hide();
                _onClick?.Invoke(_viewerId);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            UpdateHover(eventData);
        }

        public void OnPointerMove(PointerEventData eventData) => UpdateHover(eventData);

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_text != null) _text.fontStyle = _baseStyle;
            RunnerChatBanTooltip.Hide();
        }

        private void UpdateHover(PointerEventData eventData)
        {
            bool overNickname = IsOverNickname(eventData) && !string.IsNullOrWhiteSpace(_viewerId);
            if (_text != null) _text.fontStyle = _baseStyle;
            if (overNickname) RunnerChatBanTooltip.Show(eventData.position);
            else RunnerChatBanTooltip.Hide();
        }

        private bool IsOverNickname(PointerEventData eventData) => _text != null
            && TMP_TextUtilities.FindIntersectingLink(_text, eventData.position, eventData.enterEventCamera) >= 0;
    }

    public sealed class RunnerChatBanTooltip : MonoBehaviour
    {
        private static RunnerChatBanTooltip _instance;
        public GameObject tooltipObject;
        public RectTransform tooltipRect;
        public Vector2 pointerOffset = new Vector2(14f, 18f);

        private void Awake()
        {
            _instance = this;
            if (tooltipObject != null) tooltipObject.SetActive(false);
        }

        public static void Show(Vector2 screenPosition)
        {
            if (_instance == null || _instance.tooltipObject == null || _instance.tooltipRect == null) return;
            _instance.tooltipObject.SetActive(true);
            _instance.tooltipRect.position = screenPosition + _instance.pointerOffset;
        }

        public static void Hide()
        {
            if (_instance != null && _instance.tooltipObject != null) _instance.tooltipObject.SetActive(false);
        }
    }
}
