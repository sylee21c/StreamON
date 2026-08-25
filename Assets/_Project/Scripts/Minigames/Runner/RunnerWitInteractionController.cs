using System.Collections;
using System.Collections.Generic;
using System.Linq;
using StreamOn.Minigames.TileArena;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace StreamOn.Minigames.Runner
{
    public sealed class RunnerWitInteractionController : MonoBehaviour
    {
        [Header("Editable Settings")]
        [SerializeField] private RunnerWitInteractionSettings settings;
        [SerializeField] private RunnerCampaignSettings campaignSettings;

        [Header("Scene-authored UI")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text viewerText;
        [SerializeField] private Image timerFill;
        [SerializeField] private TMP_Text feedbackText;
        [SerializeField] private Button[] choiceButtons;
        [SerializeField] private TMP_Text[] choiceLabels;
        [SerializeField] private Button ignoreButton;
        [SerializeField] private TMP_Text ignoreLabel;

        private RunnerBroadcastAudienceController _runnerAudience;
        private TileArenaChatAdapter _tileAudience;
        private PlasticKnightmareBroadcastController _plasticAudience;
        private RunnerChatController _chat;
        private Coroutine _activePrompt;
        private float _nextPromptAt;
        private List<RunnerWitChoice> _visibleChoices;
        private bool _answered;
        private bool _currentIgnoreIsCorrect;
        private float _safeMomentUntil;
        private readonly Queue<string> _recentPromptMessages = new Queue<string>();
        private string _safeMomentContext = "게임 플레이 중 잠깐 여유가 생김";
        private int _correctAnswerStreak;

        public bool IsShowing => _activePrompt != null || (canvasGroup != null && canvasGroup.alpha > 0.01f);

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            _runnerAudience = FindFirstObjectByType<RunnerBroadcastAudienceController>();
            _tileAudience = FindFirstObjectByType<TileArenaChatAdapter>();
            _plasticAudience = FindFirstObjectByType<PlasticKnightmareBroadcastController>();
            _chat = FindFirstObjectByType<RunnerChatController>();
            EnsureIgnoreButton();
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                int index = i;
                choiceButtons[i]?.onClick.AddListener(() => SelectChoice(index));
            }
            ignoreButton?.onClick.AddListener(() => SelectIgnore(false));
            HideImmediate();
            _nextPromptAt = Time.time + (settings != null ? settings.firstPromptDelay : 15f);
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || settings == null || settings.prompts.Count == 0) return;
            if (_activePrompt != null)
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) SelectChoice(0);
                    else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) SelectChoice(1);
                    else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) SelectChoice(2);
                    else if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame) SelectIgnore(false);
                }
                return;
            }
            if (Time.time >= _nextPromptAt && Time.time <= _safeMomentUntil && CanPrompt())
                _activePrompt = StartCoroutine(ShowPrompt());
        }

        public void NotifySafeMoment(string context, float availableSeconds = 2.5f)
        {
            _safeMomentContext = string.IsNullOrWhiteSpace(context) ? "게임 플레이 중 잠깐 여유가 생김" : context;
            _safeMomentUntil = Mathf.Max(_safeMomentUntil, Time.time + Mathf.Max(0.5f, availableSeconds));
        }

        private bool CanPrompt()
        {
            ResolveTargets();
            if (_runnerAudience != null)
                return _runnerAudience.CanShowWitInteraction && _runnerAudience.CurrentViewers >= settings.minimumViewers;
            if (_tileAudience != null) return _tileAudience.CanShowWitInteraction
                && _tileAudience.CurrentViewers >= settings.minimumViewers;
            return _plasticAudience != null && _plasticAudience.CanShowWitInteraction
                && _plasticAudience.CurrentViewers >= settings.minimumViewers;
        }

        private int WitRank
        {
            get
            {
                if (campaignSettings != null && RunnerCampaignSaveStore.TryLoad(campaignSettings, out RunnerCampaignSaveData save))
                    return save.witRank;
                return 0;
            }
        }

        private void ResolveTargets()
        {
            if (_runnerAudience == null) _runnerAudience = FindFirstObjectByType<RunnerBroadcastAudienceController>();
            if (_tileAudience == null) _tileAudience = FindFirstObjectByType<TileArenaChatAdapter>();
            if (_plasticAudience == null) _plasticAudience = FindFirstObjectByType<PlasticKnightmareBroadcastController>();
            if (_chat == null) _chat = FindFirstObjectByType<RunnerChatController>();
        }

        private IEnumerator ShowPrompt()
        {
            RunnerWitPrompt prompt = null;
            if (settings.useAiGeneratedPrompts && _chat != null && _chat.AiEnabled)
            {
                RunnerGeneratedWitPrompt generated = null;
                yield return _chat.GenerateWitInteraction(_safeMomentContext, _recentPromptMessages, WitRank,
                    value => generated = value);
                prompt = ConvertGeneratedPrompt(generated);
            }
            if (prompt == null) prompt = PickLocalPrompt();
            prompt = PreparePromptForWitRank(prompt);
            if (prompt == null)
            {
                ScheduleNext();
                _activePrompt = null;
                yield break;
            }
            _visibleChoices = prompt.choices.Where(choice => choice != null)
                .OrderBy(_ => Random.value).Take(3).ToList();
            if (_visibleChoices.Count != 3)
            {
                ScheduleNext();
                _activePrompt = null;
                yield break;
            }
            _currentIgnoreIsCorrect = prompt.ignoreIsCorrect;

            string nickname = _chat != null ? _chat.PickDonationViewerNickname() : "시청자";
            viewerText.text = $"{nickname}  {prompt.viewerMessage}";
            RememberPrompt(prompt.viewerMessage);
            feedbackText.text = string.Empty;
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                bool show = i < _visibleChoices.Count;
                choiceButtons[i].gameObject.SetActive(show);
                if (show) choiceLabels[i].text = $"{i + 1}. {_visibleChoices[i].text}";
            }
            if (ignoreButton != null) ignoreButton.gameObject.SetActive(true);
            if (ignoreLabel != null) ignoreLabel.text = "4. 무반응";
            SetPromptVisible(Time.timeScale > 0f, true);
            _answered = false;
            _chat?.React(RunnerChatEvent.WitPrompt);

            WitRankRule witRule = campaignSettings != null ? campaignSettings.WitRule(WitRank) : null;
            float remaining = settings.ResponseSeconds(1) + (witRule != null ? witRule.responseTimeBonusSeconds : 0f);
            float duration = remaining;
            while (!_answered && remaining > 0f && CanPrompt())
            {
                if (Time.timeScale <= 0f)
                {
                    SetPromptVisible(false, false);
                    yield return null;
                    continue;
                }
                SetPromptVisible(true, true);
                remaining -= Time.unscaledDeltaTime;
                if (timerFill != null) timerFill.fillAmount = Mathf.Clamp01(remaining / duration);
                yield return null;
            }
            if (!_answered) SelectIgnore(true);
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            yield return HoldFeedbackVisible(_answered ? 1.4f : 0.7f);
            HideImmediate();
            ScheduleNext();
            _activePrompt = null;
        }

        private RunnerWitPrompt ConvertGeneratedPrompt(RunnerGeneratedWitPrompt generated)
        {
            if (generated == null || string.IsNullOrWhiteSpace(generated.viewerMessage)
                || generated.choices == null || generated.choices.Length < 3) return null;
            return new RunnerWitPrompt
            {
                viewerMessage = Crop(generated.viewerMessage, 42),
                ignoreIsCorrect = false,
                choices = generated.choices.Select(choice => new RunnerWitChoice
                {
                    text = Crop(choice.text, 42),
                    quality = Mathf.Clamp(choice.quality, 0, 2),
                    minimumTalkingLevel = 1
                }).Where(choice => !string.IsNullOrWhiteSpace(choice.text)).ToList()
            };
        }

        private RunnerWitPrompt PreparePromptForWitRank(RunnerWitPrompt prompt)
        {
            if (prompt == null || prompt.choices == null) return null;
            RunnerWitChoice[] source = prompt.choices
                .Where(choice => choice != null && !string.IsNullOrWhiteSpace(choice.text)).ToArray();
            List<RunnerWitChoice> strong = source.Where(choice => choice.quality >= 2).ToList();
            List<RunnerWitChoice> ordinary = source.Where(choice => choice.quality == 1).ToList();
            List<RunnerWitChoice> poor = source.Where(choice => choice.quality <= 0).ToList();
            if (strong.Count == 0 || ordinary.Count == 0) return null;

            WitRankRule rule = campaignSettings != null ? campaignSettings.WitRule(WitRank) : null;
            bool addSecondStrong = strong.Count >= 2 && rule != null && Random.value < rule.twoCorrectAnswerChance;
            List<RunnerWitChoice> selected = new List<RunnerWitChoice>
            {
                PickRandom(strong),
                PickRandom(ordinary)
            };
            if (addSecondStrong)
                selected.Add(PickRandom(strong.Where(choice => choice != selected[0]).ToList()));
            else if (poor.Count > 0) selected.Add(PickRandom(poor));
            else if (ordinary.Count > 1)
                selected.Add(PickRandom(ordinary.Where(choice => choice != selected[1]).ToList()));
            else if (strong.Count > 1)
                selected.Add(PickRandom(strong.Where(choice => choice != selected[0]).ToList()));
            if (selected.Count != 3 || selected.Any(choice => choice == null)) return null;

            return new RunnerWitPrompt
            {
                viewerMessage = prompt.viewerMessage,
                ignoreIsCorrect = false,
                choices = selected.Select(choice => new RunnerWitChoice
                {
                    text = choice.text,
                    quality = Mathf.Clamp(choice.quality, 0, 2),
                    minimumTalkingLevel = 1
                }).ToList()
            };
        }

        private static RunnerWitChoice PickRandom(IReadOnlyList<RunnerWitChoice> choices) =>
            choices != null && choices.Count > 0 ? choices[Random.Range(0, choices.Count)] : null;

        private static string Crop(string value, int maximum) => string.IsNullOrWhiteSpace(value)
            ? string.Empty : value.Trim().Substring(0, Mathf.Min(value.Trim().Length, maximum));

        private RunnerWitPrompt PickLocalPrompt()
        {
            RunnerWitPrompt[] candidates = settings.prompts.Where(prompt => prompt != null
                && !_recentPromptMessages.Any(history => history.StartsWith(prompt.viewerMessage + " ||"))).ToArray();
            if (candidates.Length == 0) candidates = settings.prompts.Where(prompt => prompt != null).ToArray();
            return candidates.Length > 0 ? candidates[Random.Range(0, candidates.Length)] : null;
        }

        private void RememberPrompt(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            string choices = _visibleChoices != null ? string.Join(" / ", _visibleChoices.Select(choice => choice.text)) : string.Empty;
            _recentPromptMessages.Enqueue(message.Trim() + " || " + choices);
            while (_recentPromptMessages.Count > 4) _recentPromptMessages.Dequeue();
        }

        private void SelectChoice(int index)
        {
            if (_activePrompt == null || _answered || _visibleChoices == null || index < 0 || index >= _visibleChoices.Count) return;
            _answered = true;
            int quality = _visibleChoices[index].quality;
            ApplyWitQuality(quality, quality >= 2 ? "채팅 반응이 확 살아났다!"
                : quality == 1 ? "무난하게 넘어갔다" : "채팅창이 잠깐 조용해졌다...");
        }

        private void SelectIgnore(bool timedOut)
        {
            if (_activePrompt == null || _answered) return;
            _answered = true;
            bool correct = _currentIgnoreIsCorrect;
            string feedback = correct
                ? timedOut ? "대꾸하지 않자 분탕 채팅이 묻혔다" : "괜히 받아주지 않자 채팅이 넘어갔다"
                : timedOut ? "대답할 타이밍을 놓쳤다..." : "받아칠 만한 채팅을 그냥 넘겼다...";
            ApplyWitQuality(correct ? 2 : 0, feedback);
        }

        private void ApplyWitQuality(int quality, string feedback)
        {
            WitRankRule rule = campaignSettings != null ? campaignSettings.WitRule(WitRank) : null;
            if (quality >= 2)
            {
                _correctAnswerStreak++;
                float currentHeat = _runnerAudience != null ? _runnerAudience.Hype
                    : _tileAudience != null ? _tileAudience.Hype
                    : _plasticAudience != null ? _plasticAudience.Heat : 50f;
                if (rule != null && rule.comebackHeatThreshold > 0f && currentHeat <= rule.comebackHeatThreshold)
                {
                    quality = 5;
                    feedback = "낮아진 분위기를 한 번에 다시 살렸다!";
                }
                else if (rule != null && rule.correctStreakRequired > 0
                    && _correctAnswerStreak % rule.correctStreakRequired == 0)
                {
                    quality = 4;
                    feedback = "연속으로 제대로 받아치며 채팅 흐름을 탔다!";
                }
                else if (WitRank >= 5) quality = 3;
            }
            else _correctAnswerStreak = 0;
            feedbackText.text = feedback;
            _runnerAudience?.ApplyWitInteraction(quality);
            _tileAudience?.ApplyWitInteraction(quality);
            _plasticAudience?.ApplyWitInteraction(quality);
            if (quality >= 2 && campaignSettings != null
                && RunnerCampaignSaveStore.TryLoad(campaignSettings, out RunnerCampaignSaveData save))
            {
                BroadcasterProgression.AddBroadcastExperience(campaignSettings, save, campaignSettings.correctWitExperience);
                RunnerCampaignSaveStore.Save(campaignSettings, save, true);
            }
        }

        private void EnsureIgnoreButton()
        {
            if (ignoreButton != null)
            {
                if (ignoreLabel == null) ignoreLabel = ignoreButton.GetComponentInChildren<TMP_Text>(true);
                return;
            }
            Debug.LogWarning("재치 UI의 4번 무반응 버튼이 씬에 연결되지 않았습니다.", this);
        }

        private void ScheduleNext() => _nextPromptAt = Time.time
            + Random.Range(settings.minimumPromptInterval, Mathf.Max(settings.minimumPromptInterval, settings.maximumPromptInterval));

        private IEnumerator HoldFeedbackVisible(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < Mathf.Max(0f, seconds))
            {
                bool unpaused = Time.timeScale > 0f;
                SetPromptVisible(unpaused, false);
                if (unpaused) elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void SetPromptVisible(bool visible, bool interactable)
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible && interactable;
            canvasGroup.blocksRaycasts = visible && interactable;
        }

        private void HideImmediate()
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            if (timerFill != null) timerFill.fillAmount = 1f;
        }
    }
}
