using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StreamOn.Minigames.Runner
{
    public sealed class RunnerPauseController : MonoBehaviour
    {
        [SerializeField] private RunnerGameManager gameManager;
        [SerializeField] private RunnerChatController chat;
        [SerializeField] private RunnerCampaignController campaign;
        [SerializeField] private RunnerCampaignSettings settings;
        [SerializeField, Min(1)] private int resumeCountdownSeconds = 3;

        private GameObject _pausePanel;
        private GameObject _settingsPanel;
        private TMP_Text _countdownText;
        private TMP_Text _volumeLabel;
        private TMP_Text _aiLabel;
        private Slider _volumeSlider;
        private bool _paused;
        private bool _countingDown;
        private TMP_FontAsset _font;

        private void Awake()
        {
            if (gameManager == null) gameManager = GetComponent<RunnerGameManager>();
            if (chat == null) chat = FindFirstObjectByType<RunnerChatController>();
            if (campaign == null) campaign = GetComponent<RunnerCampaignController>();
            AudioListener.volume = RunnerUserSettingsStore.Load().masterVolume;
            _font = FindFirstObjectByType<TMP_Text>()?.font;
            BuildUi();
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
            if (_countingDown) return;
            if (_paused) ResumeWithCountdown();
            else if (gameManager != null && gameManager.State == RunnerGameState.Playing) Pause();
        }

        private void OnDisable() => RestoreTime();
        private void OnDestroy() => RestoreTime();

        private void Pause()
        {
            _paused = true;
            Time.timeScale = 0f;
            _settingsPanel.SetActive(false);
            _pausePanel.SetActive(true);
        }

        private void ResumeWithCountdown()
        {
            if (!_paused || _countingDown) return;
            _pausePanel.SetActive(false);
            _settingsPanel.SetActive(false);
            StartCoroutine(CountdownAndResume());
        }

        private IEnumerator CountdownAndResume()
        {
            _countingDown = true;
            _countdownText.gameObject.SetActive(true);
            for (int number = resumeCountdownSeconds; number >= 1; number--)
            {
                _countdownText.text = number.ToString();
                yield return new WaitForSecondsRealtime(1f);
            }
            _countdownText.gameObject.SetActive(false);
            _paused = false;
            _countingDown = false;
            Time.timeScale = 1f;
        }

        private void ReturnToRoom()
        {
            RestoreTime();
            RunnerSaveSession.RequireSlotSelection = true;
            string sceneName = settings != null ? settings.roomSceneName : "StreamerRoom";
            SceneManager.LoadScene(sceneName);
        }

        private void RestoreTime()
        {
            if (_paused || _countingDown) Time.timeScale = 1f;
            _paused = false;
            _countingDown = false;
        }

        private void BuildUi()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("Pause Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            _pausePanel = CreatePanel(canvas.transform, "Pause Menu", new Color(0.025f, 0.035f, 0.06f, 0.94f));
            CreateText(_pausePanel.transform, "일시정지", 44, new Vector2(0, 155), new Vector2(520, 70));
            CreateButton(_pausePanel.transform, "계속하기", new Vector2(0, 55), ResumeWithCountdown);
            CreateButton(_pausePanel.transform, "설정", new Vector2(0, -20), OpenSettings);
            CreateButton(_pausePanel.transform, "수동 저장", new Vector2(0, -95), ManualSave);
            CreateButton(_pausePanel.transform, "메인 화면", new Vector2(0, -170), ReturnToRoom);

            _settingsPanel = CreatePanel(canvas.transform, "Settings Menu", new Color(0.025f, 0.035f, 0.06f, 0.97f));
            CreateText(_settingsPanel.transform, "설정", 42, new Vector2(0, 170), new Vector2(520, 65));
            _volumeLabel = CreateText(_settingsPanel.transform, "전체 음량", 24, new Vector2(0, 85), new Vector2(520, 45));
            _volumeSlider = CreateSlider(_settingsPanel.transform, new Vector2(0, 35));
            _volumeSlider.value = RunnerUserSettingsStore.Load().masterVolume;
            _volumeSlider.onValueChanged.AddListener(SetVolume);
            _aiLabel = CreateText(_settingsPanel.transform, string.Empty, 24, new Vector2(0, -40), new Vector2(520, 45));
            CreateButton(_settingsPanel.transform, "AI 채팅 전환", new Vector2(0, -95), ToggleAi);
            CreateButton(_settingsPanel.transform, "뒤로", new Vector2(0, -170), CloseSettings);
            RefreshSettingsLabels();

            _countdownText = CreateText(canvas.transform, string.Empty, 110, Vector2.zero, new Vector2(500, 180));
            _countdownText.fontStyle = FontStyles.Bold;
            _countdownText.gameObject.SetActive(false);
            _pausePanel.SetActive(false);
            _settingsPanel.SetActive(false);
        }

        private void OpenSettings() { _pausePanel.SetActive(false); _settingsPanel.SetActive(true); RefreshSettingsLabels(); }
        private void CloseSettings() { _settingsPanel.SetActive(false); _pausePanel.SetActive(true); }
        private void SetVolume(float value) { AudioListener.volume = value; RunnerUserSettingsData data = RunnerUserSettingsStore.Load(); data.masterVolume = value; RunnerUserSettingsStore.Save(data); RefreshSettingsLabels(); }
        private void ManualSave()
        {
            bool saved = campaign != null && campaign.ManualSave();
            TMP_Text label = _pausePanel.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.text == "수동 저장" || text.text == "저장 완료" || text.text == "저장 실패");
            if (label != null) label.text = saved ? "저장 완료" : "저장 실패";
        }
        private void ToggleAi() { if (chat != null) chat.SetAiEnabled(!chat.AiEnabled); RefreshSettingsLabels(); }
        private void RefreshSettingsLabels()
        {
            if (_volumeLabel != null) _volumeLabel.text = $"전체 음량  {Mathf.RoundToInt((_volumeSlider != null ? _volumeSlider.value : AudioListener.volume) * 100f)}%";
            if (_aiLabel != null) _aiLabel.text = $"AI 채팅  {(chat != null && chat.AiEnabled ? "ON" : "OFF")}";
        }

        private GameObject CreatePanel(Transform parent, string name, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private TMP_Text CreateText(Transform parent, string value, float size, Vector2 position, Vector2 dimensions)
        {
            GameObject obj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            TMP_Text label = obj.GetComponent<TMP_Text>();
            label.text = value; label.fontSize = size; label.color = Color.white; label.alignment = TextAlignmentOptions.Center;
            if (_font != null) label.font = _font;
            RectTransform rect = obj.GetComponent<RectTransform>(); rect.sizeDelta = dimensions; rect.anchoredPosition = position;
            return label;
        }

        private void CreateButton(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction action)
        {
            GameObject obj = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(320, 56); rect.anchoredPosition = position;
            obj.GetComponent<Image>().color = new Color(0.13f, 0.52f, 0.58f, 1f);
            obj.GetComponent<Button>().onClick.AddListener(action);
            CreateText(obj.transform, label, 25, Vector2.zero, rect.sizeDelta);
        }

        private Slider CreateSlider(Transform parent, Vector2 position)
        {
            GameObject root = new GameObject("Master Volume", typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(360, 24); rect.anchoredPosition = position;
            GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image)); background.transform.SetParent(root.transform, false);
            RectTransform bgRect = background.GetComponent<RectTransform>(); bgRect.anchorMin = new Vector2(0, .35f); bgRect.anchorMax = new Vector2(1, .65f); bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = new Color(.2f, .22f, .27f, 1f);
            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)); fill.transform.SetParent(root.transform, false); fill.GetComponent<Image>().color = new Color(.2f, .8f, .72f, 1f);
            RectTransform fillRect = fill.GetComponent<RectTransform>(); fillRect.anchorMin = new Vector2(0, .35f); fillRect.anchorMax = new Vector2(1, .65f); fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)); handle.transform.SetParent(root.transform, false); handle.GetComponent<Image>().color = Color.white;
            RectTransform handleRect = handle.GetComponent<RectTransform>(); handleRect.sizeDelta = new Vector2(24, 24);
            Slider slider = root.GetComponent<Slider>(); slider.fillRect = fillRect; slider.handleRect = handleRect; slider.targetGraphic = handle.GetComponent<Image>(); slider.minValue = 0f; slider.maxValue = 1f;
            return slider;
        }
    }
}
