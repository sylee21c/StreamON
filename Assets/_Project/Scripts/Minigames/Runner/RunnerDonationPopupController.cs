using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace StreamOn.Minigames.Runner
{
    public sealed class RunnerDonationPopupController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text donorText;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField, Min(0.1f)] private float fadeSeconds = 0.18f;
        [SerializeField, Min(0.5f)] private float visibleSeconds = 2.8f;

        private readonly Queue<DonationNotice> _pending = new Queue<DonationNotice>();
        private Coroutine _pump;

        public bool IsShowing => _pump != null || _pending.Count > 0;

        private void Awake()
        {
            // Donation notices must render above the chat and the rest of the HUD.
            transform.SetAsLastSibling();
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        public void ShowDonation(string donor, int amount, string message)
        {
            transform.SetAsLastSibling();
            _pending.Enqueue(new DonationNotice { donor = donor, amount = amount, message = message });
            if (_pump == null) _pump = StartCoroutine(Pump());
        }

        private IEnumerator Pump()
        {
            while (_pending.Count > 0)
            {
                DonationNotice notice = _pending.Dequeue();
                if (donorText != null) donorText.text = $"{notice.donor}님이";
                if (amountText != null) amountText.text = $"{notice.amount:N0}원을 후원해 주셨어요!";
                if (messageText != null) messageText.text = notice.message;
                yield return Fade(0f, 1f);
                yield return HoldVisible(visibleSeconds);
                yield return Fade(1f, 0f);
            }
            _pump = null;
        }

        private IEnumerator Fade(float from, float to)
        {
            if (canvasGroup == null) yield break;
            float duration = Mathf.Max(0.01f, fadeSeconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (Time.timeScale <= 0f)
                {
                    canvasGroup.alpha = 0f;
                    yield return null;
                    continue;
                }
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            canvasGroup.alpha = to;
        }

        private IEnumerator HoldVisible(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < Mathf.Max(0f, seconds))
            {
                bool unpaused = Time.timeScale > 0f;
                if (canvasGroup != null) canvasGroup.alpha = unpaused ? 1f : 0f;
                if (unpaused) elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            if (canvasGroup != null) canvasGroup.alpha = 1f;
        }

        private struct DonationNotice
        {
            public string donor;
            public int amount;
            public string message;
        }
    }
}
