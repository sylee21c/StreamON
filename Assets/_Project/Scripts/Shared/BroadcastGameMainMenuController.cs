using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using StreamOn.Minigames.Runner;

namespace StreamOn.Broadcast
{
    /// <summary>
    /// Drives scene-authored game title menus. Every visible element is serialized in
    /// RunnerMainMenu/TileArenaMainMenu; this component never constructs UI at runtime.
    /// </summary>
    public sealed class BroadcastGameMainMenuController : MonoBehaviour
    {
        [Header("Scene Flow")]
        [SerializeField] private string gameSceneName;
        [SerializeField] private string roomSceneName = "StreamerRoom";

        [Header("Scene-authored UI")]
        [SerializeField] private Image titleScreenImage;
        [SerializeField] private Image logoImage;
        [SerializeField] private Button playButton;
        [SerializeField] private Button tutorialButton;
        [SerializeField] private GameObject tutorialPanel;
        [SerializeField] private Button tutorialCloseButton;
        [SerializeField] private TMP_Text tutorialText;

        private bool _loading;

        private void Update()
        {
            if (_loading || Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
            if (tutorialPanel != null && tutorialPanel.activeSelf) CloseTutorial();
            else ReturnToRoom();
        }

        public void Play()
        {
            if (_loading) return;
            if (string.IsNullOrWhiteSpace(gameSceneName) || !Application.CanStreamedLevelBeLoaded(gameSceneName))
            {
                Debug.LogError($"STREAM ON game menu: 게임 씬을 찾을 수 없습니다: {gameSceneName}", this);
                return;
            }
            _loading = true;
            SceneManager.LoadScene(gameSceneName);
        }

        public void OpenTutorial()
        {
            if (_loading || tutorialPanel == null) return;
            tutorialPanel.SetActive(true);
            tutorialCloseButton?.Select();
        }

        public void CloseTutorial()
        {
            if (tutorialPanel != null) tutorialPanel.SetActive(false);
            tutorialButton?.Select();
        }

        public void ReturnToRoom()
        {
            if (_loading || string.IsNullOrWhiteSpace(roomSceneName)) return;
            _loading = true;
            if (RunnerBroadcastSessionStore.IsActive)
            {
                RunnerBroadcastSessionStore.OpenGameSelectionOnRoomLoad = true;
                RunnerSaveSession.RequireSlotSelection = false;
            }
            SceneManager.LoadScene(roomSceneName);
        }
    }
}
