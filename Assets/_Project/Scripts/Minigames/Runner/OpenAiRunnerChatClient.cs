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
        public string plasticPhase;
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
        public string lastDonationNickname;
        public int lastDonationAmount;
        public string lastDonationMessage;
        public bool lastDonationIsLarge;
        public string recentMessages;
        public bool conflictActive;
        public string conflictTroublemakerId;
        public string conflictTroublemakerNickname;
        public string conflictTargetId;
        public string conflictTargetNickname;
        public string conflictTargetMessage;
        public bool conflictTargetsStreamer;
        public bool fraternizationActive;
        public string socialViewer1Id;
        public string socialViewer1Nickname;
        public string socialViewer2Id;
        public string socialViewer2Nickname;
        public string socialViewer3Id;
        public string socialViewer3Nickname;
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
라테시온 | ?
인생편하게살고싶다 | ?
남은 자 | ???
아기앙카 | 엄
나선이오 | 오옹
속초행 | 오옹?
남은 자 | ㅋㅋㅋㅋㅋㅋㅋ
허니비야끼토리 | ㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋ
턱이커서미안해 | 굿
금손 마법사 8911 | 캬
노지선 | 오
hwk | ??
BengolCAT | ?
사과HDD | ㅇㅇ
앙그리머 | ㄱㄱㄱ
애 몽 | 아이고
시식빵 | ㅋㅋㅋㅋㅋㅋㅋㅋㅋ
민초버거 | 캬
민초버거 | 왔냐?
느렵 | 키야!
스튜가좋아 | 오
시식빵 | 헉
스튜가좋아 | ㄹㅇㅋㅋ";

        private const string EventReactionGuide = @"=== 이벤트별 반응 기준 ===
피격/하트 손실/죽음: 아니 뭐하냐 / ? / ??? / ㅋㅋㅋㅋㅋㅋ / 개못하네 / 개못하네... / 아니 이걸? / 예? / ... / 에반데 / 아니 / 뭐함?
방송 종료: ㅈㅈ / 바이바이 / 수고했다 / 수고했어요 / 다음에 봐요~ / 담방에 봐
오래 버티거나 안정적인 플레이: 오 / 가자 / 좋은데? / ㄱㄱ / ㄱㄱㄱ / 좀만 더 / 이대로만
최고 기록: ㅅㅅ / 나이스 / 오 / 다음 천 단위 점수 가자 / 가보자 / 가즈아
스트리머 답변이 별로거나 어색함: ㄴㅈ / 개노잼 / ? / 음.... / 예? / 하하 / 이건 좀... / 아...
스트리머 답변이 좋음: ㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋㅋ / ㅋㅋㅋㅋㅋ / 아 ㅋㅋ / 미쳤네ㅋㅋ
일반 도네: 도네 문구의 단어 하나에 짧게 반응. 내용을 다시 요약하거나 감사 인사를 대신 하지 않음.
큰 도네: 실제 금액원 ㄷㄷㄷㄷ / 와 / ??? / 미친 / ㅁㅊ / 와 실제 금액원....
플레이나 스트리머 반응이 너무 없을 때: ㄴㅈ / 왤케 조용함 / ... / 뭐함? / 자나
=== 기준 끝 ===";

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
            float heat01 = Mathf.Clamp01((snapshot?.broadcastHype ?? 50f) / 100f);
            float baitChance = Mathf.Lerp(0.38f, 0.07f, heat01);
            bool requestBait = UnityEngine.Random.value < baitChance;
            string requestedViewerType = requestBait
                ? "이번에는 약간 도발적이지만 재치 있게 선을 긋거나 받아칠 수 있는 채팅을 만든다. shouldIgnore=false다."
                : "이번에는 관심/질문/가벼운 놀림 채팅을 만들고 shouldIgnore=false로 판정한다.";
            WitResponseRequest payload = new WitResponseRequest
            {
                model = _model,
                store = false,
                max_output_tokens = 500,
                reasoning = new OpenAiReasoning { effort = "none" },
                input = new[]
                {
                    Input("system", "한국 게임 방송 중 실제 시청자가 막 쓸 법한 짧은 질문/놀림/반응 한 줄과 스트리머 답변 후보 5개를 만든다. "
                        + "사건을 낭독하거나 설명문처럼 쓰지 않고 기존 질문을 반복하지 않는다. "
                        + "shouldIgnore는 항상 false다. 무반응만 정답인 문제는 만들지 않는다. "
                        + "답변 후보는 정확히 5개다. 밝고 자신감 있게 받아쳐 누가 봐도 분위기를 살리는 quality 2를 2개, "
                        + "질문에 자연스럽게 답하지만 웃기려고 하지는 않는 quality 1을 2개, 누가 봐도 사회적으로 어색한 quality 0을 1개 만든다. "
                        + "quality 2는 질문의 핵심에 바로 답하면서 핵심 단어를 재치 있게 비틀고, 당당하고 유쾌한 완성문이어야 한다. "
                        + "quality 1은 담담하고 솔직한 보통 답변이다. 호감도 반감도 살 억지 드립도 넣지 않는다. "
                        + "quality 0은 화내거나 시청자에게 무례한 답변으로 때우지 않는다. 반드시 '어...', '저...', '하하...' 중 하나를 실제 문장에 넣고, "
                        + "말을 더듬거나 자신 없이 변명하거나 맥락과 전혀 안 맞는 억지 드립을 쳐서 즉시 머쓱함이 느껴져야 한다. "
                        + "quality 2에는 말 더듬기, 머뭇거림, 사과, 자신 없는 표현을 절대 넣지 않는다. "
                        + "세 등급이 같은 뜻의 말투 차이로만 보이면 안 된다. 답변에서 질문을 그대로 되풀이하지 않는다. 각 문장은 35자 이내다."),
                    Input("user", "현재 상황:\n" + JsonUtility.ToJson(snapshot)
                        + "\n최근 사용해서 반복 금지인 질문:\n" + string.Join(" | ", recentPrompts ?? Array.Empty<string>())
                        + "\n이번 시청자 유형:\n" + requestedViewerType)
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
                    || result.shouldIgnore || result.choices == null || result.choices.Length != 5
                    || result.choices.Any(choice => choice == null || string.IsNullOrWhiteSpace(choice.text))
                    || result.choices.Count(choice => choice.quality == 2) < 2
                    || result.choices.Count(choice => choice.quality == 1) < 2
                    || result.choices.Count(choice => choice.quality == 0) < 1
                    || result.choices.Where(choice => choice.quality == 0).Any(choice => !HasClearAwkwardCue(choice.text))
                    || result.choices.Where(choice => choice.quality == 2).Any(choice => HasClearAwkwardCue(choice.text)))
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
                        + "\n\n현재 방송 열기에 따른 채팅 분위기:\n" + BuildHeatDirective(snapshot)
                        + "\n\n이번 요청의 출력 형태(반드시 따름):\n" + BuildOutputShapeDirective(snapshot))
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

        private static bool HasClearAwkwardCue(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            return text.Contains("어...") || text.Contains("저...") || text.Contains("하하...");
        }

        private static string BuildSystemPrompt(IReadOnlyList<RunnerViewerData> viewers)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("아래 실제 한국 게임방송 채팅 표본과 구별하기 어려운 새 채팅을 만든다.");
            builder.AppendLine("JSON 사건을 문장으로 다시 읽지 않는다. 보이는 사실의 원인/결과를 친절하게 설명하지 않는다.");
            builder.AppendLine("금지 예: '적을 처치했네', '체력이 얼마 안 남았네', '죽었는데 신기록 갱신 ㅋㅋ', '점수가 올랐네'.");
            builder.AppendLine("실제 시청자처럼 반사 반응, 웃음, 물음표, 생략된 평가, 앞 채팅 동조/반박, 가끔만 훈수한다.");
            builder.AppendLine("긴 ㅋㅋ만 있는 줄, '?', '오', '아', '휴', '어어'도 완전한 정상 메시지다. 억지로 정보를 덧붙이지 않는다.");
            builder.AppendLine("게임 사건이 있으면 그 사건을 설명하지 말고 아래 대응 반응군과 아주 가까운 채팅을 우선한다.");
            builder.AppendLine("여러 메시지면 절반 이상은 1~10자의 원초 반응이어야 하고, 완성된 설명문은 최대 하나다.");
            builder.AppendLine("같은 뜻도 ?, ??, ???, ㅋㅋ 길이, 점 개수, 띄어쓰기, 말끝, 오타를 매번 달리한다. 모든 줄을 같은 길이/문장형으로 맞추지 않는다.");
            builder.AppendLine("최근 채팅에 답할 때 닉네임을 부르지 말고 단어를 되받거나 'ㅇㅇ', '맞음', '그건 아닌듯'처럼 잇는다.");
            builder.AppendLine("현재 상황 JSON의 conflictActive가 false면 시청자끼리 싸운다거나 채팅창이 싸운다는 말을 절대 만들지 않는다.");
            builder.AppendLine("fraternizationActive가 false면 서로 오늘도 왔냐고 알아보거나 방송 밖 친분을 과시하는 친목 대화를 만들지 않는다.");
            builder.AppendLine("타일 아레나의 패턴은 매번 무작위로 교체된다. 패턴 번호는 진행도/난이도/도달 단계가 아니며 채팅에서 숫자, '벌써', '몇 스테이지', 기록 진척으로 절대 언급하지 않는다.");
            builder.AppendLine("Plastic Knightmare는 낮 정비와 밤 공세가 반복되며 낮이 돌아올 때 Day가 증가한다. plasticPhase가 밤 전투면 '슬슬 빡세지는데', '이제 많이 나오네', 낮 정비면 '벽부터 고쳐', '정비 시간 짧다'처럼 짧게 반응한다.");
            builder.AppendLine("시청자별 수치는 발화자 선택의 약한 확률일 뿐이며 고정 역할을 연기하지 않는다.");
            builder.AppendLine("message에는 닉네임 없이 한 줄 35자 이내 한국어 채팅만 쓴다. 이모지와 이모티콘 문자는 절대 쓰지 않는다. 설명, 따옴표, 괄호 연기, 마크업은 금지한다.");
            builder.AppendLine("혐오, 차별, 협박, 심한 욕설, 성적 표현, 현실 인신공격은 금지한다. 가벼운 놀림과 의견 충돌까지만 허용한다.");
            builder.AppendLine(EventReactionGuide);
            builder.AppendLine("\n=== 실제 수집 로그: 닉네임 | 채팅 ===");
            builder.AppendLine(RealChatReferenceCorpus);
            builder.AppendLine("=== 실제 수집 로그 끝 ===");
            builder.AppendLine("위 로그의 게임 고유명사와 사실은 무시한다. 닉네임 작명 감각, 길이, 생략, 반응 밀도만 재현한다.");
            builder.AppendLine("예시 닉네임은 복사하지 않는다. ?, 오, ㅋㅋ, ㄱㄱ 같은 짧은 반응은 그대로 재사용해도 되며 길이/말끝을 바꾼 변형도 섞는다. 긴 표본 문장은 통째로 복사하지 않는다.");
            builder.AppendLine("speakerId는 반드시 아래 현재 시청자의 ID를 한 글자도 바꾸지 않고 사용한다.");
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

        private static string BuildOutputShapeDirective(RunnerChatSnapshot snapshot)
        {
            string events = snapshot?.events ?? string.Empty;
            if (snapshot != null && snapshot.conflictActive && events.Contains("분탕"))
                return snapshot.conflictTargetsStreamer
                    ? $"분탕 유저 {snapshot.conflictTroublemakerNickname}가 스트리머에게 '{snapshot.conflictTargetMessage}'라고 명확히 시비를 건 직후다. 정확히 3개를 쓴다. 1) 다른 시청자가 '@{snapshot.conflictTroublemakerNickname}'을 부르며 싫으면 나가라고 반박한다. 2) ID {snapshot.conflictTroublemakerId}가 다시 못한 걸 못한다고 했을 뿐이라며 우긴다. 3) 또 다른 시청자가 밴을 요구하거나 싸움에 짧게 반응한다. 첫 도발을 그대로 반복하거나 게임 사건을 친절하게 설명하지 않는다."
                    : $"분탕 유저가 {snapshot.conflictTargetNickname}에게 '{snapshot.conflictTargetMessage}'라고 근거 없이 시비를 건 직후다. 정확히 3개를 시간 순서대로 쓴다. 1) ID {snapshot.conflictTargetId}가 '@{snapshot.conflictTroublemakerNickname}'을 부르며 짧게 맞받아친다. 2) ID {snapshot.conflictTroublemakerId}가 다시 '@{snapshot.conflictTargetNickname}'을 부르며 '긁혔네', '아는 척하네'처럼 도발한다. 3) 둘이 아닌 시청자가 싸움을 말리거나 밴을 요구한다. 상대의 원래 평범한 채팅이나 질문을 인용해서 시비의 소재로 삼지 말고, 이미 나온 첫 도발도 그대로 반복하지 않는다.";
            if (snapshot != null && snapshot.fraternizationActive && events.Contains("친목"))
                return $"친목 대화가 시작된 직후다. 정확히 3개를 쓴다. 1) ID {snapshot.socialViewer2Id}가 '@{snapshot.socialViewer1Nickname}'을 부르며 오늘도 왔다거나 전에 본 이야기를 한다. 2) ID {snapshot.socialViewer1Id}가 '@{snapshot.socialViewer2Nickname}'을 다시 부르며 방송 밖 친분을 이어간다. 3) 둘이 아닌 시청자가 친목 보기 싫다/둘이 따로 연락해라/또 시작이네/밴 필요하다는 식으로 짧게 반응한다. 게임 상황 설명은 금지한다.";
            if (events.Contains("후원"))
            {
                int amount = Mathf.Max(0, snapshot?.lastDonationAmount ?? 0);
                string message = snapshot?.lastDonationMessage ?? string.Empty;
                if (snapshot != null && snapshot.lastDonationIsLarge)
                    return $"큰 도네 반응형. 실제 금액은 {amount:N0}원이다. 2개 이상 출력한다면 과반을 와/???/ㅁㅊ/미친/{amount:N0}원 ㄷㄷ 같은 1~12자 반응으로 쓴다. 예시의 x원을 쓰지 말고 반드시 실제 금액만 쓴다. 도네 문구 '{message}'를 길게 풀이하지 않는다.";
                return $"일반 도네 반응형. 도네 문구 '{message}'에서 눈에 띄는 말 한 조각에만 짧게 반응하거나 오/ㅋㅋ/? 같은 반응을 쓴다. 후원 사실을 설명하거나 모두가 '감사합니다'라고 하지 않는다.";
            }
            if (events.Contains("재치 있게 받아쳐") || events.Contains("반응이 좋아짐"))
                return "좋은 리액션형. 첫 message는 반드시 ㅋㅋ만 5~20자로 쓴다. 나머지도 아 ㅋㅋ/미쳤네ㅋㅋ/ㅋㅋㅋㅋ처럼 웃음 위주이며 왜 웃긴지 설명하지 않는다.";
            if (events.Contains("무난하게 답변"))
                return "무난한 리액션형. ㅇㅇ/그렇구나/오케이/그건 맞지/납득/그럴 수 있지처럼 짧게 수긍한다. 웃음이 폭발하거나 분위기가 싸해진 반응은 만들지 않는다.";
            if (events.Contains("답변이 어색"))
                return "싸늘한 리액션형. ㄴㅈ/개노잼/?/음..../예?/하하/이건 좀.../아...와 아주 가까운 1~10자 반응을 과반으로 쓴다. 어색해졌다고 설명하지 않는다.";
            if (events.Contains("방송을 정상 종료") || events.Contains("방송 종료"))
                return "방종 인사형. ㅈㅈ/바이바이/수고했다/수고했어요/다음에 봐요~/담방에 봐와 가까운 짧은 인사만 쓴다. 방송이 끝났다는 사실을 설명하지 않는다.";
            if (events.Contains("새 최고 기록") || events.Contains("신기록"))
            {
                int score = Mathf.Max(0, snapshot?.score ?? 0);
                int nextThousand = (score / 1000 + 1) * 1000;
                return $"최고 기록 반응형. ㅅㅅ/나이스/오/가보자/가즈아 또는 '{nextThousand}점 가자'처럼 짧게 쓴다. 현재 기록 달성을 해설하지 말고, 점수 목표를 말한다면 {nextThousand}만 사용한다.";
            }
            if (events.Contains("게임 오버") || events.Contains("피격") || events.Contains("목숨을 잃음") || events.Contains("저체력") || events.Contains("남은 목숨"))
                return "피격/죽음 반응형. 아니 뭐하냐/?/???/ㅋㅋㅋㅋㅋㅋ/개못하네.../아니 이걸?/예?/.../에반데/아니/뭐함?과 아주 가까운 반응을 과반으로 쓴다. 맞았다거나 죽었다는 사실을 서술하지 않는다.";
            if (events.Contains("특별한 사건 없이 게임 플레이") || events.Contains("특별한 사건 없이"))
                return UnityEngine.Random.value < 0.72f
                    ? "안정 플레이 응원형. 오/가자/좋은데?/ㄱㄱ/ㄱㄱㄱ/좀만 더/이대로만과 가까운 1~10자 반응을 쓴다. 오래 버텼다고 설명하지 않는다."
                    : "정적 반응형. ㄴㅈ/왤케 조용함/.../뭐함?/자나와 가까운 짧은 반응만 쓴다.";
            if (events.Contains("대기 화면"))
                return "정적 반응형. ㄴㅈ/왤케 조용함/.../뭐함?/자나와 가까운 1~10자 반응을 과반으로 쓴다.";

            int roll = UnityEngine.Random.Range(0, 100);
            if (roll < 35)
                return "원초 반응형. 첫 message는 반드시 반응만 쓴다. ?, ??, ???, 오, 아, 캬, 헉, 어어, 휴, ㄱㄱㄱ 또는 길이가 매번 다른 ㅋㅋ 중 상황에 맞는 하나를 고른다. 완성문으로 만들지 않는다.";
            if (roll < 65)
                return "짧은 파편형. 첫 message는 2~10자의 생략되거나 끝맺지 않은 채팅이다. 사건을 주어+서술어로 설명하지 않는다.";
            if (roll < 85)
                return "채팅 흐름형. recentMessages의 말 한 조각을 되받아 동조하거나 반박한다. 닉네임 호출과 친절한 설명은 금지한다.";
            return "짧은 의견형. 상황에 대한 평가나 훈수를 18자 이내로 쓴다. JSON에 적힌 사건 자체를 그대로 낭독하지 않는다.";
        }

        private static string BuildHeatDirective(RunnerChatSnapshot snapshot)
        {
            float heat = Mathf.Clamp(snapshot?.broadcastHype ?? 50f, 0f, 100f);
            if (heat >= 70f)
                return $"방송 열기 {heat:0}%. 채팅 분위기가 살아 있다. 같은 사건에도 응원/웃음/감탄 쪽을 우선한다. 별도 분탕 이벤트가 아니면 시비나 분쟁을 만들지 않는다.";
            if (heat <= 30f)
                return $"방송 열기 {heat:0}%. 채팅 분위기가 식었다. 같은 사건에도 ?/ㄴㅈ/.../뭐함?/개노잼 같은 냉담/불만/놀림 쪽을 우선하되 사건을 설명하지 않는다.";
            return $"방송 열기 {heat:0}%. 긍정/무심/가벼운 놀림이 섞인 보통 채팅 분위기를 유지한다.";
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
                shouldIgnore = new OpenAiBooleanSchema { type = "boolean" },
                choices = new WitArraySchema
                {
                    type = "array", minItems = 5, maxItems = 5,
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
            required = new[] { "viewerMessage", "shouldIgnore", "choices" }, additionalProperties = false
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
        [Serializable] private sealed class OpenAiBooleanSchema { public string type; }
        [Serializable] private sealed class WitResponseRequest { public string model; public bool store; public OpenAiInput[] input; public WitTextOptions text; public OpenAiReasoning reasoning; public int max_output_tokens; }
        [Serializable] private sealed class WitTextOptions { public string verbosity; public WitJsonFormat format; }
        [Serializable] private sealed class WitJsonFormat { public string type; public string name; public bool strict; public WitObjectSchema schema; }
        [Serializable] private sealed class WitObjectSchema { public string type; public WitRootProperties properties; public string[] required; public bool additionalProperties; }
        [Serializable] private sealed class WitRootProperties { public OpenAiStringSchema viewerMessage; public OpenAiBooleanSchema shouldIgnore; public WitArraySchema choices; }
        [Serializable] private sealed class WitArraySchema { public string type; public WitChoiceSchema items; public int minItems; public int maxItems; }
        [Serializable] private sealed class WitChoiceSchema { public string type; public WitChoiceProperties properties; public string[] required; public bool additionalProperties; }
        [Serializable] private sealed class WitChoiceProperties { public OpenAiStringSchema text; public OpenAiIntegerSchema quality; }
        [Serializable] private sealed class OpenAiResponse { public OpenAiOutput[] output = Array.Empty<OpenAiOutput>(); public OpenAiError error = new OpenAiError(); }
        [Serializable] private sealed class OpenAiOutput { public OpenAiOutputContent[] content = Array.Empty<OpenAiOutputContent>(); }
        [Serializable] private sealed class OpenAiOutputContent { public string type = string.Empty; public string text = string.Empty; }
        [Serializable] private sealed class OpenAiError { public string message = string.Empty; }
    }
}
