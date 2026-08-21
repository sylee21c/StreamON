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
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private GameObject gameOverPanel;

        public void SetScore(int score, int highScore, float speed, float secondsRemaining = -1f)
        {
            EnsureReferences();
            scoreText.text = $"SCORE  {score:000000}";
            highScoreText.text = $"BEST  {highScore:000000}";
            if (secondsRemaining >= 0f)
            {
                int totalSeconds = Mathf.CeilToInt(secondsRemaining);
                speedText.fontSize = 18f;
                speedText.text = $"SPD {speed:0.0}  {totalSeconds / 60:00}:{totalSeconds % 60:00}";
            }
            else speedText.text = $"SPEED  {speed:0.0}";
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

        private void EnsureReferences()
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in texts)
            {
                if (text.name == "Score") scoreText = text;
                else if (text.name == "Best") highScoreText = text;
                else if (text.name == "Speed") speedText = text;
                else if (text.name == "Health") healthText = text;
            }
        }
    }
}
