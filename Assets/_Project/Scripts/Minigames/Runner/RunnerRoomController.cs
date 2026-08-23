using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StreamOn.Minigames.Runner
{
    public sealed class RunnerRoomController : MonoBehaviour
    {
        [SerializeField] private RunnerCampaignSettings settings;
        [SerializeField] private Transform player;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text promptText;
        [Header("Scene-authored Stat Gauges")]
        [SerializeField] private GameObject statGaugePanel;
        [SerializeField] private RunnerStatGauge mentalGauge;
        [SerializeField] private RunnerStatGauge gameSkillGauge;
        [SerializeField] private RunnerStatGauge talkingSkillGauge;
        [SerializeField] private RunnerStatGauge healthGauge;
        [Header("Scene-authored Broadcast Transition")]
        [SerializeField] private CanvasGroup transitionFade;
        [SerializeField] private GameObject gameSelectionPanel;
        [SerializeField] private Button runnerGameButton;
        [SerializeField] private Button tileArenaGameButton;
        [SerializeField] private Button plasticKnightmareGameButton;
        [Header("Scene-authored Save Slots")]
        [SerializeField] private GameObject slotPanel;
        [SerializeField] private TMP_Text slotNotice;
        [SerializeField] private GameObject[] slotRows;
        [SerializeField] private TMP_Text[] slotLabels;
        [SerializeField] private Button[] slotSelectButtons;
        [SerializeField] private Button[] slotDeleteButtons;
        [SerializeField, Min(0.2f)] private float activityGaugeAnimationSeconds = 1.2f;
        [SerializeField, Min(0f)] private float activityResultHoldSeconds = 0.55f;
        [SerializeField, Min(0.1f)] private float fadeDuration = 0.65f;
        [SerializeField, Min(0.5f)] private float interactionDistance = 2.4f;

        private RunnerCampaignSaveData _save;
        private RunnerRoomActivity[] _activities;
        private string _notice = string.Empty;
        private GameObject _slotPanel;
        private TMP_Text _slotNotice;
        private int _deleteArmedSlot = -1;
        private bool _transitioning;
        private int _gameSelectionInputEnabledFrame = int.MaxValue;

        private void Start()
        {
            if (settings == null)
            {
                Debug.LogError("STREAM ON room requires RunnerCampaignSettings.", this);
                enabled = false;
                return;
            }
            AudioListener.volume = RunnerUserSettingsStore.Load().masterVolume;
            ResolveGameSelectionUi();
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
            if (RunnerSaveSession.RequireSlotSelection) ShowSlotMenu();
            else InitializeSelectedSlot();
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
            if (_slotPanel != null) _slotPanel.SetActive(false);
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
            if (_transitioning || (_slotPanel != null && _slotPanel.activeSelf)) return;
            if (_save != null && Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame)
            {
                _notice = ManualSave() ? "수동 저장 완료" : "수동 저장 실패";
            }
            if (_save == null || player == null || _activities == null) return;
            RunnerRoomActivity closest = _activities
                .Where(activity => activity != null)
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
            if (activity.IsBroadcastComputer)
            {
                if (!_save.broadcastPending)
                {
                    _save.broadcastPending = true;
                    _save.selectedAction = string.Empty;
                    RunnerCampaignSaveStore.Save(settings, _save, true);
                }
                ShowGameSelection(false);
                return;
            }
            if (_save.broadcastPending)
            {
                _notice = "오늘의 활동은 이미 끝났습니다. 컴퓨터에서 방송을 시작하세요.";
                return;
            }

            _notice = "능력치는 방송 경험치로 레벨업한 뒤 성장 패널에서 올릴 수 있습니다.";
            return;

#pragma warning disable CS0162
            RunnerCampaignActionDefinition action = settings.dayActions.FirstOrDefault(candidate => candidate != null && candidate.id == activity.ActionId);
            if (action == null)
            {
                _notice = "이 오브젝트의 Action ID가 설정 에셋과 연결되지 않았습니다.";
                return;
            }
            StatProgress previousMental = new StatProgress(_save.mentalLevel, _save.mentalExperience);
            StatProgress previousGame = new StatProgress(_save.gameSkill, _save.gameSkillExperience);
            StatProgress previousTalking = new StatProgress(_save.talkingSkill, _save.talkingSkillExperience);
            StatProgress previousHealth = new StatProgress(_save.healthStat, _save.healthStatExperience);
            settings.AddStatExperience(ref _save.gameSkill, ref _save.gameSkillExperience,
                action.gameSkillDelta, settings.maximumGameSkill);
            settings.AddStatExperience(ref _save.talkingSkill, ref _save.talkingSkillExperience,
                action.talkingSkillDelta, settings.maximumTalkingSkill);
            settings.AddStatExperience(ref _save.healthStat, ref _save.healthStatExperience,
                action.healthStatDelta, settings.maximumHealthStat);
            settings.AddStatExperience(ref _save.mentalLevel, ref _save.mentalExperience,
                action.mentalExperienceDelta, settings.maximumMentalLevel);
            _save.subscribers = Mathf.Max(settings.minimumSubscribersToStartBroadcast, _save.subscribers + action.subscriberDelta);
            _save.selectedAction = action.displayName;
            _save.broadcastPending = true;
            RunnerCampaignSaveStore.Save(settings, _save);
            StartCoroutine(ShowActivityResultThenGameSelection(action, previousMental,
                previousGame, previousTalking, previousHealth));
#pragma warning restore CS0162
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
                SelectRunnerGame();
                return;
            }
            if (fadeFirst) StartCoroutine(FadeToGameSelection());
            else
            {
                SetPlayerLocked(true);
                gameSelectionPanel.SetActive(true);
                ArmGameSelectionInput();
            }
        }

        private IEnumerator FadeToGameSelection()
        {
            if (_transitioning) yield break;
            _transitioning = true;
            SetPlayerLocked(true);
            yield return FadeToGameSelectionCore();
            _transitioning = false;
        }

        private IEnumerator ShowActivityResultThenGameSelection(RunnerCampaignActionDefinition action,
            StatProgress previousMental, StatProgress previousGame, StatProgress previousTalking, StatProgress previousHealth)
        {
            if (_transitioning) yield break;
            _transitioning = true;
            SetPlayerLocked(true);
            _notice = BuildActivityResultNotice(action);
            if (promptText != null) promptText.text = _notice;
            if (statusText != null)
                statusText.text = $"DAY {_save.day}    팔로워 {_save.subscribers:N0}    보유금 {_save.cash:N0}원    방송인 Lv.{_save.broadcasterLevel}    포인트 {_save.unspentStatPoints}";
            if (statGaugePanel != null) statGaugePanel.SetActive(false);

            float fromGame = TotalExperience(previousGame);
            float fromTalking = TotalExperience(previousTalking);
            float fromHealth = TotalExperience(previousHealth);
            float fromMental = TotalExperience(previousMental);
            float toGame = TotalExperience(new StatProgress(_save.gameSkill, _save.gameSkillExperience));
            float toTalking = TotalExperience(new StatProgress(_save.talkingSkill, _save.talkingSkillExperience));
            float toHealth = TotalExperience(new StatProgress(_save.healthStat, _save.healthStatExperience));
            float toMental = TotalExperience(new StatProgress(_save.mentalLevel, _save.mentalExperience));
            float startedAt = Time.unscaledTime;
            float duration = Mathf.Max(0.2f, activityGaugeAnimationSeconds);
            while (Time.unscaledTime - startedAt < duration)
            {
                float t = Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - startedAt) / duration);
                SetGaugeFromTotal(mentalGauge, Mathf.Lerp(fromMental, toMental, t), settings.maximumMentalLevel);
                SetGaugeFromTotal(gameSkillGauge, Mathf.Lerp(fromGame, toGame, t), settings.maximumGameSkill);
                SetGaugeFromTotal(talkingSkillGauge, Mathf.Lerp(fromTalking, toTalking, t), settings.maximumTalkingSkill);
                SetGaugeFromTotal(healthGauge, Mathf.Lerp(fromHealth, toHealth, t), settings.maximumHealthStat);
                yield return null;
            }
            RefreshStatus();
            if (activityResultHoldSeconds > 0f) yield return new WaitForSecondsRealtime(activityResultHoldSeconds);
            yield return FadeToGameSelectionCore();
            _transitioning = false;
        }

        private IEnumerator FadeToGameSelectionCore()
        {
            if (gameSelectionPanel == null)
            {
                SelectRunnerGame();
                yield break;
            }
            if (transitionFade != null)
            {
                transitionFade.transform.SetAsLastSibling();
                transitionFade.blocksRaycasts = true;
                transitionFade.interactable = true;
                float startedAt = Time.unscaledTime;
                while (transitionFade.alpha < 1f)
                {
                    transitionFade.alpha = Mathf.Clamp01((Time.unscaledTime - startedAt) / Mathf.Max(0.1f, fadeDuration));
                    yield return null;
                }
            }
            gameSelectionPanel.SetActive(true);
            ArmGameSelectionInput();
            if (transitionFade != null)
            {
                float startedAt = Time.unscaledTime;
                float startAlpha = transitionFade.alpha;
                while (transitionFade.alpha > 0f)
                {
                    transitionFade.alpha = Mathf.Lerp(startAlpha, 0f,
                        (Time.unscaledTime - startedAt) / Mathf.Max(0.1f, fadeDuration));
                    yield return null;
                }
                transitionFade.alpha = 0f;
                transitionFade.blocksRaycasts = false;
                transitionFade.interactable = false;
            }
        }

        private string BuildActivityResultNotice(RunnerCampaignActionDefinition action)
        {
            List<string> gains = new List<string>();
            if (action.gameSkillDelta != 0) gains.Add($"게임 경험치 +{action.gameSkillDelta}");
            if (action.talkingSkillDelta != 0) gains.Add($"소통 경험치 +{action.talkingSkillDelta}");
            if (action.healthStatDelta != 0) gains.Add($"체력 경험치 +{action.healthStatDelta}");
            if (action.mentalExperienceDelta != 0) gains.Add($"멘탈 경험치 +{action.mentalExperienceDelta}");
            return $"{action.displayName} 완료!  {string.Join("  ·  ", gains)}";
        }

        private float TotalExperience(StatProgress state)
        {
            if (state.level <= 1) return Mathf.Max(0, state.experience);
            float total = settings.ExperienceRequiredForLevel(1);
            if (state.level == 2) return total + Mathf.Max(0, state.experience);
            return total + settings.ExperienceRequiredForLevel(2);
        }

        private void SetGaugeFromTotal(RunnerStatGauge gauge, float totalExperience, int maximumLevel)
        {
            if (gauge == null) return;
            int levelTwoRequirement = settings.ExperienceRequiredForLevel(1);
            int levelThreeRequirement = settings.ExperienceRequiredForLevel(2);
            if (maximumLevel <= 1 || totalExperience >= levelTwoRequirement + levelThreeRequirement)
                gauge.SetNormalizedLevelProgress(maximumLevel, 1f, maximumLevel);
            else if (totalExperience >= levelTwoRequirement)
                gauge.SetNormalizedLevelProgress(2,
                    (totalExperience - levelTwoRequirement) / Mathf.Max(1f, levelThreeRequirement), maximumLevel);
            else
                gauge.SetNormalizedLevelProgress(1, totalExperience / Mathf.Max(1f, levelTwoRequirement), maximumLevel);
        }

        private readonly struct StatProgress
        {
            public readonly int level;
            public readonly int experience;
            public StatProgress(int level, int experience) { this.level = level; this.experience = experience; }
        }

        public bool ManualSave() => _save != null && RunnerCampaignSaveStore.Save(settings, _save, true);

        private void ShowSlotMenu()
        {
            SetPlayerLocked(true);
            if (slotPanel == null) return;
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

            if (legacyPanel != null && legacyPanel != gameSelectionPanel) legacyPanel.SetActive(false);
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
            if (statusText != null)
                statusText.text = $"DAY {_save.day}    팔로워 {_save.subscribers:N0}    보유금 {_save.cash:N0}원";
            if (statGaugePanel != null) statGaugePanel.SetActive(true);
            mentalGauge?.SetLevelProgress(_save.mentalLevel, _save.mentalExperience,
                settings.ExperienceRequiredForLevel(_save.mentalLevel), settings.maximumMentalLevel);
            gameSkillGauge?.SetLevelProgress(_save.gameSkill, _save.gameSkillExperience,
                settings.ExperienceRequiredForLevel(_save.gameSkill), settings.maximumGameSkill);
            talkingSkillGauge?.SetLevelProgress(_save.talkingSkill, _save.talkingSkillExperience,
                settings.ExperienceRequiredForLevel(_save.talkingSkill), settings.maximumTalkingSkill);
            healthGauge?.SetLevelProgress(_save.healthStat, _save.healthStatExperience,
                settings.ExperienceRequiredForLevel(_save.healthStat), settings.maximumHealthStat);
        }
    }
}
