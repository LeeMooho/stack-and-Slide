<h1>블록쌓기 게임</h1>

개발기간: 3개월 <br>
플랫폼: Android <br>
개발환경: C#, Unity <br>

## 개발 시작

Unity 프로젝트는 `stack and slide/` 폴더입니다. 저장된 에디터 버전은 **2019.4.16f1**입니다.

1. Unity Hub에서 해당 폴더를 프로젝트로 추가하고 엽니다.
2. Unity가 패키지를 다운로드하고 에셋을 다시 가져올 때까지 기다립니다.
3. `Assets/Scenes/stage1_1.unity`를 열고 Console의 컴파일 오류를 확인한 다음 Play를 실행합니다.

다른 Unity 버전으로 열면 패키지와 프로젝트 설정이 변환될 수 있으므로 복사본에서 먼저 확인하세요.
Android 빌드에는 해당 에디터의 Android Build Support가 추가로 필요합니다. 현재 Build Settings의 씬 목록은 비어 있으므로, 빌드할 씬을 명시적으로 추가해야 합니다.

## 저장소에 남기는 파일

`Assets`, `.meta`, `Packages/manifest.json`, `Packages/packages-lock.json`, `ProjectSettings`는 프로젝트 복원에 필요합니다.
플러그인, TextMesh Pro 리소스, 폰트, 음원도 자동 다운로드를 보장하는 설정이 없으므로 유지합니다.

`Library`, `Temp`, `obj`, `Logs`, 빌드 결과, IDE 캐시와 Unity가 생성한 `.csproj`·`.sln`은 제외합니다.
Unity가 프로젝트를 다시 가져오면 캐시를 생성하며, IDE 프로젝트 파일은 Unity Preferences의 External Tools에서 Regenerate project files로 다시 만들 수 있습니다.

## 게임 코드

- `StageManager.cs`: 블록 수와 8×8 스테이지 패턴.
- `stage.cs`: 스테이지 선택, 재고 UI, 블록 생성, 세 방향 투영 판정.
- `Block.cs`: 블록 이동·회전·착지 처리.
- `GhostBlock.cs`: 착지 위치 미리보기.
- `FPSDisplay.cs`: 프레임 시간과 FPS 표시.

씬과 프리팹에서 사용하는 공개 필드·메서드 이름과 스크립트 GUID를 유지합니다.
투영 판정은 `Block`, `OnFixedBlock`, `FixedBlock` 태그를 블록으로 취급하며, 필수 칸의 누락과 빈 칸의 추가 블록을 모두 검사합니다.

## 회귀 테스트

.NET 8 SDK가 있는 환경에서 저장소 루트 기준으로 실행합니다.

```sh
dotnet run --project tests/Gameplay.Tests
```

실제 게임 소스를 C# 7.3 문법으로 컴파일하며, 26개 테스트가 스테이지 데이터, 재고 UI, 7종 블록 생성 위치,
재고 소진, 투영 판정, 이동·회전·착지 로직을 검사합니다. 실패하면 종료 코드 1을 반환합니다.

이 테스트는 Unity API를 대체한 독립 로직 테스트입니다. 에셋 가져오기, Unity의 수명 주기·물리·렌더링,
Android 빌드를 검증하지 않으므로 Unity 에디터에서 Play 검증도 필요합니다.

플레이스토어
https://play.google.com/store/apps/details?id=happynoon.StackandSlide&pcampaignid=web_share

유튜브
https://www.youtube.com/watch?v=i3bIUjZIPn4

![image](https://github.com/LeeMooho/stack-and-Slide/assets/74390199/6a40cd5f-5894-480e-98b7-8dea3be6d2b8)
![image](https://github.com/LeeMooho/stack-and-Slide/assets/74390199/5267baea-87ac-413b-98d6-928ffc403ecf)
