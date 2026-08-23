using TMPro;
using UnityEngine;
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
        public void ShowGameOver(bool visible)
        {
            // Campaign broadcasts use RunnerBroadcastSettlementView instead of the
            // legacy game-over panel. The scene is therefore valid without this ref.
            if (gameOverPanel != null) gameOverPanel.SetActive(visible);
        }

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

}
