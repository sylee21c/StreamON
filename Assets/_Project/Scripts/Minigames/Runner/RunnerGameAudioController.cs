using System.Collections;
using UnityEngine;

namespace StreamOn.Minigames.Runner
{
    public sealed class RunnerGameAudioController : MonoBehaviour
    {
        [Header("Audio Clips")]
        [SerializeField] private AudioClip backgroundMusic;
        [SerializeField] private AudioClip jumpSound;
        [SerializeField] private AudioClip landingSound;
        [SerializeField] private AudioClip rollSound;
        [SerializeField] private AudioClip attackSound;
        [SerializeField] private AudioClip enemyDefeatedSound;
        [SerializeField] private AudioClip playerHitSound;
        [SerializeField] private AudioClip gameOverSound;

        [Header("Individual Volumes")]
        [SerializeField, Range(0f, 1f)] private float backgroundMusicVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float jumpVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float landingVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float rollVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float attackVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float enemyDefeatedVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float playerHitVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float gameOverVolume = 1f;
        [SerializeField, Min(0f)] private float musicLoopGapSeconds = 3f;
        [SerializeField, Min(0.01f)] private float gameOverMusicFadeSeconds = 2f;
        [SerializeField, Min(0f)] private float pitchChangeSpeed = 4f;

        [Header("Optional Audio Sources")]
        [Tooltip("비워 두면 실행 시 이 오브젝트에 자동 생성됩니다.")]
        [SerializeField] private AudioSource backgroundMusicSource;
        [Tooltip("비워 두면 실행 시 이 오브젝트에 자동 생성됩니다.")]
        [SerializeField] private AudioSource effectsSource;

        private static RunnerGameAudioController _instance;
        private Coroutine _musicRoutine;
        private float _bgmLevel = 1f;
        private float _sfxLevel = 1f;
        private float _musicFadeMultiplier = 1f;
        private bool _musicStoppedForGameOver;

        private void Awake()
        {
            _instance = this;
            RunnerUserSettingsData settings = RunnerUserSettingsStore.Load();
            _bgmLevel = settings.bgmVolume;
            _sfxLevel = settings.sfxVolume;
            ConfigureSources();
        }

        private void OnEnable()
        {
            if (backgroundMusic != null && _musicRoutine == null)
                _musicRoutine = StartCoroutine(MusicLoop());
        }

        private void OnDisable()
        {
            if (_musicRoutine != null)
            {
                StopCoroutine(_musicRoutine);
                _musicRoutine = null;
            }
            if (backgroundMusicSource != null) backgroundMusicSource.Stop();
        }

        private void Update()
        {
            if (backgroundMusicSource == null || Time.timeScale <= 0f) return;
            float targetPitch = Mathf.Clamp(Time.timeScale, 0.01f, 1f);
            backgroundMusicSource.pitch = Mathf.MoveTowards(backgroundMusicSource.pitch, targetPitch,
                Mathf.Max(0.01f, pitchChangeSpeed) * Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private IEnumerator MusicLoop()
        {
            while (isActiveAndEnabled && backgroundMusic != null && !_musicStoppedForGameOver)
            {
                backgroundMusicSource.clip = backgroundMusic;
                ApplyMusicVolume();
                backgroundMusicSource.Play();
                while (backgroundMusicSource.isPlaying && !_musicStoppedForGameOver) yield return null;
                if (!isActiveAndEnabled || backgroundMusic == null || _musicStoppedForGameOver) break;
                yield return new WaitForSecondsRealtime(musicLoopGapSeconds);
            }
            _musicRoutine = null;
        }

        public void PlayJump() => Play(jumpSound, jumpVolume);
        public void PlayLanding() => Play(landingSound, landingVolume);
        public void PlayRoll() => Play(rollSound, rollVolume);
        public void PlayAttack() => Play(attackSound, attackVolume);
        public void PlayEnemyDefeated() => Play(enemyDefeatedSound, enemyDefeatedVolume);
        public void PlayPlayerHit() => Play(playerHitSound, playerHitVolume);
        public void PlayGameOver() => Play(gameOverSound, gameOverVolume);

        public void FadeOutMusicForGameOver()
        {
            if (_musicStoppedForGameOver) return;
            _musicStoppedForGameOver = true;
            StartCoroutine(FadeOutMusicRoutine());
        }

        public static void SetGlobalBgmVolume(float value)
        {
            if (_instance == null) return;
            _instance._bgmLevel = Mathf.Clamp01(value);
            _instance.ApplyMusicVolume();
        }

        public static void SetGlobalSfxVolume(float value)
        {
            if (_instance != null) _instance._sfxLevel = Mathf.Clamp01(value);
        }

        private void Play(AudioClip clip, float volume)
        {
            if (clip != null && effectsSource != null)
                effectsSource.PlayOneShot(clip, volume * _sfxLevel);
        }

        private IEnumerator FadeOutMusicRoutine()
        {
            float startMultiplier = _musicFadeMultiplier;
            float elapsed = 0f;
            float duration = gameOverMusicFadeSeconds > 0f ? gameOverMusicFadeSeconds : 2f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _musicFadeMultiplier = Mathf.Lerp(startMultiplier, 0f, Mathf.Clamp01(elapsed / duration));
                ApplyMusicVolume();
                yield return null;
            }
            _musicFadeMultiplier = 0f;
            ApplyMusicVolume();
            backgroundMusicSource?.Stop();
        }

        private void ApplyMusicVolume()
        {
            if (backgroundMusicSource != null)
                backgroundMusicSource.volume = backgroundMusicVolume * _bgmLevel * _musicFadeMultiplier;
        }

        private void ConfigureSources()
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            if (backgroundMusicSource == null)
                backgroundMusicSource = sources.Length > 0
                    ? sources[0]
                    : gameObject.AddComponent<AudioSource>();

            if (effectsSource == null)
            {
                foreach (AudioSource source in sources)
                {
                    if (source == null || source == backgroundMusicSource) continue;
                    effectsSource = source;
                    break;
                }
                if (effectsSource == null) effectsSource = gameObject.AddComponent<AudioSource>();
            }

            backgroundMusicSource.playOnAwake = false;
            backgroundMusicSource.loop = false;
            backgroundMusicSource.spatialBlend = 0f;
            effectsSource.playOnAwake = false;
            effectsSource.loop = false;
            effectsSource.spatialBlend = 0f;
        }
    }
}
