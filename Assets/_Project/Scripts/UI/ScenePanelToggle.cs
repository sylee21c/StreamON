using UnityEngine;
using UnityEngine.UI;

namespace StreamOn.UI
{
    public sealed class ScenePanelToggle : MonoBehaviour
    {
        public Button openButton;
        public Button closeButton;
        public GameObject panel;
        public bool startOpen;

        private void Awake()
        {
            openButton?.onClick.AddListener(Open);
            closeButton?.onClick.AddListener(Close);
            if (panel != null) panel.SetActive(startOpen);
        }

        public void Open() { if (panel != null) panel.SetActive(true); }
        public void Close() { if (panel != null) panel.SetActive(false); }
    }
}
