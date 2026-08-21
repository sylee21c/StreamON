using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StreamOn.Minigames.TileArena
{
    public sealed class TileArenaAudioControls : MonoBehaviour
    {
        [SerializeField] private TileArenaAudioController audioController;
        [SerializeField] private TMP_Text soundButtonLabel;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private TMP_Text musicValue;
        [SerializeField] private Slider effectsSlider;
        [SerializeField] private TMP_Text effectsValue;

        private void Start()
        {
            if (audioController == null) audioController = FindFirstObjectByType<TileArenaAudioController>();
            if (audioController == null) return;
            if (musicSlider != null) musicSlider.SetValueWithoutNotify(audioController.MusicLevel);
            if (effectsSlider != null) effectsSlider.SetValueWithoutNotify(audioController.EffectsLevel);
            RefreshLabels();
        }

        public void ToggleSound()
        {
            audioController?.ToggleMute();
            RefreshLabels();
        }

        public void SetMusicVolume(float value)
        {
            audioController?.SetMusicVolume(value);
            RefreshLabels();
        }

        public void SetEffectsVolume(float value)
        {
            audioController?.SetEffectsVolume(value);
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            if (audioController == null) return;
            if (soundButtonLabel != null) soundButtonLabel.text = audioController.Muted ? "SOUND OFF" : "SOUND ON";
            if (musicValue != null) musicValue.text = Mathf.RoundToInt(audioController.MusicLevel * 100f) + "%";
            if (effectsValue != null) effectsValue.text = Mathf.RoundToInt(audioController.EffectsLevel * 100f) + "%";
        }
    }
}
