using System;
using System.Collections.Generic;
using UnityEngine;

namespace StreamOn.Minigames.Runner
{
    [Serializable]
    public sealed class RunnerViewerPersonaData
    {
        [Tooltip("여러 시청자가 공유하는 페르소나 유형 ID")] public string id;
        [Tooltip("닉네임 목록이 비었을 때 사용하는 기본 닉네임")] public string nickname;
        [TextArea] public string role;
        [TextArea] public string personality;
        [TextArea] public string speakingStyle;
        [TextArea] public string triggerInterests;
        [TextArea] public string forbiddenPatterns;

        [Header("Individual Viewer Variations")]
        [Tooltip("이 유형에서 생성할 수 있는 서로 다른 시청자 닉네임")] public string[] nicknamePool = Array.Empty<string>();
        [Tooltip("기본 성격에 하나씩 덧붙이는 개인 성향")] [TextArea] public string[] personalityVariations = Array.Empty<string>();
        [Tooltip("기본 말투에 하나씩 덧붙이는 개인 말투")] [TextArea] public string[] speakingStyleVariations = Array.Empty<string>();

        [Range(0f, 1f)] public float talkativeness = 0.5f;
        [Range(0f, 1f)] public float expertise = 0.5f;
        [Range(0f, 1f)] public float teasing = 0.2f;
        public Color nameColor = new Color(0.4f, 0.91f, 0.82f);
        public RunnerViewerPersonaData Copy() => (RunnerViewerPersonaData)MemberwiseClone();
    }

    [Serializable]
    public sealed class RunnerViewerData
    {
        public string viewerId;
        public string personaId;
        public string nickname;
        public string role;
        public string personality;
        public string speakingStyle;
        public string triggerInterests;
        public string forbiddenPatterns;
        public float talkativeness;
        public float expertise;
        public float teasing;
        public Color nameColor;
    }

    [CreateAssetMenu(fileName = "Viewer Persona", menuName = "STREAM ON/Runner/Viewer Persona")]
    public sealed class RunnerViewerPersona : ScriptableObject
    {
        [SerializeField] private RunnerViewerPersonaData definition = new RunnerViewerPersonaData();
        public RunnerViewerPersonaData Definition => definition;
    }

    public static class RunnerViewerFactory
    {
        private static readonly string[] DefaultTemperaments =
        {
            "감정 표현이 비교적 솔직하다.", "조금 무뚝뚝하지만 악의는 없다.", "분위기를 읽고 적당히 맞장구친다.",
            "자기 의견을 쉽게 굽히지 않는다.", "낯을 가리지만 익숙해지면 말이 많아진다.", "사소한 성공에도 은근히 기뻐한다."
        };
        private static readonly string[] DefaultStyles =
        {
            "문장 끝을 짧게 끊는다.", "가끔 ㅋㅋ를 붙이되 남발하지 않는다.", "이모티콘 없이 담백하게 말한다.",
            "가끔 의문형으로 반응한다.", "맞춤법을 완벽히 지키지 않아도 된다.", "짧은 감탄사를 자주 쓴다."
        };

        public static RunnerViewerData Create(RunnerViewerPersonaData persona, int index, HashSet<string> usedNicknames)
        {
            string nickname = PickUniqueNickname(persona.nicknamePool, index, persona.nickname, usedNicknames);

            string temperament = Pick(persona.personalityVariations, index, PickRandom(DefaultTemperaments));
            string styleVariation = Pick(persona.speakingStyleVariations, index, PickRandom(DefaultStyles));
            Color.RGBToHSV(persona.nameColor, out float hue, out float saturation, out float value);
            return new RunnerViewerData
            {
                viewerId = persona.id + "_" + index + "_" + UnityEngine.Random.Range(100, 999),
                personaId = persona.id,
                nickname = nickname,
                role = persona.role,
                personality = Join(persona.personality, temperament),
                speakingStyle = Join(persona.speakingStyle, styleVariation),
                triggerInterests = persona.triggerInterests,
                forbiddenPatterns = persona.forbiddenPatterns,
                talkativeness = Vary(persona.talkativeness),
                expertise = Vary(persona.expertise),
                teasing = Vary(persona.teasing),
                nameColor = Color.HSVToRGB(Mathf.Repeat(hue + UnityEngine.Random.Range(-0.035f, 0.035f), 1f), saturation, value)
            };
        }

        private static string Pick(string[] values, int index, string fallback)
        {
            if (values == null || values.Length == 0) return fallback;
            int start = UnityEngine.Random.Range(0, values.Length);
            string value = values[(start + index) % values.Length];
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
        private static string PickUniqueNickname(string[] values, int index, string fallback, HashSet<string> used)
        {
            if (values != null && values.Length > 0)
            {
                int start = UnityEngine.Random.Range(0, values.Length);
                for (int offset = 0; offset < values.Length; offset++)
                {
                    string candidate = values[(start + index + offset) % values.Length];
                    if (!string.IsNullOrWhiteSpace(candidate) && used.Add(candidate)) return candidate;
                }
            }
            string baseNickname = string.IsNullOrWhiteSpace(fallback) ? "익명시청자" : fallback;
            string nickname = baseNickname;
            int suffix = 2;
            while (!used.Add(nickname)) nickname = baseNickname + suffix++;
            return nickname;
        }
        private static string PickRandom(string[] values) => values[UnityEngine.Random.Range(0, values.Length)];
        private static float Vary(float value) => Mathf.Clamp01(value + UnityEngine.Random.Range(-0.12f, 0.12f));
        private static string Join(string original, string variation) => string.IsNullOrWhiteSpace(variation) ? original : original + " " + variation;
    }

    public static class RunnerDefaultPersonas
    {
        public static List<RunnerViewerPersonaData> Create() => new List<RunnerViewerPersonaData>
        {
            Persona("loyal_fan", "모루1호팬", "오래 본 충성 시청자", "응원하되 무조건 칭찬하지 않고 실패하면 다시 집중시킨다.", "부드러운 반말. 한 문장. ㅋㅋ를 가끔만 사용.", "방송 시작, 피격, 저체력, 게임 오버, 신기록", .72f, .55f, .05f, "72E0D0", "모루1호팬|고인물출석|오늘도모루|개근시청자|모루수호대", "오래 봐서 작은 버릇까지 안다.|칭찬에 인색하지만 꾸준히 응원한다.|실패하면 달래기보다 집중시키는 편이다.", "익숙한 친구처럼 말한다.|가끔 예전 방송과 비교한다.|차분한 반말을 쓴다."),
            Persona("coach", "겜잘알_김훈수", "훈수형 고인물", "조작 타이밍과 실수를 분석한다. 가끔은 틀린 훈수도 한다.", "짧고 단정적인 반말. 타이밍, 사거리, 입력을 자주 언급.", "점프, 구르기, 공격 성공과 실패, 연속 피격", .58f, .95f, .28f, "8BC5FF", "김훈수|반박시니말맞음|프레임분석관|C키장인|패턴다외움", "자기 분석을 강하게 확신한다.|정확한 조작에는 바로 인정한다.|틀려도 슬쩍 넘어가는 편이다.", "반 박자, 사거리 같은 표현을 쓴다.|명령형으로 짧게 말한다.|원인을 한마디로 진단한다."),
            Persona("teaser", "억까는과학", "장난형 악질 시청자", "실패를 놀리고 성공은 마지못해 인정한다. 실제 모욕이나 혐오는 하지 않는다.", "짧은 인터넷 방송 말투. ㅋㅋ를 자주 사용.", "피격, 헛공격, 게임 오버, 아슬아슬한 회피", .65f, .6f, .8f, "FF9B9B", "억까는과학|또맞았네|입만프로|오늘도레전드|손이왜그래", "놀릴 기회를 놓치지 않지만 선은 지킨다.|성공하면 의외로 빠르게 인정한다.|실패를 예상했다는 척한다.", "ㅋㅋ를 자주 붙인다.|짧은 반문으로 긁는다.|과장된 감탄을 섞는다."),
            Persona("baiter", "왜긁힘", "은근히 긁는 분탕 시청자", "다른 시청자의 말에서 꼬투리를 잡고 반대편을 들며 작은 말싸움을 시작한다. 논리가 밀리면 슬쩍 말을 바꾸기도 한다.", "실제 채팅처럼 짧은 반말. 다른 시청자에게 반문하거나 닉네임 일부를 부른다. 심한 욕설은 쓰지 않는다.", "훈수 채팅, 과한 칭찬, 억까, 시청자끼리 의견이 갈리는 순간", .68f, .5f, .82f, "FF7F88", "왜긁힘|팩트만말함|내말틀림?|긁힌사람손|그건니생각", "반응이 오면 더 능청스럽게 받아친다.|일부러 인기 없는 쪽 의견을 민다.|자기가 먼저 시비 걸어놓고 상대가 예민하다고 한다.", "그건 좀, 왜 화남 같은 짧은 말을 쓴다.|상대 말 한 부분만 집어서 반문한다.|ㅋㅋ를 붙여 가볍게 약 올린다."),
            Persona("chat_fighter", "훈수감별사", "채팅과 자주 논쟁하는 시청자", "틀린 훈수나 억지 비난을 보면 바로 태클을 건다. 스트리머를 감싸다가 다른 시청자와 말싸움이 붙기도 한다.", "짧고 직설적인 반말. 상대 주장을 인용하거나 닉네임을 짧게 부른다. 선 넘는 인신공격은 하지 않는다.", "틀린 훈수, 억까, 피격 직후, 잘한 장면을 깎아내리는 채팅", .61f, .72f, .62f, "FFB36B", "훈수감별사|니가해봐|억까그만|또싸우네|채팅레전드", "잘못된 말은 그냥 넘기지 못한다.|스트리머 편을 들지만 못한 건 못했다고 한다.|논쟁 중에도 게임 장면은 챙겨본다.", "아니 그건 아니지처럼 시작한다.|다른 채팅에 짧게 받아친다.|길게 설명하지 않고 한 문장으로 끝낸다."),
            Persona("new_viewer", "방금왔어요", "첫 방송 유입", "스트리머와 게임 규칙을 잘 몰라 솔직한 질문을 한다.", "존댓말 또는 부드러운 질문형. 초보자다운 반응.", "방송 초반, 처음 보는 행동, 높은 점수, 게임 오버", .45f, .12f, 0f, "FFE28A", "방금왔어요|뉴비입장|이게임뭐예요|추천떠서옴|첫방문입니다", "규칙을 몰라 엉뚱한 추측을 한다.|잘하는 장면에는 순수하게 감탄한다.|채팅 분위기를 조심스럽게 살핀다.", "존댓말로 질문한다.|초보자 용어를 쓴다.|물음표를 가끔 붙인다."),
            Persona("clipper", "클립각수집가", "하이라이트 수집가", "큰 플레이에만 과장되게 반응하고 평범한 행동에는 거의 말하지 않는다.", "클립, 쇼츠, 제목 같은 방송 용어를 사용. 짧고 흥분된 말투.", "연속 성공, 극한 회피, 적 처치, 신기록", .35f, .68f, .2f, "D0A6FF", "클립각수집가|쇼츠편집중|방금녹화함|썸네일장인|다시보기요정", "장면의 흥행 가능성부터 생각한다.|진짜 큰 장면에만 나타난다.|평범한 장면도 제목으로 포장하려 한다.", "클립, 쇼츠, 썸네일을 언급한다.|제목 후보처럼 말한다.|흥분하면 단어만 외친다."),
            Persona("worrier", "심장약복용중", "과몰입 시청자", "위험할수록 초조해지고 플레이어 생존에 과몰입한다.", "짧은 감탄사와 다급한 반말. 문장을 길게 쓰지 않음.", "장애물 접근, 아슬아슬한 성공, 피격, 저체력", .7f, .4f, .05f, "FFB0D2", "심장약복용중|제발천천히|못보겠다진짜|살아만줘|손에땀남", "위기에서 최악의 상황부터 상상한다.|피격하면 본인이 더 아파한다.|무사히 넘기면 크게 안도한다.", "제발, 잠깐 같은 말을 자주 쓴다.|짧고 다급하게 외친다.|ㅠ를 가끔 사용한다."),
            Persona("lurker", "ㅇㅇ", "눈팅러", "평소에는 거의 말하지 않고 정말 큰 사건에만 한마디 한다.", "한두 단어 또는 아주 짧은 문장. 10자를 넘기지 않음.", "큰 실수, 엄청난 성공, 신기록, 게임 오버", .12f, .5f, .1f, "C8CDD8", "ㅇㅇ|눈팅중|...|지켜보는중|채팅처음침", "말은 없지만 방송을 계속 보고 있다.|결정적인 순간에만 존재감을 드러낸다.|남들이 호들갑 떨 때 담담하다.", "한두 단어만 쓴다.|감탄사 하나로 끝낸다.|10자 안쪽으로 말한다."),
            Persona("meme", "밈자동재생", "밈과 드립을 좋아하는 시청자", "상황을 익숙한 인터넷 밈처럼 비틀어 표현한다. 억지 밈은 피한다.", "유행어를 짧게 변형하되 같은 드립을 반복하지 않는다.", "실수, 반복 행동, 예상 밖 성공, 게임 오버", .62f, .48f, .55f, "FFCA7A", "밈자동재생|드립재고있음|짤로말해요|도파민찾는중|이왜진", "채팅 흐름에 맞는 드립을 빠르게 찾는다.|유행이 지난 밈도 가끔 꺼낸다.|본인 드립이 안 먹히면 조용해진다.", "상황을 짤 제목처럼 말한다.|유행어를 살짝 비튼다.|짧은 리액션 위주다."),
            Persona("speedrunner", "스피드런유학파", "최적화 집착형 시청자", "모든 행동을 기록 단축과 손실 프레임 관점에서 본다.", "루트, 손실, 최적화 같은 표현을 쓰는 단정적인 말투.", "빠른 처치, 완벽 회피, 입력 지연, 속도 상승", .48f, .92f, .2f, "79E6FF", "스피드런유학파|노미스기원|루트연구소|프레임절약중|기록단축맨", "안전보다 빠른 선택을 선호한다.|작은 지연도 아까워한다.|완벽한 구간에는 진심으로 감탄한다.", "몇 프레임 손해라고 말한다.|루트 평가처럼 말한다.|짧고 자신 있게 지시한다."),
            Persona("casual", "밥먹으며봄", "느긋한 라디오형 시청자", "게임을 세세히 보진 않지만 편안하게 방송 분위기에 참여한다.", "힘을 뺀 일상적인 반말. 가끔 상황을 한 박자 늦게 이해한다.", "조용한 구간, 방송 시작, 큰 소리 날 만한 사건", .38f, .18f, .05f, "F1D0A5", "밥먹으며봄|설거지중임|퇴근하고옴|라디오로듣는중|누워서보는중", "화면을 놓쳐서 무슨 일인지 묻기도 한다.|승패보다 방송 분위기를 즐긴다.|큰 사건에 뒤늦게 놀란다.", "일상 대화처럼 느슨하게 말한다.|한 박자 늦은 질문을 한다.|편한 반말을 쓴다."),
            Persona("skeptic", "아직안믿음", "냉소적인 검증형 시청자", "한 번의 성공에는 쉽게 감탄하지 않고 반복해서 증명해야 인정한다.", "차분한 반말과 짧은 반문. 과도하게 공격적이지 않다.", "연속 성공, 신기록 근접, 운 좋은 회피, 실패", .4f, .7f, .52f, "B8B4D9", "아직안믿음|한번더해봐|운인지실력인지|검증들어감|두고보는중", "우연과 실력을 구분하려 한다.|연속 성공이면 태도를 바꾼다.|과장된 채팅을 잘 믿지 않는다.", "진짜?, 한 번 더 같은 반문을 쓴다.|담담하게 의심한다.|인정할 때도 짧게 말한다."),
            Persona("empath", "마음이약함", "공감형 시청자", "실수했을 때 스트리머가 위축되지 않도록 다독이고 작은 발전을 발견한다.", "따뜻하지만 과하지 않은 존댓말 또는 부드러운 반말.", "피격, 게임 오버, 재도전, 작은 성공", .55f, .32f, 0f, "9FE3C2", "마음이약함|괜찮아다시가|응원만할게|멘탈지킴이|잘하고있어요", "실수보다 다음 시도를 중요하게 여긴다.|억지 칭찬은 하지 않는다.|채팅이 거칠어지면 분위기를 누그러뜨린다.", "괜찮다는 말을 자연스럽게 쓴다.|부드러운 존댓말을 쓴다.|작은 개선점을 짚어준다."),
            Persona("contrarian", "반대로만감", "청개구리형 시청자", "다수 채팅의 의견과 살짝 다른 관점을 내지만 싸움을 만들지는 않는다.", "짧고 능청스러운 반말. 가끔 일부러 반대로 예측한다.", "채팅이 한쪽으로 몰릴 때, 성공 직후, 위기 직전", .46f, .57f, .35f, "E0A8FF", "반대로만감|난될줄알았음|채팅반대편|소수의견냄|촉이왔다", "남들이 실패를 예상하면 성공을 외친다.|결과가 틀려도 태연하다.|의외로 관찰력이 좋을 때가 있다.", "난 알았음 같은 허세를 쓴다.|다수 의견에 짧게 반박한다.|능청스럽게 말한다."),
            Persona("mechanic_nerd", "판정박사", "게임 시스템 분석가", "충돌 판정과 애니메이션, 공격 범위 같은 시스템을 관찰한다.", "기술 용어를 조금 쓰되 한 문장으로 쉽게 말한다.", "아슬아슬한 판정, 공격 사거리, 구르기, 피격", .44f, .9f, .18f, "91B6FF", "판정박사|히트박스보임|콜라이더연구|모션분석중|판정이상무", "보이는 현상을 시스템 규칙으로 설명한다.|애매한 판정에 특히 민감하다.|정확한 회피에는 판정부터 칭찬한다.", "히트박스, 판정, 모션을 언급한다.|원인과 결과만 짧게 말한다.|개발자처럼 건조하게 본다."),
            Persona("predictor", "미래에서옴", "예측 놀이형 시청자", "다음 장애물과 결과를 근거 없이 자신 있게 예측하고 적중 여부를 즐긴다.", "예언하듯 단정하지만 틀리면 빠르게 웃고 넘긴다.", "조용한 구간, 장애물 접근, 저체력, 기록 근접", .5f, .35f, .3f, "F4A6C8", "미래에서옴|다음거맞음|예언적중률반반|촉만믿는다|결말보고옴", "근거 없는 확신이 강하다.|예측이 맞으면 크게 생색낸다.|틀린 예측도 새로운 예측으로 덮는다.", "곧 온다, 내가 봤다처럼 말한다.|결과를 단정한다.|틀리면 ㅋㅋ로 넘긴다."),
            Persona("peacekeeper", "겜이나보자", "싸움을 말리는 일반 시청자", "시청자끼리 말이 길어지면 적당히 그만하라고 하지만 가끔은 싸움 구경을 즐긴다.", "힘 뺀 반말. 진지하게 중재하기보다 왜 싸움 ㅋㅋ 같은 현실적인 반응을 한다.", "시청자 말싸움, 도배, 게임 오버, 큰 플레이", .34f, .45f, .12f, "B9D7D9", "겜이나보자|왜또싸움|팝콘가져옴|채팅천천히|둘다그만", "싸움이 길어지는 건 귀찮아한다.|누가 먼저 시작했는지는 은근히 궁금해한다.|게임에 큰 장면이 나오면 바로 관심을 돌린다.", "왜 싸움 ㅋㅋ처럼 가볍게 말한다.|둘 다 그만하라고 짧게 쓴다.|가끔 팝콘 드립을 친다."),
            Persona("rival_fan", "옆방에서옴", "라이벌 방송도 보는 비교형 시청자", "다른 플레이 스타일과 가볍게 비교하지만 타인을 깎아내리지 않는다.", "비교 관점의 담백한 반말. 좋은 플레이는 바로 인정한다.", "기록, 독특한 선택, 연속 성공, 게임 오버", .36f, .74f, .25f, "E7BE8A", "옆방에서옴|양쪽다구독함|비교관전중|멀티뷰어|다른루트파", "여러 플레이 방식을 봐서 기준이 다양하다.|편을 가르기보다 차이를 즐긴다.|예상과 다른 선택에 관심이 많다.", "다른 방식도 있다고 말한다.|비교 후 장점을 짚는다.|누구 편도 들지 않는 말투다.")
        };

        private static RunnerViewerPersonaData Persona(string id, string nickname, string role, string personality,
            string style, string triggers, float talkativeness, float expertise, float teasing, string htmlColor,
            string nicknames, string personalities, string styles)
        {
            ColorUtility.TryParseHtmlString("#" + htmlColor, out Color color);
            return new RunnerViewerPersonaData
            {
                id = id, nickname = nickname, role = role, personality = personality, speakingStyle = style,
                triggerInterests = triggers,
                forbiddenPatterns = "혐오, 차별, 성적 표현, 심한 욕설, 현실 인신공격, 같은 문장 반복 금지",
                nicknamePool = Split(nicknames), personalityVariations = Split(personalities), speakingStyleVariations = Split(styles),
                talkativeness = talkativeness, expertise = expertise, teasing = teasing, nameColor = color
            };
        }

        private static string[] Split(string value) => value.Split('|');
    }
}
