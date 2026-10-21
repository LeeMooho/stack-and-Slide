# 게임 로직 테스트

.NET 8 SDK와 Python 3가 있는 환경에서 저장소 루트 기준으로 실행합니다.

```sh
python3 tests/run_gameplay_tests.py
```

Windows에서는 `py tests/run_gameplay_tests.py`를 사용할 수 있습니다. `dotnet`이 PATH에 없으면 설치 경로를 지정합니다.

```sh
python3 tests/run_gameplay_tests.py --dotnet /workspace/toolchains/dotnet/dotnet
```

실제 게임 스크립트와 테스트를 C# 7.3 설정으로 함께 컴파일합니다. NuGet 복원이나 Unity 설치 없이 .NET SDK의 컴파일러와 참조 어셈블리를 사용하므로 홈 폴더가 읽기 전용인 클라우드에서도 실행할 수 있습니다. 출력은 Git에서 제외된 `Gameplay.Tests/bin/isolated/` 아래에 임시로 생성하고 실행 후 정리합니다. 컴파일 또는 테스트가 실패하면 실패 종료 코드를 반환합니다.

일반 .NET 개발 환경에서는 기존 SDK 프로젝트를 직접 실행할 수도 있습니다.

```sh
dotnet run --project tests/Gameplay.Tests
```

테스트는 Unity API 대체 객체를 사용해 스테이지 데이터와 게임 로직을 검사합니다. Unity의 에셋 가져오기, 씬 수명 주기, 실제 물리·렌더링과 Android 빌드는 검사하지 않습니다. 변경 후 Unity 에디터의 Play 검증도 필요합니다.

모듈별 테스트는 `StageDataTests`, `PieceModuleTests`, `ProjectionModuleTests`, `InventoryPresenterTests`에 있습니다.
`StageIntegrationTests`는 새 스테이지·블록 ID가 씬 진입점에서 재고·생성·UI까지 연결되는지,
잘못된 등록이 기존 세션을 보존하는지, 생성·정리 콜백에서 세션을 바꿨을 때 이전 작업이 취소되는지 검사합니다.
판정 이벤트는 리스너가 다음 스테이지로 이동한 뒤에도 평가 당시 ID를 보존해야 합니다.

확장할 때의 책임 분리와 에셋 등록 순서는 [영어 모듈 안내](../docs/gameplay-architecture.md)를 참고하세요.

`ProgressionTests`는 점수·별·업적·최고 기록, 중복 방지, 저장 검증과 실패 복구를 검사합니다.
`GameplayExperienceTests`는 실제 세션과 HUD·업적·저장 연결, 콜백 순서, 모달 입력 잠금을 검사합니다.
`ExperienceHudTests`는 입력 연결, 안전 영역 계산, 모션 줄이기, 제한된 토스트·파티클 소유권을 검사합니다.
`BuiltInChallengeTests`는 추가 도전 102·103의 데이터와 사용자 정의 에셋 우선순위를 검사합니다.

Python 테스트는 검증 도구와 실제 에셋 데이터 기반 배치 증명을 함께 실행합니다.

```sh
python3 -m unittest discover -s tools -p 'test_*.py' -v
```

`test_challenge_layouts.py`는 실제 Unity YAML의 격자 레이와 활성 충돌체를 읽습니다.
이는 목표 배치의 기하학적 가능성을 확인하며 PhysX·고스트 착지·화면 렌더링을 실행하는 테스트는 아닙니다.
