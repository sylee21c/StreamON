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

        private void Start()
        {
            if (settings == null)
            {
                Debug.LogError("STREAM ON room requires RunnerCampaignSettings.", this);
                enabled = false;
                return;
            }
            AudioListener.volume = RunnerUserSettingsStore.Load().masterVolume;
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
            BuildSlotMenu();
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
            if (_save.broadcastSessionActive || RunnerBroadcastSessionStore.OpenGameSelectionOnRoomLoad)
            {
                RunnerBroadcastSessionStore.BeginOrResume(settings, _save);
                RunnerBroadcastSessionStore.OpenGameSelectionOnRoomLoad = false;
                ShowGameSelection(false);
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
                string prefix = closest.IsBroadcastComputer && !_save.broadcastPending ? "먼저 오늘의 활동을 선택하세요" : $"E  {closest.InteractionName}";
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
                    _notice = "낮 활동을 하나 선택한 뒤 방송을 시작할 수 있습니다.";
                    return;
                }
                ShowGameSelection(false);
                return;
            }
            if (_save.broadcastPending)
            {
                _notice = "오늘의 활동은 이미 끝났습니다. 컴퓨터에서 방송을 시작하세요.";
                return;
            }

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
        }

        public void SelectRunnerGame() => LoadSelectedGame("runner",
            string.IsNullOrWhiteSpace(settings.runnerSceneName) ? settings.broadcastSceneName : settings.runnerSceneName);

        public void SelectTileArenaGame() => LoadSelectedGame("tile_arena", settings.tileArenaSceneName);

        public void SelectPlasticKnightmareGame() => LoadSelectedGame("plastic_knightmare",
            settings.plasticKnightmareMenuSceneName);

        private void LoadSelectedGame(string gameId, string sceneName)
        {
            if (_save == null || string.IsNullOrWhiteSpace(sceneName)) return;
            _save.selectedBroadcastGame = gameId;
            RunnerBroadcastSessionStore.ApplyToSave(_save);
            RunnerCampaignSaveStore.Save(settings, _save, true);
            SceneManager.LoadScene(sceneName);
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
                statusText.text = $"DAY {_save.day}    팔로워 {_save.subscribers:N0}    보유금 {_save.cash:N0}원";
            if (statGaugePanel != null) statGaugePanel.SetActive(true);

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
            _slotPanel.SetActive(true);
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
                _slotNotice.text = $"슬롯 {slot}을 삭제하려면 같은 삭제 버튼을 한 번 더 누르세요.";
                RefreshSlotMenu();
                return;
            }
            RunnerCampaignSaveStore.Delete(settings, slot);
            _deleteArmedSlot = -1;
            _slotNotice.text = $"슬롯 {slot}을 삭제했습니다.";
            RefreshSlotMenu();
        }

        private void BuildSlotMenu()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            _slotPanel = new GameObject("Save Slot Menu", typeof(RectTransform), typeof(Image));
            _slotPanel.transform.SetParent(canvas.transform, false);
            RectTransform panelRect = _slotPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one; panelRect.offsetMin = Vector2.zero; panelRect.offsetMax = Vector2.zero;
            _slotPanel.GetComponent<Image>().color = new Color(.025f, .035f, .06f, .97f);
            CreateSlotText(_slotPanel.transform, "게임 시작", 43, new Vector2(0, 225), new Vector2(650, 70));
            _slotNotice = CreateSlotText(_slotPanel.transform, "이어할 저장을 선택하거나 빈 슬롯에서 새 게임을 시작하세요.", 19, new Vector2(0, 175), new Vector2(850, 50));
            _slotPanel.SetActive(false);
        }

        private void RefreshSlotMenu()
        {
            foreach (Transform child in _slotPanel.transform.Cast<Transform>().Where(child => child.name.StartsWith("Slot Row")).ToArray())
                Destroy(child.gameObject);
            IReadOnlyList<RunnerSaveSlotInfo> slots = RunnerCampaignSaveStore.GetSlotInfos(settings);
            for (int index = 0; index < slots.Count; index++)
            {
                RunnerSaveSlotInfo info = slots[index];
                float y = 90f - index * 92f;
                GameObject row = new GameObject($"Slot Row {info.slot}", typeof(RectTransform));
                row.transform.SetParent(_slotPanel.transform, false);
                RectTransform rowRect = row.GetComponent<RectTransform>(); rowRect.sizeDelta = new Vector2(760, 76); rowRect.anchoredPosition = new Vector2(0, y);
                string label = info.exists
                    ? $"슬롯 {info.slot}  이어하기    DAY {info.day}  팔로워 {info.subscribers:N0}  체력 Lv.{info.healthStat}" + (info.recoveredFromBackup ? "  [백업 복구]" : string.Empty)
                    : info.corrupted ? $"슬롯 {info.slot}  손상됨 - 새 게임으로 교체" : $"슬롯 {info.slot}  새 게임";
                int capturedSlot = info.slot;
                bool capturedExists = info.exists;
                CreateSlotButton(row.transform, label, new Vector2(-65, 0), new Vector2(610, 66), () => SelectSlot(capturedSlot, capturedExists));
                if (info.exists || info.corrupted)
                    CreateSlotButton(row.transform, _deleteArmedSlot == info.slot ? "정말 삭제" : "삭제", new Vector2(325, 0), new Vector2(130, 66), () => RequestDeleteSlot(capturedSlot), new Color(.62f, .22f, .28f));
            }
        }

        private TMP_Text CreateSlotText(Transform parent, string value, float size, Vector2 position, Vector2 dimensions)
        {
            GameObject obj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)); obj.transform.SetParent(parent, false);
            TMP_Text text = obj.GetComponent<TMP_Text>(); text.text = value; text.font = statusText != null ? statusText.font : null; text.fontSize = size; text.color = Color.white; text.alignment = TextAlignmentOptions.Center;
            RectTransform rect = obj.GetComponent<RectTransform>(); rect.sizeDelta = dimensions; rect.anchoredPosition = position; return text;
        }

        private void CreateSlotButton(Transform parent, string label, Vector2 position, Vector2 dimensions, UnityEngine.Events.UnityAction action, Color? color = null)
        {
            GameObject obj = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button)); obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>(); rect.sizeDelta = dimensions; rect.anchoredPosition = position;
            obj.GetComponent<Image>().color = color ?? new Color(.13f, .52f, .58f, 1f); obj.GetComponent<Button>().onClick.AddListener(action);
            CreateSlotText(obj.transform, label, 19, Vector2.zero, dimensions - new Vector2(16, 8));
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
