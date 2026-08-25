using UnityEngine;

namespace StreamOn.Minigames.Runner
{
    /// <summary>
    /// Stable entry point for the UI and controllers shared by every broadcast minigame.
    /// Game-specific controllers reference these children instead of owning copied UI objects.
    /// </summary>
    public sealed class SharedBroadcastSystemRoot : MonoBehaviour
    {
        [Header("Shared broadcast components")]
        [SerializeField] private RunnerChatController chat;
        [SerializeField] private RunnerDonationPopupController donationPopup;
        [SerializeField] private BroadcastMissionEventController missionEvent;
        [SerializeField] private RunnerWitInteractionController witInteraction;
        [SerializeField] private RunnerBroadcastHeatGauge heatAndFocusGauge;
        [SerializeField] private RunnerBroadcastSettlementView settlementView;

        public RunnerChatController Chat => chat;
        public RunnerDonationPopupController DonationPopup => donationPopup;
        public BroadcastMissionEventController MissionEvent => missionEvent;
        public RunnerWitInteractionController WitInteraction => witInteraction;
        public RunnerBroadcastHeatGauge HeatAndFocusGauge => heatAndFocusGauge;
        public RunnerBroadcastSettlementView SettlementView => settlementView;
    }
}
