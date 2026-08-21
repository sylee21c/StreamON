using StreamOn.Minigames.Runner;
using UnityEngine;

namespace StreamOn.Minigames.TileArena
{
    public sealed class TileArenaChatAdapter : MonoBehaviour
    {
        [Header("Shared Chat References")]
        [SerializeField] private TileArenaController gameController;
        [SerializeField] private RunnerChatController chatController;
        [SerializeField] private RunnerBroadcastGrowthSettings growthSettings;
        [SerializeField] private RunnerCampaignSettings campaignSettings;
        [SerializeField] private RunnerDonationPopupController donationPopup;

        [Header("Editable Tile Arena Audience")]
        [SerializeField, Min(0)] private int startingViewers = 3;
        [SerializeField, Min(0)] private int maximumViewers = 500;
        [SerializeField, Min(0f)] private float viewersPerScore = 0.18f;
        [SerializeField, Min(0f)] private float viewersPerHypePoint = 0.10f;
        [SerializeField, Min(0.5f)] private float viewerUpdateInterval = 5f;
        [SerializeField, Range(0.01f, 1f)] private float viewerAdjustmentRate = 0.35f;
        [SerializeField, Range(0f, 0.5f)] private float randomVariation = 0.12f;

        [Header("Editable Hype Reactions")]
        [SerializeField, Range(0f, 100f)] private float startingHype = 30f;
        [SerializeField, Range(0f, 100f)] private float restingHype = 28f;
        [SerializeField, Min(0f)] private float hypeReturnPerSecond = 0.08f;
        [SerializeField] private float pickupHype = 0.7f;
        [SerializeField] private float stageClearHype = 6f;
        [SerializeField] private float playerHitHype = -7f;
        [SerializeField] private float lowLivesDramaHype = 3f;

        private int _currentViewers;
        private int _chattingViewers;
        private int _peakViewers;
        private float _hype;
        private float _nextViewerUpdate;
        private bool _initialized;
        private float _nextDonationAt;
        private int _liveDonationWon;
        private int _talkingSkill = 1;
        private RunnerWitInteractionController _witInteraction;

        public int CurrentViewers => _currentViewers;
        public int PeakViewers => _peakViewers;
        public int TalkingSkill => _talkingSkill;
        public bool CanShowWitInteraction => _initialized && gameController != null && gameController.IsRunning;

        private void Start() => InitializeIfNeeded();

        private void Update()
        {
            InitializeIfNeeded();
            if (!_initialized || gameController == null) return;
            if (gameController.IsRunning)
                _hype = Mathf.MoveTowards(_hype, restingHype, hypeReturnPerSecond * Time.unscaledDeltaTime);
            if (Time.unscaledTime >= _nextViewerUpdate)
            {
                _nextViewerUpdate = Time.unscaledTime + viewerUpdateInterval;
                UpdateAudience();
            }
            PushSnapshot();
        }

        public void OnGameStarted()
        {
            InitializeIfNeeded();
            if (!_initialized) return;
            chatController.ResumeExternalGame();
            RefreshChatScale();
            PushSnapshot(SharedChatGameState.Playing);
            chatController.React(RunnerChatEvent.TileArenaStarted);
        }

        public void OnJumped() => React(RunnerChatEvent.TileArenaJumped, 0f);
        public void OnBluePickedUp(int count)
        {
            React(RunnerChatEvent.TileArenaPickup, pickupHype * Mathf.Max(1, count));
            if (count >= 2) TryLiveDonation(growthSettings != null ? growthSettings.tilePickupDonationChance : 0f,
                "한 번에 여러 개 먹는 거 시원하다");
        }

        public void OnStageCleared()
        {
            React(RunnerChatEvent.TileArenaStageCleared, stageClearHype);
            _witInteraction?.NotifySafeMoment("타일 아레나 스테이지를 방금 클리어함", 4f);
            TryLiveDonation(growthSettings != null ? growthSettings.tileStageClearDonationChance : 0f,
                "파란 타일 올클리어!");
        }

        public void OnPlayerHit(bool lowLives)
        {
            React(RunnerChatEvent.TileArenaPlayerHit, playerHitHype + (lowLives ? lowLivesDramaHype : 0f));
            if (lowLives) chatController?.React(RunnerChatEvent.TileArenaLowLives);
        }

        public void OnGameOver(bool isNewHighScore)
        {
            InitializeIfNeeded();
            if (!_initialized) return;
            PushSnapshot(SharedChatGameState.GameOver);
            chatController.React(RunnerChatEvent.TileArenaGameOver);
            _witInteraction?.NotifySafeMoment("타일 아레나 게임오버 직후 방금 판을 되짚는 중", 8f);
            chatController.BeginExternalGameOverChat(isNewHighScore);
        }

        private void InitializeIfNeeded()
        {
            if (_initialized) return;
            if (gameController == null) gameController = FindFirstObjectByType<TileArenaController>();
            if (chatController == null) chatController = FindFirstObjectByType<RunnerChatController>();
            if (gameController == null || chatController == null) return;
            if (donationPopup == null) donationPopup = FindFirstObjectByType<RunnerDonationPopupController>();
            if (_witInteraction == null) _witInteraction = FindFirstObjectByType<RunnerWitInteractionController>();
            if (campaignSettings != null && RunnerCampaignSaveStore.TryLoad(campaignSettings, out RunnerCampaignSaveData save))
                _talkingSkill = Mathf.Max(1, save.talkingSkill);
            _currentViewers = Mathf.Clamp(startingViewers, 0, maximumViewers);
            _peakViewers = _currentViewers;
            _hype = startingHype;
            _liveDonationWon = 0;
            _nextDonationAt = 0f;
            _nextViewerUpdate = Time.unscaledTime + viewerUpdateInterval;
            chatController.BindExternalGame("타일 아레나");
            RefreshChatScale();
            PushSnapshot(SharedChatGameState.Ready);
            _initialized = true;
        }

        private void React(RunnerChatEvent chatEvent, float hypeDelta)
        {
            InitializeIfNeeded();
            if (!_initialized) return;
            _hype = Mathf.Clamp(_hype + hypeDelta, 0f, 100f);
            PushSnapshot();
            chatController.React(chatEvent);
        }

        public void ApplyWitInteraction(int quality)
        {
            InitializeIfNeeded();
            if (!_initialized || growthSettings == null) return;
            if (quality >= 2)
            {
                _hype = Mathf.Clamp(_hype + growthSettings.witSuccessHype * (quality >= 3 ? 1.35f : 1f), 0f, 100f);
                TryLiveDonation(growthSettings.witSuccessDonationChance
                    * (1f + Mathf.Max(0, _talkingSkill - 1) * growthSettings.donationChancePerTalkingLevel),
                    "이런 받아치기 좋다 ㅋㅋ");
                chatController.React(RunnerChatEvent.WitReplySuccess);
            }
            else if (quality == 1)
            {
                _hype = Mathf.Clamp(_hype + growthSettings.witOkayHype, 0f, 100f);
                chatController.React(RunnerChatEvent.WitReplyOkay);
            }
            else
            {
                _hype = Mathf.Clamp(_hype + growthSettings.witAwkwardHype, 0f, 100f);
                chatController.React(RunnerChatEvent.WitReplyAwkward);
            }
            UpdateAudience();
            PushSnapshot();
        }

        private void UpdateAudience()
        {
            float target = startingViewers + gameController.Score * viewersPerScore + _hype * viewersPerHypePoint;
            target *= Random.Range(1f - randomVariation, 1f + randomVariation);
            int next = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(_currentViewers, target, viewerAdjustmentRate)), 0, maximumViewers);
            if (next == _currentViewers && _currentViewers > 0 && _currentViewers < maximumViewers && Random.value < 0.45f)
                next += Random.value < 0.5f ? -1 : 1;
            _currentViewers = Mathf.Clamp(next, 0, maximumViewers);
            _peakViewers = Mathf.Max(_peakViewers, _currentViewers);
            RefreshChatScale();
        }

        private void RefreshChatScale()
        {
            _chattingViewers = growthSettings != null ? growthSettings.ChattersForViewers(_currentViewers)
                : (_currentViewers <= 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(1.2f + Mathf.Sqrt(_currentViewers)), 1, Mathf.Min(_currentViewers, 40)));
            chatController.ConfigureAudience(_currentViewers, _chattingViewers,
                growthSettings != null ? growthSettings.ChatDelayMultiplierForViewers(_currentViewers) : 1f,
                growthSettings != null ? growthSettings.EventReactionChanceForViewers(_currentViewers) : 0.55f,
                growthSettings != null ? growthSettings.EventCooldownForViewers(_currentViewers) : 2f);
        }

        private void TryLiveDonation(float chance, string message)
        {
            if (growthSettings == null || _currentViewers < growthSettings.minimumViewersForDonation
                || Time.unscaledTime < _nextDonationAt || Random.value > chance) return;
            _nextDonationAt = Time.unscaledTime + growthSettings.liveDonationCooldown;
            int amount = RunnerBroadcastAudienceController.RollDonationAmount(growthSettings);
            string donor = chatController.PickDonationViewerNickname();
            _liveDonationWon += amount;
            donationPopup?.ShowDonation(donor, amount, message);
            chatController.OnDonationReceived(donor, amount);
        }

        private void PushSnapshot(SharedChatGameState? forcedState = null)
        {
            if (chatController == null || gameController == null) return;
            SharedChatGameState state = forcedState ?? (gameController.IsRunning
                ? SharedChatGameState.Playing
                : gameController.Lives <= 0 ? SharedChatGameState.GameOver : SharedChatGameState.Ready);
            chatController.UpdateExternalGame(state, new RunnerChatSnapshot
            {
                gameTitle = "타일 아레나",
                score = gameController.Score,
                highScore = gameController.BestScore,
                health = gameController.Lives,
                maxHealth = gameController.MaximumLives,
                blueTilesRemaining = gameController.BlueTilesRemaining,
                elapsedSeconds = gameController.ElapsedSeconds,
                currentViewers = _currentViewers,
                chattingViewers = _chattingViewers,
                peakViewers = _peakViewers,
                broadcastHype = _hype,
                donationWon = _liveDonationWon
            });
        }
    }
}
