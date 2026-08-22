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
        private RunnerChatController _chat;
        private Coroutine _activePrompt;
        private float _nextPromptAt;
        private List<RunnerWitChoice> _visibleChoices;
        private bool _answered;
        private bool _currentIgnoreIsCorrect;
        private float _safeMomentUntil;
        private readonly Queue<string> _recentPromptMessages = new Queue<string>();
        private string _safeMomentContext = "게임 플레이 중 잠깐 여유가 생김";

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            _runnerAudience = FindFirstObjectByType<RunnerBroadcastAudienceController>();
            _tileAudience = FindFirstObjectByType<TileArenaChatAdapter>();
            _chat = FindFirstObjectByType<RunnerChatController>();
            EnsureIgnoreButton();
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                int index = i;
                choiceButtons[i]?.onClick.AddListener(() => SelectChoice(index));
            }
            ignoreButton?.onClick.AddListener(() => SelectIgnore(false));
            HideImmediate();
            _nextPromptAt = Time.unscaledTime + (settings != null ? settings.firstPromptDelay : 15f);
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
            if (Time.unscaledTime >= _nextPromptAt && Time.unscaledTime <= _safeMomentUntil && CanPrompt())
                _activePrompt = StartCoroutine(ShowPrompt());
        }

        public void NotifySafeMoment(string context, float availableSeconds = 2.5f)
        {
            _safeMomentContext = string.IsNullOrWhiteSpace(context) ? "게임 플레이 중 잠깐 여유가 생김" : context;
            _safeMomentUntil = Mathf.Max(_safeMomentUntil, Time.unscaledTime + Mathf.Max(0.5f, availableSeconds));
        }

        private bool CanPrompt()
        {
            ResolveTargets();
            if (_runnerAudience != null)
                return _runnerAudience.CanShowWitInteraction && _runnerAudience.CurrentViewers >= settings.minimumViewers;
            return _tileAudience != null && _tileAudience.CanShowWitInteraction
                && _tileAudience.CurrentViewers >= settings.minimumViewers;
        }

        private int TalkingLevel => _runnerAudience != null ? _runnerAudience.TalkingSkill
            : _tileAudience != null ? _tileAudience.TalkingSkill : 1;

        private void ResolveTargets()
        {
            if (_runnerAudience == null) _runnerAudience = FindFirstObjectByType<RunnerBroadcastAudienceController>();
            if (_tileAudience == null) _tileAudience = FindFirstObjectByType<TileArenaChatAdapter>();
            if (_chat == null) _chat = FindFirstObjectByType<RunnerChatController>();
        }

        private IEnumerator ShowPrompt()
        {
            RunnerWitPrompt prompt = null;
            if (settings.useAiGeneratedPrompts && _chat != null && _chat.AiEnabled)
            {
                RunnerGeneratedWitPrompt generated = null;
                yield return _chat.GenerateWitInteraction(_safeMomentContext, _recentPromptMessages, value => generated = value);
                prompt = ConvertGeneratedPrompt(generated);
            }
            if (prompt == null) prompt = PickLocalPrompt();
            prompt = PreparePromptForTalkingLevel(prompt);
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
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            _answered = false;
            _chat?.React(RunnerChatEvent.WitPrompt);

            float remaining = settings.ResponseSeconds(TalkingLevel);
            float duration = remaining;
            while (!_answered && remaining > 0f && CanPrompt())
            {
                remaining -= Time.unscaledDeltaTime;
                if (timerFill != null) timerFill.fillAmount = Mathf.Clamp01(remaining / duration);
                yield return null;
            }
            if (!_answered) SelectIgnore(true);
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            yield return new WaitForSecondsRealtime(_answered ? 1.4f : 0.7f);
            HideImmediate();
            ScheduleNext();
            _activePrompt = null;
        }

        private RunnerWitPrompt ConvertGeneratedPrompt(RunnerGeneratedWitPrompt generated)
        {
            if (generated == null || string.IsNullOrWhiteSpace(generated.viewerMessage)
                || generated.choices == null || generated.choices.Length < 3) return null;
            RunnerGeneratedWitChoice[] ordered = generated.choices.OrderByDescending(choice => choice.quality).Take(3).ToArray();
            return new RunnerWitPrompt
            {
                viewerMessage = Crop(generated.viewerMessage, 42),
                ignoreIsCorrect = generated.shouldIgnore,
                choices = ordered.Select(choice => new RunnerWitChoice
                {
                    text = Crop(choice.text, 42),
                    quality = Mathf.Clamp(choice.quality, 0, 2),
                    minimumTalkingLevel = 1
                }).Where(choice => !string.IsNullOrWhiteSpace(choice.text)).ToList()
            };
        }

        private RunnerWitPrompt PreparePromptForTalkingLevel(RunnerWitPrompt prompt)
        {
            if (prompt == null || prompt.choices == null) return null;
            RunnerWitChoice[] source = prompt.choices.Where(choice => choice != null && !string.IsNullOrWhiteSpace(choice.text))
                .Take(3).ToArray();
            if (source.Length != 3) return null;

            int[] ranked = Enumerable.Range(0, source.Length).OrderByDescending(index => source[index].quality).ToArray();
            int successfulAnswers = prompt.ignoreIsCorrect ? 0 : Mathf.Clamp(TalkingLevel, 1, 3);
            Dictionary<int, int> rankByIndex = ranked.Select((sourceIndex, rank) => new { sourceIndex, rank })
                .ToDictionary(item => item.sourceIndex, item => item.rank);
            return new RunnerWitPrompt
            {
                viewerMessage = prompt.viewerMessage,
                ignoreIsCorrect = prompt.ignoreIsCorrect,
                choices = source.Select((choice, index) => new RunnerWitChoice
                {
                    text = choice.text,
                    quality = rankByIndex[index] < successfulAnswers ? 2
                        : !prompt.ignoreIsCorrect && rankByIndex[index] == successfulAnswers ? 1 : 0,
                    minimumTalkingLevel = 1
                }).ToList()
            };
        }

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
            if (quality >= 2 && TalkingLevel >= 3) quality = 3;
            feedbackText.text = feedback;
            _runnerAudience?.ApplyWitInteraction(quality);
            _tileAudience?.ApplyWitInteraction(quality);
        }

        private void EnsureIgnoreButton()
        {
            if (ignoreButton != null)
            {
                if (ignoreLabel == null) ignoreLabel = ignoreButton.GetComponentInChildren<TMP_Text>(true);
                return;
            }
            Button template = choiceButtons?.LastOrDefault(button => button != null);
            if (template == null) return;

            GameObject clone = Instantiate(template.gameObject, template.transform.parent);
            clone.name = "Choice 4 - Ignore";
            ignoreButton = clone.GetComponent<Button>();
            ignoreLabel = clone.GetComponentInChildren<TMP_Text>(true);
            RectTransform ignoreRect = clone.GetComponent<RectTransform>();
            RectTransform templateRect = template.GetComponent<RectTransform>();
            if (ignoreRect != null && templateRect != null)
                ignoreRect.anchoredPosition = templateRect.anchoredPosition + Vector2.down * 35f;
            Image background = clone.GetComponent<Image>();
            if (background != null) background.color = new Color(0.16f, 0.17f, 0.22f, 1f);

            RectTransform panel = transform as RectTransform;
            if (panel != null) panel.sizeDelta = new Vector2(panel.sizeDelta.x, panel.sizeDelta.y + 75f);
            RectTransform feedbackRect = feedbackText != null ? feedbackText.rectTransform : null;
            if (feedbackRect != null) feedbackRect.anchoredPosition += Vector2.down * 38f;
        }

        private void ScheduleNext() => _nextPromptAt = Time.unscaledTime
            + Random.Range(settings.minimumPromptInterval, Mathf.Max(settings.minimumPromptInterval, settings.maximumPromptInterval));

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
