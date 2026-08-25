using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StreamOn.Minigames.Runner
{
    public sealed class RunnerRoomAudioController : MonoBehaviour
    {
        [Header("Inspector Audio Clips")]
        [SerializeField] private AudioClip backgroundMusic;
        [SerializeField] private AudioClip uiButtonClick;
        [Tooltip("Streamer name input sound. Leave empty in scenes that do not use text input.")]
        [SerializeField] private AudioClip uiTextInput;
        [SerializeField] private AudioClip accessOpen;
        [SerializeField] private AudioClip accessClose;
        [SerializeField] private AudioClip playerFootsteps;
        [Header("Inspector Clip Volumes")]
        [SerializeField, Range(0f, 1f)] private float backgroundMusicVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float uiButtonVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float uiTextInputVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float accessOpenVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float accessCloseVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.7f;
        [SerializeField, Min(0f)] private float musicLoopGapSeconds = 3f;
        [Header("Scene-authored Sources")]
        [SerializeField] private AudioSource backgroundMusicSource;
        [SerializeField] private AudioSource effectsSource;
        [SerializeField] private AudioSource footstepSource;
        [SerializeField] private RunnerRoomPlayerController player;

        private static RunnerRoomAudioController _instance;
        private float _bgmLevel;
        private float _sfxLevel;

        private void Awake()
        {
            _instance = this;
            RunnerUserSettingsData data = RunnerUserSettingsStore.Load();
            _bgmLevel = data.bgmVolume;
            _sfxLevel = data.sfxVolume;
            if (player == null) player = FindFirstObjectByType<RunnerRoomPlayerController>();
            ConfigureSources();
            foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                button.onClick.AddListener(PlayUiButton);
            foreach (TMP_InputField input in FindObjectsByType<TMP_InputField>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                input.onValueChanged.AddListener(PlayTextInput);
        }

        private void Start()
        {
            if (backgroundMusic != null) StartCoroutine(MusicLoop());
        }

        private void Update()
        {
            bool walking = player != null && player.IsMoving && Time.timeScale > 0f;
            if (walking && playerFootsteps != null)
            {
                if (footstepSource.clip != playerFootsteps) footstepSource.clip = playerFootsteps;
                footstepSource.volume = footstepVolume * _sfxLevel;
                if (!footstepSource.isPlaying) footstepSource.Play();
            }
            else if (footstepSource != null && footstepSource.isPlaying) footstepSource.Stop();
        }

        private IEnumerator MusicLoop()
        {
            while (backgroundMusic != null)
            {
                backgroundMusicSource.clip = backgroundMusic;
                backgroundMusicSource.volume = backgroundMusicVolume * _bgmLevel;
                backgroundMusicSource.Play();
                yield return new WaitForSecondsRealtime(backgroundMusic.length);
                backgroundMusicSource.Stop();
                yield return new WaitForSecondsRealtime(musicLoopGapSeconds);
            }
        }

        public void PlayAccessOpen() => Play(accessOpen, accessOpenVolume);
        public void PlayAccessClose() => Play(accessClose, accessCloseVolume);
        private void PlayUiButton() => Play(uiButtonClick, uiButtonVolume);
        private void PlayTextInput(string _) => Play(uiTextInput, uiTextInputVolume);
        private void Play(AudioClip clip, float volume) { if (clip != null) effectsSource.PlayOneShot(clip, volume * _sfxLevel); }

        public static void SetGlobalBgmVolume(float value)
        {
            RunnerUserSettingsData data = RunnerUserSettingsStore.Load();
            data.bgmVolume = Mathf.Clamp01(value); RunnerUserSettingsStore.Save(data);
            if (_instance != null) { _instance._bgmLevel = data.bgmVolume; _instance.backgroundMusicSource.volume = _instance.backgroundMusicVolume * data.bgmVolume; }
        }

        public static void SetGlobalSfxVolume(float value)
        {
            RunnerUserSettingsData data = RunnerUserSettingsStore.Load();
            data.sfxVolume = Mathf.Clamp01(value); RunnerUserSettingsStore.Save(data);
            if (_instance != null) _instance._sfxLevel = data.sfxVolume;
        }

        private void ConfigureSources()
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            backgroundMusicSource ??= sources.ElementAtOrDefault(0) ?? gameObject.AddComponent<AudioSource>();
            effectsSource ??= sources.ElementAtOrDefault(1) ?? gameObject.AddComponent<AudioSource>();
            footstepSource ??= sources.ElementAtOrDefault(2) ?? gameObject.AddComponent<AudioSource>();
            backgroundMusicSource.playOnAwake = false; backgroundMusicSource.loop = false; backgroundMusicSource.spatialBlend = 0f;
            effectsSource.playOnAwake = false; effectsSource.loop = false; effectsSource.spatialBlend = 0f;
            footstepSource.playOnAwake = false; footstepSource.loop = true; footstepSource.spatialBlend = 0f;
        }
    }
}
