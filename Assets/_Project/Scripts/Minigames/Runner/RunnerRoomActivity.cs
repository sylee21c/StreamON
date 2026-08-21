using UnityEngine;

namespace StreamOn.Minigames.Runner
{
    public sealed class RunnerRoomActivity : MonoBehaviour
    {
        [SerializeField] private string actionId;
        [SerializeField] private bool broadcastComputer;
        [SerializeField] private string interactionName;

        public string ActionId => actionId;
        public bool IsBroadcastComputer => broadcastComputer;
        public string InteractionName => string.IsNullOrWhiteSpace(interactionName) ? gameObject.name : interactionName;

        public void Configure(string id, bool isComputer, string label)
        {
            actionId = id;
            broadcastComputer = isComputer;
            interactionName = label;
        }
    }
}
