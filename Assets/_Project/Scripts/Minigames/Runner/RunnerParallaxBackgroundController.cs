using System;
using System.Collections;
using UnityEngine;

namespace StreamOn.Minigames.Runner
{
    public sealed class RunnerParallaxBackgroundController : MonoBehaviour
    {
        [Serializable]
        private sealed class BackgroundTheme
        {
            public string displayName;
            public GameObject root;
            public RunnerParallaxLayer[] layers;
        }

        [SerializeField] private RunnerGameManager gameManager;
        [SerializeField] private BackgroundTheme[] themes;
        [SerializeField, Min(0.05f)] private float crossFadeSeconds = 0.28f;

        private int _currentTheme = -1;
        private int _requestedTheme;
        private Coroutine _transition;

        private void Awake()
        {
            int initial = ThemeIndexForScore(gameManager != null ? gameManager.Score : 0);
            ActivateInitial(initial);
        }

        private void Update()
        {
            if (gameManager == null || themes == null || themes.Length == 0) return;
            int wanted = ThemeIndexForScore(gameManager.Score);
            if (wanted == _currentTheme && _transition == null) return;
            if (_transition != null) { _requestedTheme = wanted; return; }
            _transition = StartCoroutine(CrossFadeTo(wanted));
        }

        private int ThemeIndexForScore(int score)
        {
            int segment = Mathf.Max(0, score) / 1000;
            if (segment < 8) return Mathf.Clamp(segment, 0, Mathf.Max(0, themes.Length - 1));
            return Mathf.Clamp(segment % 2 == 0 ? 6 : 7, 0, Mathf.Max(0, themes.Length - 1));
        }

        private void ActivateInitial(int index)
        {
            if (themes == null || themes.Length == 0) return;
            index = Mathf.Clamp(index, 0, themes.Length - 1);
            for (int i = 0; i < themes.Length; i++)
            {
                bool active = i == index;
                if (themes[i]?.root != null) themes[i].root.SetActive(active);
                SetThemeAlpha(i, active ? 1f : 0f);
            }
            _currentTheme = index;
        }

        private IEnumerator CrossFadeTo(int next)
        {
            next = Mathf.Clamp(next, 0, themes.Length - 1);
            _requestedTheme = next;
            int previous = _currentTheme;
            if (themes[next]?.root != null) themes[next].root.SetActive(true);
            SetThemeAlpha(next, 0f);

            float elapsed = 0f;
            while (elapsed < crossFadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, crossFadeSeconds));
                t = t * t * (3f - 2f * t);
                SetThemeAlpha(previous, 1f - t);
                SetThemeAlpha(next, t);
                yield return null;
            }

            SetThemeAlpha(previous, 0f);
            SetThemeAlpha(next, 1f);
            if (previous >= 0 && previous < themes.Length && themes[previous]?.root != null)
                themes[previous].root.SetActive(false);
            _currentTheme = next;
            _transition = null;

            int latest = ThemeIndexForScore(gameManager != null ? gameManager.Score : 0);
            if (latest != _currentTheme) _transition = StartCoroutine(CrossFadeTo(latest));
        }

        private void SetThemeAlpha(int index, float alpha)
        {
            if (themes == null || index < 0 || index >= themes.Length || themes[index]?.layers == null) return;
            foreach (RunnerParallaxLayer layer in themes[index].layers) layer?.SetAlpha(alpha);
        }

        private void OnValidate() => crossFadeSeconds = Mathf.Max(0.05f, crossFadeSeconds);
    }
}
