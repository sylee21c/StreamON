# STREAM ON — Claude 인수인계 문서

## 1. 프로젝트 한 줄 설명

`STREAM ON`은 낮에는 스트리머의 상태와 능력을 관리하고, 밤의 방송에서는 직접 게임을 플레이해 구독자와 멘탈을 지키는 2D 스트리머 생존/성장 게임이다. 현재는 메타 게임 프로토타입과 방송용 2D 러너 프로토타입이 각각 구현되어 있으며, 다음 핵심 작업은 두 프로토타입을 하나의 게임 루프로 통합하는 것이다.

## 2. 개발 환경

- Unity 버전: `6000.3.20f1` (Unity 6)
- 렌더 파이프라인: URP 17.3.0
- 입력: New Input System 1.19.0
- UI: uGUI 레이아웃 (`Button`, `Canvas`) + TextMeshProUGUI 텍스트
- 2D Tilemap/Physics2D 사용
- 프로젝트 경로: `C:\Users\black\Unity Projects\Stream ON`
- 현재 Git 저장소는 감지되지 않는다. 큰 변경 전에는 별도 백업 또는 Git 초기화를 권장한다.

## 3. 의도하는 최종 게임의 구체적인 형태

플레이어는 신입 스트리머를 운영한다. 하루는 다음과 같이 진행되는 구조를 목표로 한다.

1. 낮 행동 선택
   - 게임 훈련: 게임 실력이 상승한다.
   - 멘탈 케어: 멘탈을 회복한다.
2. 방송 시작
   - 시청자에게 보여 주는 실제 방송 콘텐츠로 2D 횡스크롤 러너를 플레이한다.
   - 캐릭터는 자동으로 앞으로 달리는 것처럼 보이며 실제로는 지면, 장애물, 적이 왼쪽으로 이동한다.
   - 점프, 구르기, 타이밍 공격으로 장애물과 적을 처리한다.
   - 플레이 결과에 따라 점수, 시청자 반응, 구독자 변화, 멘탈 변화가 결정된다.
3. 방송 결산
   - 성과와 실패를 요약한다.
   - 구독자와 멘탈을 반영한다.
4. 다음 날 진행
   - 현재 메타 프로토타입 기준 7일 동안 생존하면 클리어한다.
   - 구독자 또는 멘탈이 0이 되면 게임 오버다.

중요: 위 구조가 의도된 통합 방향이지만, 현재 코드에서 `StreamOnPrototype`의 7일 메타 루프와 `BroadcastRunner`는 아직 서로 호출하거나 결과를 교환하지 않는다. 현재는 독립 실행 가능한 두 프로토타입이다.

## 4. 현재 씬과 실행 진입점

### `Assets/Scenes/BroadcastRunner.unity`

- Build Settings의 첫 번째 활성 씬이다.
- 현재 가장 발전된 실제 플레이 씬이다.
- 씬에 카메라, 플레이어, 바닥 Tilemap 청크, 장애물 풀, 스포너, HUD, 게임 오버 UI, 라이브 채팅 UI가 저장되어 있다.
- 실행 즉시 러너가 시작된다. 별도의 Ready 카운트다운은 아직 없다.

주요 Hierarchy:

- `Environment`
  - `Sky Background`
  - `Ground`
  - `Scrolling Ground Tiles`
    - `Ground Chunk 1~3`
- `Systems`
  - `Runner Game Manager`
  - `Obstacle Spawner`
    - `Spawn Point`
- `Player`
  - `Mor Visual`
  - `Ground Check`
- `Obstacle Pool (preloaded)`
  - `Obstacle 1~5`
- `UI`
  - `HUD`
  - `Live Chat Panel`
  - `Controls`
  - `Game Over Panel`
- `Main Camera`
- `EventSystem`

### `Assets/Scenes/StreamOnPrototype.unity`

- 7일 메타 게임을 검증하기 위한 별도 프로토타입이다.
- `StreamOnPrototypeBootstrap`이 씬 로드 후 UI 전체를 런타임에 생성한다.
- 낮 행동 → 60초 타이밍 방송 → 결산 → 다음 날 흐름이 동작한다.
- 이 씬의 60초 흰색 커서 타이밍 게임은 초기 임시 방송 콘텐츠이며, 현재 러너와 연결되어 있지 않다.

### `Assets/Scenes/SampleScene.unity`

- Build Settings에서 비활성화된 샘플 씬이다.

## 5. 완료된 메타 게임 기능

관련 코드:

- `Assets/_Project/Scripts/Core/PlayerState.cs`
- `Assets/_Project/Scripts/Core/GameSession.cs`
- `Assets/_Project/Scripts/Core/GamePhase.cs`
- `Assets/_Project/Scripts/Core/BroadcastResult.cs`
- `Assets/_Project/Scripts/Gameplay/BroadcastMiniGame.cs`
- `Assets/_Project/Scripts/UI/StreamOnPrototypeBootstrap.cs`
- `Assets/_Project/Scripts/Chat/LocalChatService.cs`

구현 내용:

- 상태 데이터
  - 시작 일차: 1일
  - 시작 구독자: 100명
  - 시작 멘탈: 100
  - 시작 게임 실력: Lv.1
  - 말하기 실력 필드가 존재하지만 아직 실질적으로 사용하지 않는다.
- 페이즈
  - `Day`, `Broadcast`, `Settlement`, `GameOver`, `Clear`
- 낮 행동
  - 게임 훈련: 게임 실력 +1
  - 멘탈 케어: 멘탈 +30, 최대 100
- 임시 방송 미니게임
  - 60초 동안 좌우로 왕복하는 커서를 중앙 성공 구간에서 클릭한다.
  - 게임 실력이 높을수록 성공 구간이 넓어진다.
  - 성공 조건: 성공 5회 이상이며 성공 횟수가 실패 횟수 이상
  - 성공 시 구독자 변화: `10 + 성공 횟수 * 3 - 실패 횟수`
  - 실패 시 구독자 감소: `-max(5, 5 + 실패 횟수 * 2 - 성공 횟수)`
  - 성공 시 멘탈 -5, 실패 시 멘탈 -20
- 7일 루프
  - 구독자 또는 멘탈이 0이면 게임 오버
  - 7일 결산을 마치면 클리어
- 로컬 라이브 채팅
  - 방송 시작, 타이밍 성공/실패, 연속 성공, 방송 성공/실패에 따라 가짜 시청자 메시지가 출력된다.

## 6. 완료된 2D 러너 기능

### 6.1 기본 러너 루프

관련 코드:

- `Assets/_Project/Scripts/Minigames/Runner/RunnerGameManager.cs`
- `RunnerGroundLooper.cs`
- `RunnerObstacleSpawner.cs`
- `RunnerObstacle.cs`
- `RunnerHUD.cs`
- `RunnerChatController.cs`

동작:

- 플레이어의 X 위치는 고정되고 지면과 장애물이 왼쪽으로 움직인다.
- 시작 속도: 5.5
- 최대 속도: 12
- 초당 속도 증가: 0.08
- 기본 점수 증가: 초당 10점에 현재 속도 비율을 곱한다.
- 장애물을 통과하면 25점이 추가된다.
- 적을 처치하면 75점이 추가된다.
- 최고 점수는 `PlayerPrefs`의 `Runner.HighScore`에 저장된다.
- HP는 3이며 0이 되면 게임 오버다.
- 피격 후 무적 시간은 1초다.
- 게임 오버 후 `R` 또는 재시도 버튼으로 같은 씬에서 즉시 재시작한다.
- 명확한 스테이지 종료/승리 조건은 아직 없으며 현재는 HP가 0이 될 때까지 이어지는 무한 러너다.

### 6.2 플레이어 조작

관련 코드: `Assets/_Project/Scripts/Minigames/Runner/RunnerPlayerController.cs`

- 점프
  - `Space`, `↑`, `W`
  - 점프 힘 12
  - 코요테 타임 0.1초
  - 점프 입력 버퍼 0.12초
  - 지면 접촉 직후 애니메이터가 조기 Run 상태로 돌아가는 문제를 막는 별도 착지 판정이 있다.
- 구르기
  - 기본 키: `C`
  - 보조 키: `↓`, `Left Ctrl`
  - 지상에서만 가능하다.
  - 지속 시간 0.72초
  - 구르는 동안 BoxCollider2D의 높이와 오프셋을 줄여 머리 높이 장애물 아래로 통과한다.
  - 구르기 중에는 공격할 수 없다.
- 공격
  - 마우스 좌클릭 한 번
  - 지상에서만 가능하다.
  - 공격 쿨다운 0.72초
  - 플레이어 앞 거리 0.35~3.25 사이에 활성 적이 있을 때만 적을 처치한다.
  - 사정거리 밖에서 너무 일찍 누르면 공격 모션과 쿨다운이 소모되므로 적이 도착하기 전에 다시 공격하지 못할 수 있다. 이것이 현재 타이밍 판정이다.
  - 사정거리 안에서 가장 가까운 적 한 명만 처리한다.
  - 현재 적 처치는 애니메이션의 실제 칼날 프레임과 동기화된 Animation Event 방식이 아니라 클릭 순간의 거리 판정이다.

### 6.3 장애물과 적

`RunnerObstacleType`은 다음 3종이다.

- `Jump`
  - 바닥형 붉은 장애물
  - 점프로 넘긴다.
- `Roll`
  - 공중에 배치된 주황색 장애물
  - 서 있으면 맞고 구르면 축소된 콜라이더로 통과한다.
  - `spawnOffset.y = 1.45`
- `Enemy`
  - `mob1_walk.png`의 녹색 적 스프라이트를 사용한다.
  - 올바른 거리에서 좌클릭으로 처치한다.
  - 점프로 회피해서 플레이어 X선을 살아서 통과하면 `OnEnemyEscaped()`가 호출되어 플레이어가 피해를 입는다. 즉, 적은 공격으로 처리해야 한다.

장애물 풀:

- 총 5개 오브젝트를 사전 배치하여 재사용한다. 런타임 Instantiate/Destroy를 사용하지 않는다.
- 현재 구성: Jump 2개, Roll 1개, Enemy 2개
- 생성 간격: 1.4~2.6초 랜덤
- 풀에서 사용할 수 있는 오브젝트를 랜덤 시작 인덱스로 탐색한다.
- 화면 왼쪽 `x <= -12`에서 비활성화된다.
- 플레이어 통과 판정 X는 현재 `-5`로 하드코딩되어 플레이어 시작 X와 일치한다.

### 6.4 애니메이션과 사용 에셋

플레이어 원본 스프라이트 경로:

`Assets/Art/Inhumania_Asset/CharacterSprite/mor/`

- 달리기: `mor_run.png`
- 점프: `mor_jump.png`
- 구르기: `mor_rolling.png`
- 공격: `mor_attack2.png`
- 피격: `mor_hit.png`
- 사망: `mor_die.png`

생성된 클립/컨트롤러:

`Assets/_Project/Animations/Runner/`

- `Mor_Run.anim`
- `Mor_Jump.anim`
- `Mor_Roll.anim`
- `Mor_Attack.anim`
- `Mor_Hurt.anim`
- `Mor_Dead.anim`
- `MorRunner.controller`
- `Enemy_Walk.anim`
- `RunnerEnemy.controller`

적 원본 스프라이트:

- `Assets/Art/Inhumania_Asset/CharacterSprite/Enemies/mob1_walk.png`

에디터 자동 설정 코드:

- `Assets/_Project/Editor/RunnerAnimatorSetup.cs`
  - 누락된 클립과 Animator 파라미터/상태를 만든다.
  - `Roll`, `Attack` 트리거를 컨트롤러에 추가한다.
  - 적 Walk 컨트롤러를 만든다.
  - 기존 `BroadcastRunner` 씬의 풀 오브젝트 타입, 스프라이트, 콜라이더, 조작 안내를 보정한다.
- `RunnerSceneBuilder.cs`
  - 러너 씬이 전혀 없을 때 프로토타입 씬을 만드는 초기 도구다.
- `RunnerGroundSetup.cs`
  - 편집 가능한 Tilemap 지면 청크를 구성한다.

현재 애니메이션 에셋들은 실제로 생성된 상태다. 에디터 자동 설정 스크립트가 `[InitializeOnLoad]`로 씬과 에셋을 변경할 수 있으므로, 관련 씬을 수동 편집할 때 자동 보정 코드와 충돌하지 않는지 확인해야 한다.

### 6.5 러너 UI와 라이브 채팅

- HUD
  - SCORE
  - BEST
  - SPEED
  - HP 하트 표시
- 조작 안내
  - `SPACE / UP : JUMP`
  - `C / DOWN : ROLL`
  - `LMB : ATTACK`
- 게임 오버 패널과 재시도 버튼
- 오른쪽 LIVE CHAT 패널
  - 러너 시작
  - 점프
  - 장애물 통과
  - 적 처치
  - 피격
  - 낮은 체력
  - 게임 오버
  - 신기록
  이벤트에 반응한다.
- 채팅은 실제 네트워크/스트리밍 연동이 아니라 로컬 랜덤 문자열이다.
- 프로젝트 전용 UI 텍스트는 모두 TMP 타입을 사용한다. 한글 지원을 위해 실행 시 `Malgun Gothic` 기반 동적 TMP 폰트를 적용한다.

## 7. 현재 검증 상태

- `dotnet build "Stream ON.sln" --no-restore` 기준 C# 오류 0개
- 기존 Unity/외부 패키지의 `System.Net.Http`, `System.IO.Compression` 버전 충돌 경고 2개가 있으나 이번 기능 코드 오류는 아니다.
- 점프/구르기/적 타입 5개와 적 스프라이트가 씬에 직렬화되어 있다.
- Roll/Attack/Enemy Walk 클립과 컨트롤러가 생성되어 있다.
- 이 환경에서는 Unity headless 라이선스가 없어 배치 PlayMode 실행은 하지 못했다. 일반 Unity Editor에서 실제 체감, 애니메이션 전환, 콜라이더를 반드시 플레이 테스트해야 한다.

## 8. 아직 완료되지 않은 핵심 작업

### 최우선: 메타 게임과 러너 통합

- `StreamOnPrototype`의 방송 시작 시 임시 60초 타이밍 바 대신 `BroadcastRunner`를 실행할지 결정해야 한다.
- 러너 결과를 `BroadcastResult`로 변환해야 한다.
  - 예: 점수, 처치 수, 피격 수, 생존 시간으로 성공 여부/구독자 증감/멘탈 증감 계산
- 러너 종료 후 Settlement 화면으로 돌아와야 한다.
- 낮 행동의 `GameSkill`이 러너에 어떤 영향을 줄지 설계해야 한다.
  - 공격 사정거리/쿨다운
  - 점프/구르기 허용 시간
  - 시작 HP
  - 점수 배율
  중 하나 또는 복수로 연결 가능하다.
- `TalkingSkill`도 시청자 증가량이나 채팅 분위기에 연결할 수 있다.
- 현재 Build Settings 첫 씬이 러너이므로 최종 시작 씬과 게임 흐름을 재정의해야 한다.

### 러너 게임플레이 완성도

- 시작 전 3-2-1 카운트다운과 일시정지
- 명확한 방송 제한 시간 또는 스테이지 종료 조건
- 공격 판정을 Animation Event/히트 프레임과 동기화
- 적 피격/사망 애니메이션과 즉시 사라지지 않는 짧은 연출
- 공격 성공/실패 이펙트, 사운드, 화면 흔들림
- 피격 넉백 또는 시각적 무적 깜빡임
- 장애물 등장 전 가독성/텔레그래프 개선
- 난이도 곡선에 따른 생성 간격과 타입 가중치 조정
- 동시에 생성된 장애물 조합이 불가능한 패턴을 만들지 않는지 검사
- `FindObjectsByType<RunnerObstacle>`를 매 공격마다 호출하는 대신 스포너/풀에서 활성 적을 조회하도록 최적화 가능
- 플레이어 X `-5` 하드코딩을 실제 player Transform 참조로 교체
- 키 리바인딩/게임패드/모바일 입력은 아직 없다.

### 메타/콘텐츠 확장

- 저장/불러오기
- 설정 화면과 음량 조절
- 여러 방송 게임 또는 이벤트
- 구독자 목표, 스폰서, 장비 업그레이드
- 날마다 달라지는 사건과 채팅 반응
- 튜토리얼과 첫 플레이 안내
- 최종 UI/아트 스타일 통일
- 실제 오디오와 BGM

### 품질/도구

- EditMode/PlayMode 테스트 추가
- Git 저장소와 `.gitignore` 정비
- 런타임 Bootstrap UI와 씬 직렬화 UI 중 최종 방식을 결정해 일관화
- `[InitializeOnLoad]` 에디터 스크립트가 매 로드 시 씬을 수정하지 않도록 버전 기반 마이그레이션 또는 수동 메뉴 도구로 전환 고려

## 9. 다음 작업 권장 순서

1. `BroadcastRunner`를 일반 Unity Editor에서 플레이 테스트한다.
   - Run/Jump/Roll/Attack/Hurt/Dead 전환
   - C 구르기 콜라이더
   - 적 좌클릭 거리 판정
   - 점프로 적을 넘겼을 때 피해
   - 재시작 후 콜라이더와 애니메이터 초기화
2. 발견된 애니메이션 위치/크기/피벗/콜라이더 문제를 수정한다.
3. 러너 한 판의 종료 조건과 결과 데이터 구조를 만든다.
4. `GameSession`과 러너 결과를 연결해 낮 행동 → 러너 방송 → 결산 → 다음 날 루프를 완성한다.
5. 게임 실력과 말하기 실력을 러너 및 구독자 보상에 연결한다.
6. 사운드/VFX/UI 피드백과 콘텐츠를 확장한다.
7. 저장/테스트/빌드 흐름을 정리한다.

## 10. Claude에게 요청할 때 사용할 작업 원칙

- 기존 에셋 스프라이트를 우선 사용한다. 특히 Mor의 동작은 `CharacterSprite/mor` 원본 시트를 그대로 사용한다.
- 기존 사용자의 변경을 보존하고 관련 없는 에셋을 삭제하거나 대규모 재생성하지 않는다.
- `BroadcastRunner.unity`에는 씬 참조가 직렬화되어 있으므로 필드명 변경 시 Missing Reference 여부를 확인한다.
- New Input System을 유지한다. 레거시 `Input.GetKey`와 혼용하지 않는다.
- 오브젝트 풀 구조를 유지하고 플레이 중 반복 Instantiate/Destroy를 피한다.
- 변경 후 C# 컴파일뿐 아니라 Unity Editor PlayMode에서 입력, 애니메이션, 콜라이더를 검증한다.
- 통합 방향이 불명확하면 먼저 “임시 타이밍 미니게임을 러너로 완전히 교체할지, 여러 방송 게임 중 하나로 유지할지”를 사용자에게 확인한다.

## 11. Claude에게 그대로 보낼 수 있는 시작 메시지

아래 프로젝트를 이어서 개발해 줘.

프로젝트 경로는 `C:\Users\black\Unity Projects\Stream ON`이고 Unity `6000.3.20f1`, URP, New Input System 기반이야. 루트의 `CLAUDE_HANDOFF.md`를 처음부터 끝까지 읽고, 그 문서에 적힌 현재 구조와 완료/미완료 상태를 기준으로 작업해 줘. 이 게임은 낮에 스트리머를 훈련하거나 멘탈을 관리하고, 방송에서는 Mor 캐릭터로 자동 진행형 2D 러너를 플레이해 구독자와 멘탈을 지키는 7일 생존/성장 게임이야.

현재 러너에는 점프, C 구르기, 좌클릭 타이밍 공격, 점프/구르기/적 장애물, HP, 점수, 최고 기록, 난이도 상승, 로컬 라이브 채팅, 재시작이 구현되어 있어. Mor의 run/jump/rolling/attack2/hit/die 원본 스프라이트와 mob1_walk 적 스프라이트를 사용하고 있어. 별도의 `StreamOnPrototype` 씬에는 낮 행동, 60초 임시 타이밍 방송, 결산, 7일 클리어/게임 오버 메타 루프가 있지만 러너와 아직 연결되지 않았어.

먼저 기존 파일을 조사하고 일반 Unity Editor에서 `BroadcastRunner`를 플레이 테스트해 현재 구현이 실제로 정상 동작하는지 확인해 줘. 사용자 변경을 보존하고, 관련 없는 파일은 건드리지 마. 다음 큰 목표는 임시 타이밍 방송과 러너의 관계를 나에게 확인한 뒤, 낮 행동 → 러너 방송 → 결과 결산 → 다음 날로 이어지는 통합 게임 루프를 만드는 것이야. 모든 변경 후 컴파일과 PlayMode를 검증하고, 변경 파일과 확인 결과를 구체적으로 보고해 줘.
