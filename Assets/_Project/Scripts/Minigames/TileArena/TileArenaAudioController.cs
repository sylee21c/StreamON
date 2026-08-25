using System.Collections;
using UnityEngine;

namespace StreamOn.Minigames.TileArena
{
    public sealed class TileArenaAudioController : MonoBehaviour
    {
        [Header("Scene Audio Sources")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource effectsSource;

        [Header("Optional replacement clips")]
        [Tooltip("비워두면 기본 칩튠 BGM을 생성합니다.")]
        [SerializeField] private AudioClip customBackgroundMusic;
        [SerializeField] private AudioClip customJump;
        [SerializeField] private AudioClip customPickup;
        [SerializeField] private AudioClip customHit;
        [SerializeField] private AudioClip customStageClear;
        [SerializeField] private AudioClip customGameOver;

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float defaultMusicVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float defaultEffectsVolume = 1f;
        [SerializeField, Min(0.01f)] private float gameOverMusicFadeSeconds = 2f;
        [SerializeField, Min(0f)] private float pitchChangeSpeed = 4f;

        private AudioClip _jump;
        private AudioClip _pickup;
        private AudioClip _hit;
        private AudioClip _stageClear;
        private AudioClip _gameOver;
        private bool _muted;
        private bool _musicWanted;
        private float _musicLevel;
        private float _effectsLevel;
        private Coroutine _musicFadeRoutine;
        private float _musicFadeMultiplier = 1f;

        public bool Muted => _muted;
        public float MusicLevel => _musicLevel;
        public float EffectsLevel => _effectsLevel;

        private void Awake()
        {
            if (musicSource == null || effectsSource == null)
            {
                AudioSource[] sources = GetComponentsInChildren<AudioSource>(true);
                if (sources.Length > 0) musicSource = sources[0];
                if (sources.Length > 1) effectsSource = sources[1];
            }
            if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
            if (effectsSource == null) effectsSource = gameObject.AddComponent<AudioSource>();
            _muted = PlayerPrefs.GetInt("tileArenaMuted", 0) != 0;
            _musicLevel = Mathf.Clamp01(PlayerPrefs.GetFloat("tileArenaBgmVolume", defaultMusicVolume));
            _effectsLevel = Mathf.Clamp01(PlayerPrefs.GetFloat("tileArenaSfxVolume", defaultEffectsVolume));
            _jump = customJump != null ? customJump : Sweep("Tile Arena Jump", 0.16f, 330f, 660f, 0.35f, false);
            _pickup = customPickup != null ? customPickup : Sweep("Tile Arena Pickup", 0.10f, 820f, 1320f, 0.30f, false);
            _hit = customHit != null ? customHit : Sweep("Tile Arena Hit", 0.24f, 180f, 65f, 0.48f, true);
            _stageClear = customStageClear != null ? customStageClear : Arpeggio("Tile Arena Stage Clear", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.095f, 0.32f);
            _gameOver = customGameOver != null ? customGameOver : Arpeggio("Tile Arena Game Over", new[] { 392f, 329.63f, 261.63f, 196f }, 0.16f, 0.38f);

            if (musicSource != null)
            {
                musicSource.playOnAwake = false;
                musicSource.loop = true;
                musicSource.volume = _muted ? 0f : _musicLevel;
                musicSource.clip = customBackgroundMusic != null ? customBackgroundMusic : BuildBackgroundMusic();
            }
            if (effectsSource != null)
            {
                effectsSource.playOnAwake = false;
                effectsSource.loop = false;
                effectsSource.volume = 1f;
            }
        }

        private void Update()
        {
            if (musicSource == null || Time.timeScale <= 0f) return;
            float targetPitch = Mathf.Clamp(Time.timeScale, 0.01f, 1f);
            musicSource.pitch = Mathf.MoveTowards(musicSource.pitch, targetPitch,
                Mathf.Max(0.01f, pitchChangeSpeed) * Time.unscaledDeltaTime);
        }

        public void PlayJump() => Play(_jump, 0.55f);
        public void PlayPickup() => Play(_pickup, 0.65f);
        public void PlayHit() => Play(_hit, 1f);
        public void PlayStageClear() => Play(_stageClear, 0.75f);
        public void PlayGameOver() => Play(_gameOver, 0.8f);

        public void StartMusic()
        {
            if (_musicFadeRoutine != null)
            {
                StopCoroutine(_musicFadeRoutine);
                _musicFadeRoutine = null;
            }
            _musicFadeMultiplier = 1f;
            _musicWanted = true;
            ApplyMusicVolume();
            if (!_muted && musicSource != null && musicSource.clip != null && !musicSource.isPlaying) musicSource.Play();
        }

        public void StopMusic()
        {
            _musicWanted = false;
            if (musicSource != null) musicSource.Pause();
        }

        public void FadeOutMusicForGameOver()
        {
            _musicWanted = false;
            if (_musicFadeRoutine != null) StopCoroutine(_musicFadeRoutine);
            _musicFadeRoutine = StartCoroutine(FadeOutMusicRoutine());
        }

        public void ToggleMute()
        {
            _muted = !_muted;
            PlayerPrefs.SetInt("tileArenaMuted", _muted ? 1 : 0);
            if (musicSource != null)
            {
                ApplyMusicVolume();
                if (_muted) musicSource.Pause();
                else if (_musicWanted) StartMusic();
            }
            PlayerPrefs.Save();
        }

        public void SetMusicVolume(float value)
        {
            _musicLevel = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat("tileArenaBgmVolume", _musicLevel);
            ApplyMusicVolume();
            PlayerPrefs.Save();
        }

        public void SetEffectsVolume(float value)
        {
            _effectsLevel = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat("tileArenaSfxVolume", _effectsLevel);
            PlayerPrefs.Save();
        }

        private void Play(AudioClip clip, float clipVolume)
        {
            if (!_muted && effectsSource != null && clip != null) effectsSource.PlayOneShot(clip, clipVolume * _effectsLevel);
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
            musicSource?.Pause();
            _musicFadeRoutine = null;
        }

        private void ApplyMusicVolume()
        {
            if (musicSource != null)
                musicSource.volume = _muted ? 0f : _musicLevel * _musicFadeMultiplier;
        }

        private static AudioClip BuildBackgroundMusic()
        {
            const int sampleRate = 22050;
            const float beat = 0.25f;
            float[] melody = { 523.25f, 659.25f, 783.99f, 659.25f, 587.33f, 698.46f, 880f, 698.46f, 523.25f, 659.25f, 783.99f, 1046.5f, 880f, 783.99f, 659.25f, 587.33f };
            float[] bass = { 130.81f, 130.81f, 146.83f, 146.83f, 174.61f, 174.61f, 146.83f, 146.83f };
            int samples = Mathf.RoundToInt(melody.Length * beat * sampleRate);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float time = i / (float)sampleRate;
                int melodyIndex = Mathf.Min(melody.Length - 1, Mathf.FloorToInt(time / beat));
                int bassIndex = Mathf.Min(bass.Length - 1, Mathf.FloorToInt(time / (beat * 2f)));
                float withinBeat = time % beat;
                float envelope = Mathf.Clamp01(withinBeat / 0.012f) * Mathf.Clamp01((beat - withinBeat) / 0.045f);
                float square = Mathf.Sin(time * melody[melodyIndex] * Mathf.PI * 2f) >= 0f ? 1f : -1f;
                float bassWave = Mathf.Sin(time * bass[bassIndex] * Mathf.PI * 2f);
                float pulse = Mathf.Sin(time * 2f * Mathf.PI / beat) > 0.92f ? 0.10f : 0f;
                data[i] = (square * 0.13f * envelope + bassWave * 0.10f + pulse) * 0.72f;
            }
            AudioClip clip = AudioClip.Create("Tile Arena Arcade Loop", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Sweep(string name, float duration, float startFrequency, float endFrequency, float volume, bool noise)
        {
            const int sampleRate = 22050;
            int samples = Mathf.RoundToInt(duration * sampleRate);
            float[] data = new float[samples];
            uint seed = 2463534242u;
            for (int i = 0; i < samples; i++)
            {
                float progress = i / (float)Mathf.Max(1, samples - 1);
                float frequency = Mathf.Lerp(startFrequency, endFrequency, progress);
                float envelope = Mathf.Sin(progress * Mathf.PI);
                float wave = Mathf.Sin(i / (float)sampleRate * frequency * Mathf.PI * 2f);
                if (noise)
                {
                    seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
                    wave = wave * 0.45f + ((seed & 65535) / 32767.5f - 1f) * 0.55f;
                }
                data[i] = wave * envelope * volume;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Arpeggio(string name, float[] notes, float noteDuration, float volume)
        {
            const int sampleRate = 22050;
            int noteSamples = Mathf.RoundToInt(noteDuration * sampleRate);
            float[] data = new float[noteSamples * notes.Length];
            for (int note = 0; note < notes.Length; note++)
            for (int i = 0; i < noteSamples; i++)
            {
                float progress = i / (float)Mathf.Max(1, noteSamples - 1);
                float square = Mathf.Sin(i / (float)sampleRate * notes[note] * Mathf.PI * 2f) >= 0f ? 1f : -1f;
                data[note * noteSamples + i] = square * Mathf.Sin(progress * Mathf.PI) * volume;
            }
            AudioClip clip = AudioClip.Create(name, data.Length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
