using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using StreamOn.UI;

namespace StreamOn.Minigames.Runner
{
    public sealed class RunnerRoomController : MonoBehaviour
    {
        [SerializeField] private RunnerCampaignSettings settings;
        [SerializeField] private Transform player;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text promptText;
        [Header("Scene-authored Broadcast Transition")]
        [SerializeField] private CanvasGroup transitionFade;
        [SerializeField] private GameObject gameSelectionPanel;
        [SerializeField] private Button runnerGameButton;
        [SerializeField] private Button tileArenaGameButton;
        [SerializeField] private Button plasticKnightmareGameButton;
        [Header("Room Cameras")]
        [SerializeField] private Camera mainRoomCamera;
        [SerializeField] private Camera broadcastCutsceneCamera;
        [SerializeField] private GameObject roomUi;
        [SerializeField] private Vector3 nearCameraPosition = new Vector3(-1.273f, 2.039f, -0.225f);
        [SerializeField] private Vector3 farCameraPosition = new Vector3(1.037f, 2.754f, 1.109f);
        [SerializeField, Min(0.1f)] private float farCameraPlayerDistance = 6f;
        [SerializeField, Min(0.1f)] private float cameraFollowSharpness = 3.5f;
        [SerializeField] private float cutsceneStartX = -4.13f;
        [SerializeField] private float cutsceneEndX = -4.55f;
        [SerializeField, Min(0.1f)] private float cutsceneDuration = 0.65f;
        [Header("Broadcast Computer Interaction")]
        [SerializeField] private Transform broadcastInteractionZone;
        [SerializeField] private Collider broadcastInteractionCollider;
        [SerializeField] private Canvas broadcastPromptCanvas;
        [SerializeField] private TMP_Text broadcastPromptWorldText;
        [SerializeField, Min(0f)] private float interactionZoneTolerance = 0.15f;
        [Header("Laptop Growth & Leaderboard Interaction")]
        [SerializeField] private Transform laptopInteractionZone;
        [SerializeField] private Collider laptopInteractionCollider;
        [SerializeField] private Canvas laptopPromptCanvas;
        [SerializeField] private GameObject growthAndLeaderboardPanel;
        [SerializeField, Min(0f)] private float laptopInteractionZoneTolerance = 0.75f;
        [SerializeField] private bool logLaptopInteraction;
        [Header("Scene-authored Room Status HUD")]
        [SerializeField] private Canvas statusHudCanvas;
        [SerializeField] private TMP_Text hudFollowersText;
        [SerializeField] private TMP_Text hudMoneyText;
        [SerializeField] private TMP_Text hudWitLabel;
        [SerializeField] private TMP_Text hudComposureLabel;
        [SerializeField] private TMP_Text hudControlLabel;
        [SerializeField] private TMP_Text hudWitLevelText;
        [SerializeField] private TMP_Text hudComposureLevelText;
        [SerializeField] private TMP_Text hudControlLevelText;
        [SerializeField] private Image hudWitFill;
        [SerializeField] private Image hudComposureFill;
        [SerializeField] private Image hudControlFill;
        [SerializeField] private TMP_Text hudXpLabel;
        [SerializeField] private Image hudXpFill;
        [Header("Scene-authored Save Slots")]
        [SerializeField] private GameObject slotPanel;
        [SerializeField] private TMP_Text slotNotice;
        [SerializeField] private GameObject[] slotRows;
        [SerializeField] private TMP_Text[] slotLabels;
        [SerializeField] private Button[] slotSelectButtons;
        [SerializeField] private Button[] slotDeleteButtons;
        [SerializeField, Min(0.5f)] private float interactionDistance = 2.4f;

        private RunnerCampaignSaveData _save;
        private RunnerRoomActivity[] _activities;
        private string _notice = string.Empty;
        private GameObject _slotPanel;
        private TMP_Text _slotNotice;
        private int _deleteArmedSlot = -1;
        private bool _transitioning;
        private int _gameSelectionInputEnabledFrame = int.MaxValue;
        private Vector3 _playerInitialPosition;
        private Vector3 _cutsceneCameraInitialPosition;
        private GameObject[] _roomSiblingPanels;
        private ScenePanelToggle _growthPanelToggle;
        private void Start()
        {
            // The room is never a paused gameplay scene. A pause left behind by the
            // previous scene would otherwise make CharacterController movement receive
            // a zero delta time even though keyboard input is being detected.
            Time.timeScale = 1f;
            if (settings == null)
            {
                Debug.LogError("STREAM ON room requires RunnerCampaignSettings.", this);
                enabled = false;
                return;
            }
            AudioListener.volume = RunnerUserSettingsStore.Load().masterVolume;
            ResolveGameSelectionUi();
            ResolveRoomPresentation();
            if (runnerGameButton != null) runnerGameButton.onClick.AddListener(SelectRunnerGame);
            if (tileArenaGameButton != null) tileArenaGameButton.onClick.AddListener(SelectTileArenaGame);
            if (plasticKnightmareGameButton != null) plasticKnightmareGameButton.onClick.AddListener(SelectPlasticKnightmareGame);
            if (transitionFade != null)
            {
                transitionFade.alpha = 0f;
                transitionFade.blocksRaycasts = false;
                transitionFade.interactable = false;
            }
            if (gameSelectionPanel != null) gameSelectionPanel.SetActive(false);
            _activities = FindObjectsByType<RunnerRoomActivity>(FindObjectsSortMode.None);
            BindSlotMenu();
            // StreamerRoom must be immediately playable. Use the currently selected
            // save slot here; slot management belongs to the menu and must not silently
            // lock room movement behind an inactive overlay.
            InitializeSelectedSlot();
        }

        private void LateUpdate()
        {
            if (broadcastPromptCanvas != null && mainRoomCamera != null)
                broadcastPromptCanvas.transform.rotation = mainRoomCamera.transform.rotation;
            if (laptopPromptCanvas != null && mainRoomCamera != null)
                laptopPromptCanvas.transform.rotation = mainRoomCamera.transform.rotation;
            if (player == null || mainRoomCamera == null || _transitioning) return;

            Vector3 playerOffset = player.position - _playerInitialPosition;
            playerOffset.y = 0f;
            float distance01 = Mathf.Clamp01(playerOffset.magnitude / Mathf.Max(0.1f, farCameraPlayerDistance));
            Vector3 targetPosition = Vector3.Lerp(nearCameraPosition, farCameraPosition, distance01);
            float blend = 1f - Mathf.Exp(-cameraFollowSharpness * Time.deltaTime);
            mainRoomCamera.transform.position = Vector3.Lerp(
                mainRoomCamera.transform.position, targetPosition, blend);
        }

        private void InitializeSelectedSlot()
        {
            if (!RunnerCampaignSaveStore.TryLoad(settings, out _save))
            {
                _save = RunnerCampaignSaveStore.CreateNew(settings);
                RunnerCampaignSaveStore.Save(settings, _save, true);
            }
            _save.gameSkill = Mathf.Clamp(_save.gameSkill, 1, settings.maximumGameSkill);
            _save.talkingSkill = Mathf.Clamp(_save.talkingSkill, 1, settings.maximumTalkingSkill);
            _save.healthStat = Mathf.Clamp(_save.healthStat > 0 ? _save.healthStat : settings.startingHealthStat, 1, settings.maximumHealthStat);
            _save.mentalLevel = Mathf.Clamp(_save.mentalLevel > 0 ? _save.mentalLevel : settings.startingMentalLevel, 1, settings.maximumMentalLevel);
            _save.pcLevel = Mathf.Clamp(_save.pcLevel, 1, 3);
            _save.microphoneLevel = Mathf.Clamp(_save.microphoneLevel, 1, 3);
            _save.fitnessLevel = Mathf.Clamp(_save.fitnessLevel, 1, 3);
            _save.interiorLevel = Mathf.Clamp(_save.interiorLevel, 1, 3);
            if (_save.awaitingAdvance)
            {
                _save.day++;
                _save.awaitingAdvance = false;
                _save.broadcastPending = false;
                RunnerCampaignSaveStore.Save(settings, _save, true);
            }
            RunnerSaveSession.RequireSlotSelection = false;
            SetPlayerLocked(false);
            SetStatusHudVisible(true);
            if (_slotPanel != null) _slotPanel.SetActive(false);
            if (roomUi != null) roomUi.SetActive(false);
            RefreshStatus();
            if (RunnerBroadcastSessionStore.OpenGameSelectionOnRoomLoad)
            {
                RunnerBroadcastSessionStore.OpenGameSelectionOnRoomLoad = false;
                RunnerBroadcastSessionStore.End(settings, _save);
                ShowGameSelection(false);
            }
            else if (_save.broadcastSessionActive)
            {
                // Loading a save slot must always enter the room first. A broadcast
                // can remain resumable in the save, but it should only continue after
                // the player interacts with the computer and chooses the game again.
                _notice = "중단된 방송이 있습니다. 컴퓨터에서 게임을 선택하면 이어서 진행합니다.";
                if (promptText != null) promptText.text = _notice;
            }
        }

        private void Update()
        {
            // The growth panel locks movement, so it must always be closable from the
            // keyboard. Relying on its Close button alone strands the player whenever
            // the panel fails to render.
            bool growthPanelOpen = growthAndLeaderboardPanel != null
                && growthAndLeaderboardPanel.activeInHierarchy;
            if (growthPanelOpen && Keyboard.current != null
                && (Keyboard.current.escapeKey.wasPressedThisFrame
                    || Keyboard.current.eKey.wasPressedThisFrame))
            {
                CloseGrowthAndLeaderboardPanel();
                return;
            }
            if (_transitioning || (_slotPanel != null && _slotPanel.activeSelf)
                || (gameSelectionPanel != null && gameSelectionPanel.activeInHierarchy)
                || growthPanelOpen)
            {
                SetBroadcastPromptVisible(false);
                SetLaptopPromptVisible(false);
                return;
            }
            if (_save != null && Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame)
            {
                _notice = ManualSave() ? "수동 저장 완료" : "수동 저장 실패";
            }
            if (_save == null || player == null || _activities == null) return;
            if (UpdateBroadcastComputerInteraction()) return;
            if (UpdateLaptopInteraction()) return;
            RunnerRoomActivity closest = _activities
                .Where(activity => activity != null && !activity.IsBroadcastComputer)
                .OrderBy(activity => Vector3.SqrMagnitude(activity.transform.position - player.position))
                .FirstOrDefault();
            float distance = closest != null ? Vector3.Distance(closest.transform.position, player.position) : float.MaxValue;
            if (closest == null || distance > interactionDistance)
            {
                if (promptText != null) promptText.text = string.IsNullOrEmpty(_notice) ? "WASD 이동  |  E 상호작용  |  F5 수동 저장" : _notice;
                return;
            }

            if (promptText != null)
            {
                string prefix = $"E  {closest.InteractionName}";
                promptText.text = string.IsNullOrEmpty(_notice) ? prefix : _notice + "\n" + prefix;
            }
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) Interact(closest);
        }

        private void Interact(RunnerRoomActivity activity)
        {
            _notice = string.Empty;
            if (_save.broadcastPending)
            {
                _notice = "오늘의 활동은 이미 끝났습니다. 컴퓨터에서 방송을 시작하세요.";
                return;
            }

            _notice = "능력치는 방송 경험치로 레벨업한 뒤 성장 패널에서 올릴 수 있습니다.";
            return;
        }

        private bool UpdateBroadcastComputerInteraction()
        {
            if (broadcastInteractionCollider == null)
            {
                SetBroadcastPromptVisible(false);
                return false;
            }

            Vector3 closestPoint = broadcastInteractionCollider.ClosestPoint(player.position);
            bool insideZone = (closestPoint - player.position).sqrMagnitude
                <= interactionZoneTolerance * interactionZoneTolerance;
            SetBroadcastPromptVisible(insideZone);
            if (!insideZone) return false;

            if (promptText != null) promptText.text = string.Empty;
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                BeginBroadcastSelection();
            return true;
        }

        private void BeginBroadcastSelection()
        {
            if (_save == null || _transitioning) return;
            _notice = string.Empty;
            if (!_save.broadcastPending)
            {
                _save.broadcastPending = true;
                _save.selectedAction = string.Empty;
                RunnerCampaignSaveStore.Save(settings, _save, true);
            }
            SetBroadcastPromptVisible(false);
            ShowGameSelection(false);
        }

        private void SetBroadcastPromptVisible(bool visible)
        {
            if (broadcastPromptCanvas != null && broadcastPromptCanvas.gameObject.activeSelf != visible)
                broadcastPromptCanvas.gameObject.SetActive(visible);
        }

        private bool UpdateLaptopInteraction()
        {
            if (laptopInteractionCollider == null)
            {
                SetLaptopPromptVisible(false);
                return false;
            }

            // The room player walks on the floor while the zone box is authored around
            // desk height. Comparing full 3D positions makes the test fail or pass purely
            // on how the CharacterController settles vertically, so match on the floor
            // plane only and let the box's own height be irrelevant.
            Vector3 probe = player.position;
            probe.y = laptopInteractionCollider.bounds.center.y;
            Vector3 closestPoint = laptopInteractionCollider.ClosestPoint(probe);
            float distance = Vector3.Distance(closestPoint, probe);
            bool insideZone = distance <= laptopInteractionZoneTolerance;
            if (logLaptopInteraction)
                Debug.Log($"STREAM ON laptop: distance {distance:F3} / tolerance {laptopInteractionZoneTolerance:F3} -> {insideZone}", this);
            SetLaptopPromptVisible(insideZone);
            if (!insideZone) return false;

            if (promptText != null) promptText.text = string.Empty;
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                OpenGrowthAndLeaderboardPanel();
            return true;
        }

        private void SetLaptopPromptVisible(bool visible)
        {
            if (laptopPromptCanvas != null && laptopPromptCanvas.gameObject.activeSelf != visible)
                laptopPromptCanvas.gameObject.SetActive(visible);
        }

        private void OpenGrowthAndLeaderboardPanel()
        {
            if (growthAndLeaderboardPanel == null || _transitioning) return;
            SetLaptopPromptVisible(false);
            SetStatusHudVisible(false);
            // Room UI is a shared container. Several of its panels are left active in the
            // scene and stay hidden only because the container itself is off, so turning
            // it on for one panel would reveal every sibling along with it.
            for (int index = 0; _roomSiblingPanels != null && index < _roomSiblingPanels.Length; index++)
                if (_roomSiblingPanels[index] != null) _roomSiblingPanels[index].SetActive(false);
            if (roomUi != null) roomUi.SetActive(true);
            growthAndLeaderboardPanel.SetActive(true);
            _growthPanelToggle?.Open();
            SetPlayerLocked(true);
        }

        private void CloseGrowthAndLeaderboardPanel()
        {
            _growthPanelToggle?.Close();
            if (growthAndLeaderboardPanel != null) growthAndLeaderboardPanel.SetActive(false);
            // Restore the container to the state the room starts in, otherwise the next
            // flow that turns it on inherits a half-open UI.
            if (roomUi != null) roomUi.SetActive(false);
            SetPlayerLocked(false);
            RefreshStatus();
            SetStatusHudVisible(true);
        }

        public void SelectRunnerGame() => LoadSelectedGame("runner",
            string.IsNullOrWhiteSpace(settings.runnerSceneName) ? settings.broadcastSceneName : settings.runnerSceneName);

        public void SelectTileArenaGame() => LoadSelectedGame("tile_arena", settings.tileArenaSceneName);

        public void SelectPlasticKnightmareGame() => LoadSelectedGame("plastic_knightmare",
            string.IsNullOrWhiteSpace(settings.plasticKnightmareMenuSceneName)
                ? settings.plasticKnightmareSceneName
                : settings.plasticKnightmareMenuSceneName);

        private void LoadSelectedGame(string gameId, string sceneName)
        {
            // A panel can become visible from another button's onClick (for example,
            // after choosing a save slot). Do not let that same UI event also choose
            // whichever executable happens to be underneath the pointer.
            if (gameSelectionPanel != null && (!gameSelectionPanel.activeInHierarchy
                || Time.frameCount < _gameSelectionInputEnabledFrame))
                return;

            if (_save == null)
            {
                ShowSelectionError("저장 데이터를 불러오지 못했습니다.");
                return;
            }
            if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                ShowSelectionError($"게임 씬을 찾을 수 없습니다: {sceneName}");
                return;
            }
            _save.selectedBroadcastGame = gameId;
            _save.broadcastSessionExperienceEarned = 0;
            BroadcastGameId id = gameId == "tile_arena" ? BroadcastGameId.TileArena
                : gameId == "plastic_knightmare" ? BroadcastGameId.PlasticKnightmare : BroadcastGameId.Runner;

            // A selection panel is only shown before a new broadcast. If an obsolete
            // session from an older build survived in the save, clear it instead of
            // silently making every other game button appear broken.
            if (_save.broadcastSessionActive
                && (!System.Enum.TryParse(_save.broadcastSessionGameId, out BroadcastGameId savedId) || savedId != id))
            {
                RunnerBroadcastSessionStore.End(settings, _save);
            }

            if (!RunnerBroadcastSessionStore.BeginOrResume(settings, _save, id))
            {
                ShowSelectionError("방송 세션을 시작하지 못했습니다. 저장 상태를 확인해 주세요.");
                return;
            }
            RunnerBroadcastSessionStore.ApplyToSave(_save);
            RunnerCampaignSaveStore.Save(settings, _save, true);
            SceneManager.LoadScene(sceneName);
        }

        private void ResumeLockedBroadcast()
        {
            if (_save == null) return;
            if (!System.Enum.TryParse(_save.broadcastSessionGameId, out BroadcastGameId gameId))
            {
                _save.broadcastSessionActive = false;
                RunnerCampaignSaveStore.Save(settings, _save, true);
                ShowGameSelection(false);
                return;
            }
            if (!RunnerBroadcastSessionStore.BeginOrResume(settings, _save, gameId))
            {
                RunnerBroadcastSessionStore.End(settings, _save);
                ShowSelectionError("이전 방송을 복원하지 못했습니다. 게임을 다시 선택해 주세요.");
                ShowGameSelection(false);
                return;
            }
            string sceneName = gameId == BroadcastGameId.TileArena ? settings.tileArenaSceneName
                : gameId == BroadcastGameId.PlasticKnightmare
                    ? (string.IsNullOrWhiteSpace(settings.plasticKnightmareMenuSceneName)
                        ? settings.plasticKnightmareSceneName
                        : settings.plasticKnightmareMenuSceneName)
                : string.IsNullOrWhiteSpace(settings.runnerSceneName) ? settings.broadcastSceneName : settings.runnerSceneName;
            if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                RunnerBroadcastSessionStore.End(settings, _save);
                ShowSelectionError($"게임 씬을 찾을 수 없습니다: {sceneName}");
                ShowGameSelection(false);
                return;
            }
            SceneManager.LoadScene(sceneName);
        }

        private void ShowSelectionError(string message)
        {
            _notice = message;
            if (promptText != null) promptText.text = message;
            Debug.LogError($"STREAM ON: {message}", this);
        }

        private void ShowGameSelection(bool fadeFirst)
        {
            if (gameSelectionPanel == null)
            {
                ShowSelectionError("방송 게임 선택 UI를 찾을 수 없습니다.");
                return;
            }
            if (_transitioning || gameSelectionPanel.activeInHierarchy) return;
            StartCoroutine(PlayBroadcastStartCutscene());
        }

        private IEnumerator PlayBroadcastStartCutscene()
        {
            if (_transitioning) yield break;
            _transitioning = true;
            SetPlayerLocked(true);
            SetBroadcastPromptVisible(false);
            SetStatusHudVisible(false);

            if (roomUi != null) roomUi.SetActive(false);
            if (mainRoomCamera != null) mainRoomCamera.gameObject.SetActive(false);

            if (broadcastCutsceneCamera != null)
            {
                Transform cutsceneTransform = broadcastCutsceneCamera.transform;
                Vector3 start = _cutsceneCameraInitialPosition;
                start.x = cutsceneStartX;
                Vector3 end = start;
                end.x = cutsceneEndX;
                cutsceneTransform.position = start;
                broadcastCutsceneCamera.gameObject.SetActive(true);

                float startedAt = Time.unscaledTime;
                float duration = Mathf.Max(0.1f, cutsceneDuration);
                while (Time.unscaledTime - startedAt < duration)
                {
                    float progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / duration);
                    progress = progress * progress * (3f - 2f * progress);
                    cutsceneTransform.position = Vector3.Lerp(start, end, progress);
                    yield return null;
                }

                cutsceneTransform.position = _cutsceneCameraInitialPosition;
                broadcastCutsceneCamera.gameObject.SetActive(false);
            }

            if (mainRoomCamera != null) mainRoomCamera.gameObject.SetActive(true);
            if (roomUi != null) roomUi.SetActive(true);
            gameSelectionPanel.SetActive(true);
            ArmGameSelectionInput();
            _transitioning = false;
        }

        public bool ManualSave() => _save != null && RunnerCampaignSaveStore.Save(settings, _save, true);

        private void ShowSlotMenu()
        {
            if (slotPanel == null)
            {
                // A missing slot panel must never leave the room player permanently locked.
                InitializeSelectedSlot();
                return;
            }
            SetPlayerLocked(true);
            if (roomUi != null) roomUi.SetActive(true);
            slotPanel.SetActive(true);
            RefreshSlotMenu();
        }

        private void SelectSlot(int slot, bool hasSave)
        {
            RunnerCampaignSaveStore.SelectSlot(settings, slot);
            if (!hasSave)
            {
                RunnerCampaignSaveStore.Delete(settings, slot);
                RunnerCampaignSaveData newSave = RunnerCampaignSaveStore.CreateNew(settings, slot);
                RunnerCampaignSaveStore.Save(settings, newSave, true);
            }
            _deleteArmedSlot = -1;
            InitializeSelectedSlot();
        }

        private void RequestDeleteSlot(int slot)
        {
            if (_deleteArmedSlot != slot)
            {
                _deleteArmedSlot = slot;
                if (slotNotice != null) slotNotice.text = $"슬롯 {slot}을 삭제하려면 같은 삭제 버튼을 한 번 더 누르세요.";
                RefreshSlotMenu();
                return;
            }
            RunnerCampaignSaveStore.Delete(settings, slot);
            _deleteArmedSlot = -1;
            if (slotNotice != null) slotNotice.text = $"슬롯 {slot}을 삭제했습니다.";
            RefreshSlotMenu();
        }

        private void BindSlotMenu()
        {
            _slotPanel = slotPanel;
            _slotNotice = slotNotice;
            for (int index = 0; slotSelectButtons != null && index < slotSelectButtons.Length; index++)
            {
                int captured = index;
                slotSelectButtons[index]?.onClick.AddListener(() => SelectSlotByIndex(captured));
            }
            for (int index = 0; slotDeleteButtons != null && index < slotDeleteButtons.Length; index++)
            {
                int captured = index;
                slotDeleteButtons[index]?.onClick.AddListener(() => DeleteSlotByIndex(captured));
            }
            if (slotPanel != null) slotPanel.SetActive(false);
        }

        private void ResolveGameSelectionUi()
        {
            Transform explorer = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate != null
                    && candidate.name.Replace(" ", string.Empty) == "BroadcastGameExplorer");
            if (explorer == null) return;

            GameObject legacyPanel = gameSelectionPanel;
            gameSelectionPanel = explorer.gameObject;
            Button[] explorerButtons = explorer.GetComponentsInChildren<Button>(true);
            runnerGameButton = explorerButtons.FirstOrDefault(button => button.name == "Runner Game Button");
            tileArenaGameButton = explorerButtons.FirstOrDefault(button => button.name == "Tile Arena Game Button");
            plasticKnightmareGameButton = explorerButtons.FirstOrDefault(button => button.name == "Plastic Knightmare Game Button");
            Button closeButton = explorerButtons.FirstOrDefault(button => button.name == "Close Button");
            if (closeButton != null) closeButton.onClick.AddListener(CloseGameSelection);

            if (legacyPanel != null && legacyPanel != gameSelectionPanel) legacyPanel.SetActive(false);
        }

        public void CloseGameSelection()
        {
            if (gameSelectionPanel != null) gameSelectionPanel.SetActive(false);
            if (_save != null)
            {
                _save.broadcastPending = false;
                RunnerCampaignSaveStore.Save(settings, _save, true);
            }
            SetPlayerLocked(false);
            SetStatusHudVisible(true);
        }

        private void SetStatusHudVisible(bool visible)
        {
            if (statusHudCanvas != null && statusHudCanvas.gameObject.activeSelf != visible)
                statusHudCanvas.gameObject.SetActive(visible);
        }

        private void ResolveRoomPresentation()
        {
            Transform[] sceneTransforms = FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            mainRoomCamera ??= sceneTransforms.FirstOrDefault(candidate => candidate != null
                && candidate.name == "Main Camera")?.GetComponent<Camera>();
            broadcastCutsceneCamera ??= sceneTransforms.FirstOrDefault(candidate => candidate != null
                && candidate.name == "Second Camera")?.GetComponent<Camera>();
            roomUi ??= sceneTransforms.FirstOrDefault(candidate => candidate != null
                && candidate.name == "Room UI")?.gameObject;
            broadcastInteractionZone ??= sceneTransforms.FirstOrDefault(candidate => candidate != null
                && candidate.name == "Computer_InteractionZone");
            if (broadcastInteractionCollider == null && broadcastInteractionZone != null)
                broadcastInteractionCollider = broadcastInteractionZone.GetComponent<Collider>();
            ResolveBroadcastPrompt();

            ResolveLaptopInteraction(sceneTransforms);

            _playerInitialPosition = player != null ? player.position : Vector3.zero;
            if (roomUi != null) roomUi.transform.localScale = Vector3.one;
            if (mainRoomCamera != null)
            {
                mainRoomCamera.transform.position = nearCameraPosition;
                mainRoomCamera.gameObject.SetActive(true);
            }
            if (broadcastCutsceneCamera != null)
            {
                _cutsceneCameraInitialPosition = broadcastCutsceneCamera.transform.position;
                _cutsceneCameraInitialPosition.x = cutsceneStartX;
                broadcastCutsceneCamera.transform.position = _cutsceneCameraInitialPosition;
                broadcastCutsceneCamera.gameObject.SetActive(false);
            }
            if (roomUi != null) roomUi.SetActive(false);
        }

        private void ResolveBroadcastPrompt()
        {
            SetBroadcastPromptVisible(false);
        }

        // Every lookup here reports its own failure. A silent null is what makes a
        // scene-authored interaction look like it was never implemented at all.
        private void ResolveLaptopInteraction(Transform[] sceneTransforms)
        {
            laptopInteractionZone ??= sceneTransforms.FirstOrDefault(candidate => candidate != null
                && candidate.name == "Laptop_InteractionZone");
            if (laptopInteractionZone == null)
            {
                Debug.LogError("STREAM ON laptop: 'Laptop_InteractionZone' 오브젝트를 씬에서 찾지 못했습니다.", this);
                return;
            }

            if (laptopInteractionCollider == null)
                laptopInteractionCollider = laptopInteractionZone.GetComponent<Collider>();
            if (laptopInteractionCollider == null)
                Debug.LogError("STREAM ON laptop: Laptop_InteractionZone에 Collider가 없습니다.", laptopInteractionZone);

            if (laptopPromptCanvas == null && laptopInteractionZone.parent != null)
                laptopPromptCanvas = laptopInteractionZone.parent
                    .GetComponentsInChildren<Canvas>(true).FirstOrDefault();
            if (laptopPromptCanvas == null)
                Debug.LogError("STREAM ON laptop: Laptop 하위에서 프롬프트 Canvas를 찾지 못했습니다.", laptopInteractionZone);

            growthAndLeaderboardPanel ??= sceneTransforms.FirstOrDefault(candidate => candidate != null
                && candidate.name == "Growth And Leaderboard UI")?.gameObject;
            if (growthAndLeaderboardPanel == null)
                Debug.LogError("STREAM ON laptop: 'Growth And Leaderboard UI' 패널을 씬에서 찾지 못했습니다.", this);
            else
            {
                _growthPanelToggle = growthAndLeaderboardPanel.GetComponent<ScenePanelToggle>();
                Button closeButton = growthAndLeaderboardPanel.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(button => button.name == "Close");
                if (closeButton != null) closeButton.onClick.AddListener(CloseGrowthAndLeaderboardPanel);
                else Debug.LogWarning("STREAM ON laptop: 패널에서 'Close' 버튼을 찾지 못했습니다. 닫기가 동작하지 않습니다.",
                    growthAndLeaderboardPanel);
            }

            CacheRoomSiblingPanels();
            SetLaptopPromptVisible(false);
        }

        // Every Room UI child except the growth panel and the two pass-through overlays
        // is a modal panel that must not ride along when the container is turned on.
        private void CacheRoomSiblingPanels()
        {
            if (roomUi == null) return;
            _roomSiblingPanels = roomUi.transform.Cast<Transform>()
                .Where(child => child != null
                    && child.gameObject != growthAndLeaderboardPanel
                    && child.name != "Broadcast Fade Overlay"
                    && child.name != "UGS Service Notification UI")
                .Select(child => child.gameObject)
                .ToArray();
        }

        private static void SetHudGauge(TMP_Text label, TMP_Text levelText, Image fill,
            string name, int rank, int maximum)
        {
            maximum = Mathf.Max(1, maximum);
            rank = Mathf.Clamp(rank, 0, maximum);
            if (label != null) label.text = name;
            if (levelText != null)
                levelText.text = rank >= maximum ? "Lvl. MAX" : $"Lvl. {rank + 1}";
            if (fill != null)
            {
                RectTransform rect = fill.rectTransform;
                rect.anchorMax = new Vector2(rank / (float)maximum, 1f);
            }
        }

        private void ArmGameSelectionInput()
        {
            _gameSelectionInputEnabledFrame = Time.frameCount + 1;
        }

        private void RefreshSlotMenu()
        {
            IReadOnlyList<RunnerSaveSlotInfo> slots = RunnerCampaignSaveStore.GetSlotInfos(settings);
            for (int index = 0; slotRows != null && index < slotRows.Length; index++)
            {
                bool visible = index < slots.Count;
                if (slotRows[index] != null) slotRows[index].SetActive(visible);
                if (!visible) continue;
                RunnerSaveSlotInfo info = slots[index];
                string label = info.exists
                    ? $"슬롯 {info.slot}  이어하기    DAY {info.day}  팔로워 {info.subscribers:N0}  방송인 Lv.{info.broadcasterLevel}" + (info.recoveredFromBackup ? "  [백업 복구]" : string.Empty)
                    : info.corrupted ? $"슬롯 {info.slot}  손상됨 - 새 게임으로 교체" : $"슬롯 {info.slot}  새 게임";
                if (slotLabels != null && index < slotLabels.Length && slotLabels[index] != null) slotLabels[index].text = label;
                if (slotDeleteButtons != null && index < slotDeleteButtons.Length && slotDeleteButtons[index] != null)
                {
                    slotDeleteButtons[index].gameObject.SetActive(info.exists || info.corrupted);
                    TMP_Text deleteLabel = slotDeleteButtons[index].GetComponentInChildren<TMP_Text>(true);
                    if (deleteLabel != null) deleteLabel.text = _deleteArmedSlot == info.slot ? "정말 삭제" : "삭제";
                }
            }
        }

        private void SelectSlotByIndex(int index)
        {
            IReadOnlyList<RunnerSaveSlotInfo> slots = RunnerCampaignSaveStore.GetSlotInfos(settings);
            if (index >= 0 && index < slots.Count) SelectSlot(slots[index].slot, slots[index].exists);
        }

        private void DeleteSlotByIndex(int index)
        {
            IReadOnlyList<RunnerSaveSlotInfo> slots = RunnerCampaignSaveStore.GetSlotInfos(settings);
            if (index >= 0 && index < slots.Count) RequestDeleteSlot(slots[index].slot);
        }


        private void SetPlayerLocked(bool locked)
        {
            if (player != null && player.TryGetComponent(out RunnerRoomPlayerController controller)) controller.InputLocked = locked;
        }

        public void RefreshStatus()
        {
            if (_save == null) return;
            // These labels are rewritten every refresh, so they must match what
            // StatusUI.prefab authors ("팔로워 1,349" / "소지금 10,000 ₩") or the scene
            // text visibly flips to another language the moment the room starts.
            if (hudFollowersText != null) hudFollowersText.text = $"팔로워 {_save.subscribers:N0}";
            if (hudMoneyText != null) hudMoneyText.text = $"소지금 {_save.cash:N0} ₩";
            int maximumWit = BroadcasterProgression.MaximumRank(settings, BroadcasterStatType.Wit);
            int maximumComposure = BroadcasterProgression.MaximumRank(settings, BroadcasterStatType.Composure);
            int maximumControl = BroadcasterProgression.MaximumRank(settings, BroadcasterStatType.Control);
            SetHudGauge(hudWitLabel, hudWitLevelText, hudWitFill,
                "재치", _save.witRank, maximumWit);
            SetHudGauge(hudComposureLabel, hudComposureLevelText, hudComposureFill,
                "평정심", _save.ComposureRank, maximumComposure);
            SetHudGauge(hudControlLabel, hudControlLevelText, hudControlFill,
                "통제력", _save.ControlRank, maximumControl);

            bool atMaxLevel = _save.broadcasterLevel >= settings.maximumBroadcasterLevel;
            if (hudXpLabel != null)
                hudXpLabel.text = atMaxLevel ? "Lvl. MAX" : $"Lvl. {_save.broadcasterLevel}";
            if (hudXpFill != null)
            {
                float xpProgress = 0f;
                if (!atMaxLevel)
                {
                    BroadcasterLevelRule xpRule = settings.broadcasterLevels?.Find(r => r != null && r.level == _save.broadcasterLevel);
                    int required = Mathf.Max(1, xpRule != null ? xpRule.experienceToNextLevel : 100);
                    xpProgress = Mathf.Clamp01(_save.broadcasterExperience / (float)required);
                }
                hudXpFill.rectTransform.anchorMax = new Vector2(xpProgress, 1f);
            }

            if (statusText != null && statusHudCanvas == null)
                statusText.text = $"팔로워 {_save.subscribers:N0}    보유금 {_save.cash:N0}원    방송인 Lv.{_save.broadcasterLevel}    남은 포인트 {_save.unspentStatPoints}";
        }
    }
}
