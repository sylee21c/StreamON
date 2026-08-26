using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StreamOn.Minigames.Runner
{
    public interface IBroadcastGameSuspendHandler
    {
        bool TrySuspendForGameSwitch();
    }

    public sealed class RunnerPauseController : MonoBehaviour
    {
        [SerializeField] private RunnerGameManager gameManager;
        [SerializeField] private RunnerChatController chat;
        [SerializeField] private RunnerCampaignController campaign;
        [SerializeField] private RunnerCampaignSettings settings;
        [SerializeField, Min(1)] private int resumeCountdownSeconds = 3;

        [Header("Scene-authored Pause UI")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private TMP_Text countdownText;
        [SerializeField] private TMP_Text volumeLabel;
        [SerializeField] private TMP_Text aiLabel;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Slider bgmVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private TMP_Text bgmVolumeLabel;
        [SerializeField] private TMP_Text sfxVolumeLabel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button manualSaveButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button aiToggleButton;
        [SerializeField] private Button settingsBackButton;

        private bool _paused;
        private bool _countingDown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureGameplayPauseController()
        {
            if (!IsBroadcastGameplayScene(SceneManager.GetActiveScene().name)
                || FindFirstObjectByType<RunnerPauseController>() != null) return;
            GameObject root = new GameObject("Shared Broadcast Pause", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            root.AddComponent<RunnerPauseController>();
        }

        private void Awake()
        {
            if (gameManager == null) gameManager = GetComponent<RunnerGameManager>();
            if (chat == null) chat = FindFirstObjectByType<RunnerChatController>();
            if (campaign == null) campaign = GetComponent<RunnerCampaignController>();
            AudioListener.volume = RunnerUserSettingsStore.Load().masterVolume;
            BindSceneUi();
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
            if (_countingDown) return;
            if (_paused && settingsPanel != null && settingsPanel.activeSelf) CloseSettings();
            else if (_paused) ResumeWithCountdown();
            else Pause();
        }

        private void OnDisable() => RestoreTime();
        private void OnDestroy() => RestoreTime();

        private void Pause()
        {
            _paused = true;
            Time.timeScale = 0f;
            settingsPanel.SetActive(false);
            pausePanel.SetActive(true);
        }

        private void ResumeWithCountdown()
        {
            if (!_paused || _countingDown) return;
            pausePanel.SetActive(false);
            settingsPanel.SetActive(false);
            if (IsBroadcastGameplayScene(SceneManager.GetActiveScene().name)) StartCoroutine(CountdownAndResume());
            else ResumeImmediately();
        }

        private IEnumerator CountdownAndResume()
        {
            _countingDown = true;
            countdownText.gameObject.SetActive(true);
            for (int number = resumeCountdownSeconds; number >= 1; number--)
            {
                countdownText.text = number.ToString();
                yield return new WaitForSecondsRealtime(1f);
            }
            countdownText.gameObject.SetActive(false);
            _paused = false;
            _countingDown = false;
            Time.timeScale = 1f;
        }

        private void ResumeImmediately()
        {
            countdownText.gameObject.SetActive(false);
            _paused = false;
            _countingDown = false;
            Time.timeScale = 1f;
        }

        private void ReturnToRoom()
        {
            IBroadcastGameSuspendHandler[] handlers = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .OfType<IBroadcastGameSuspendHandler>().ToArray();
            if (handlers.Any(handler => !handler.TrySuspendForGameSwitch()))
            {
                pausePanel.SetActive(true);
                return;
            }
            RunnerBroadcastSessionStore.SaveProgress(settings);
            RunnerBroadcastSessionStore.OpenGameSelectionOnRoomLoad = true;
            RestoreTime();
            RunnerSaveSession.RequireSlotSelection = false;
            string sceneName = settings != null ? settings.roomSceneName : "StreamerRoom";
            SceneManager.LoadScene(sceneName);
        }

        private void RestoreTime()
        {
            if (_paused || _countingDown) Time.timeScale = 1f;
            _paused = false;
            _countingDown = false;
        }

        private void BindSceneUi()
        {
            Canvas canvas = pausePanel != null ? pausePanel.GetComponentInParent<Canvas>(true) : null;
            if (canvas == null) canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                Transform[] transforms = canvas.GetComponentsInChildren<Transform>(true);
                pausePanel ??= transforms.FirstOrDefault(item => item.name == "Pause Menu")?.gameObject;
                settingsPanel ??= transforms.FirstOrDefault(item => item.name == "Settings Menu")?.gameObject;
                countdownText ??= transforms.FirstOrDefault(item => item.name == "Resume Countdown")?.GetComponent<TMP_Text>();
                EnsureRuntimeUi(canvas.transform);
            }
            if (pausePanel == null || settingsPanel == null || countdownText == null)
            {
                Debug.LogError("STREAM ON pause UI could not be prepared.", this);
                enabled = false;
                return;
            }

            Button[] pauseButtons = pausePanel.GetComponentsInChildren<Button>(true);
            Button[] settingButtons = settingsPanel.GetComponentsInChildren<Button>(true);
            resumeButton ??= pauseButtons.FirstOrDefault(item => item.name == "Continue Button");
            settingsButton ??= pauseButtons.FirstOrDefault(item => item.name == "Settings Button");
            manualSaveButton ??= pauseButtons.FirstOrDefault(item => item.name == "Manual Save Button");
            mainMenuButton ??= pauseButtons.FirstOrDefault(item => item.name == "Main Menu Button");
            aiToggleButton ??= settingButtons.FirstOrDefault(item => item.name == "AI Toggle Button");
            settingsBackButton ??= settingButtons.FirstOrDefault(item => item.name == "Back Button");
            volumeSlider ??= settingsPanel.GetComponentsInChildren<Slider>(true)
                .FirstOrDefault(item => item.name == "Volume Slider" || item.name == "Master Volume");
            Slider[] sliders = settingsPanel.GetComponentsInChildren<Slider>(true);
            bgmVolumeSlider ??= sliders.FirstOrDefault(item => item.name == "BGM Volume");
            sfxVolumeSlider ??= sliders.FirstOrDefault(item => item.name == "SFX Volume");
            TMP_Text[] settingTexts = settingsPanel.GetComponentsInChildren<TMP_Text>(true);
            volumeLabel ??= settingTexts.FirstOrDefault(item => item.name == "Volume Label");
            aiLabel ??= settingTexts.FirstOrDefault(item => item.name == "AI Label");
            bgmVolumeLabel ??= settingTexts.FirstOrDefault(item => item.name == "BGM Volume Label");
            sfxVolumeLabel ??= settingTexts.FirstOrDefault(item => item.name == "SFX Volume Label");

            ConfigurePauseButtons();
            ConfigureSettingsUi();

            resumeButton?.onClick.AddListener(ResumeWithCountdown);
            settingsButton?.onClick.AddListener(OpenSettings);
            aiToggleButton?.onClick.AddListener(ToggleAi);
            settingsBackButton?.onClick.AddListener(CloseSettings);
            if (volumeSlider != null)
            {
                volumeSlider.SetValueWithoutNotify(RunnerUserSettingsStore.Load().masterVolume);
                volumeSlider.onValueChanged.AddListener(SetVolume);
            }
            RunnerUserSettingsData audioSettings = RunnerUserSettingsStore.Load();
            if (bgmVolumeSlider != null)
            {
                bgmVolumeSlider.SetValueWithoutNotify(audioSettings.bgmVolume);
                bgmVolumeSlider.onValueChanged.AddListener(SetBgmVolume);
            }
            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.SetValueWithoutNotify(audioSettings.sfxVolume);
                sfxVolumeSlider.onValueChanged.AddListener(SetSfxVolume);
            }
            RefreshSettingsLabels();
            countdownText.fontStyle = FontStyles.Normal;
            countdownText.gameObject.SetActive(false);
            pausePanel.SetActive(false);
            settingsPanel.SetActive(false);
        }

        private void OpenSettings() { pausePanel.SetActive(false); settingsPanel.SetActive(true); RefreshSettingsLabels(); }
        private void CloseSettings() { settingsPanel.SetActive(false); pausePanel.SetActive(true); }
        private void SetBgmVolume(float value) { RunnerRoomAudioController.SetGlobalBgmVolume(value); RunnerGameAudioController.SetGlobalBgmVolume(value); RefreshSettingsLabels(); }
        private void SetSfxVolume(float value) { RunnerRoomAudioController.SetGlobalSfxVolume(value); RunnerGameAudioController.SetGlobalSfxVolume(value); RefreshSettingsLabels(); }
        private void SetVolume(float value) { AudioListener.volume = value; RunnerUserSettingsData data = RunnerUserSettingsStore.Load(); data.masterVolume = value; RunnerUserSettingsStore.Save(data); RefreshSettingsLabels(); }
        private void ToggleAi() { if (chat != null) chat.SetAiEnabled(!chat.AiEnabled); RefreshSettingsLabels(); }
        private void RefreshSettingsLabels()
        {
            RunnerUserSettingsData data = RunnerUserSettingsStore.Load();
            if (bgmVolumeLabel != null) bgmVolumeLabel.text = $"BGM  {Mathf.RoundToInt(data.bgmVolume * 100f)}%";
            if (sfxVolumeLabel != null) sfxVolumeLabel.text = $"SFX  {Mathf.RoundToInt(data.sfxVolume * 100f)}%";
        }

        private void ConfigurePauseButtons()
        {
            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveAllListeners();
                SetButtonLabel(resumeButton, "게임 재개");
                SetButtonPosition(resumeButton, 52.5f);
            }
            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveAllListeners();
                SetButtonLabel(settingsButton, "설정");
                SetButtonPosition(settingsButton, -52.5f);
            }
            if (manualSaveButton != null) manualSaveButton.gameObject.SetActive(false);
            if (mainMenuButton != null) mainMenuButton.gameObject.SetActive(false);
        }

        private void ConfigureSettingsUi()
        {
            TMP_FontAsset font = settingsPanel.GetComponentInChildren<TMP_Text>(true)?.font;
            Slider template = volumeSlider ?? settingsPanel.GetComponentInChildren<Slider>(true);
            if (bgmVolumeSlider == null)
                bgmVolumeSlider = template != null ? Instantiate(template, settingsPanel.transform)
                    : CreateRuntimeSlider(settingsPanel.transform, "BGM Volume", 40f);
            bgmVolumeSlider.name = "BGM Volume";
            SetControlPosition(bgmVolumeSlider.GetComponent<RectTransform>(), 40f, new Vector2(500f, 44f));
            if (sfxVolumeSlider == null)
                sfxVolumeSlider = template != null ? Instantiate(template, settingsPanel.transform)
                    : CreateRuntimeSlider(settingsPanel.transform, "SFX Volume", -75f);
            sfxVolumeSlider.name = "SFX Volume";
            SetControlPosition(sfxVolumeSlider.GetComponent<RectTransform>(), -75f, new Vector2(500f, 44f));

            if (bgmVolumeLabel == null)
                bgmVolumeLabel = CreateRuntimeText(settingsPanel.transform, "BGM Volume Label", "BGM", 26f, font);
            SetControlPosition(bgmVolumeLabel.rectTransform, 90f, new Vector2(520f, 40f));
            if (sfxVolumeLabel == null)
                sfxVolumeLabel = CreateRuntimeText(settingsPanel.transform, "SFX Volume Label", "SFX", 26f, font);
            SetControlPosition(sfxVolumeLabel.rectTransform, -25f, new Vector2(520f, 40f));

            if (settingsBackButton == null)
                settingsBackButton = CreateRuntimeButton(settingsPanel.transform, "Back Button", "뒤로", -190f, font);
            settingsBackButton.onClick.RemoveAllListeners();
            SetButtonPosition(settingsBackButton, -190f);
            if (volumeSlider != null && volumeSlider != bgmVolumeSlider && volumeSlider != sfxVolumeSlider)
                volumeSlider.gameObject.SetActive(false);
            if (volumeLabel != null) volumeLabel.gameObject.SetActive(false);
            if (aiToggleButton != null) aiToggleButton.gameObject.SetActive(false);
            if (aiLabel != null) aiLabel.gameObject.SetActive(false);
            bgmVolumeSlider.onValueChanged.RemoveAllListeners();
            sfxVolumeSlider.onValueChanged.RemoveAllListeners();
        }

        private void EnsureRuntimeUi(Transform canvasTransform)
        {
            TMP_FontAsset font = canvasTransform.GetComponentInChildren<TMP_Text>(true)?.font;
            if (pausePanel == null)
            {
                pausePanel = CreateRuntimePanel(canvasTransform, "Pause Menu", new Color(0f, 0f, 0f, .84f));
                TMP_Text title = CreateRuntimeText(pausePanel.transform, "Title", "일시정지", 42f, font);
                SetControlPosition(title.rectTransform, 235f, new Vector2(600f, 70f));
                resumeButton = CreateRuntimeButton(pausePanel.transform, "Continue Button", "게임 재개", 52.5f, font);
                settingsButton = CreateRuntimeButton(pausePanel.transform, "Settings Button", "설정", -52.5f, font);
            }
            if (settingsPanel == null)
            {
                settingsPanel = CreateRuntimePanel(canvasTransform, "Settings Menu", new Color(0f, 0f, 0f, .88f));
                TMP_Text title = CreateRuntimeText(settingsPanel.transform, "Title", "설정", 42f, font);
                SetControlPosition(title.rectTransform, 235f, new Vector2(600f, 70f));
            }
            if (countdownText == null)
            {
                countdownText = CreateRuntimeText(canvasTransform, "Resume Countdown", "3", 160f, font);
                countdownText.fontStyle = FontStyles.Normal;
                countdownText.alignment = TextAlignmentOptions.Center;
                SetControlPosition(countdownText.rectTransform, 0f, new Vector2(420f, 220f));
            }
        }

        private static GameObject CreateRuntimePanel(Transform parent, string name, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static TMP_Text CreateRuntimeText(Transform parent, string name, string value, float size, TMP_FontAsset font)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            TMP_Text text = obj.GetComponent<TMP_Text>();
            text.text = value; text.fontSize = size; text.font = font;
            text.color = Color.white; text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private static Button CreateRuntimeButton(Transform parent, string name, string label, float y, TMP_FontAsset font)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            Image image = obj.GetComponent<Image>(); image.color = new Color(.19f, .38f, .58f, 1f);
            Button button = obj.GetComponent<Button>(); button.targetGraphic = image;
            SetControlPosition(obj.GetComponent<RectTransform>(), y, new Vector2(460f, 82f));
            TMP_Text text = CreateRuntimeText(obj.transform, "Label", label, 28f, font);
            Stretch(text.rectTransform, new Vector2(12f, 8f));
            return button;
        }

        private static Slider CreateRuntimeSlider(Transform parent, string name, float y)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Slider));
            obj.transform.SetParent(parent, false);
            SetControlPosition(obj.GetComponent<RectTransform>(), y, new Vector2(500f, 44f));
            Slider slider = obj.GetComponent<Slider>();
            GameObject background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            background.transform.SetParent(obj.transform, false); Stretch(background.GetComponent<RectTransform>(), new Vector2(0f, 14f));
            background.GetComponent<Image>().color = new Color(.18f, .2f, .24f, 1f);
            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fill.transform.SetParent(obj.transform, false); Stretch(fill.GetComponent<RectTransform>(), new Vector2(8f, 17f));
            fill.GetComponent<Image>().color = new Color(.28f, .72f, .82f, 1f);
            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handle.transform.SetParent(obj.transform, false);
            RectTransform handleRect = handle.GetComponent<RectTransform>(); handleRect.sizeDelta = new Vector2(28f, 42f);
            handle.GetComponent<Image>().color = Color.white;
            slider.fillRect = fill.GetComponent<RectTransform>(); slider.handleRect = handleRect;
            slider.targetGraphic = handle.GetComponent<Image>(); slider.minValue = 0f; slider.maxValue = 1f;
            return slider;
        }

        private static void SetButtonLabel(Button button, string value)
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = value;
        }

        private static void SetButtonPosition(Button button, float y) =>
            SetControlPosition(button.GetComponent<RectTransform>(), y, new Vector2(460f, 82f));

        private static void SetControlPosition(RectTransform rect, float y, Vector2 size)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(0f, y); rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, Vector2 padding)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = padding; rect.offsetMax = -padding;
        }

        private static bool IsBroadcastGameplayScene(string sceneName) => sceneName == "BroadcastRunner"
            || sceneName == "TileArena" || sceneName == "MainScene";
    }
}
