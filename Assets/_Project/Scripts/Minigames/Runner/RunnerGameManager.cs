using System;
using UnityEngine;

namespace StreamOn.Minigames.Runner
{
    public enum RunnerGameState { Ready, Playing, GameOver }

    public sealed class RunnerGameManager : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private RunnerPlayerController player;
        [SerializeField] private RunnerObstacleSpawner spawner;
        [SerializeField] private RunnerGroundLooper groundLooper;
        [SerializeField] private RunnerChatController chat;
        [SerializeField] private RunnerHUD hud;

        [Header("Run Balance")]
        [SerializeField] private float startingSpeed = 5.5f;
        [SerializeField] private float maximumSpeed = 12f;
        [SerializeField] private float speedGainPerSecond = 0.08f;
        [SerializeField] private float scorePerSecond = 10f;

        public RunnerGameState State { get; private set; } = RunnerGameState.Ready;
        public float WorldSpeed { get; private set; }
        public int Score { get; private set; }
        public int HighScore { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public int EnemiesDefeated { get; private set; }
        public int HitsTaken { get; private set; }
        public event Action<float> SpeedChanged;

        private float _rawScore;

        private void Start()
        {
            HighScore = PlayerPrefs.GetInt("Runner.HighScore", 0);
            BeginRun();
        }

        private void Update()
        {
            if (State != RunnerGameState.Playing) return;
            WorldSpeed = Mathf.Min(maximumSpeed, WorldSpeed + speedGainPerSecond * Time.deltaTime);
            ElapsedSeconds += Time.deltaTime;
            _rawScore += scorePerSecond * Time.deltaTime * (WorldSpeed / startingSpeed);
            Score = Mathf.FloorToInt(_rawScore);
            hud.SetScore(Score, HighScore, WorldSpeed);
            SpeedChanged?.Invoke(WorldSpeed);
        }

        public void BeginRun()
        {
            State = RunnerGameState.Playing;
            WorldSpeed = startingSpeed;
            _rawScore = 0f;
            Score = 0;
            ElapsedSeconds = 0f;
            EnemiesDefeated = 0;
            HitsTaken = 0;
            groundLooper?.ResetTiles();
            spawner.ResetRun();
            player.ResetPlayer();
            hud.ShowGameOver(false);
            hud.SetScore(0, HighScore, WorldSpeed);
            hud.SetHealth(player.CurrentHealth, player.MaxHealth);
            chat.ResetChat();
            chat.React(RunnerChatEvent.RunStarted);
        }

        public void OnObstacleCleared(RunnerObstacleType obstacleType)
        {
            if (State != RunnerGameState.Playing) return;
            _rawScore += 25f;
            chat.React(obstacleType == RunnerObstacleType.Roll
                ? RunnerChatEvent.PlayerRolled
                : RunnerChatEvent.ObstacleCleared);
        }

        public void OnPlayerHit()
        {
            if (State != RunnerGameState.Playing) return;
            HitsTaken++;
            chat.React(player.CurrentHealth <= 1 ? RunnerChatEvent.LowHealth : RunnerChatEvent.PlayerHit);
            hud.SetHealth(player.CurrentHealth, player.MaxHealth);
            if (player.CurrentHealth <= 0) EndRun();
        }

        public void OnPlayerJumped() => chat.React(RunnerChatEvent.PlayerJumped);

        public void OnAttackMissed() => chat.React(RunnerChatEvent.AttackMissed);

        public void OnEnemyDefeated()
        {
            if (State != RunnerGameState.Playing) return;
            EnemiesDefeated++;
            _rawScore += 75f;
            chat.React(RunnerChatEvent.EnemyDefeated);
        }

        public void OnEnemyEscaped() => player.ReceiveHit();

        public void EndRun()
        {
            if (State == RunnerGameState.GameOver) return;
            State = RunnerGameState.GameOver;
            bool isNewHighScore = Score > HighScore;
            if (isNewHighScore)
            {
                HighScore = Score;
                PlayerPrefs.SetInt("Runner.HighScore", HighScore);
                PlayerPrefs.Save();
            }
            chat.BeginGameOverChat(isNewHighScore);
            hud.SetScore(Score, HighScore, WorldSpeed);
            hud.ShowGameOver(true);
        }

        public void RestartRun()
        {
            if (State == RunnerGameState.GameOver) BeginRun();
        }

        public RunnerChatSnapshot CreateChatSnapshot(string events)
        {
            return new RunnerChatSnapshot
            {
                gameState = State.ToString(),
                events = events,
                score = Score,
                highScore = HighScore,
                speed = WorldSpeed,
                health = player != null ? player.CurrentHealth : 0,
                maxHealth = player != null ? player.MaxHealth : 0,
                enemiesDefeated = EnemiesDefeated,
                hitsTaken = HitsTaken,
                elapsedSeconds = ElapsedSeconds
            };
        }
    }
}
