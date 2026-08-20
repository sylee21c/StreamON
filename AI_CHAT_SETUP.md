# STREAM ON AI 채팅 설정

러너 채팅은 기본적으로 OpenAI Responses API를 사용하며, API를 사용할 수 없으면 로컬 페르소나 채팅으로 자동 전환된다.

## 개발 PC에서 API 키 설정

PowerShell에서 다음 명령을 실행하되 `YOUR_KEY`를 실제 키로 교체한다.

```powershell
[Environment]::SetEnvironmentVariable("OPENAI_API_KEY", "YOUR_KEY", "User")
```

환경변수를 설정한 뒤 Unity Editor를 완전히 종료하고 다시 실행한다. 키는 Unity 씬, 소스 코드, `PlayerPrefs`에 저장하지 않는다.

`BroadcastRunner` 씬의 `Live Chat Panel > RunnerChatController`에서 다음 값을 조정할 수 있다.

- `Use Ai Chat`: AI 채팅 사용 여부
- `Endpoint`: 기본값 `https://api.openai.com/v1/responses`
- `Model`: 기본값 `gpt-5.6-luna`
- `Api Key Environment Variable`: 기본값 `OPENAI_API_KEY`
- `Require Api Key`: 자체 서버 프록시를 연결할 때만 끈다.
- `Custom Personas`: 추가한 `Viewer Persona` 에셋 목록

새 페르소나는 Project 창에서 `Create > STREAM ON > Runner > Viewer Persona`로 만들고 `Custom Personas`에 넣는다. 새 ID면 내장 로스터에 추가되고, 내장 페르소나와 같은 ID면 해당 설정을 덮어쓴다. `Replace Built In Personas`를 켜면 커스텀 목록만 사용한다. 기본 상태에서는 내장된 8명 중 매 방송 4~6명이 접속한다.

## 배포 주의

클라이언트 빌드에 API 키를 포함하면 추출될 수 있다. 실제 배포에서는 `Endpoint`를 자체 백엔드 프록시로 바꾸고 서버에서 API 키와 사용량 제한을 관리해야 한다. 프록시가 OpenAI Responses API 요청/응답 형식을 유지하면 현재 클라이언트 코드를 그대로 사용할 수 있다.
