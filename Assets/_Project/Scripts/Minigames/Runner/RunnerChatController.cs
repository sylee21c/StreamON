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
        PostGameDiscussion, IdleChat
    }

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
        [SerializeField, Range(4, 40)] private int maximumActiveViewers = 18;
        [Tooltip("매 플레이에 분탕 또는 논쟁형 페르소나를 최소 한 유형 포함")]
        [SerializeField] private bool ensureConflictPersona = true;

        private readonly Queue<string> _pending = new Queue<string>();
        private readonly Queue<string> _visible = new Queue<string>();
        private readonly Queue<string> _recentChatContext = new Queue<string>();
        private readonly Queue<RunnerChatEvent> _aiEvents = new Queue<RunnerChatEvent>();
        private readonly List<RunnerViewerData> _activeViewers = new List<RunnerViewerData>();
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

        private void Awake()
        {
            if (gameManager == null) gameManager = FindFirstObjectByType<RunnerGameManager>();
            EnsureSlots();
            _titleText = GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.name == "Title");
            SetConnectionLabel("LOCAL");
            SelectActiveViewers();
        }

        private void Update()
        {
            if (gameManager == null || Time.unscaledTime < _nextAmbientAt) return;
            RunnerChatEvent ambientEvent;
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
            React(ambientEvent);
        }

        public void React(RunnerChatEvent chatEvent)
        {
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

        public void ResetChat()
        {
            _runGeneration++;
            _pending.Clear();
            _visible.Clear();
            _recentChatContext.Clear();
            _aiEvents.Clear();
            if (_displayPump != null) StopCoroutine(_displayPump);
            if (_aiPump != null) StopCoroutine(_aiPump);
            if (_postGamePump != null) StopCoroutine(_postGamePump);
            _displayPump = null;
            _aiPump = null;
            _postGamePump = null;
            SelectActiveViewers();
            ScheduleNextAmbient(ambientMinimumDelay, ambientMaximumDelay);
            RefreshSlots();
        }

        public void BeginGameOverChat(bool isNewHighScore)
        {
            if (_postGamePump != null) StopCoroutine(_postGamePump);
            React(RunnerChatEvent.GameOver);
            if (isNewHighScore) React(RunnerChatEvent.NewHighScore);
            ScheduleNextAmbient(postGameAmbientMinimumDelay, postGameAmbientMaximumDelay);
            _postGamePump = StartCoroutine(PumpPostGameReactions(_runGeneration));
        }

        private IEnumerator PumpPostGameReactions(int generation)
        {
            yield return new WaitForSecondsRealtime(2.6f);
            if (!IsCurrentGameOver(generation)) yield break;
            React(RunnerChatEvent.PostGameDiscussion);
            yield return new WaitForSecondsRealtime(3.8f);
            if (!IsCurrentGameOver(generation)) yield break;
            React(RunnerChatEvent.PostGameDiscussion);
            _postGamePump = null;
        }

        private bool IsCurrentGameOver(int generation) => generation == _runGeneration
            && gameManager != null && gameManager.State == RunnerGameState.GameOver;

        private void ScheduleNextAmbient(float minimum, float maximum) =>
            _nextAmbientAt = Time.unscaledTime + UnityEngine.Random.Range(minimum, maximum);

        private IEnumerator PumpAi(string apiKey, int generation)
        {
            while (_aiEvents.Count > 0 && generation == _runGeneration)
            {
                float wait = Mathf.Max(eventBatchWindow, _nextAiRequestAt - Time.unscaledTime);
                if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
                List<RunnerChatEvent> events = new List<RunnerChatEvent>();
                while (_aiEvents.Count > 0) events.Add(_aiEvents.Dequeue());
                string eventText = string.Join(", ", events.Select(EventLabel));
                RunnerChatSnapshot snapshot = gameManager != null
                    ? gameManager.CreateChatSnapshot(eventText)
                    : new RunnerChatSnapshot { events = eventText };
                snapshot.recentMessages = string.Join(" | ", _recentChatContext);

                RunnerGeneratedChatBatch generated = null;
                string failure = null;
                OpenAiRunnerChatClient client = new OpenAiRunnerChatClient(endpoint, model, apiKey);
                _nextAiRequestAt = Time.unscaledTime + minimumApiInterval;
                yield return client.Generate(_activeViewers, snapshot, value => generated = value, error => failure = error);

                if (generation != _runGeneration) yield break;
                if (generated?.messages != null)
                {
                    int accepted = 0;
                    foreach (RunnerGeneratedChat message in generated.messages)
                    {
                        RunnerViewerData viewer = _activeViewers.FirstOrDefault(item => item.viewerId == message.speakerId);
                        if (viewer == null || !TrySanitize(message.message, out string safeMessage)) continue;
                        EnqueueRendered(viewer, safeMessage);
                        accepted++;
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
            if (_titleText != null) _titleText.text = $"LIVE CHAT [{mode}]";
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
            foreach (RunnerViewerPersonaData persona in selectedPersonas)
            {
                int viewerCount = UnityEngine.Random.Range(minimumViewersPerPersona, maximumViewersPerPersona + 1);
                for (int i = 0; i < viewerCount && _activeViewers.Count < maximumActiveViewers; i++)
                    _activeViewers.Add(RunnerViewerFactory.Create(persona, i, usedNicknames));
            }
            Debug.Log($"STREAM ON chat roster: {_activeViewers.Count} viewers from {selectedPersonas.Count} persona types.", this);
        }

        private static bool IsConflictPersona(RunnerViewerPersonaData persona) =>
            persona != null && (persona.id == "baiter" || persona.id == "chat_fighter");

        private void EnqueueLocal(RunnerChatEvent chatEvent)
        {
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
                RunnerChatEvent.NewHighScore => new[] { "오 신기록", "이건 인정", "오늘 폼 뭐임", "드디어 넘었네" },
                RunnerChatEvent.QuietMoment => new[] { "은근 빠르네", "왜 갑자기 조용함", "집중했네 ㅋㅋ", "이러다 또 맞음" },
                RunnerChatEvent.PostGameDiscussion => new[] { "그래서 다시함?", "아까 그거만 안맞았어도", "채팅 또 싸우네 ㅋㅋ", "이번 판은 좀 아깝다", "R 안누름?" },
                RunnerChatEvent.IdleChat => new[] { "언제 시작함", "다들 뭐함", "아직 있냐", "채팅 왜 조용해", "물 좀 마시고 와" },
                _ => new[] { "아깝네", "다시 ㄱ", "뭐 예상했음", "끝?" }
            };
            EnqueueRendered(PickLocalPersona(chatEvent), pool[UnityEngine.Random.Range(0, pool.Length)]);
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
                RunnerChatEvent.PostGameDiscussion => new[] { "baiter", "chat_fighter", "peacekeeper", "loyal_fan", "teaser", "casual" },
                RunnerChatEvent.IdleChat => new[] { "casual", "new_viewer", "loyal_fan", "lurker" },
                _ => Array.Empty<string>()
            };
            RunnerViewerData[] preferredViewers = _activeViewers.Where(item => preferred.Contains(item.personaId)).ToArray();
            if (preferredViewers.Length > 0) return preferredViewers[UnityEngine.Random.Range(0, preferredViewers.Length)];
            return _activeViewers[UnityEngine.Random.Range(0, _activeViewers.Count)];
        }

        private void EnqueueRendered(RunnerViewerData viewer, string message)
        {
            if (_pending.Count >= 12) return;
            string color = ColorUtility.ToHtmlStringRGB(viewer.nameColor);
            _pending.Enqueue($"<color=#{color}>{viewer.nickname}</color>  {message}");
            _recentChatContext.Enqueue(viewer.nickname + ": " + message);
            while (_recentChatContext.Count > 6) _recentChatContext.Dequeue();
            if (_displayPump == null) _displayPump = StartCoroutine(PumpDisplay());
        }

        private IEnumerator PumpDisplay()
        {
            while (_pending.Count > 0)
            {
                yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(minimumDelay, maximumDelay));
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
            RunnerChatEvent.NewHighScore => "신기록",
            RunnerChatEvent.PostGameDiscussion => "게임 오버 후 결과 화면에서 시청자들이 방금 판을 이야기하는 중",
            RunnerChatEvent.IdleChat => "게임을 플레이하지 않는 대기 화면에서 채팅하는 중",
            _ => "특별한 사건 없이 계속 달리는 중"
        };

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
