# WindowFix 인수인계 (다른 PC·세션용)

- 갱신: 2026-11-13 (Claude)
- 저장소: https://github.com/Mino7406/WindowFix — 브랜치 `main` (마지막 커밋 `725681a`)
- 이 문서는 사무실 PC 등 **이 대화를 모르는 다른 세션**이 바로 이어서 작업할 수 있게 쓴 것이다.

## 1. 무엇을 만드는 중인가
듀얼 모니터에서 디스코드·카카오톡처럼 늘 켜 두는 창을 **원하는 자리에 고정**하는 Windows 앱.
- 배치(프리셋) 저장/적용, 창 고정(움직이면 되돌림), 재부팅 후 복원, 화면 가장자리 고정 탭, 트레이 아이콘.
- **핵심 원칙(사용자 요구): 처음 쓰는 사람도 설명 없이 쓸 수 있는 UI.** 쉬운 한국어, 툴팁, 다음에 할 일을 알려 주는 상태 문구.
- 설계 문서: `docs/superpowers/specs/2026-11-12-windowfix-design.md` (5.5~5.7이 최근 결정), 초기 구현 계획: `docs/superpowers/plans/2026-11-12-windowfix.md`, 수동 점검표: `docs/manual-test-checklist.md`.

## 2. 새 PC에서 시작하기
```bash
git clone https://github.com/Mino7406/WindowFix.git
```
- .NET 10 SDK 필요: `winget install Microsoft.DotNet.SDK.10` (설치 전 사용자에게 확인). PATH에 없으면 `C:\Program Files\dotnet\dotnet.exe`.
- 테스트: `dotnet test` (현재 136개 전부 통과. 통합 테스트는 실제 창을 잠깐 띄움)
- 실행(개발): `dotnet run --project src/WindowFix`
- 배포 exe: `dotnet publish src/WindowFix -c Release -r win-x64 -o publish` → `publish/WindowFix.exe` 하나(자체 포함, 약 75MB)
  - **WindowFix가 실행 중이면 exe가 잠겨서 publish 실패.** 고정이 켜져 있으면 창을 닫아도 백그라운드에 남는다(정상 동작). 사용자 허락을 받고 프로세스를 종료한 뒤 publish, 끝나면 다시 실행해 줄 것.

## 3. 구조
- `src/WindowFix.Core` — 테스트 가능한 로직 + Win32
  - `Model/` 설정 모델(`AppConfig`, `Preset`, `WindowEntry`, `AppSettings`: Theme, AlwaysAdmin 등)
  - `Storage/ConfigStore.cs` 설정 읽기/쓰기(원자적 저장, 손상 시 .bak, 읽기 실패 시 덮어쓰지 않음)
  - `Layout/` 좌표 변환(모니터 기준), 모니터 찾기, 고정 탭 위치·모양 계산
  - `Matching/WindowMatcher.cs` 창 매칭(저장 당시 제목 `SavedTitle` 우선, 카톡 채팅창 같은 닮은 창 `IsDecoy` 제외)
  - `Apply/` 배치 저장(`PresetCapture`)·적용(`PresetApplier`, 결과 `ApplyOutcome`: NeedsAdmin, LaunchFailed 등)
  - `Lock/` 고정 판단(`LockTracker`), 되돌리기 제한(`SnapBackPolicy`, 사람이 직접 끈 건 항상 되돌림), 일시정지(`LockController`), WinEvent 훅(`WinEventLockService`)
  - `Startup/` 부팅 복원 시퀀스, 시작 등록(Run 키 / "항상 관리자"면 작업 스케줄러), 단일 실행, 종료 규칙(`AppLifetime`)
- `src/WindowFix` — WPF 화면
  - `MainWindow` 사이드바(배치 목록·우클릭 메뉴·설정) + 배치 화면(창 고정 카드, 미리보기, 이 배치의 창, 창 설정 패널) + 설정 탭
  - `SaveDialog` 새 배치 만들기 / 배치 수정(누른 모니터 크게, 나머지는 썸네일)
  - `Controls/MonitorPreview.cs` 모니터 미리보기(리테인드 모드, hover 들어올림 애니메이션)
  - `FloatingLockButton` 가장자리 고정 탭(A안: 6px 막대 → hover 시 펼침, 일시정지면 주황으로 펼친 채)
  - `Dialogs/AppDialog` Windows 기본 MessageBox/InputBox 대체
  - `Themes/Dark.xaml`, `Light.xaml` + `ThemeManager.cs`(시스템/라이트/다크, 시스템 따라가기)
  - `Tray/` 트레이 아이콘 + 테마 메뉴 렌더러
- `tests/WindowFix.Core.Tests` — xUnit

## 4. 사용자 관련해서 꼭 지킬 것
- 사용자 설정 파일 `%APPDATA%\WindowFix\config.json`(실제 배치가 들어 있음)은 **절대 덮어쓰거나 지우지 말 것.** 테스트는 별도 경로/가짜로.
- 업데이트해도 기존 사용자의 배치가 유지돼야 함 → 설정 모델에 필드를 추가할 땐 기본값을 안전하게, `Load_ConfigFromFirstRelease_KeepsLayoutsAndFillsNewSettings` 테스트 유지.
- **GitHub push는 사용자가 요청할 때만.** 커밋 메시지 끝에 `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- **릴리스(GitHub Releases)는 보류.** 사용자가 지시할 때까지 만들지 말 것.
- **자동 업데이트 기능은 사용자가 "1.0 완성"을 선언한 뒤에** 만든다(GitHub Releases 기반, 설계 메모는 6절).
- 사용자 PC에서 마우스를 움직이는 자동 클릭 테스트는 사용자가 거부한 적 있음 → 하지 말고, 화면 캡처/UI Automation 읽기 위주로.
- 응답은 한국어, 쉬운 말.

## 5. 현재 상태
- 동작 확인됨(실제 앱): 메인 화면, 설정 탭, 배치 수정 창(모니터 클릭), 모니터 hover, 저장 대화상자, 고정/되돌림(별도 테스트 창으로), 작업 스케줄러 등록 명령.
- **아직 사람이 직접 확인 안 된 것**: 우클릭 메뉴·트레이 메뉴의 새 모양, 삭제 확인 창, "항상 관리자 권한" 켜기(UAC 흐름), 메이플 등 관리자 권한 게임 고정, 새 고정 탭 펼침 동작, 다크 테마 전체, 재부팅 복원, 친구 PC(.NET 없는 PC) 실행. → `docs/manual-test-checklist.md`
- 사용자 PC 특이사항: 모든 프로그램이 관리자 권한으로 도는 환경으로 보임(UAC 꺼짐 추정). 관리자 관련 판단은 "WindowFix보다 권한이 높은 창"일 때만 걸리게 해 둠.

## 6. 남은 일 / 후보
1. 사용자 피드백 대기 중(애니메이션 끊김이 해결됐는지, 새 고정 탭 마음에 드는지).
2. 미뤄 둔 작은 문제(최종 리뷰에서 나온 것):
   - 응답 없는 창도 "맞췄어요"로 표시될 수 있음(비동기 SetWindowPos)
   - 최대화로 저장한 창을 다른 모니터로 보내면(Win+Shift+→) 되돌리지 않음
   - 모니터 배율 변경/모니터 분리 시 고정 탭이 가장자리에서 어긋날 수 있음
   - 부팅 직후 같은 프로그램 창 두 개가 잠깐 한 자리를 두고 겹칠 수 있음(5초 뒤 재적용으로 바로잡힘)
3. 1.0 선언 후: 자동 업데이트 — 시작 시/하루 1회 `releases/latest` 확인, 배너+트레이 알림, 내려받아 exe 교체 후 재실행, 설정 탭 "업데이트 자동 확인" 토글, README에 릴리스 절차.

## 7. 작업하면서 밟은 함정
- WPF `Window` 안에서 `ThemeMode`는 `Window.ThemeMode` 속성과 충돌 → `Core.Model.ThemeMode.X`로 쓸 것.
- XAML 주석에 `--` 넣으면 빌드 오류.
- `ContextMenu`를 `Style` Setter 안에 직접 넣고 `Click` 핸들러를 달면 실행 시 XAML 로드가 터짐(InvalidCastException) → 리소스로 분리(`LayoutMenu`).
- RadioButton 네비게이션은 `Click` 말고 `Checked`로(키보드/UIA 선택도 반응하게).
- 미리보기를 매 프레임 `InvalidateVisual`로 다시 그리면 끊김 → 자식 비주얼 + 변환 애니메이션으로.
- PowerShell에서 Win32 함수에 `$null` 문자열을 넘기면 빈 문자열로 바뀜.
- Git Bash heredoc 안에 따옴표가 많은 Python을 넣으면 깨지기 쉬움 → 스크립트를 파일로 저장해서 실행.
