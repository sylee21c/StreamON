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
        public string gameTitle;
        public string gameState;
        public int campaignDay;
        public int campaignMaximumDays;
        public bool campaignEndless;
        public int subscribers;
        public float mental;
        public int gameSkill;
        public int talkingSkill;
        public int healthStat;
        public int targetScore;
        public int campaignBestScore;
        public string selectedDayAction;
        public string events;
        public int score;
        public int highScore;
        public int blueTilesRemaining;
        public float speed;
        public int health;
        public int maxHealth;
        public int enemiesDefeated;
        public int hitsTaken;
        public float elapsedSeconds;
        public float broadcastSecondsRemaining;
        public float broadcastDurationSeconds;
        public string runEndReason;
        public int currentViewers;
        public int chattingViewers;
        public int peakViewers;
        public int totalVisitors;
        public float broadcastHype;
        public float broadcastRating;
        public int donationWon;
        public string recentMessages;
    }

    [Serializable] public sealed class RunnerGeneratedChat { public string speakerId; public string message; }
    [Serializable] public sealed class RunnerGeneratedChatBatch { public RunnerGeneratedChat[] messages; }

    public sealed class OpenAiRunnerChatClient
    {
        private const string RealChatReferenceCorpus = @"타카미야 마나 | 불로도 못 녹이는 얼음 ㄷㄷㄷ
타카미야 마나 | 기적의 조작감
사자소생 | 아까 거기가 안되는게 맞네요
사자소생 | 여긴 잘되는데
타카미야 마나 | ㄹㅇ
슬라점프 | 이걸 어떻게 알아
슬라점프 | ㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋ
타카미야 마나 | 오
사자소생 | ?
노멀맨 | ㅇㅎ
슬라점프 | 어?
슬라점프 | 그냥 못가네
이일욱 | 저 구간
이일욱 | 또 당했다
이일욱 | 소류겐까지 쓰면 삼단점프 ㄷㄷ
밥먹는 소환사 6026920 | 이거 2시간전에도 봤었는데 ㅋㅋㅋ
자랭 | 챱
짧은 명란젓 나나 | ㅋㅋㅋㅋㅋㅋ
자랭 | 다시 건실하게 올라가보자
체리셔 | 기름 잘 넘겼는데 ㅠ
베들링턴 | 나은 거 맞나
해시레 | 휴~
해둥펀치 | 아쉽
온나라메모보고 | 게임이 친절하네 세이브포인트도 있고
미스터꽃 | 그래
핀딘 | 휴
밀덕마로 | 어디까지 내려가는거에요?
오로로롯 | 오
고독한히나 | 이걸 살려줘?
한마 부키 | 개억빠
치킨마요버터 | ㄲㅂ
썬리오 | 대 허 수
히나나나 | 어어
해둥검성빌리 | 태~~~~~~~~초
김감자ㅏ | 어디까지 가냐 ㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋ
마음을 나누어요 | 안일해 안일
목욕탕수건도둑박종우 | 양쪽 다 열었고
pix11 | 따봉하면 효과 뭔가요 데쌤들
르에냥 | 냉판해
눈눈곡곡 | 슬슬...
애플몽몽 | 걍 줘
뭔지모르겠지만 | b
춘탬삡뫄 | 1따봉
이 그린 | 어
넌감자 | 맵이 좀 창틀이 많긴하다
생도 | 그냥 두고 가
adksd1 | 잘했는데?
한앙뚜 | ㅇㅇ잘했어
노벨브라이트 | 잘 내림
SoboroBang | 맞음
int1999 | 그건 맞는데
ㅎㅆ | 걍 몰라서 당했다
쿠쿠쿤 | 판단은 좋앗다
int1999 | 못할떈 왤케못해 ㅋㅋ
호무새는사실사람이다 | 니르한테 풀딜 ㅋㅋ
더블락 | ㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋ
도쿠로38 | 힘 좋은거 보소
더블락 | 와우 무빙
삼겹소쥬 | 어김없이 누워있는 용
라테시온 | 니또죽
혀눨 | 딜은 널널하네
실뭉터기 | 타이밍 봐바ㅋㅋㅋ 진짜 딱 줄어듦ㅋㅋ
나베아 | 억울할만혀 ㅋㅋ
바쁜 우주인 452812 | 아니 타이밍이
더블락 | 타이밍 쩔긴했어
공허의지팡이 | 개억울해하네 ㅋㅋㅋ
러버똘마니쿠엥등잡덕 | 전 가만히 있었습니다
시바학개론 | 난 안 그랬어!
시바학개론 | 난 죄가 없다!
시청자123412 | 휴 안 쳤다 ㅎㅎ
원트원클 | ^^7
바쁜 우주인 452812 | ㄷㄷㄷ
아코너 | 부검 ㄱㄱ 난 챗 안 쳤으 ㅁㅋㅋㅋㅋㅋ
Marcusjun | 아
려아04 | 아하
라테시온 | ?";

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

        public IEnumerator GenerateWit(RunnerChatSnapshot snapshot, IReadOnlyCollection<string> recentPrompts,
            Action<RunnerGeneratedWitPrompt> onSuccess, Action<string> onFailure)
        {
            WitResponseRequest payload = new WitResponseRequest
            {
                model = _model,
                store = false,
                max_output_tokens = 350,
                reasoning = new OpenAiReasoning { effort = "none" },
                input = new[]
                {
                    Input("system", "한국 게임 방송 중 시청자와 스트리머의 짧은 재치 상호작용을 만든다. "
                        + "현재 상황에서 실제 시청자가 막 쓸 법한 질문·놀림·반응 한 줄을 만들고, 스트리머 답변 3개를 만든다. "
                        + "사건을 그대로 낭독하거나 설명문처럼 쓰지 않는다. 기존 질문을 반복하지 않는다. "
                        + "첫 답변은 자연스럽고 재치 있는 답변(quality 2), 둘째는 무난한 답변(quality 1), "
                        + "셋째는 분위기를 어색하게 만드는 답변(quality 0)이어야 한다. 각 문장은 35자 이내다."),
                    Input("user", "현재 상황:\n" + JsonUtility.ToJson(snapshot)
                        + "\n최근 사용해서 반복 금지인 질문:\n" + string.Join(" | ", recentPrompts ?? Array.Empty<string>()))
                },
                text = new WitTextOptions
                {
                    verbosity = "low",
                    format = new WitJsonFormat { type = "json_schema", name = "wit_interaction", strict = true, schema = CreateWitSchema() }
                }
            };

            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            using (UnityWebRequest request = new UnityWebRequest(_endpoint, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrWhiteSpace(_apiKey)) request.SetRequestHeader("Authorization", "Bearer " + _apiKey);
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    onFailure?.Invoke($"HTTP {request.responseCode}: {request.error}");
                    yield break;
                }
                OpenAiResponse response = JsonUtility.FromJson<OpenAiResponse>(request.downloadHandler.text);
                string outputText = response?.output?.SelectMany(item => item.content ?? Array.Empty<OpenAiOutputContent>())
                    .FirstOrDefault(item => item.type == "output_text" && !string.IsNullOrWhiteSpace(item.text))?.text;
                if (string.IsNullOrWhiteSpace(outputText))
                {
                    onFailure?.Invoke("The wit response did not contain output_text.");
                    yield break;
                }
                RunnerGeneratedWitPrompt result = JsonUtility.FromJson<RunnerGeneratedWitPrompt>(outputText);
                if (result == null || string.IsNullOrWhiteSpace(result.viewerMessage)
                    || result.choices == null || result.choices.Length != 3)
                {
                    onFailure?.Invoke("The generated wit interaction was incomplete.");
                    yield break;
                }
                onSuccess?.Invoke(result);
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
                input = new[]
                {
                    Input("system", BuildSystemPrompt(viewers)),
                    Input("user", "현재 게임 상황 JSON:\n" + JsonUtility.ToJson(snapshot)
                        + "\n\n이번 요청의 출력 형태(반드시 따름):\n" + BuildOutputShapeDirective())
                },
                text = new OpenAiTextOptions
                {
                    verbosity = "low",
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
            builder.AppendLine("아래 실제 한국 게임방송 채팅 표본과 구별하기 어려운 새 채팅을 만든다.");
            builder.AppendLine("JSON 사건을 문장으로 다시 읽지 않는다. 보이는 사실의 원인·결과를 친절하게 설명하지 않는다.");
            builder.AppendLine("금지 예: '적을 처치했네', '체력이 얼마 안 남았네', '죽었는데 신기록 갱신 ㅋㅋ', '점수가 올랐네'.");
            builder.AppendLine("실제 시청자처럼 반사 반응, 웃음, 물음표, 생략된 평가, 앞 채팅 동조·반박, 가끔만 훈수한다.");
            builder.AppendLine("긴 ㅋㅋ만 있는 줄, '?', '오', '아', '휴', '어어'도 완전한 정상 메시지다. 억지로 정보를 덧붙이지 않는다.");
            builder.AppendLine("여러 메시지면 적어도 하나는 1~6자의 원초 반응이어야 하고, 완성된 설명문은 최대 하나다.");
            builder.AppendLine("최근 채팅에 답할 때 닉네임을 부르지 말고 단어를 되받거나 'ㅇㅇ', '맞음', '그건 아닌듯'처럼 잇는다.");
            builder.AppendLine("타일 아레나의 패턴은 매번 무작위로 교체된다. 패턴 번호는 진행도·난이도·도달 단계가 아니며 채팅에서 숫자, '벌써', '몇 스테이지', 기록 진척으로 절대 언급하지 않는다.");
            builder.AppendLine("시청자별 수치는 발화자 선택의 약한 확률일 뿐이며 고정 역할을 연기하지 않는다.");
            builder.AppendLine("message에는 닉네임 없이 한 줄 35자 이내 한국어 채팅만 쓴다. 설명, 따옴표, 괄호 연기, 마크업은 금지한다.");
            builder.AppendLine("혐오, 차별, 협박, 심한 욕설, 성적 표현, 현실 인신공격은 금지한다. 가벼운 놀림과 의견 충돌까지만 허용한다.");
            builder.AppendLine("\n=== 실제 수집 로그: 닉네임 | 채팅 ===");
            builder.AppendLine(RealChatReferenceCorpus);
            builder.AppendLine("=== 실제 수집 로그 끝 ===");
            builder.AppendLine("위 로그의 게임 고유명사와 사실은 무시한다. 닉네임 작명 감각, 길이, 생략, 반응 밀도만 재현한다.");
            builder.AppendLine("예시 닉네임과 문장을 그대로 복사하지 않는다. speakerId는 반드시 아래 현재 시청자의 ID를 한 글자도 바꾸지 않고 사용한다.");
            builder.AppendLine("현재 말할 수 있는 시청자:");
            foreach (RunnerViewerData viewer in viewers)
            {
                builder.Append("- ID=").Append(viewer.viewerId).Append(", 닉네임=").Append(viewer.nickname)
                    .Append(", 말많음=").Append(viewer.talkativeness.ToString("0.00"))
                    .Append(", 게임이해도=").Append(viewer.expertise.ToString("0.00"))
                    .Append(", 놀림강도=").Append(viewer.teasing.ToString("0.00"))
                    .Append(", 금지=").Append(viewer.forbiddenPatterns).AppendLine();
            }
            return builder.ToString();
        }

        private static string BuildOutputShapeDirective()
        {
            int roll = UnityEngine.Random.Range(0, 100);
            if (roll < 22)
                return "원초 반응형. 첫 message는 반드시 1~6자다. 웃기거나 어이없으면 ㅋㅋ만 길게 써도 되고, 아니면 오/아/?/어어/휴 같은 반응만 쓴다. 첫 message를 완성문으로 만들지 않는다.";
            if (roll < 52)
                return "짧은 파편형. 첫 message는 2~10자의 생략되거나 끝맺지 않은 채팅이다. 사건을 주어+서술어로 설명하지 않는다.";
            if (roll < 78)
                return "채팅 흐름형. recentMessages의 말 한 조각을 되받아 동조하거나 반박한다. 닉네임 호출과 친절한 설명은 금지한다.";
            return "짧은 의견형. 상황에 대한 평가나 훈수를 18자 이내로 쓴다. JSON에 적힌 사건 자체를 그대로 낭독하지 않는다.";
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

        private static WitObjectSchema CreateWitSchema() => new WitObjectSchema
        {
            type = "object",
            properties = new WitRootProperties
            {
                viewerMessage = new OpenAiStringSchema { type = "string" },
                choices = new WitArraySchema
                {
                    type = "array", minItems = 3, maxItems = 3,
                    items = new WitChoiceSchema
                    {
                        type = "object",
                        properties = new WitChoiceProperties
                        {
                            text = new OpenAiStringSchema { type = "string" },
                            quality = new OpenAiIntegerSchema { type = "integer", minimum = 0, maximum = 2 }
                        },
                        required = new[] { "text", "quality" }, additionalProperties = false
                    }
                }
            },
            required = new[] { "viewerMessage", "choices" }, additionalProperties = false
        };

        [Serializable] private sealed class OpenAiResponseRequest { public string model; public bool store; public OpenAiInput[] input; public OpenAiTextOptions text; public OpenAiReasoning reasoning; public int max_output_tokens; }
        [Serializable] private sealed class OpenAiReasoning { public string effort; }
        [Serializable] private sealed class OpenAiInput { public string role; public OpenAiInputContent[] content; }
        [Serializable] private sealed class OpenAiInputContent { public string type; public string text; }
        [Serializable] private sealed class OpenAiTextOptions { public string verbosity; public OpenAiJsonFormat format; }
        [Serializable] private sealed class OpenAiJsonFormat { public string type; public string name; public bool strict; public OpenAiObjectSchema schema; }
        [Serializable] private sealed class OpenAiObjectSchema { public string type; public OpenAiRootProperties properties; public string[] required; public bool additionalProperties; }
        [Serializable] private sealed class OpenAiRootProperties { public OpenAiArraySchema messages; }
        [Serializable] private sealed class OpenAiArraySchema { public string type; public OpenAiMessageSchema items; public int minItems; public int maxItems; }
        [Serializable] private sealed class OpenAiMessageSchema { public string type; public OpenAiMessageProperties properties; public string[] required; public bool additionalProperties; }
        [Serializable] private sealed class OpenAiMessageProperties { public OpenAiStringSchema speakerId; public OpenAiStringSchema message; }
        [Serializable] private sealed class OpenAiStringSchema { public string type; }
        [Serializable] private sealed class OpenAiIntegerSchema { public string type; public int minimum; public int maximum; }
        [Serializable] private sealed class WitResponseRequest { public string model; public bool store; public OpenAiInput[] input; public WitTextOptions text; public OpenAiReasoning reasoning; public int max_output_tokens; }
        [Serializable] private sealed class WitTextOptions { public string verbosity; public WitJsonFormat format; }
        [Serializable] private sealed class WitJsonFormat { public string type; public string name; public bool strict; public WitObjectSchema schema; }
        [Serializable] private sealed class WitObjectSchema { public string type; public WitRootProperties properties; public string[] required; public bool additionalProperties; }
        [Serializable] private sealed class WitRootProperties { public OpenAiStringSchema viewerMessage; public WitArraySchema choices; }
        [Serializable] private sealed class WitArraySchema { public string type; public WitChoiceSchema items; public int minItems; public int maxItems; }
        [Serializable] private sealed class WitChoiceSchema { public string type; public WitChoiceProperties properties; public string[] required; public bool additionalProperties; }
        [Serializable] private sealed class WitChoiceProperties { public OpenAiStringSchema text; public OpenAiIntegerSchema quality; }
        [Serializable] private sealed class OpenAiResponse { public OpenAiOutput[] output = Array.Empty<OpenAiOutput>(); public OpenAiError error = new OpenAiError(); }
        [Serializable] private sealed class OpenAiOutput { public OpenAiOutputContent[] content = Array.Empty<OpenAiOutputContent>(); }
        [Serializable] private sealed class OpenAiOutputContent { public string type = string.Empty; public string text = string.Empty; }
        [Serializable] private sealed class OpenAiError { public string message = string.Empty; }
    }
}
