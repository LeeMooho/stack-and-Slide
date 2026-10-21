# Unity 6.6에서 안전하게 업그레이드 검증하기

사용자 PC에는 Unity 6.6이 설치되어 있습니다. 정확한 패치 버전은 설치된 에디터의 `-version` 출력으로 확인합니다.
`6.6`이라는 제품 이름만으로 패치 번호나 LTS 여부를 가정하지 않습니다. 저장소에 기록된 원래 버전은 아직
`2019.4.16f1`이며, 이 문서와 도구를 추가한 것만으로 업그레이드가 완료된 것은 아닙니다.

현재 Linux 클라우드에서는 사용자 PC의 `C:\Program Files\Unity\`에 접근할 수 없으며 Unity 에디터·라이선스를 찾지 못했습니다.
따라서 이번 작업에서 실제 Unity 6.6 가져오기, Play 모드, Android 빌드를 통과했다고 주장하지 않습니다.

## Windows에서 설치본 확인

Unity Hub 프로그램 자체가 아니라 Unity Editor의 `Unity.exe`가 필요합니다. PowerShell에서 설치 경로를 찾을 수 있습니다.

```powershell
Get-ChildItem 'C:\Program Files\Unity\Hub\Editor' -Filter Unity.exe -Recurse |
    Select-Object -ExpandProperty FullName
```

다음 명령의 `<실제버전>`을 출력된 설치 폴더 이름으로 바꾸고, 저장소 루트에서 실행합니다. Python 3.10 이상이 필요합니다.
라이선스는 해당 PC의 Unity Hub에서 정상적으로 활성화되어 있어야 합니다. 계정·비밀번호·라이선스 내용을 명령이나 채팅에 넣지 마세요.

```powershell
py -3 tools\validate_unity.py `
    --editor 'C:\Program Files\Unity\Hub\Editor\<실제버전>\Editor\Unity.exe' `
    --output-dir 'C:\UnityValidation\stack-and-slide'
```

정확한 버전을 고정하고 검증하려면 `--expected-version '<실제버전>'`을 추가합니다.
알파(`a`)·베타(`b`) 버전도 실제 출력대로 보고하며 `editor_is_preview: true`로 표시합니다.
릴리스 검증에는 의도적으로 선택한 정식 버전을 사용하세요.

## 도구가 확인하는 것

도구는 다음 순서로 실제 에디터를 실행합니다.

1. `Unity.exe -version`이 성공하고 에디터 버전 하나를 식별하는지 확인합니다. 원본보다 오래된 에디터이면 가져오기를 중단합니다.
2. 체크아웃 밖의 새 디렉터리에 `Assets`, `Packages`, `ProjectSettings`만 복사합니다. 기존 `Library`와 Git 메타데이터는 가져가지 않습니다.
3. 복사본을 `-batchmode -nographics -accept-apiupdate -quit`으로 가져옵니다. API 변환은 복사본에만 적용됩니다.
4. 프로세스 종료 코드, 컴파일·패키지·라이선스 오류, 가져오기 로그의 실제 버전, 에디터가 저장한 `ProjectVersion.txt`, 새 `Assembly-CSharp.dll`을 확인합니다.
5. 원본 파일이 달라지지 않았는지 다시 검사하고 `report.json`, 로그, 변환된 복사본을 남깁니다.

성공 상태는 `validated_import`입니다. `project_converted_in_copy`는 복사본의 프로젝트 파일이 감지된 에디터 버전으로 변환됐다는 뜻입니다.
이는 **복사본의 가져오기·컴파일 검증** 결과이며
원본에 업그레이드를 적용했다는 뜻은 아닙니다. 종료 코드가 0이어도 로그 오류나 필수 증거가 없으면 실패합니다.
출력 폴더는 체크아웃 밖이어야 하며 파일을 덮어쓰지 않고 매번 새 폴더를 만듭니다. 기본 제한 시간은 15분이고 `--timeout`으로 바꿀 수 있습니다.
실패한 결과도 삭제하지 않으므로 보고서가 가리키는 로그를 진단할 수 있습니다.
타임아웃에는 도구가 시작한 프로세스만 정리합니다. Linux에서는 별도 프로세스 그룹을 종료하고,
Windows에서는 실행 중인 에디터 PID에 `taskkill /T /F`를 사용합니다. 이미 부모가 종료됐거나 별도로 분리된 프로세스는
운영체제의 그룹·트리에서 벗어날 수 있으므로, 실패 후 해당 검증 디렉터리를 사용하는 프로세스가 남았는지도 확인하세요.

이 도구는 Unity 씬 수명 주기, 실제 물리·렌더링, 키보드/터치 입력, Android 기기 동작을 검증하지 않습니다.
툴의 자동 테스트는 가짜 에디터로 격리·실패 처리만 검사하며 실제 Unity 통과 결과와 구분합니다.

```sh
python -m unittest discover -s tools -p 'test_validate_unity.py' -v
```

## 가져오기 이후 확인할 변경

패키지와 프로젝트 버전 번호를 먼저 손으로 바꾸지 않습니다. Unity가 변환한 복사본의 변경을 검토하고,
실제 Play 모드와 필요한 빌드까지 통과한 파일만 별도 커밋으로 원본에 반영합니다.

| 항목 | 현재 상태 | 확인할 내용 |
| --- | --- | --- |
| 렌더링 | 기존 Built-in 렌더 파이프라인 | 첫 업그레이드에서는 유지하고 재질·조명·카메라 확인 |
| UI | uGUI 및 기존 TextMesh Pro 에셋 | 씬/폰트 GUID, 버튼 이벤트, TMP 에셋 재임포트 확인 |
| 입력 | 기존 `UnityEngine.Input` | Legacy Input을 활성화한 상태에서 키보드·터치 동작 확인 |
| 에디터 도구 | Button Sounds Editor 플러그인 | prefab 자산 제외 및 실제 버튼 사운드 편집 확인 |
| 패키지 | 오래된 명시 버전과 잠금 파일 | 에디터의 패키지 변환 결과를 확인하고 필요한 버전만 고정 |
| Android | 최소 API 19, ARMv7만 저장됨 | 공식 6000.6.6f1 소스의 최소 API는 26. 설치된 패치의 요구 사항, ARM64·IL2CPP, 해당 에디터의 SDK/NDK/JDK로 기기 빌드 검증 |
| 빌드 씬 | `Assets/Scenes/stage1_1.unity`가 원래 GUID로 등록됨 | 실제 빌드에서 이 씬이 포함되는지 확인 |

`Assets/Scenes/stage1_1.unity`를 열고 메뉴 → 스테이지 101 선택 → 블록 생성·이동·회전·착지 → 투영 판정 → 메뉴 재진입을 검증합니다.
검은 목표 타일, 남은 블록 수, 고스트 위치와 블록 고정 이후 판정을 실제 화면에서 확인하세요.

UPM의 `latest` 태그는 정식 버전이라는 보장이 없습니다. 조사 시 TextMesh Pro는 `3.2.0-pre.15`, uGUI는
`3.0.0-exp.4`를 가리켰으므로 일괄 최신화하지 않습니다. 패키지 레지스트리의 내려받기 주소에는
`download.packages.unity.com`도 사용되므로 제한된 네트워크에서는 해당 공식 목적지의 접근도 확인해야 합니다.

## 조사 근거와 범위

- [Unity 공식 C# 소스의 6000.6.6f1 태그](https://github.com/Unity-Technologies/UnityCsReference/tree/6000.6.6f1)는 존재합니다. 이는 사용자 PC의 설치 버전 또는 최신 버전을 증명하지 않습니다.
- [PrefabUtility 공식 소스](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.6.6f1/Editor/Mono/Prefabs/PrefabUtility.cs): 오래된 `GetPrefabType` 대신 prefab 자산 여부를 직접 확인할 수 있습니다.
- [Android PlayerSettings 공식 소스](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.6.6f1/Editor/Mono/PlayerSettingsAndroid.bindings.cs): 실제 대상 에디터가 요구하는 최소 API와 지원 아키텍처를 확인할 근거입니다.
- 패키지 버전·최소 에디터 버전·의존성은 `https://packages.unity.com/<패키지 이름>`의 공식 레지스트리 메타데이터로 확인했습니다.
- 클라우드에서 공식 에디터 릴리스 API·매뉴얼 도메인은 네트워크 정책으로 접근이 차단되어 최신 버전과 설치 리비전을 확정하지 않았습니다. GitHub 소스 커밋 SHA는 에디터 다운로드 리비전으로 사용하면 안 됩니다.
