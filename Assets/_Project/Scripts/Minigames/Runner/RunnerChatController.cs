using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

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
        TileArenaStageCleared, TileArenaPlayerHit, TileArenaLowLives, TileArenaGameOver
    }

    public enum SharedChatGameState { Ready, Playing, GameOver }

    public sealed class RunnerChatController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private RunnerGameManager gameManager;
        [SerializeField] private TMP_Text[] messageSlots;

        [Header("Display Timing")]
        [SerializeField] private float minimumDelay = 0.2f;
        [SerializeField] private float maximumDelay = 0.6f;

        [Header("AI Chat (development connection)")]
        [SerializeField] private bool useAiChat = true;
        [SerializeField] private string endpoint = "https://api.openai.com/v1/responses";
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

        private readonly Queue<string> _pending = new Queue<string>();
        private readonly Queue<string> _visible = new Queue<string>();
        private readonly Queue<string> _recentChatContext = new Queue<string>();
        private readonly Queue<RunnerChatEvent> _aiEvents = new Queue<RunnerChatEvent>();
        private readonly List<RunnerViewerData> _activeViewers = new List<RunnerViewerData>();
        private readonly Dictionary<string, Queue<string>> _recentMessagesByViewer = new Dictionary<string, Queue<string>>();
        private Coroutine _displayPump;
        private Coroutine _aiPump;
        private Coroutine _postGamePump;
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
        private float _nextEventReactionAt;
        private int _lastLoggedViewerCount = -1;
        private int _lastLoggedChatterCount = -1;
        private bool _externalGameBound;
        private string _externalGameTitle = "게임";
        private SharedChatGameState _externalGameState = SharedChatGameState.Ready;
        private RunnerChatSnapshot _externalSnapshot;

        private void Awake()
        {
            useAiChat = RunnerUserSettingsStore.Load(useAiChat).aiChatEnabled;
            if (gameManager == null) gameManager = FindFirstObjectByType<RunnerGameManager>();
            EnsureSlots();
            _titleText = GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.name == "Title");
            if (_titleText != null)
            {
                _titleText.fontSize = 18f;
                _titleText.textWrappingMode = TextWrappingModes.NoWrap;
                _titleText.alignment = TextAlignmentOptions.MidlineLeft;
                _titleText.rectTransform.sizeDelta = new Vector2(270f, 68f);
            }
            SetConnectionLabel("LOCAL");
            SelectActiveViewers();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || Time.unscaledTime < _nextAmbientAt) return;
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
            OpenAiRunnerChatClient client = new OpenAiRunnerChatClient(endpoint, model, apiKey);
            yield return client.GenerateWit(snapshot, recentPrompts, value => generated = value, _ => { });
            onComplete?.Invoke(generated);
        }

        public string PickDonationViewerNickname()
        {
            IReadOnlyList<RunnerViewerData> speaking = SpeakingViewers();
            if (speaking.Count == 0) speaking = _activeViewers;
            return speaking.Count > 0 ? speaking[UnityEngine.Random.Range(0, speaking.Count)].nickname : "익명의 시청자";
        }

        public void OnDonationReceived(string donorNickname, int amount)
        {
            React(RunnerChatEvent.DonationReceived);
            _recentChatContext.Enqueue($"{donorNickname}님이 {amount:N0}원을 후원함");
            while (_recentChatContext.Count > 6) _recentChatContext.Dequeue();
        }

        public void ConfigureAudience(int currentViewers, int chattingViewers, float delayMultiplier,
            float eventReactionChance, float eventReactionCooldown)
        {
            _audienceViewerCount = Mathf.Max(0, currentViewers);
            _chattingViewerCount = Mathf.Clamp(chattingViewers, 0, Mathf.Min(_audienceViewerCount, _activeViewers.Count));
            _audienceDelayMultiplier = Mathf.Clamp(delayMultiplier, 0.2f, 1f);
            _eventReactionChance = Mathf.Clamp01(eventReactionChance);
            _eventReactionCooldown = Mathf.Max(0f, eventReactionCooldown);
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
            _aiEvents.Clear();
            if (_displayPump != null) StopCoroutine(_displayPump);
            if (_aiPump != null) StopCoroutine(_aiPump);
            if (_postGamePump != null) StopCoroutine(_postGamePump);
            _displayPump = null;
            _aiPump = null;
            _postGamePump = null;
            _nextEventReactionAt = 0f;
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
            React(completedTimeLimit ? RunnerChatEvent.BroadcastCompleted : RunnerChatEvent.GameOver);
            if (isNewHighScore) React(RunnerChatEvent.NewHighScore);
            ScheduleNextAmbient(postGameAmbientMinimumDelay, postGameAmbientMaximumDelay);
            _postGamePump = StartCoroutine(PumpPostGameReactions(_runGeneration));
        }

        private IEnumerator PumpPostGameReactions(int generation)
        {
            yield return new WaitForSecondsRealtime(0.9f);
            if (!IsCurrentGameOver(generation)) yield break;
            React(RunnerChatEvent.PostGameDiscussion);
            yield return new WaitForSecondsRealtime(1.7f);
            if (!IsCurrentGameOver(generation)) yield break;
            React(RunnerChatEvent.PostGameDiscussion);
            yield return new WaitForSecondsRealtime(2.8f);
            if (!IsCurrentGameOver(generation)) yield break;
            React(RunnerChatEvent.PostGameDiscussion);
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
                string eventText = BuildEventText(events);
                RunnerChatSnapshot snapshot = gameManager != null
                    ? gameManager.CreateChatSnapshot(eventText)
                    : _externalSnapshot ?? new RunnerChatSnapshot { gameTitle = _externalGameTitle };
                snapshot.events = eventText;
                snapshot.recentMessages = string.Join(" | ", _recentChatContext);

                RunnerGeneratedChatBatch generated = null;
                string failure = null;
                OpenAiRunnerChatClient client = new OpenAiRunnerChatClient(endpoint, model, apiKey);
                _nextAiRequestAt = Time.unscaledTime + minimumApiInterval;
                IReadOnlyList<RunnerViewerData> speakingViewers = SpeakingViewers();
                if (speakingViewers.Count == 0) continue;
                yield return client.Generate(speakingViewers, snapshot, value => generated = value, error => failure = error);

                if (generation != _runGeneration) yield break;
                if (generated?.messages != null)
                {
                    int accepted = 0;
                    foreach (RunnerGeneratedChat message in generated.messages)
                    {
                        RunnerViewerData viewer = _activeViewers.FirstOrDefault(item => item.viewerId == message.speakerId);
                        if (viewer == null || !TrySanitize(message.message, out string safeMessage)) continue;
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
            bool available = useAiChat && !string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(model)
                && (!requireApiKey || !string.IsNullOrWhiteSpace(apiKey))
                && Time.unscaledTime >= _aiRetryAfter;
            if (!available && useAiChat && !_loggedAiUnavailable)
            {
                Debug.Log("STREAM ON AI chat is using local fallback. Set " + apiKeyEnvironmentVariable
                    + " and restart Unity to enable the API connection.");
                _loggedAiUnavailable = true;
            }
            return available;
        }

        private string ReadApiKey()
        {
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

        private void SetConnectionLabel(string mode)
        {
            _connectionMode = mode;
            RefreshTitle();
        }

        private void RefreshTitle()
        {
            if (_titleText != null)
                _titleText.text = $"LIVE CHAT [{_connectionMode}]\n현재 시청자 {_audienceViewerCount:N0}명";
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
            if (chatEvent == RunnerChatEvent.PlayerHit || chatEvent == RunnerChatEvent.TileArenaPlayerHit) chance = Mathf.Max(chance, 0.45f);
            if (chatEvent == RunnerChatEvent.EnemyDefeated) chance = Mathf.Max(chance, 0.30f);
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
            string[] pool = chatEvent switch
            {
                RunnerChatEvent.RunStarted => new[] { "왔냐", "가보자고", "오늘 몇 점 봄?", "오 시작했네" },
                RunnerChatEvent.PlayerJumped => new[] { "오", "방금 좀 쫄았다", "점프는 잘하네", "이건 깔끔" },
                RunnerChatEvent.PlayerRolled => new[] { "C 굿", "오 이건 잘함", "살짝 늦은 거 아님?", "휴" },
                RunnerChatEvent.ObstacleCleared => new[] { "이걸 사네", "어우", "아슬아슬 ㅋㅋ", "계속가" },
                RunnerChatEvent.EnemyDefeated => new[] { "오 잡았네", "이건 좀 멋있었다", "한방이네", "클립각?" },
                RunnerChatEvent.AttackMissed => new[] { "누구 때림?", "허공컷 ㅋㅋ", "아 너무 빨랐어", "못본척함" },
                RunnerChatEvent.PlayerHit => new[] { "아", "그걸 맞냐 ㅋㅋ", "또 시작이네", "괜찮 아직 안죽음" },
                RunnerChatEvent.LowHealth => new[] { "아 제발", "한대 남았네", "이제 진짜 집중", "못보겠다" },
                RunnerChatEvent.GameOver => new[] { "아 끝났네", "까비", "마지막에 급했음", "그래서 다시함?", "이번 판은 좀 아깝다" },
                RunnerChatEvent.BroadcastCompleted => new[] { "방송 끝까지 버텼네", "시간 다됐다", "오늘 방송 종료", "완주 굿", "결과 보자" },
                RunnerChatEvent.DonationReceived => new[] { "오 후원", "큰손 등장", "리액션 가자", "후원 감사합니다", "이걸 보고 쏘네 ㅋㅋ" },
                RunnerChatEvent.WitPrompt => new[] { "대답해봐 ㅋㅋ", "채팅 읽었냐", "이건 뭐라 할 건데", "해명해" },
                RunnerChatEvent.WitReplySuccess => new[] { "오 받아쳤다 ㅋㅋ", "말빨 뭐임", "이건 좀 웃겼다", "클립 따놔" },
                RunnerChatEvent.WitReplyOkay => new[] { "무난하네", "그래 그럴 수 있지", "대답은 했네", "일단 넘어감" },
                RunnerChatEvent.WitReplyAwkward => new[] { "갑분싸 ㅋㅋ", "아 그건 좀", "채팅 얼었는데", "못 들은 걸로 하자" },
                RunnerChatEvent.NewHighScore => new[] { "오 신기록", "이건 인정", "오늘 폼 뭐임", "드디어 넘었네" },
                RunnerChatEvent.QuietMoment => new[] { "은근 빠르네", "왜 갑자기 조용함", "집중했네 ㅋㅋ", "이러다 또 맞음" },
                RunnerChatEvent.PostGameDiscussion => new[] { "그래서 다시함?", "아까 그거만 안맞았어도", "채팅 또 싸우네 ㅋㅋ", "이번 판은 좀 아깝다", "R 안누름?" },
                RunnerChatEvent.IdleChat => new[] { "언제 시작함", "다들 뭐함", "아직 있냐", "채팅 왜 조용해", "물 좀 마시고 와" },
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
                RunnerChatEvent.TileArenaPlayerHit => new[] { "아 맞았네", "빨간거 밟음 ㅋㅋ", "그걸 왜 들어가", "아직 목숨 있음" },
                RunnerChatEvent.TileArenaLowLives => new[] { "이제 진짜 조심", "목숨 얼마 안남음", "아 제발", "점프 아껴" },
                RunnerChatEvent.TileArenaGameOver => new[] { "아 끝", "까비", "마지막 빨강 억까네", "한판 더 ㄱ", "그래도 꽤 갔다" },
                _ => new[] { "아깝네", "다시 ㄱ", "뭐 예상했음", "끝?" }
            };
            for (int attempt = 0; attempt < Mathf.Min(8, pool.Length * 2); attempt++)
            {
                RunnerViewerData viewer = PickLocalPersona(chatEvent);
                string message = pool[UnityEngine.Random.Range(0, pool.Length)];
                if (EnqueueRendered(viewer, message)) return;
            }
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

        private IReadOnlyList<RunnerViewerData> SpeakingViewers() => _activeViewers.Take(Mathf.Clamp(_chattingViewerCount, 0, _activeViewers.Count)).ToArray();

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
            string color = ColorUtility.ToHtmlStringRGB(viewer.nameColor);
            _pending.Enqueue($"<color=#{color}>{viewer.nickname}</color>  {message}");
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
            string[] values = _visible.ToArray();
            int empty = messageSlots.Length - values.Length;
            for (int i = 0; i < messageSlots.Length; i++) messageSlots[i].text = i < empty ? string.Empty : values[i - empty];
        }

        private void EnsureSlots()
        {
            if (messageSlots != null && messageSlots.Length > 0 && messageSlots.All(slot => slot != null)) return;
            messageSlots = GetComponentsInChildren<TMP_Text>(true).Where(text => text.name.StartsWith("Message ")).OrderBy(text => text.name).ToArray();
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
}
