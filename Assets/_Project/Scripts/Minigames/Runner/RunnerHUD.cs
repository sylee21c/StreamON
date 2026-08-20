using TMPro;
using UnityEngine;

namespace StreamOn.Minigames.Runner
{
    public sealed class RunnerHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text highScoreText;
        [SerializeField] private TMP_Text speedText;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private GameObject gameOverPanel;

        public void SetScore(int score, int highScore, float speed)
        {
            EnsureReferences();
            scoreText.text = $"SCORE  {score:000000}";
            highScoreText.text = $"BEST  {highScore:000000}";
            speedText.text = $"SPEED  {speed:0.0}";
        }

        public void SetHealth(int current, int maximum)
        {
            EnsureReferences();
            healthText.text = $"HP  {new string('♥', current)}{new string('·', maximum - current)}";
        }
        public void ShowGameOver(bool visible) => gameOverPanel.SetActive(visible);

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
