# STREAM ON Prototype

## 실행

1. `Assets/Scenes/StreamOnPrototype.unity`를 연다.
2. Play를 누른다. 런타임 부트스트랩이 프로토타입 UI를 자동 생성한다.
3. 낮 행동으로 `게임 훈련` 또는 `멘탈 케어`를 하나 선택한다.
4. 60초 방송에서 흰색 커서가 초록 영역에 들어왔을 때 `지금이다!`를 누른다.
5. 성공, 실패, 연속 성공에 따라 오른쪽 `LIVE CHAT`의 시청자 반응을 확인한다.
6. 방송 결산 후 다음 날로 진행한다. 7일 생존 시 클리어된다.

## 현재 판정

- 방송 성공: 성공 5회 이상이며 성공 횟수가 실패 횟수 이상
- 성공 보상: 구독자 `10 + 성공 * 3 - 실패`, 멘탈 `-5`
- 실패 페널티: 구독자 감소, 멘탈 `-20`
- 게임 오버: 멘탈 또는 구독자 수가 0
- 훈련 효과: 타이밍 성공 영역이 조금씩 넓어진다.

## 코드 위치

- `Scripts/Core`: 세션, 상태, 페이즈, 방송 결과
- `Scripts/Gameplay`: 60초 타이밍 미니게임
- `Scripts/Chat`: 게임 이벤트별 로컬 시청자 채팅 생성
- `Scripts/UI`: 런타임 프로토타입 UI와 화면 전환

`StreamOnPrototypeBootstrap`은 씬 로드 후 자동 실행되므로 씬에 컴포넌트를 직접 연결할 필요가 없다.

## 2D 방송 러너

- 씬: `Assets/Scenes/BroadcastRunner.unity`
- 조작: `Space`/`↑` 점프, `C`/`↓`/`Left Ctrl` 구르기, 적이 사정거리 안에 왔을 때 `좌클릭` 공격, 게임 오버 후 `R` 재시작
- 캐릭터: `Art/Inhumania_Asset/CharacterSprite/mor`의 run/jump/rolling/attack2 스프라이트
- 애니메이션: `_Project/Animations/Runner`의 Animation Clip과 `MorRunner.controller`
- 카메라, 플레이어, 바닥, 장애물 풀, 스포너, HUD, 채팅 슬롯은 모두 씬 Hierarchy에 저장된다.
- 장애물은 `Obstacle Pool (preloaded)` 아래 오브젝트를 재사용하며 런타임에 생성하지 않는다.
