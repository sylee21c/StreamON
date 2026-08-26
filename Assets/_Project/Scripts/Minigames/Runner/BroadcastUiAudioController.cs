using UnityEngine;

namespace StreamOn.Minigames.Runner
{
    public enum BroadcastUiSound
    {
        Donation,
        LargeDonation,
        EventPrompt,
        ViewerBan,
        Success,
        Neutral,
        Failure,
        SettlementClose
    }

    /// <summary>Shared inspector-authored SFX bank for broadcast chat and event UI.</summary>
    public sealed class BroadcastUiAudioController : MonoBehaviour
    {
        [Header("도네 발생")]
        [SerializeField] private AudioClip donationSound;
        [SerializeField, Range(0f, 1f)] private float donationVolume = .8f;

        [Header("고액 도네 발생")]
        [SerializeField] private AudioClip largeDonationSound;
        [SerializeField, Range(0f, 1f)] private float largeDonationVolume = .9f;

        [Header("리액션 / 미션 등장")]
        [SerializeField] private AudioClip eventPromptSound;
        [SerializeField, Range(0f, 1f)] private float eventPromptVolume = .8f;

        [Header("시청자 차단")]
        [SerializeField] private AudioClip viewerBanSound;
        [SerializeField, Range(0f, 1f)] private float viewerBanVolume = .8f;

        [Header("미션 / 리액션 성공")]
        [SerializeField] private AudioClip successSound;
        [SerializeField, Range(0f, 1f)] private float successVolume = .9f;

        [Header("리액션 무난")]
        [SerializeField] private AudioClip neutralSound;
        [SerializeField, Range(0f, 1f)] private float neutralVolume = .75f;

        [Header("미션 / 리액션 실패")]
        [SerializeField] private AudioClip failureSound;
        [SerializeField, Range(0f, 1f)] private float failureVolume = .85f;

        [Header("방송 결산 닫기")]
        [SerializeField] private AudioClip settlementCloseSound;
        [SerializeField, Range(0f, 1f)] private float settlementCloseVolume = .8f;

        private static BroadcastUiAudioController _instance;
        private AudioSource _source;

        private void Awake()
        {
            _instance = this;
            _source = GetComponent<AudioSource>();
            if (_source == null) _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        public static void Play(BroadcastUiSound sound)
        {
            if (_instance == null) _instance = FindFirstObjectByType<BroadcastUiAudioController>();
            _instance?.PlayInternal(sound);
        }

        private void PlayInternal(BroadcastUiSound sound)
        {
            AudioClip clip;
            float volume;
            switch (sound)
            {
                case BroadcastUiSound.Donation: clip = donationSound; volume = donationVolume; break;
                case BroadcastUiSound.LargeDonation: clip = largeDonationSound; volume = largeDonationVolume; break;
                case BroadcastUiSound.EventPrompt: clip = eventPromptSound; volume = eventPromptVolume; break;
                case BroadcastUiSound.ViewerBan: clip = viewerBanSound; volume = viewerBanVolume; break;
                case BroadcastUiSound.Success: clip = successSound; volume = successVolume; break;
                case BroadcastUiSound.Neutral: clip = neutralSound; volume = neutralVolume; break;
                case BroadcastUiSound.Failure: clip = failureSound; volume = failureVolume; break;
                case BroadcastUiSound.SettlementClose: clip = settlementCloseSound; volume = settlementCloseVolume; break;
                default: return;
            }
            if (clip == null || _source == null) return;
            RunnerUserSettingsData settings = RunnerUserSettingsStore.Load();
            float effectiveVolume = Mathf.Clamp01(settings.masterVolume * settings.sfxVolume * volume);
            if (sound == BroadcastUiSound.SettlementClose) PlayAcrossSceneLoad(clip, effectiveVolume);
            else _source.PlayOneShot(clip, effectiveVolume);
        }

        private static void PlayAcrossSceneLoad(AudioClip clip, float volume)
        {
            GameObject oneShot = new GameObject("Broadcast UI One Shot");
            DontDestroyOnLoad(oneShot);
            AudioSource source = oneShot.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = volume;
            source.clip = clip;
            source.Play();
            Destroy(oneShot, Mathf.Max(.1f, clip.length + .1f));
        }
    }
}
