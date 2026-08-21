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
            _save.mental = Mathf.Clamp(_save.mental, settings.minimumMentalToStartBroadcast, settings.maximumMental);
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
            settings.AddStatExperience(ref _save.gameSkill, ref _save.gameSkillExperience,
                action.gameSkillDelta, settings.maximumGameSkill);
            settings.AddStatExperience(ref _save.talkingSkill, ref _save.talkingSkillExperience,
                action.talkingSkillDelta, settings.maximumTalkingSkill);
            settings.AddStatExperience(ref _save.healthStat, ref _save.healthStatExperience,
                action.healthStatDelta, settings.maximumHealthStat);
            _save.mental = Mathf.Clamp(_save.mental + action.mentalDelta, settings.minimumMentalToStartBroadcast, settings.maximumMental);
            _save.subscribers = Mathf.Max(settings.minimumSubscribersToStartBroadcast, _save.subscribers + action.subscriberDelta);
            _save.selectedAction = action.displayName;
            _save.broadcastPending = true;
            RunnerCampaignSaveStore.Save(settings, _save);
            _notice = $"{action.displayName} 완료! 방송할 게임을 선택하세요.";
            RefreshStatus();
            ShowGameSelection(true);
        }

        public void SelectRunnerGame() => LoadSelectedGame("runner",
            string.IsNullOrWhiteSpace(settings.runnerSceneName) ? settings.broadcastSceneName : settings.runnerSceneName);

        public void SelectTileArenaGame() => LoadSelectedGame("tile_arena", settings.tileArenaSceneName);

        private void LoadSelectedGame(string gameId, string sceneName)
        {
            if (_save == null || string.IsNullOrWhiteSpace(sceneName)) return;
            _save.selectedBroadcastGame = gameId;
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
            if (transitionFade != null)
            {
                transitionFade.blocksRaycasts = true;
                float startedAt = Time.unscaledTime;
                while (transitionFade.alpha < 1f)
                {
                    transitionFade.alpha = Mathf.Clamp01((Time.unscaledTime - startedAt) / Mathf.Max(0.1f, fadeDuration));
                    yield return null;
                }
            }
            gameSelectionPanel.SetActive(true);
            _transitioning = false;
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

        private void RefreshStatus()
        {
            if (_save == null) return;
            if (statusText != null)
                statusText.text = $"DAY {_save.day}    팔로워 {_save.subscribers:N0}    누적 후원 {_save.lifetimeDonations:N0}원";
            if (statGaugePanel != null) statGaugePanel.SetActive(true);
            mentalGauge?.SetValue(_save.mental, settings.maximumMental);
            gameSkillGauge?.SetLevelProgress(_save.gameSkill, _save.gameSkillExperience,
                settings.ExperienceRequiredForLevel(_save.gameSkill), settings.maximumGameSkill);
            talkingSkillGauge?.SetLevelProgress(_save.talkingSkill, _save.talkingSkillExperience,
                settings.ExperienceRequiredForLevel(_save.talkingSkill), settings.maximumTalkingSkill);
            healthGauge?.SetLevelProgress(_save.healthStat, _save.healthStatExperience,
                settings.ExperienceRequiredForLevel(_save.healthStat), settings.maximumHealthStat);
        }
    }
}
