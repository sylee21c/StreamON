using TMPro;
using UnityEngine;

namespace StreamOn.Minigames.Runner
{
    public sealed class StreamerProfilePanel : MonoBehaviour
    {
        public RunnerCampaignSettings settings;
        public TMP_InputField nameInput;
        public UnityEngine.UI.Button saveButton;
        public TMP_Text feedbackText;

        private void Awake() => saveButton?.onClick.AddListener(SaveName);
        private void OnEnable()
        {
            if (settings != null && nameInput != null && RunnerCampaignSaveStore.TryLoad(settings, out RunnerCampaignSaveData save))
                nameInput.text = save.streamerName;
        }

        public void SaveName()
        {
            if (settings == null || nameInput == null || !RunnerCampaignSaveStore.TryLoad(settings, out RunnerCampaignSaveData save)) return;
            string value = (nameInput.text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value)) value = settings.defaultStreamerName;
            save.streamerName = value.Substring(0, Mathf.Min(value.Length, 16));
            RunnerCampaignSaveStore.Save(settings, save, true);
            if (feedbackText != null) feedbackText.text = "스트리머 이름 저장 완료";
        }
    }
}
