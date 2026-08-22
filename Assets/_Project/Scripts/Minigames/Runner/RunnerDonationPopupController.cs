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
                yield return new WaitForSecondsRealtime(visibleSeconds);
                yield return Fade(1f, 0f);
            }
            _pump = null;
        }

        private IEnumerator Fade(float from, float to)
        {
            if (canvasGroup == null) yield break;
            float duration = Mathf.Max(0.01f, fadeSeconds);
            float startedAt = Time.unscaledTime;
            while (Time.unscaledTime - startedAt < duration)
            {
                canvasGroup.alpha = Mathf.Lerp(from, to, (Time.unscaledTime - startedAt) / duration);
                yield return null;
            }
            canvasGroup.alpha = to;
        }

        private struct DonationNotice
        {
            public string donor;
            public int amount;
            public string message;
        }
    }
}
