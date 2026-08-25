using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using StreamOn.Minigames.Runner;

namespace StreamOn.MainMenu
{
    public sealed class StreamOnMainMenuController : MonoBehaviour
    {
        [Header("Save and Scene Flow")]
        [SerializeField] private RunnerCampaignSettings settings;
        [SerializeField] private string roomSceneName = "StreamerRoom";
        [SerializeField, Range(1, 32)] private int maximumNameLength = 16;

        [Header("Menu Presentation")]
        [SerializeField] private Transform mainCameraTransform;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Animator playerAnimator;
        [SerializeField] private Vector3 cameraStartPosition = new Vector3(-1.273f, 1.293f, -.225f);
        [SerializeField] private Vector3 cameraEndPosition = new Vector3(-2.445f, 1.293f, -1.397f);
        [SerializeField, Min(.1f)] private float cameraTravelSeconds = 18f;
        [SerializeField, Min(.1f)] private float playerTurnSharpness = 8f;

        [Header("Scene-authored Main UI")]
        [SerializeField] private Image gameLogoImage;
        [SerializeField] private TMP_InputField streamerNameInput;
        [SerializeField] private Button startButton;
        [SerializeField] private Button optionButton;

        [Header("Scene-authored Option UI")]
        [SerializeField] private GameObject optionPanel;
        [SerializeField] private Button optionCloseButton;
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider bgmVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private TMP_Text masterVolumeLabel;
        [SerializeField] private TMP_Text bgmVolumeLabel;
        [SerializeField] private TMP_Text sfxVolumeLabel;

        private float _cameraElapsed;
        private bool _starting;
        private static readonly int IdleState = Animator.StringToHash("Base Layer.idle");
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");

        private void Awake()
        {
            Time.timeScale = 1f;
            if (mainCameraTransform != null) mainCameraTransform.position = cameraStartPosition;
            if (playerAnimator != null)
            {
                playerAnimator.applyRootMotion = false;
                playerAnimator.SetFloat(SpeedParameter, 1f);
                playerAnimator.Play(IdleState, 0, 0f);
            }
            if (streamerNameInput != null)
            {
                streamerNameInput.characterLimit = maximumNameLength;
                streamerNameInput.lineType = TMP_InputField.LineType.SingleLine;
                streamerNameInput.SetTextWithoutNotify(string.Empty);
            }
            if (optionPanel != null) optionPanel.SetActive(false);
            LoadAudioOptions();
            RefreshStartButton();
        }

        private void LateUpdate()
        {
            if (mainCameraTransform != null)
            {
                _cameraElapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(_cameraElapsed / Mathf.Max(.1f, cameraTravelSeconds));
                mainCameraTransform.position = Vector3.Lerp(cameraStartPosition, cameraEndPosition,
                    Mathf.SmoothStep(0f, 1f, progress));
            }
            if (playerTransform == null || mainCameraTransform == null) return;
            Vector3 towardCamera = mainCameraTransform.position - playerTransform.position;
            towardCamera.y = 0f;
            if (towardCamera.sqrMagnitude < .0001f) return;
            Quaternion facing = Quaternion.LookRotation(towardCamera.normalized, Vector3.up);
            float blend = 1f - Mathf.Exp(-playerTurnSharpness * Time.unscaledDeltaTime);
            playerTransform.rotation = Quaternion.Slerp(playerTransform.rotation, facing, blend);
        }

        public void HandleNameChanged(string value) => RefreshStartButton();

        public void StartGame()
        {
            if (_starting || settings == null || streamerNameInput == null) return;
            string streamerName = (streamerNameInput.text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(streamerName)) return;
            if (streamerName.Length > maximumNameLength)
                streamerName = streamerName.Substring(0, maximumNameLength);

            RunnerCampaignSaveData save = RunnerCampaignSaveStore.TryLoad(settings, out RunnerCampaignSaveData loaded)
                ? loaded
                : RunnerCampaignSaveStore.CreateNew(settings);
            save.streamerName = streamerName;
            if (!RunnerCampaignSaveStore.Save(settings, save, true))
            {
                Debug.LogError("STREAM ON main menu: 스트리머 이름을 저장하지 못했습니다.", this);
                return;
            }
            _starting = true;
            RunnerSaveSession.RequireSlotSelection = false;
            SceneManager.LoadScene(string.IsNullOrWhiteSpace(roomSceneName) ? settings.roomSceneName : roomSceneName);
        }

        public void OpenOptions()
        {
            LoadAudioOptions();
            if (optionPanel != null) optionPanel.SetActive(true);
            optionCloseButton?.Select();
        }

        public void CloseOptions()
        {
            if (optionPanel != null) optionPanel.SetActive(false);
            optionButton?.Select();
        }

        public void SetMasterVolume(float value)
        {
            RunnerUserSettingsData data = RunnerUserSettingsStore.Load();
            data.masterVolume = Mathf.Clamp01(value);
            RunnerUserSettingsStore.Save(data);
            AudioListener.volume = data.masterVolume;
            RefreshAudioLabels(data);
        }

        public void SetBgmVolume(float value)
        {
            RunnerRoomAudioController.SetGlobalBgmVolume(value);
            RefreshAudioLabels(RunnerUserSettingsStore.Load());
        }

        public void SetSfxVolume(float value)
        {
            RunnerRoomAudioController.SetGlobalSfxVolume(value);
            RefreshAudioLabels(RunnerUserSettingsStore.Load());
        }

        private void RefreshStartButton()
        {
            if (startButton == null) return;
            startButton.interactable = streamerNameInput != null
                && !string.IsNullOrWhiteSpace(streamerNameInput.text);
        }

        private void LoadAudioOptions()
        {
            RunnerUserSettingsData data = RunnerUserSettingsStore.Load();
            AudioListener.volume = data.masterVolume;
            masterVolumeSlider?.SetValueWithoutNotify(data.masterVolume);
            bgmVolumeSlider?.SetValueWithoutNotify(data.bgmVolume);
            sfxVolumeSlider?.SetValueWithoutNotify(data.sfxVolume);
            RefreshAudioLabels(data);
        }

        private void RefreshAudioLabels(RunnerUserSettingsData data)
        {
            if (masterVolumeLabel != null) masterVolumeLabel.text = $"전체 음량  {Mathf.RoundToInt(data.masterVolume * 100f)}%";
            if (bgmVolumeLabel != null) bgmVolumeLabel.text = $"BGM  {Mathf.RoundToInt(data.bgmVolume * 100f)}%";
            if (sfxVolumeLabel != null) sfxVolumeLabel.text = $"SFX  {Mathf.RoundToInt(data.sfxVolume * 100f)}%";
        }
    }
}
