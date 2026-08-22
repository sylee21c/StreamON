using System;
using System.Collections.Generic;
using UnityEngine;

namespace StreamOn.Minigames.Runner
{
    [Serializable]
    public sealed class RunnerWitChoice
    {
        [TextArea(1, 2)] public string text;
        [Range(0, 2)] public int quality;
        [Min(1)] public int minimumTalkingLevel = 1;
    }

    [Serializable]
    public sealed class RunnerWitPrompt
    {
        [TextArea(1, 3)] public string viewerMessage;
        [Tooltip("켜져 있으면 답변하지 않고 넘기는 것이 올바른 반응입니다.")]
        public bool ignoreIsCorrect;
        public List<RunnerWitChoice> choices = new List<RunnerWitChoice>();
    }

    [CreateAssetMenu(fileName = "Runner Wit Interaction Settings", menuName = "STREAM ON/Shared/Wit Interaction Settings")]
    public sealed class RunnerWitInteractionSettings : ScriptableObject
    {
        [Header("Dynamic Generation")]
        [Tooltip("AI 채팅 연결이 가능하면 현재 게임 상황에 맞춘 새 질문과 답변을 생성합니다.")]
        public bool useAiGeneratedPrompts = true;

        [Header("Prompt Timing")]
        [Min(1f)] public float firstPromptDelay = 14f;
        [Min(3f)] public float minimumPromptInterval = 18f;
        [Min(3f)] public float maximumPromptInterval = 28f;
        [Min(2f)] public float levelOneResponseSeconds = 7f;
        [Min(2f)] public float levelTwoResponseSeconds = 8f;
        [Min(2f)] public float levelThreeResponseSeconds = 9f;
        [Min(0)] public int minimumViewers = 1;

        [Header("Editable Prompt Library")]
        public List<RunnerWitPrompt> prompts = new List<RunnerWitPrompt>();

        public float ResponseSeconds(int talkingLevel) => talkingLevel >= 3 ? levelThreeResponseSeconds
            : talkingLevel == 2 ? levelTwoResponseSeconds : levelOneResponseSeconds;
    }

    [Serializable]
    public sealed class RunnerGeneratedWitPrompt
    {
        public string viewerMessage;
        public bool shouldIgnore;
        public RunnerGeneratedWitChoice[] choices = Array.Empty<RunnerGeneratedWitChoice>();
    }

    [Serializable]
    public sealed class RunnerGeneratedWitChoice
    {
        public string text;
        public int quality;
    }
}
