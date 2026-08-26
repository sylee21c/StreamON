using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using StreamOn.Minigames.Runner;

namespace StreamOn.Broadcast
{
    /// <summary>
    /// Drives scene-authored game title menus. Every visible element is serialized in
    /// RunnerMainMenu/TileArenaMainMenu; this component never constructs UI at runtime.
    /// </summary>
    public sealed class BroadcastGameMainMenuController : MonoBehaviour
    {
        [Header("Scene Flow")]
        [SerializeField] private string gameSceneName;
        [SerializeField] private string roomSceneName = "StreamerRoom";

        [Header("Scene-authored UI")]
        [SerializeField] private Image titleScreenImage;
        [SerializeField] private Image logoImage;
        [SerializeField] private Button playButton;
        [SerializeField] private Button tutorialButton;
        [SerializeField] private GameObject tutorialPanel;
        [SerializeField] private Button tutorialCloseButton;

        [Header("Title Audio")]
        [SerializeField] private AudioClip backgroundMusic;
        [SerializeField] private AudioClip uiButtonSound;
        [SerializeField, Range(0f, 1f)] private float backgroundMusicVolume = .7f;
        [SerializeField, Range(0f, 1f)] private float uiButtonVolume = .8f;
        [SerializeField, Min(0f)] private float buttonTransitionDelay = .1f;

        private bool _loading;
        private BroadcastTitleScreenPresentation _presentation;

        private void Awake()
        {
            Canvas canvas = titleScreenImage != null ? titleScreenImage.GetComponentInParent<Canvas>()
                : FindFirstObjectByType<Canvas>();
            _presentation = GetComponent<BroadcastTitleScreenPresentation>();
            if (_presentation == null) _presentation = gameObject.AddComponent<BroadcastTitleScreenPresentation>();
            _presentation.Configure(canvas, tutorialButton, tutorialPanel, backgroundMusic, uiButtonSound,
                backgroundMusicVolume, uiButtonVolume, tutorialCloseButton);
        }

        private void Update()
        {
            if (_loading || Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
            if (tutorialPanel != null && tutorialPanel.activeSelf) return;
            ReturnToRoom();
        }

        public void Play()
        {
            if (_loading) return;
            if (string.IsNullOrWhiteSpace(gameSceneName) || !Application.CanStreamedLevelBeLoaded(gameSceneName))
            {
                Debug.LogError($"STREAM ON game menu: 게임 씬을 찾을 수 없습니다: {gameSceneName}", this);
                return;
            }
            _loading = true;
            _presentation?.PlayButtonSound();
            StartCoroutine(LoadGameAfterButtonSound());
        }

        private IEnumerator LoadGameAfterButtonSound()
        {
            if (buttonTransitionDelay > 0f) yield return new WaitForSecondsRealtime(buttonTransitionDelay);
            SceneManager.LoadScene(gameSceneName);
        }

        public void OpenTutorial()
        {
            if (_loading) return;
            if (_presentation != null) _presentation.OpenTutorial();
            else if (tutorialPanel != null) tutorialPanel.SetActive(true);
            tutorialCloseButton?.Select();
        }

        public void CloseTutorial()
        {
            if (_presentation != null) _presentation.CloseTutorial();
            else if (tutorialPanel != null) tutorialPanel.SetActive(false);
            tutorialButton?.Select();
        }

        public void ReturnToRoom()
        {
            if (_loading || string.IsNullOrWhiteSpace(roomSceneName)) return;
            _loading = true;
            _presentation?.PlayButtonSound();
            if (RunnerBroadcastSessionStore.IsActive)
            {
                RunnerBroadcastSessionStore.OpenGameSelectionOnRoomLoad = true;
                RunnerSaveSession.RequireSlotSelection = false;
            }
            SceneManager.LoadScene(roomSceneName);
        }
    }

    public sealed class BroadcastTitleScreenPresentation : MonoBehaviour
    {
        private GameObject _tutorialPanel;
        private Button _tutorialButton;
        private Button _tutorialCloseButton;
        private AudioClip _buttonSound;
        private AudioSource _musicSource;
        private AudioSource _effectsSource;
        private float _buttonVolume = .8f;

        public bool TutorialOpen => _tutorialPanel != null && _tutorialPanel.activeSelf;
        public Button TutorialButton => _tutorialButton;

        public void Configure(Canvas canvas, Button tutorialButton, GameObject tutorialPanel,
            AudioClip backgroundMusic, AudioClip buttonSound, float musicVolume, float buttonVolume,
            Button tutorialCloseButton = null)
        {
            if (canvas == null) return;
            _buttonSound = buttonSound;
            _buttonVolume = Mathf.Clamp01(buttonVolume);
            PrepareAudio(backgroundMusic, musicVolume);
            _tutorialButton = tutorialButton;
            _tutorialPanel = tutorialPanel;
            if (_tutorialButton == null || _tutorialPanel == null)
            {
                Debug.LogError("STREAM ON title tutorial: 씬에 Tutorial Button과 Tutorial Panel을 직접 배치하고 연결해야 합니다.", this);
                return;
            }
            if (_tutorialButton != null) _tutorialButton.onClick.AddListener(OpenTutorial);
            _tutorialCloseButton = tutorialCloseButton != null
                ? tutorialCloseButton
                : FindTutorialCloseButton(_tutorialPanel);
            if (_tutorialCloseButton != null) _tutorialCloseButton.onClick.AddListener(CloseTutorial);
            else Debug.LogError("STREAM ON title tutorial: Tutorial Panel 안의 Back 버튼을 찾지 못했습니다.", this);
            _tutorialPanel.SetActive(false);
        }

        public void PlayButtonSound()
        {
            if (_buttonSound == null || _effectsSource == null) return;
            RunnerUserSettingsData data = RunnerUserSettingsStore.Load();
            _effectsSource.volume = Mathf.Clamp01(data.masterVolume * data.sfxVolume);
            _effectsSource.PlayOneShot(_buttonSound, _buttonVolume);
        }

        public void OpenTutorial()
        {
            if (_tutorialPanel == null || _tutorialPanel.activeSelf) return;
            PlayButtonSound();
            _tutorialPanel.transform.SetAsLastSibling();
            _tutorialPanel.SetActive(true);
            _tutorialCloseButton?.Select();
        }

        public void CloseTutorial()
        {
            if (_tutorialPanel == null || !_tutorialPanel.activeSelf) return;
            PlayButtonSound();
            _tutorialPanel.SetActive(false);
            _tutorialButton?.Select();
        }

        private static Button FindTutorialCloseButton(GameObject tutorialPanel)
        {
            if (tutorialPanel == null) return null;
            Button[] buttons = tutorialPanel.GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                if (button == null) continue;
                string objectName = button.gameObject.name;
                if (objectName.IndexOf("Back", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || objectName.IndexOf("Close", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return button;
            }
            return null;
        }

        private void PrepareAudio(AudioClip backgroundMusic, float musicVolume)
        {
            _musicSource = gameObject.AddComponent<AudioSource>();
            _effectsSource = gameObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false; _musicSource.loop = true;
            _effectsSource.playOnAwake = false;
            RunnerUserSettingsData data = RunnerUserSettingsStore.Load();
            _musicSource.clip = backgroundMusic;
            _musicSource.volume = Mathf.Clamp01(data.masterVolume * data.bgmVolume * musicVolume);
            if (backgroundMusic != null) _musicSource.Play();
        }

    }
}
