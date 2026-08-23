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
        [Header("Scene-authored broadcast feedback")]
        public TMP_Text timeBonusText;
        [Min(0f)] public float timeBonusVisibleSeconds = 2f;

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

        public void ShowTimeBonus(float seconds)
        {
            if (timeBonusText == null) return;
            StopAllCoroutines();
            StartCoroutine(ShowTimeBonusRoutine(seconds));
        }

        private System.Collections.IEnumerator ShowTimeBonusRoutine(float seconds)
        {
            timeBonusText.gameObject.SetActive(true);
            timeBonusText.text = $"방송 호조! 방송 시간 +{seconds:0.#}초";
            yield return new WaitForSecondsRealtime(timeBonusVisibleSeconds);
            timeBonusText.gameObject.SetActive(false);
        }

        private void EnsureReferences() { }
    }

    public sealed class RunnerBroadcastHeatGauge : MonoBehaviour
    {
        private static RunnerBroadcastHeatGauge _instance;
        [Header("Scene-authored UI references")]
        public RunnerCampaignSettings settings;
        public RectTransform heatFill;
        public Image heatFillImage;
        public TMP_Text heatLabel;
        public RectTransform focusFill;
        public Image focusFillImage;
        public TMP_Text focusLabel;
        [Header("Inspector colours")]
        public Color coldColor = new Color(1f, 0.16f, 0.13f);
        public Color neutralColor = Color.white;
        public Color hotColor = new Color(0.16f, 0.92f, 0.30f);
        public Color emptyFocusColor = new Color(1f, 0.35f, 0.25f);
        public Color fullFocusColor = new Color(0.35f, 0.76f, 1f);
        public Color slowMotionLabelColor = new Color(1f, 0.88f, 0.36f);
        private float _displayedValue = 50f;
        private float _targetValue = 50f;
        private float _focus = 100f;
        private float _maximumFocus = 100f;
        private float _lastSlowMotionStoppedAt;
        private bool _depletionRecoveryUsed;
        private bool _slowMotionActive;

        private void Awake()
        {
            _instance = this;
            ConfigureFromSave();
        }

        private void ConfigureFromSave()
        {
            StaminaRankRule rule = null;
            if (settings != null && RunnerCampaignSaveStore.TryLoad(settings, out RunnerCampaignSaveData save))
                rule = settings.StaminaRule(save.staminaRank);
            int fitnessUpgrades = 0;
            if (settings != null && RunnerCampaignSaveStore.TryLoad(settings, out RunnerCampaignSaveData equipmentSave))
                fitnessUpgrades = Mathf.Max(0, equipmentSave.fitnessLevel - 1);
            _maximumFocus = (rule != null ? Mathf.Max(1f, rule.maximumFocus) : 100f)
                + fitnessUpgrades * (settings != null ? settings.focusCapacityPerFitnessUpgrade : 0f);
            _focus = _maximumFocus;
            _depletionRecoveryUsed = false;
        }

        public static void Show(float value)
        {
            if (_instance == null) return;
            _instance.gameObject.SetActive(true);
            _instance._targetValue = Mathf.Clamp(value, 0f, 100f);
            _instance.ConfigureFromSave();
            _instance.SetSlowMotion(false);
            _instance.RefreshVisuals(true);
        }

        public static void SetValue(float value)
        {
            if (_instance == null) return;
            _instance._targetValue = Mathf.Clamp(value, 0f, 100f);
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance.SetSlowMotion(false);
            _instance.gameObject.SetActive(false);
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
                StaminaRankRule stamina = CurrentStaminaRule();
                float drainReduction = stamina != null ? stamina.focusDrainReduction : 0f;
                _focus = Mathf.Max(0f, _focus - settings.focusDrainPerSecond * (1f - drainReduction) * Time.unscaledDeltaTime);
                if (_focus <= 0f)
                {
                    SetSlowMotion(false);
                    if (!_depletionRecoveryUsed && stamina != null && stamina.depletionRecoveryAmount > 0f)
                    {
                        _focus = Mathf.Min(_maximumFocus, stamina.depletionRecoveryAmount);
                        _depletionRecoveryUsed = true;
                    }
                }
                else if (!Mathf.Approximately(Time.timeScale, settings.slowMotionTimeScale)) Time.timeScale = settings.slowMotionTimeScale;
            }
            else if (!_slowMotionActive && Time.unscaledTime >= _lastSlowMotionStoppedAt + RecoveryDelay())
            {
                StaminaRankRule stamina = CurrentStaminaRule();
                float recoveryBonus = stamina != null ? stamina.focusRecoveryBonus : 0f;
                if (settings != null && RunnerCampaignSaveStore.TryLoad(settings, out RunnerCampaignSaveData equipmentSave))
                    recoveryBonus += Mathf.Max(0, equipmentSave.fitnessLevel - 1) * settings.focusRecoveryPerFitnessUpgrade;
                _focus = Mathf.Min(_maximumFocus, _focus + settings.focusRecoveryPerSecond * (1f + recoveryBonus) * Time.unscaledDeltaTime);
            }
            _displayedValue = Mathf.MoveTowards(_displayedValue, _targetValue, settings.heatGaugeVisualSpeed * Time.unscaledDeltaTime);
            RefreshVisuals(false);
        }

        private void SetSlowMotion(bool active)
        {
            _slowMotionActive = active && _focus > 0f;
            if (!active) _lastSlowMotionStoppedAt = Time.unscaledTime;
            if (Time.timeScale > 0f) Time.timeScale = _slowMotionActive ? settings.slowMotionTimeScale : 1f;
        }

        private void RefreshVisuals(bool immediate)
        {
            if (immediate) _displayedValue = _targetValue;
            float normalized = Mathf.Clamp01(_displayedValue / 100f);
            if (heatFill != null) heatFill.anchorMax = new Vector2(normalized, 1f);
            Color heatColor = normalized <= 0.5f ? Color.Lerp(coldColor, neutralColor, normalized * 2f) : Color.Lerp(neutralColor, hotColor, (normalized - 0.5f) * 2f);
            if (heatFillImage != null) heatFillImage.color = heatColor;
            if (heatLabel != null) { heatLabel.text = $"방송 열기  {Mathf.RoundToInt(_displayedValue)}%"; heatLabel.color = heatColor; }
            float focus01 = Mathf.Clamp01(_focus / _maximumFocus);
            if (focusFill != null) focusFill.anchorMax = new Vector2(focus01, 1f);
            Color focusColor = Color.Lerp(emptyFocusColor, fullFocusColor, focus01);
            if (focusFillImage != null) focusFillImage.color = focusColor;
            if (focusLabel == null) return;
            focusLabel.text = _slowMotionActive
                ? $"집중력  {Mathf.CeilToInt(_focus)} / {Mathf.CeilToInt(_maximumFocus)}  ·  TAB 슬로우 ON"
                : $"집중력  {Mathf.CeilToInt(_focus)} / {Mathf.CeilToInt(_maximumFocus)}  ·  TAB";
            focusLabel.color = _slowMotionActive ? slowMotionLabelColor : focusColor;
        }

        private StaminaRankRule CurrentStaminaRule()
        {
            if (settings == null || !RunnerCampaignSaveStore.TryLoad(settings, out RunnerCampaignSaveData save)) return null;
            return settings.StaminaRule(save.staminaRank);
        }

        private float RecoveryDelay()
        {
            StaminaRankRule stamina = CurrentStaminaRule();
            return Mathf.Max(0f, settings.focusRecoveryDelaySeconds - (stamina != null ? stamina.focusRecoveryDelayReduction : 0f));
        }

        private void OnDisable() => SetSlowMotion(false);
        private void OnDestroy() => SetSlowMotion(false);

    }
}
