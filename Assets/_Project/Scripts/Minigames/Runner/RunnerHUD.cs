using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace StreamOn.Minigames.Runner
{
    public sealed class RunnerHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text highScoreText;
        [SerializeField] private TMP_Text speedText;
        [SerializeField] private TMP_Text broadcastTimeText;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private GameObject gameOverPanel;

        public void SetScore(int score, int highScore, float speed, float secondsRemaining = -1f)
        {
            EnsureReferences();
            scoreText.text = $"SCORE  {score:000000}";
            highScoreText.text = $"BEST  {highScore:000000}";
            speedText.text = $"SPEED  {speed:0.0}";
            if (secondsRemaining >= 0f)
            {
                int totalSeconds = Mathf.CeilToInt(secondsRemaining);
                if (broadcastTimeText != null)
                    broadcastTimeText.text = $"STREAM  {totalSeconds / 60:00}:{totalSeconds % 60:00}";
            }
            else if (broadcastTimeText != null) broadcastTimeText.text = "STREAM  --:--";
        }

        public void SetHealth(int current, int maximum)
        {
            EnsureReferences();
            healthText.text = $"HP  {new string('♥', current)}{new string('-', maximum - current)}";
        }
        public void ShowGameOver(bool visible) => gameOverPanel.SetActive(visible);

        public void SetRetryAvailable(bool available, float secondsRemaining)
        {
            if (gameOverPanel == null) return;
            Button retryButton = gameOverPanel.GetComponentInChildren<Button>(true);
            if (retryButton == null) return;
            retryButton.interactable = available;
            TMP_Text label = retryButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = available
                ? $"RETRY  ({Mathf.CeilToInt(secondsRemaining)}s)"
                : "BROADCAST ENDING";
        }

        private void EnsureReferences()
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in texts)
            {
                if (text.name == "Score") scoreText = text;
                else if (text.name == "Best") highScoreText = text;
                else if (text.name == "Speed") speedText = text;
                else if (text.name == "Broadcast Time") broadcastTimeText = text;
                else if (text.name == "Health") healthText = text;
            }
        }
    }

    public sealed class RunnerBroadcastHeatGauge : MonoBehaviour
    {
        private static RunnerBroadcastHeatGauge _instance;
        private RectTransform _fill;
        private Image _fillImage;
        private TMP_Text _label;
        private RectTransform _focusFill;
        private Image _focusFillImage;
        private TMP_Text _focusLabel;
        private float _displayedValue = 50f;
        private float _targetValue = 50f;
        private float _focus = 100f;
        private bool _slowMotionActive;
        private const float SlowTimeScale = 0.24f;
        private const float FocusDrainPerSecond = 24f;
        private const float FocusRecoveryPerSecond = 13f;

        public static void Show(float value)
        {
            EnsureExists();
            _instance.gameObject.SetActive(true);
            _instance._targetValue = Mathf.Clamp(value, 0f, 100f);
            _instance._focus = 100f;
            _instance.SetSlowMotion(false);
            _instance.RefreshVisuals(true);
        }

        public static void SetValue(float value)
        {
            if (_instance == null) EnsureExists();
            _instance._targetValue = Mathf.Clamp(value, 0f, 100f);
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance.SetSlowMotion(false);
            _instance.gameObject.SetActive(false);
        }

        private static void EnsureExists()
        {
            if (_instance != null) return;
            GameObject canvasObject = new GameObject("Broadcast Heat UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            GameObject gaugeObject = new GameObject("Broadcast Heat Gauge", typeof(RectTransform));
            gaugeObject.transform.SetParent(canvasObject.transform, false);
            RectTransform root = gaugeObject.GetComponent<RectTransform>();
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -18f);
            root.sizeDelta = new Vector2(330f, 98f);

            _instance = gaugeObject.AddComponent<RunnerBroadcastHeatGauge>();
            _instance.Build(root);
        }

        private void Build(RectTransform root)
        {
            _label = CreateText("Label", root, new Vector2(0f, 62f), new Vector2(330f, 24f));
            _label.alignment = TextAlignmentOptions.Center;
            _label.fontSize = 18f;
            _label.fontStyle = FontStyles.Bold;

            Image shadow = CreateImage("Shadow", root, new Color(0f, 0f, 0f, 0.72f));
            RectTransform shadowRect = shadow.rectTransform;
            shadowRect.anchorMin = shadowRect.anchorMax = new Vector2(0.5f, 0f);
            shadowRect.pivot = new Vector2(0.5f, 0f);
            shadowRect.anchoredPosition = new Vector2(0f, 46f);
            shadowRect.sizeDelta = new Vector2(306f, 18f);

            Image background = CreateImage("Background", shadowRect, new Color(0.08f, 0.09f, 0.11f, 0.96f));
            Stretch(background.rectTransform, 2f);

            _fillImage = CreateImage("Fill", background.rectTransform, Color.white);
            _fill = _fillImage.rectTransform;
            _fill.anchorMin = new Vector2(0f, 0f);
            _fill.anchorMax = new Vector2(0.5f, 1f);
            _fill.offsetMin = new Vector2(2f, 2f);
            _fill.offsetMax = new Vector2(-2f, -2f);
            _fill.pivot = new Vector2(0f, 0.5f);

            _focusLabel = CreateText("Focus Label", root, new Vector2(0f, 16f), new Vector2(330f, 24f));
            _focusLabel.alignment = TextAlignmentOptions.Center;
            _focusLabel.fontSize = 16f;
            _focusLabel.fontStyle = FontStyles.Bold;

            Image focusShadow = CreateImage("Focus Shadow", root, new Color(0f, 0f, 0f, 0.72f));
            RectTransform focusShadowRect = focusShadow.rectTransform;
            focusShadowRect.anchorMin = focusShadowRect.anchorMax = new Vector2(0.5f, 0f);
            focusShadowRect.pivot = new Vector2(0.5f, 0f);
            focusShadowRect.anchoredPosition = Vector2.zero;
            focusShadowRect.sizeDelta = new Vector2(306f, 16f);
            Image focusBackground = CreateImage("Focus Background", focusShadowRect, new Color(0.08f, 0.09f, 0.11f, 0.96f));
            Stretch(focusBackground.rectTransform, 2f);
            _focusFillImage = CreateImage("Focus Fill", focusBackground.rectTransform, new Color(0.35f, 0.76f, 1f));
            _focusFill = _focusFillImage.rectTransform;
            _focusFill.anchorMin = Vector2.zero;
            _focusFill.anchorMax = Vector2.one;
            _focusFill.offsetMin = new Vector2(2f, 2f);
            _focusFill.offsetMax = new Vector2(-2f, -2f);
            _focusFill.pivot = new Vector2(0f, 0.5f);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame && Time.timeScale > 0f)
            {
                if (_slowMotionActive) SetSlowMotion(false);
                else if (_focus > 0.5f) SetSlowMotion(true);
            }
            if (_slowMotionActive && Time.timeScale > 0f)
            {
                _focus = Mathf.Max(0f, _focus - FocusDrainPerSecond * Time.unscaledDeltaTime);
                if (_focus <= 0f) SetSlowMotion(false);
                else if (!Mathf.Approximately(Time.timeScale, SlowTimeScale)) Time.timeScale = SlowTimeScale;
            }
            else if (!_slowMotionActive)
            {
                _focus = Mathf.Min(100f, _focus + FocusRecoveryPerSecond * Time.unscaledDeltaTime);
            }
            _displayedValue = Mathf.MoveTowards(_displayedValue, _targetValue, 55f * Time.unscaledDeltaTime);
            RefreshVisuals(false);
        }

        private void SetSlowMotion(bool active)
        {
            _slowMotionActive = active && _focus > 0f;
            if (Time.timeScale > 0f) Time.timeScale = _slowMotionActive ? SlowTimeScale : 1f;
        }

        private void RefreshVisuals(bool immediate)
        {
            if (immediate) _displayedValue = _targetValue;
            float normalized = Mathf.Clamp01(_displayedValue / 100f);
            _fill.anchorMax = new Vector2(normalized, 1f);
            _fillImage.color = normalized <= 0.5f
                ? Color.Lerp(new Color(1f, 0.16f, 0.13f), Color.white, normalized * 2f)
                : Color.Lerp(Color.white, new Color(0.16f, 0.92f, 0.30f), (normalized - 0.5f) * 2f);
            _label.text = $"방송 열기  {Mathf.RoundToInt(_displayedValue)}%";
            _label.color = _fillImage.color;
            float focus01 = Mathf.Clamp01(_focus / 100f);
            _focusFill.anchorMax = new Vector2(focus01, 1f);
            _focusFillImage.color = Color.Lerp(new Color(1f, 0.35f, 0.25f), new Color(0.35f, 0.76f, 1f), focus01);
            _focusLabel.text = _slowMotionActive
                ? $"집중력  {Mathf.CeilToInt(_focus)}%  ·  TAB 슬로우 ON"
                : $"집중력  {Mathf.CeilToInt(_focus)}%  ·  TAB";
            _focusLabel.color = _slowMotionActive ? new Color(1f, 0.88f, 0.36f) : _focusFillImage.color;
        }

        private void OnDisable() => SetSlowMotion(false);
        private void OnDestroy() => SetSlowMotion(false);

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(parent, false);
            Image image = child.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(string name, Transform parent, Vector2 position, Vector2 size)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            child.transform.SetParent(parent, false);
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            TMP_Text text = child.GetComponent<TMP_Text>();
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
