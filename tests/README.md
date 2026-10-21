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
