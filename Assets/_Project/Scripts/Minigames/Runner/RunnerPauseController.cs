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
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button manualSaveButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button aiToggleButton;
        [SerializeField] private Button settingsBackButton;
        private bool _paused;
        private bool _countingDown;

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
            if (_paused) ResumeWithCountdown();
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
            StartCoroutine(CountdownAndResume());
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
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                Transform[] transforms = canvas.GetComponentsInChildren<Transform>(true);
                pausePanel ??= transforms.FirstOrDefault(item => item.name == "Pause Menu")?.gameObject;
                settingsPanel ??= transforms.FirstOrDefault(item => item.name == "Settings Menu")?.gameObject;
                countdownText ??= transforms.FirstOrDefault(item => item.name == "Resume Countdown")?.GetComponent<TMP_Text>();
            }
            if (pausePanel == null || settingsPanel == null || countdownText == null)
            {
                Debug.LogError("STREAM ON pause UI references are missing. Bake the scene-authored UI before Play Mode.", this);
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
            volumeSlider ??= settingsPanel.GetComponentInChildren<Slider>(true);
            TMP_Text[] settingTexts = settingsPanel.GetComponentsInChildren<TMP_Text>(true);
            volumeLabel ??= settingTexts.FirstOrDefault(item => item.name == "Volume Label");
            aiLabel ??= settingTexts.FirstOrDefault(item => item.name == "AI Label");

            resumeButton?.onClick.AddListener(ResumeWithCountdown);
            settingsButton?.onClick.AddListener(OpenSettings);
            manualSaveButton?.onClick.AddListener(ManualSave);
            mainMenuButton?.onClick.AddListener(ReturnToRoom);
            if (mainMenuButton != null)
            {
                TMP_Text label = mainMenuButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = "종료하기\n<size=55%>게임 선택 화면으로 돌아가기</size>";
            }
            aiToggleButton?.onClick.AddListener(ToggleAi);
            settingsBackButton?.onClick.AddListener(CloseSettings);
            if (volumeSlider != null)
            {
                volumeSlider.value = RunnerUserSettingsStore.Load().masterVolume;
                volumeSlider.onValueChanged.AddListener(SetVolume);
            }
            RefreshSettingsLabels();
            countdownText.gameObject.SetActive(false);
            pausePanel.SetActive(false);
            settingsPanel.SetActive(false);
        }

        private void OpenSettings() { pausePanel.SetActive(false); settingsPanel.SetActive(true); RefreshSettingsLabels(); }
        private void CloseSettings() { settingsPanel.SetActive(false); pausePanel.SetActive(true); }
        private void SetVolume(float value) { AudioListener.volume = value; RunnerUserSettingsData data = RunnerUserSettingsStore.Load(); data.masterVolume = value; RunnerUserSettingsStore.Save(data); RefreshSettingsLabels(); }
        private void ManualSave()
        {
            bool saved = campaign != null ? campaign.ManualSave()
                : settings != null && RunnerCampaignSaveStore.TryLoad(settings, out RunnerCampaignSaveData data)
                    && RunnerCampaignSaveStore.Save(settings, data, true);
            TMP_Text label = pausePanel.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.text == "수동 저장" || text.text == "저장 완료" || text.text == "저장 실패");
            if (label != null) label.text = saved ? "저장 완료" : "저장 실패";
        }
        private void ToggleAi() { if (chat != null) chat.SetAiEnabled(!chat.AiEnabled); RefreshSettingsLabels(); }
        private void RefreshSettingsLabels()
        {
            if (volumeLabel != null) volumeLabel.text = $"전체 음량  {Mathf.RoundToInt((volumeSlider != null ? volumeSlider.value : AudioListener.volume) * 100f)}%";
            if (aiLabel != null) aiLabel.text = $"AI 채팅  {(chat != null && chat.AiEnabled ? "ON" : "OFF")}";
        }
    }
}
