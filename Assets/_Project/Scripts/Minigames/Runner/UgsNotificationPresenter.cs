using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Services.Authentication;
using UnityEngine;

namespace StreamOn.Minigames.Runner
{
    public sealed class UgsNotificationPresenter : MonoBehaviour
    {
        public GameObject panel;
        public TMP_Text messageText;
        public UnityEngine.UI.Button closeButton;

        private void Awake() => closeButton?.onClick.AddListener(Close);

        public void Show(IReadOnlyList<Notification> notifications)
        {
            if (notifications == null || notifications.Count == 0 || panel == null) return;
            if (messageText != null)
            {
                messageText.text = string.Join("\n\n", notifications.Select(notification =>
                    string.IsNullOrWhiteSpace(notification.CaseId)
                        ? notification.Message
                        : $"{notification.Message}\n사례 번호: {notification.CaseId}"));
            }
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
        }

        public void Close() => panel?.SetActive(false);
    }
}
