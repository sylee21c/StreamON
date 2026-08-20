using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace StreamOn.Minigames.Runner
{
    [Serializable]
    public sealed class RunnerChatSnapshot
    {
        public string gameState;
        public string events;
        public int score;
        public int highScore;
        public float speed;
        public int health;
        public int maxHealth;
        public int enemiesDefeated;
        public int hitsTaken;
        public float elapsedSeconds;
        public string recentMessages;
    }

    [Serializable] public sealed class RunnerGeneratedChat { public string speakerId; public string message; }
    [Serializable] public sealed class RunnerGeneratedChatBatch { public RunnerGeneratedChat[] messages; }

    public sealed class OpenAiRunnerChatClient
    {
        private readonly string _endpoint;
        private readonly string _model;
        private readonly string _apiKey;

        public OpenAiRunnerChatClient(string endpoint, string model, string apiKey)
        {
            _endpoint = endpoint;
            _model = model;
            _apiKey = apiKey;
        }

        public IEnumerator Generate(IReadOnlyList<RunnerViewerData> viewers, RunnerChatSnapshot snapshot,
            Action<RunnerGeneratedChatBatch> onSuccess, Action<string> onFailure)
        {
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(CreateRequest(viewers, snapshot)));
            using (UnityWebRequest request = new UnityWebRequest(_endpoint, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrWhiteSpace(_apiKey)) request.SetRequestHeader("Authorization", "Bearer " + _apiKey);
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string details = request.downloadHandler?.text ?? string.Empty;
                    if (details.Length > 600) details = details.Substring(0, 600);
                    onFailure?.Invoke($"HTTP {request.responseCode}: {request.error} {details}");
                    yield break;
                }

                OpenAiResponse response;
                try { response = JsonUtility.FromJson<OpenAiResponse>(request.downloadHandler.text); }
                catch (Exception exception)
                {
                    onFailure?.Invoke("Response envelope parsing failed: " + exception.Message);
                    yield break;
                }

                string outputText = response?.output?.SelectMany(item => item.content ?? Array.Empty<OpenAiOutputContent>())
                    .FirstOrDefault(item => item.type == "output_text" && !string.IsNullOrWhiteSpace(item.text))?.text;
                if (string.IsNullOrWhiteSpace(outputText))
                {
                    onFailure?.Invoke(response?.error?.message ?? "The response did not contain output_text.");
                    yield break;
                }

                try
                {
                    RunnerGeneratedChatBatch batch = JsonUtility.FromJson<RunnerGeneratedChatBatch>(outputText);
                    if (batch?.messages == null || batch.messages.Length == 0)
                    {
                        onFailure?.Invoke("The generated chat list was empty.");
                        yield break;
                    }
                    onSuccess?.Invoke(batch);
                }
                catch (Exception exception) { onFailure?.Invoke("Generated chat parsing failed: " + exception.Message); }
            }
        }

        private OpenAiResponseRequest CreateRequest(IReadOnlyList<RunnerViewerData> viewers, RunnerChatSnapshot snapshot)
        {
            return new OpenAiResponseRequest
            {
                model = _model,
                store = false,
                max_output_tokens = 500,
                reasoning = new OpenAiReasoning { effort = "none" },
                input = new[] { Input("system", BuildSystemPrompt(viewers)), Input("user", "현재 게임 상황 JSON:\n" + JsonUtility.ToJson(snapshot)) },
                text = new OpenAiTextOptions
                {
                    format = new OpenAiJsonFormat { type = "json_schema", name = "runner_chat_batch", strict = true, schema = CreateSchema() }
                }
            };
        }

        private static OpenAiInput Input(string role, string text) => new OpenAiInput
        {
            role = role, content = new[] { new OpenAiInputContent { type = "input_text", text = text } }
        };

        private static string BuildSystemPrompt(IReadOnlyList<RunnerViewerData> viewers)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("당신은 한국 인터넷 개인방송의 실제 라이브 채팅을 재현한다.");
            builder.AppendLine("자동 진행형 2D 러너를 보며 아래 접속 중인 시청자만 사용한다.");
            builder.AppendLine("가장 중요한 규칙: 캐릭터 설정을 설명하거나 완성된 문장으로 연기하지 말고, 사람이 순간적으로 친 실제 채팅처럼 쓴다.");
            builder.AppendLine("짧은 반말, 말줄임, 자음, 오타, ㅋㅋ, ㅠㅠ, 물음표를 성격에 맞게 자연스럽게 허용한다. 모두가 같은 표현을 쓰지는 않는다.");
            builder.AppendLine("매번 게임을 분석할 필요가 없다. 단순 리액션, 다른 채팅에 대한 태클, 편들기, 뒷북, 잡담도 섞는다.");
            builder.AppendLine("게임오버 뒤에는 방금 죽은 원인, 점수, 아까 장면, 재도전 여부, 서로의 훈수를 소재로 대화를 이어간다.");
            builder.AppendLine("게임 플레이 중이 아니어도 채팅은 살아 있다. 결과 화면이나 대기 중에는 잡담, 뒷북, 시청자끼리 대화를 자연스럽게 쓴다.");
            builder.AppendLine("최근 채팅이 있으면 약 30% 확률로 그 말에 이어서 답한다. 필요하면 상대 닉네임의 짧은 부분을 부를 수 있다.");
            builder.AppendLine("messages 배열은 실제 표시 순서다. 뒤 메시지는 같은 배열의 앞 메시지에 바로 태클을 걸거나 맞받아칠 수 있다.");
            builder.AppendLine("분탕형은 꼬투리, 반문, 편 가르기로 가벼운 말싸움을 만들 수 있고 다른 시청자는 맞받아치거나 말릴 수 있다.");
            builder.AppendLine("가벼운 '뭔 소리임', '니가 해봐', '왜 긁힘 ㅋㅋ' 수준은 허용한다. 혐오, 차별, 협박, 심한 욕설, 현실 인신공격은 금지한다.");
            builder.AppendLine("예시 느낌: '아 그건 좀', '또 훈수 시작', '방금은 잘했는데?', '둘이 왜 싸움 ㅋㅋ'. 예시를 그대로 반복하지 않는다.");
            builder.AppendLine("사소한 사건은 1개, 일반 사건은 1~2개, 저체력·신기록·게임 오버·말싸움은 2~4개를 만든다.");
            builder.AppendLine("message에는 닉네임 없이 한 줄 35자 이내 한국어 채팅만 쓴다.");
            builder.AppendLine("수치는 정말 자연스러울 때만 실제 제공된 값을 사용한다. 설명문, 따옴표, 마크업, 같은 반응 반복을 금지한다.");
            builder.AppendLine("같은 페르소나 유형의 시청자라도 서로 다른 사람이다. 각자의 말투와 성향을 유지한다.");
            builder.AppendLine("최근 채팅에 나온 사람만 반복 선택하지 말고 관심 상황과 말많음 수치에 따라 발화자를 순환한다.");
            builder.AppendLine("동시에 같은 페르소나 유형의 여러 사람이 말해도 되지만 서로 비슷한 문장을 쓰지 않는다.");
            builder.AppendLine("모든 사람이 매번 말하게 하지 않는다. 접속 중인 개별 시청자:");
            foreach (RunnerViewerData viewer in viewers)
            {
                builder.Append("- ID=").Append(viewer.viewerId).Append(", 유형=").Append(viewer.personaId)
                    .Append(", 닉네임=").Append(viewer.nickname)
                    .Append(", 역할=").Append(viewer.role).Append(", 성격=").Append(viewer.personality)
                    .Append(", 말투=").Append(viewer.speakingStyle).Append(", 관심상황=").Append(viewer.triggerInterests)
                    .Append(", 말많음=").Append(viewer.talkativeness.ToString("0.00"))
                    .Append(", 게임이해도=").Append(viewer.expertise.ToString("0.00"))
                    .Append(", 놀림강도=").Append(viewer.teasing.ToString("0.00"))
                    .Append(", 금지=").Append(viewer.forbiddenPatterns).AppendLine();
            }
            return builder.ToString();
        }

        private static OpenAiObjectSchema CreateSchema() => new OpenAiObjectSchema
        {
            type = "object",
            properties = new OpenAiRootProperties
            {
                messages = new OpenAiArraySchema
                {
                    type = "array", minItems = 1, maxItems = 4,
                    items = new OpenAiMessageSchema
                    {
                        type = "object",
                        properties = new OpenAiMessageProperties
                        {
                            speakerId = new OpenAiStringSchema { type = "string" },
                            message = new OpenAiStringSchema { type = "string" }
                        },
                        required = new[] { "speakerId", "message" }, additionalProperties = false
                    }
                }
            },
            required = new[] { "messages" }, additionalProperties = false
        };

        [Serializable] private sealed class OpenAiResponseRequest { public string model; public bool store; public OpenAiInput[] input; public OpenAiTextOptions text; public OpenAiReasoning reasoning; public int max_output_tokens; }
        [Serializable] private sealed class OpenAiReasoning { public string effort; }
        [Serializable] private sealed class OpenAiInput { public string role; public OpenAiInputContent[] content; }
        [Serializable] private sealed class OpenAiInputContent { public string type; public string text; }
        [Serializable] private sealed class OpenAiTextOptions { public OpenAiJsonFormat format; }
        [Serializable] private sealed class OpenAiJsonFormat { public string type; public string name; public bool strict; public OpenAiObjectSchema schema; }
        [Serializable] private sealed class OpenAiObjectSchema { public string type; public OpenAiRootProperties properties; public string[] required; public bool additionalProperties; }
        [Serializable] private sealed class OpenAiRootProperties { public OpenAiArraySchema messages; }
        [Serializable] private sealed class OpenAiArraySchema { public string type; public OpenAiMessageSchema items; public int minItems; public int maxItems; }
        [Serializable] private sealed class OpenAiMessageSchema { public string type; public OpenAiMessageProperties properties; public string[] required; public bool additionalProperties; }
        [Serializable] private sealed class OpenAiMessageProperties { public OpenAiStringSchema speakerId; public OpenAiStringSchema message; }
        [Serializable] private sealed class OpenAiStringSchema { public string type; }
        [Serializable] private sealed class OpenAiResponse { public OpenAiOutput[] output = Array.Empty<OpenAiOutput>(); public OpenAiError error = new OpenAiError(); }
        [Serializable] private sealed class OpenAiOutput { public OpenAiOutputContent[] content = Array.Empty<OpenAiOutputContent>(); }
        [Serializable] private sealed class OpenAiOutputContent { public string type = string.Empty; public string text = string.Empty; }
        [Serializable] private sealed class OpenAiError { public string message = string.Empty; }
    }
}
