using System.Collections;
using System.Collections.Generic;
using StreamOn.Chat;
using StreamOn.Core;
using StreamOn.Gameplay;
using StreamOn.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StreamOn.UI
{
    public sealed class StreamOnPrototypeBootstrap : MonoBehaviour
    {
        private static readonly Color Background = new Color(0.055f, 0.063f, 0.09f, 0.96f);
        private static readonly Color Panel = new Color(0.11f, 0.12f, 0.17f, 0.98f);
        private static readonly Color Accent = new Color(0.18f, 0.78f, 0.70f, 1f);
        private static readonly Color Danger = new Color(0.95f, 0.32f, 0.38f, 1f);

        private GameSession _session;
        private BroadcastMiniGame _miniGame;
        private LocalChatService _chatService;
        private readonly Queue<string> _pendingChats = new Queue<string>();
        private readonly Queue<string> _visibleChats = new Queue<string>();
        private Coroutine _chatRoutine;
        private TMP_FontAsset _font;
        private TextMeshProUGUI _statusText;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _bodyText;
        private TextMeshProUGUI _timerText;
        private TextMeshProUGUI _scoreText;
        private RectTransform _cursor;
        private RectTransform _track;
        private TextMeshProUGUI _chatText;
        private GameObject _chatPanel;
        private GameObject _dayPanel;
        private GameObject _broadcastPanel;
        private GameObject _settlementPanel;
        private GameObject _endingPanel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreatePrototype()
        {
            if (SceneManager.GetActiveScene().name != "StreamOnPrototype")
            {
                return;
            }

            if (FindFirstObjectByType<StreamOnPrototypeBootstrap>() != null)
            {
                return;
            }

            new GameObject("STREAM ON Prototype").AddComponent<StreamOnPrototypeBootstrap>();
        }

        private void Awake()
        {
            _session = new GameSession();
            _chatService = new LocalChatService();
            _font = RuntimeTMPFont.CreateKoreanFont();
            _miniGame = gameObject.AddComponent<BroadcastMiniGame>();
            _miniGame.ProgressChanged += OnBroadcastProgress;
            _miniGame.AttemptResolved += OnAttemptResolved;
            _miniGame.Finished += OnBroadcastFinished;
            BuildInterface();
            ShowCurrentPhase();
        }

        private void OnDestroy()
        {
            if (_miniGame == null) return;
            _miniGame.ProgressChanged -= OnBroadcastProgress;
            _miniGame.AttemptResolved -= OnAttemptResolved;
            _miniGame.Finished -= OnBroadcastFinished;
        }

        private void BuildInterface()
        {
            EnsureEventSystem();
            GameObject canvasObject = CreateObject("Prototype Canvas", transform);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280f, 720f);
            canvasObject.AddComponent<GraphicRaycaster>();

            RectTransform backdrop = CreateImage("Backdrop", canvasObject.transform, Background);
            Stretch(backdrop);

            RectTransform shell = CreateImage("Game Panel", canvasObject.transform, Panel);
            SetRect(shell, new Vector2(0.5f, 0.5f), new Vector2(1120f, 600f), Vector2.zero);

            _titleText = CreateText("Title", shell, "STREAM ON!", 36, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            SetRect(_titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(1040f, 60f), new Vector2(0f, -50f));
            _titleText.color = Accent;

            _statusText = CreateText("Status", shell, string.Empty, 19, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            SetRect(_statusText.rectTransform, new Vector2(0.5f, 1f), new Vector2(1040f, 40f), new Vector2(0f, -100f));

            _dayPanel = CreatePanel("Day Panel", shell);
            _bodyText = CreateText("Day Description", _dayPanel.transform,
                "낮 행동을 하나 선택하세요. 선택 즉시 60초 방송이 시작됩니다.", 23, FontStyles.Normal, TextAlignmentOptions.Center);
            SetRect(_bodyText.rectTransform, new Vector2(0.5f, 0.68f), new Vector2(650f, 100f), Vector2.zero);
            Button train = CreateButton("Train Button", _dayPanel.transform, "플레이 연습", Accent);
            SetRect(train.GetComponent<RectTransform>(), new Vector2(0.5f, 0.43f), new Vector2(330f, 64f), Vector2.zero);
            train.onClick.AddListener(ChooseTrain);
            Button rest = CreateButton("Rest Button", _dayPanel.transform, "멘탈 케어  (+30 멘탈)", new Color(0.35f, 0.55f, 0.95f));
            SetRect(rest.GetComponent<RectTransform>(), new Vector2(0.5f, 0.25f), new Vector2(330f, 64f), Vector2.zero);
            rest.onClick.AddListener(ChooseRest);

            _broadcastPanel = CreatePanel("Broadcast Panel", shell);
            _broadcastPanel.GetComponent<RectTransform>().offsetMax = new Vector2(-320f, 0f);
            _timerText = CreateText("Timer", _broadcastPanel.transform, "방송 60.0초", 30, FontStyles.Bold, TextAlignmentOptions.Center);
            SetRect(_timerText.rectTransform, new Vector2(0.5f, 0.78f), new Vector2(400f, 55f), Vector2.zero);
            _scoreText = CreateText("Score", _broadcastPanel.transform, "성공 0 / 실패 0", 21, FontStyles.Normal, TextAlignmentOptions.Center);
            SetRect(_scoreText.rectTransform, new Vector2(0.5f, 0.66f), new Vector2(400f, 45f), Vector2.zero);

            _track = CreateImage("Timing Track", _broadcastPanel.transform, new Color(0.24f, 0.25f, 0.32f)).GetComponent<RectTransform>();
            SetRect(_track, new Vector2(0.5f, 0.50f), new Vector2(560f, 54f), Vector2.zero);
            RectTransform successZone = CreateImage("Success Zone", _track, new Color(0.15f, 0.62f, 0.49f)).GetComponent<RectTransform>();
            SetRect(successZone, new Vector2(0.5f, 0.5f), new Vector2(115f, 54f), Vector2.zero);
            _cursor = CreateImage("Cursor", _track, Color.white).GetComponent<RectTransform>();
            SetRect(_cursor, new Vector2(0.5f, 0.5f), new Vector2(12f, 72f), Vector2.zero);

            Button hit = CreateButton("Hit Button", _broadcastPanel.transform, "지금이다!", Danger);
            SetRect(hit.GetComponent<RectTransform>(), new Vector2(0.5f, 0.25f), new Vector2(260f, 72f), Vector2.zero);
            hit.onClick.AddListener(() => _miniGame.AttemptHit());

            RectTransform chatRect = CreateImage("Live Chat", shell, new Color(0.075f, 0.082f, 0.115f, 1f));
            _chatPanel = chatRect.gameObject;
            SetRect(chatRect, new Vector2(1f, 0f), new Vector2(290f, 435f), new Vector2(-175f, 35f));
            chatRect.pivot = new Vector2(0.5f, 0f);
            TextMeshProUGUI chatTitle = CreateText("Chat Title", chatRect, "LIVE CHAT", 20, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            SetRect(chatTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(250f, 50f), new Vector2(0f, -28f));
            chatTitle.color = Accent;
            _chatText = CreateText("Messages", chatRect, string.Empty, 18, FontStyles.Normal, TextAlignmentOptions.BottomLeft);
            _chatText.textWrappingMode = TextWrappingModes.Normal;
            _chatText.overflowMode = TextOverflowModes.Truncate;
            _chatText.lineSpacing = 1.15f;
            SetRect(_chatText.rectTransform, new Vector2(0.5f, 0f), new Vector2(250f, 355f), new Vector2(0f, 25f));
            _chatText.rectTransform.pivot = new Vector2(0.5f, 0f);

            _settlementPanel = CreatePanel("Settlement Panel", shell);
            Button next = CreateButton("Next Day Button", _settlementPanel.transform, "다음 날", Accent);
            SetRect(next.GetComponent<RectTransform>(), new Vector2(0.5f, 0.22f), new Vector2(260f, 64f), Vector2.zero);
            next.onClick.AddListener(AdvanceDay);

            _endingPanel = CreatePanel("Ending Panel", shell);
            Button restart = CreateButton("Restart Button", _endingPanel.transform, "처음부터 다시", Accent);
            SetRect(restart.GetComponent<RectTransform>(), new Vector2(0.5f, 0.22f), new Vector2(260f, 64f), Vector2.zero);
            restart.onClick.AddListener(Restart);
        }

        private void ChooseTrain()
        {
            _session.Train();
            StartBroadcast();
        }

        private void ChooseRest()
        {
            _session.Rest();
            StartBroadcast();
        }

        private void StartBroadcast()
        {
            ResetChat();
            ShowCurrentPhase();
            _miniGame.Begin(_session.Player.GameSkill);
            EnqueueChat(BroadcastChatEvent.BroadcastStarted);
            EnqueueChat(BroadcastChatEvent.BroadcastStarted);
            EnqueueChat(BroadcastChatEvent.BroadcastStarted);
        }

        private void OnBroadcastProgress(float timeRemaining, float cursor01, int hits, int misses)
        {
            _timerText.text = $"방송 {timeRemaining:0.0}초";
            _scoreText.text = $"성공 {hits} / 실패 {misses}";
            float width = _track.rect.width - 12f;
            _cursor.anchoredPosition = new Vector2((cursor01 - 0.5f) * width, 0f);
        }

        private void OnBroadcastFinished(BroadcastResult result)
        {
            EnqueueChat(result.Succeeded ? BroadcastChatEvent.BroadcastSuccess : BroadcastChatEvent.BroadcastFailure);
            _session.FinishBroadcast(result);
            ShowCurrentPhase();
        }

        private void OnAttemptResolved(bool succeeded, int hits, int misses)
        {
            EnqueueChat(succeeded ? BroadcastChatEvent.TimingHit : BroadcastChatEvent.TimingMiss);
            if (succeeded && hits > 0 && hits % 3 == 0)
            {
                EnqueueChat(BroadcastChatEvent.HotStreak);
            }
        }

        private void EnqueueChat(BroadcastChatEvent chatEvent)
        {
            if (_pendingChats.Count >= 12)
            {
                return;
            }

            _pendingChats.Enqueue(_chatService.CreateMessage(chatEvent));
            if (_chatRoutine == null)
            {
                _chatRoutine = StartCoroutine(PumpChat());
            }
        }

        private IEnumerator PumpChat()
        {
            while (_pendingChats.Count > 0)
            {
                yield return new WaitForSecondsRealtime(Random.Range(0.2f, 0.6f));
                _visibleChats.Enqueue(_pendingChats.Dequeue());
                while (_visibleChats.Count > 9)
                {
                    _visibleChats.Dequeue();
                }

                _chatText.text = string.Join("\n\n", _visibleChats);
            }

            _chatRoutine = null;
        }

        private void ResetChat()
        {
            _pendingChats.Clear();
            _visibleChats.Clear();
            _chatText.text = string.Empty;
            if (_chatRoutine != null)
            {
                StopCoroutine(_chatRoutine);
                _chatRoutine = null;
            }
        }

        private void AdvanceDay()
        {
            _session.AdvanceDay();
            ShowCurrentPhase();
        }

        private void Restart()
        {
            _session = new GameSession();
            ShowCurrentPhase();
        }

        private void ShowCurrentPhase()
        {
            PlayerState player = _session.Player;
            _statusText.text = $"DAY {player.Day}/{GameSession.MaxDays}     팔로워 {player.Subscribers:N0}명";
            _dayPanel.SetActive(_session.Phase == GamePhase.Day);
            _broadcastPanel.SetActive(_session.Phase == GamePhase.Broadcast);
            _chatPanel.SetActive(_session.Phase == GamePhase.Broadcast);
            _settlementPanel.SetActive(_session.Phase == GamePhase.Settlement);
            _endingPanel.SetActive(_session.Phase == GamePhase.GameOver || _session.Phase == GamePhase.Clear);

            if (_session.Phase == GamePhase.Day)
            {
                _titleText.text = $"DAY {player.Day} / 방송 준비";
            }
            else if (_session.Phase == GamePhase.Broadcast)
            {
                _titleText.text = "LIVE / 타이밍 방송";
            }
            else if (_session.Phase == GamePhase.Settlement)
            {
                BroadcastResult result = _session.LastBroadcast;
                _titleText.text = result.Succeeded ? "방송 성공!" : "방송 실패…";
                SetSettlementText(result);
            }
            else
            {
                bool clear = _session.Phase == GamePhase.Clear;
                _titleText.text = clear ? "7일 생존 성공!" : "GAME OVER";
                SetEndingText(clear);
            }
        }

        private void SetSettlementText(BroadcastResult result)
        {
            TextMeshProUGUI text = GetOrCreateResultText(_settlementPanel.transform);
            string subscriberSign = result.SubscriberDelta >= 0 ? "+" : string.Empty;
            string mentalSign = result.MentalDelta >= 0 ? "+" : string.Empty;
            text.text = $"타이밍 성공 {result.Hits}회 / 실패 {result.Misses}회\n\n구독자 {subscriberSign}{result.SubscriberDelta}명\n멘탈 {mentalSign}{result.MentalDelta:0}";
            Button button = _settlementPanel.GetComponentInChildren<Button>();
            button.GetComponentInChildren<TMP_Text>().text = _session.Player.Day >= GameSession.MaxDays ? "최종 결과 보기" : "다음 날";
        }

        private void SetEndingText(bool clear)
        {
            TextMeshProUGUI text = GetOrCreateResultText(_endingPanel.transform);
            text.text = clear
                ? $"최종 구독자 {_session.Player.Subscribers:N0}명\n7일간의 방송을 무사히 마쳤습니다."
                : $"최종 구독자 {_session.Player.Subscribers:N0}명\n멘탈 또는 구독자가 바닥났습니다.";
        }

        private TextMeshProUGUI GetOrCreateResultText(Transform parent)
        {
            Transform existing = parent.Find("Result Text");
            if (existing != null) return existing.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI text = CreateText("Result Text", parent, string.Empty, 26, FontStyles.Normal, TextAlignmentOptions.Center);
            SetRect(text.rectTransform, new Vector2(0.5f, 0.60f), new Vector2(620f, 240f), Vector2.zero);
            return text;
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        private GameObject CreatePanel(string name, Transform parent)
        {
            GameObject panel = CreateObject(name, parent);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0.78f);
            rect.offsetMin = new Vector2(30f, 25f);
            rect.offsetMax = new Vector2(-30f, 0f);
            return panel;
        }

        private Button CreateButton(string name, Transform parent, string label, Color color)
        {
            RectTransform rect = CreateImage(name, parent, color);
            Button button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.22f);
            button.colors = colors;
            TextMeshProUGUI text = CreateText("Label", rect, label, 21, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            return button;
        }

        private TextMeshProUGUI CreateText(string name, Transform parent, string value, int size, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject obj = CreateObject(name, parent);
            TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.richText = true;
            return text;
        }

        private static RectTransform CreateImage(string name, Transform parent, Color color)
        {
            GameObject obj = CreateObject(name, parent);
            Image image = obj.AddComponent<Image>();
            image.color = color;
            return obj.GetComponent<RectTransform>();
        }

        private static GameObject CreateObject(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
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
    }
}
