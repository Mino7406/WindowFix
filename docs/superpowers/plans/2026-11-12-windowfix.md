# WindowFix 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 창 배치를 프리셋으로 저장·적용하고, 잠금과 재부팅 후 복원으로 그 배치를 지키는 Windows 데스크톱 앱 WindowFix를 만든다.

**Architecture:** 두 부분으로 나눈다.
- `WindowFix.Core` (클래스 라이브러리): 순수 로직(모델, 설정 저장, 좌표 변환, 창 매칭, 프리셋 적용, 잠금 판단, 시작 시퀀스)과 Win32 상호운용. 순수 로직은 `IWindowSystem` 같은 인터페이스 뒤에서 동작하므로 가짜 구현으로 단위 테스트할 수 있다.
- `WindowFix` (WPF 앱): 화면(메인 창, 저장 대화상자, 떠 있는 고정 버튼, 트레이)과 앱 생명주기. 얇게 유지하고, Core를 연결하는 일만 한다.

**Tech Stack:**
- C# 14 / .NET 10 (`net10.0-windows`)
- WPF. 트레이 아이콘에만 WinForms `NotifyIcon`을 쓴다.
- xUnit
- System.Text.Json
- P/Invoke: user32, dwmapi, kernel32

**Spec:** `docs/superpowers/specs/2026-11-12-windowfix-design.md` (구현자는 이 계획과 함께 반드시 읽는다)

## Global Constraints

- 대상 프레임워크: `net10.0-windows`. 앱에는 `<UseWPF>true</UseWPF>`와 `<UseWindowsForms>true</UseWindowsForms>`를 켠다.
- 배포: `win-x64`, self-contained, 단일 파일 exe. 사용하는 PC에 .NET 설치가 필요 없어야 한다.
- DPI: `app.manifest`에 `PerMonitorV2`를 설정한다. 모든 좌표는 물리 픽셀로 다룬다.
- 관리자 권한을 요구하지 않는다. 매니페스트는 `asInvoker`로 둔다.
- 설정 파일: `%APPDATA%\WindowFix\config.json`
- 시작 프로그램 등록: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. 값 이름은 `WindowFix`, 값은 `"<exe 경로>" --startup`이다.
- UI 문구는 한국어로 쓴다.
- 잠금은 창 스타일(WS_THICKFRAME 등)을 바꾸지 않는다. `SetWinEventHook`의 out-of-context 방식만 쓴다.
- **잠금 설정이 꺼져 있으면 메인 창을 닫을 때 프로세스가 종료돼야 한다.**
- 시작 시 대기: 2초 간격으로 확인하고, 최대 3분 기다린다. 모든 창을 적용한 뒤 5초 후에 한 번 더 적용한다.
- 고정 버튼:
  - 크기: 지름 40px
  - 투명도: 평소 0.4, 마우스를 올리면 1.0
  - 기본 위치: 서브모니터 오른쪽 아래, 가장자리에서 24px 안쪽
  - 일시 해제 상태는 저장하지 않는다.

## Review Focus

1. **서브모니터가 주 모니터 왼쪽이나 위에 있는 경우.** 이때 좌표가 음수가 된다. 저장한 뒤 적용하면 정확히 같은 자리로 돌아와야 한다. → Task 2 `RoundTrip_NegativeMonitorOrigin`
2. **설정 파일이 깨지거나 반쯤만 써진 경우**(전원이 나간 경우 등). 앱이 기본값으로 켜지고, 깨진 파일은 `.bak`으로 남겨야 한다. → Task 1 `Load_CorruptFile_ReturnsDefaultsAndBacksUp`. 저장은 임시 파일에 쓴 뒤 교체하는 방식으로 한다. → Task 1 `Save_IsAtomic_TempThenReplace`
3. **앱이 스스로 크기를 계속 바꾸는 경우**(Discord 로딩 중 등). 잠금이 몇 번 되돌린 뒤 포기하고 CPU를 잡아먹지 않아야 한다. → Task 6 `SnapBackPolicy_GivesUpAfterLimit`
4. **같은 exe 창이 여러 개인 경우**(카카오톡 메인 창과 채팅방 창). 한 항목이 엉뚱한 창을 잡지 않아야 하고, 한 창이 두 항목에 동시에 잡히지 않아야 한다. → Task 3 `PickBest_PrefersClassMatchThenLargest`, Task 5 `Apply_SameWindowNotUsedTwice`
5. **응답 없는 창이나 관리자 권한으로 실행된 창.** 적용이 멈추지 않고 그 창만 실패로 보고해야 한다. → Task 4는 `SWP_ASYNCWINDOWPOS`를 쓴다. Task 5 `Apply_SetPlacementFails_ReportsAccessDenied`

---

## 파일 구조

```
WindowFix.sln
src/WindowFix.Core/
  WindowFix.Core.csproj
  Model/Config.cs              — AppConfig, AppSettings, Preset, WindowEntry, WindowMatch, MonitorRef
  Model/Geometry.cs            — PixelRect, RelRect, MonitorInfo, WindowInfo
  Storage/ConfigStore.cs       — JSON 읽기/쓰기(원자적 저장, 손상 복구)
  Layout/Placement.cs          — 절대↔모니터 상대 좌표 변환, clamp
  Layout/MonitorResolver.cs    — 저장된 MonitorRef → 현재 MonitorInfo
  Matching/WindowMatcher.cs    — 매칭, 후보 필터, 최적 후보 선택, 제목 조건 제안
  Platform/IWindowSystem.cs    — 창/모니터/프로세스 추상화
  Platform/Win32WindowSystem.cs — 실제 구현(P/Invoke)
  Platform/NativeMethods.cs
  Apply/PresetCapture.cs       — 선택한 창들 → Preset
  Apply/PresetApplier.cs       — Preset 적용 → ApplyReport
  Lock/SnapBackPolicy.cs       — 되돌리기 횟수 제한
  Lock/LockTracker.cs          — 창↔항목 바인딩, 이벤트별 판단
  Lock/LockController.cs       — Off / Active / Paused 상태
  Lock/WinEventLockService.cs  — SetWinEventHook 연결(얇은 glue)
  Startup/StartupSequence.cs   — 시작 시 대기·적용 루프
  Startup/StartupRegistration.cs — Run 키 동기화
  Startup/SingleInstance.cs    — Mutex + 이벤트로 "창 열어" 신호
src/WindowFix/
  WindowFix.csproj, app.manifest, App.xaml(.cs)
  AppHost.cs                   — 생명주기: --startup, 창 닫기, 종료 판단
  MainWindow.xaml(.cs)
  Controls/MonitorPreview.cs   — 모니터와 창 사각형을 그리는 Canvas
  SaveDialog.xaml(.cs)
  FloatingLockButton.xaml(.cs)
  Tray/TrayIcon.cs
tests/WindowFix.Core.Tests/
  (Core 파일마다 *Tests.cs, Fakes/FakeWindowSystem.cs, Fakes/FakeClock.cs)
```

---

### Task 1: 솔루션 뼈대, 모델, 설정 저장

**Files:**
- Create: `WindowFix.sln`, `src/WindowFix.Core/WindowFix.Core.csproj`, `src/WindowFix.Core/Model/Config.cs`, `src/WindowFix.Core/Model/Geometry.cs`, `src/WindowFix.Core/Storage/ConfigStore.cs`
- Create: `src/WindowFix/WindowFix.csproj` (빈 WPF 앱, 빌드만 되면 됨), `tests/WindowFix.Core.Tests/WindowFix.Core.Tests.csproj`
- Test: `tests/WindowFix.Core.Tests/ConfigStoreTests.cs`

**Interfaces:**
- Produces (Model/Geometry.cs):
  - `readonly record struct PixelRect(int X, int Y, int Width, int Height)`
    - 속성: `Right`, `Bottom`
  - `readonly record struct RelRect(int X, int Y, int Width, int Height)`
    - 모니터의 왼쪽 위를 기준으로 한 좌표
  - `record MonitorInfo(string DevicePath, string GdiName, PixelRect Bounds, PixelRect WorkArea, bool IsPrimary)`
  - `record WindowInfo(nint Handle, string ExeName, string? ExePath, string Title, string ClassName, PixelRect Rect, bool IsVisible, bool IsMinimized, bool IsMaximized, bool IsOwned, bool IsToolWindow, bool IsCloaked, int ProcessId)`
- Produces (Model/Config.cs, 직렬화 가능한 class와 `init` 속성):
  - `WindowMatch`
    - `string ExeName`
    - `string? TitleContains`
    - `string? ClassName`
  - `MonitorRef`
    - `string DevicePath`
    - `bool IsPrimary`
    - `int Width`, `int Height`
  - `WindowEntry`
    - `Guid Id`
    - `WindowMatch Match`
    - `MonitorRef Monitor`
    - `RelRect Rect`
    - `bool Maximized`
    - `bool LaunchIfMissing`
    - `string? LaunchPath`, `string? LaunchArgs`
    - `string DisplayName`
  - `Preset`
    - `Guid Id`
    - `string Name`
    - `List<WindowEntry> Windows`
  - `AppSettings`
    - `bool LockEnabled`
    - `Guid? CurrentPresetId`
    - `Guid? StartupPresetId`
    - `bool ShowTrayIcon` (기본값 `false`)
    - `bool ShowLockButton` (기본값 `true`)
    - `int? LockButtonX`, `int? LockButtonY`
  - `AppConfig`
    - `int Version = 1`
    - `List<Preset> Presets`
    - `AppSettings Settings`
- Produces (Storage/ConfigStore.cs): `sealed class ConfigStore(string path)`
  - `AppConfig Load()`
  - `void Save(AppConfig config)`
  - `static string DefaultPath`
    - `%APPDATA%\WindowFix\config.json`

- [ ] **Step 1: .NET 10 SDK 설치 확인.** `dotnet --list-sdks`에 `10.` 이 없으면 `winget install Microsoft.DotNet.SDK.10`로 설치한다. 이 PC는 2026-11-12 기준 런타임만 있고 SDK가 없다. 설치 전에 사용자에게 확인한다.
- [ ] **Step 2: 솔루션과 프로젝트 생성.**
  - `dotnet new sln -n WindowFix`
  - `dotnet new classlib`: `src/WindowFix.Core`, 대상 프레임워크 `net10.0-windows`, `<Nullable>enable</Nullable>`
  - `dotnet new wpf`: `src/WindowFix`
  - `dotnet new xunit`: `tests/WindowFix.Core.Tests`
  - 참조를 연결하고 `dotnet build`가 성공하는지 확인한다.
- [ ] **Step 3: 실패하는 테스트 작성** (`ConfigStoreTests`, 각 테스트는 임시 폴더를 쓴다)
  - `Load_MissingFile_ReturnsDefaults`
    - `Presets`가 비어 있음
    - `Settings.ShowLockButton == true`
    - `Settings.ShowTrayIcon == false`
    - `Settings.LockEnabled == false`
  - `SaveThenLoad_RoundTripsPresetWithAllFields`
    - 모든 필드를 채운 프리셋을 저장했다가 읽으면 값이 전부 같음. `RelRect`의 음수 X도 포함.
  - `Load_CorruptFile_ReturnsDefaultsAndBacksUp`
    - 파일 내용 `"{ not json"` → 기본값 반환
    - `config.json.bak`이 생기고 원래 내용이 그대로 들어 있음
  - `Save_IsAtomic_TempThenReplace`
    - 저장 후 `config.json.tmp`가 남아 있지 않음
    - 폴더가 없으면 만들어서 저장함
- [ ] **Step 4: 실패 확인.** `dotnet test --filter ConfigStoreTests`를 실행하면 컴파일 오류 또는 FAIL이 나야 한다.
- [ ] **Step 5: 모델과 `ConfigStore` 구현.**
  - `JsonSerializerOptions { WriteIndented = true }`를 쓴다.
  - 저장 순서: `.tmp`에 쓰기 → `File.Replace` (대상 파일이 없으면 `File.Move`)
- [ ] **Step 6: 통과 확인.** `dotnet test --filter ConfigStoreTests` → 4개 PASS
- [ ] **Step 7: 커밋.** `.gitignore`, 솔루션, 프로젝트 파일 전부를 넣는다. 메시지: `feat: scaffold solution with config model and store`

---

### Task 2: 좌표 변환과 모니터 찾기

**Files:**
- Create: `src/WindowFix.Core/Layout/Placement.cs`, `src/WindowFix.Core/Layout/MonitorResolver.cs`
- Test: `tests/WindowFix.Core.Tests/PlacementTests.cs`, `tests/WindowFix.Core.Tests/MonitorResolverTests.cs`

**Interfaces:**
- Consumes: `PixelRect`, `RelRect`, `MonitorInfo`, `MonitorRef` (Task 1)
- Produces:
  - `static class Placement`
    - `RelRect ToRelative(PixelRect abs, MonitorInfo m)`
    - `PixelRect ToAbsolute(RelRect rel, MonitorInfo m)`
      - 해상도가 바뀌었으면 `m.Bounds` 안으로 clamp한다. 크기를 먼저 줄이고, 그다음 위치를 옮긴다.
    - `MonitorInfo MonitorOf(PixelRect abs, IReadOnlyList<MonitorInfo> monitors)`
      - 창과 가장 많이 겹치는 모니터를 고른다. 하나도 안 겹치면 주 모니터를 고른다.
    - `MonitorRef ToRef(MonitorInfo m)`
  - `static class MonitorResolver`
    - `MonitorInfo? Resolve(MonitorRef saved, IReadOnlyList<MonitorInfo> current)`
    - 찾는 순서:
      1. `DevicePath`가 같은 모니터 (대소문자 무시)
      2. `IsPrimary`와 `Width`/`Height`가 모두 같은 모니터 (첫 번째로 찾은 것)
      3. 둘 다 없으면 `null`

- [ ] **Step 1: 실패하는 테스트 작성**
  - `RoundTrip_SameMonitor_IsExact`
    - 모니터 (1920,0,1920,1080), 창 (1925,10,1500,900)
    - `ToRelative` 결과가 (5,10,1500,900)이고, `ToAbsolute`로 되돌리면 원래 값
  - `RoundTrip_NegativeMonitorOrigin`
    - 모니터 (-2560,-200,2560,1440), 창 (-2550,-190,1557,930)
    - 왕복하면 원래 값과 같음
  - `ToAbsolute_MonitorMovedButSameSize_KeepsRelativeSpot`
    - 저장 당시 모니터 원점 (1920,0), 지금 원점 (-1920,0), RelRect (5,10,100,100) → (-1915,10,100,100)
  - `ToAbsolute_SmallerResolution_ClampsInsideBounds`
    - RelRect (1800,0,1557,930), 모니터 1920×1080
    - 결과 사각형이 모니터 안에 들어감
    - 너비는 그대로 1557, X는 363 (1920−1557)
  - `ToAbsolute_WindowLargerThanMonitor_ShrinksToBounds`
  - `MonitorOf_PicksLargestOverlap`, `MonitorOf_NoOverlap_ReturnsPrimary`
  - `Resolve_ByDevicePath_EvenIfGdiNameChanged`
  - `Resolve_FallbackPrimaryFlagAndResolution`
  - `Resolve_NothingMatches_ReturnsNull`
- [ ] **Step 2: 실패 확인.** `dotnet test --filter "PlacementTests|MonitorResolverTests"` → FAIL
- [ ] **Step 3: `Placement`와 `MonitorResolver` 구현.**
- [ ] **Step 4: 통과 확인.** 같은 필터 → 전부 PASS
- [ ] **Step 5: 커밋.** 메시지: `feat: monitor-relative placement and monitor resolution`

---

### Task 3: 창 매칭

**Files:**
- Create: `src/WindowFix.Core/Matching/WindowMatcher.cs`
- Test: `tests/WindowFix.Core.Tests/WindowMatcherTests.cs`

**Interfaces:**
- Consumes: `WindowInfo`, `WindowMatch` (Task 1)
- Produces: `static class WindowMatcher`
  - `bool IsCandidate(WindowInfo w)`
    - 조건: 보임, 소유자 없음, 도구 창 아님, cloaked 아님, 제목이 비어 있지 않음
  - `bool Matches(WindowMatch m, WindowInfo w)`
    - `ExeName`: 대소문자 무시하고 같아야 함
    - `TitleContains`: 있으면 제목에 포함돼야 함 (대소문자 무시)
    - `ClassName`: 있으면 같아야 함
  - `WindowInfo? PickBest(WindowMatch m, IEnumerable<WindowInfo> windows, ISet<nint> taken)`
    - `taken`에 든 창은 제외한다.
    - 매칭되는 창 중 `ClassName`까지 일치하는 창을 우선한다. 그다음은 넓이가 큰 창이다.
  - `IReadOnlyList<WindowMatch> SuggestMatches(IReadOnlyList<WindowInfo> selected)`
    - 각 창마다 `ExeName`과 `ClassName`을 채운다.
    - 선택한 창 중 같은 exe가 2개 이상이면 그 창들의 `TitleContains`에 현재 제목을 넣는다. 아니면 `null`이다.

- [ ] **Step 1: 실패하는 테스트 작성**
  - `IsCandidate_RejectsOwnedToolCloakedUntitledInvisible`: 다섯 경우 각각 false
  - `Matches_ExeCaseInsensitive`: `"discord.exe"`가 `"Discord.exe"`와 매칭됨
  - `Matches_TitleContains_CaseInsensitive`: 조건 `"youtube"`, 제목 `"재생목록 - YouTube - Chrome"` → 매칭
  - `PickBest_PrefersClassMatchThenLargest`
    - KakaoTalk.exe 창 두 개: 클래스 `"EVA_Window_Dblclk"` 800×600, 클래스 `"Other"` 1200×900
    - 조건 클래스가 `"EVA_Window_Dblclk"`이면 앞의 창을 고름
  - `PickBest_SkipsTaken`
  - `SuggestMatches_SameExeTwice_FillsTitle`, `SuggestMatches_UniqueExe_NoTitle`
- [ ] **Step 2: 실패 확인.** `dotnet test --filter WindowMatcherTests` → FAIL
- [ ] **Step 3: `WindowMatcher` 구현.**
- [ ] **Step 4: 통과 확인.** 같은 필터 → PASS
- [ ] **Step 5: 커밋.** 메시지: `feat: window matching and candidate selection`

---

### Task 4: Win32 플랫폼 계층

**Files:**
- Create: `src/WindowFix.Core/Platform/IWindowSystem.cs`, `Platform/NativeMethods.cs`, `Platform/Win32WindowSystem.cs`
- Test: `tests/WindowFix.Core.Tests/Win32WindowSystemTests.cs` (`[Trait("Category","Integration")]`)

**Interfaces:**
- Consumes: `WindowInfo`, `MonitorInfo`, `PixelRect` (Task 1)
- Produces: `interface IWindowSystem`
  - `IReadOnlyList<WindowInfo> GetWindows(bool includeHidden)`
  - `IReadOnlyList<MonitorInfo> GetMonitors()`
  - `WindowInfo? GetWindow(nint hwnd)`
  - `bool SetPlacement(nint hwnd, PixelRect rect, bool maximize)`
    - 최소화나 최대화 상태면 먼저 `ShowWindow(SW_RESTORE)` 한다.
    - 그다음 `SetWindowPos(... SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS)`
    - `maximize`면 맞춘 뒤 `SW_MAXIMIZE` 한다.
    - 실패하면 false를 돌려준다.
  - `bool Show(nint hwnd)`
    - `ShowWindow(SW_SHOW)` 후 `SW_RESTORE`
  - `bool IsRunning(string exeName)`
  - `void Launch(string path, string? args)`
    - `Process.Start`, `UseShellExecute = true`
- 구현 요점(`Win32WindowSystem`):
  - 창 목록: `EnumWindows`. 창 사각형은 **`GetWindowRect`로 읽고 `SetWindowPos`로 쓴다.** 같은 좌표계로 왕복해야 보이지 않는 테두리 오차가 생기지 않는다.
  - 창 정보:
    - cloaked 여부: `DwmGetWindowAttribute(DWMWA_CLOAKED)`
    - exe 경로: `QueryFullProcessImageName`. 실패하면 `ExePath = null`, `ExeName = ""`
  - 모니터:
    - 목록: `EnumDisplayMonitors` + `GetMonitorInfo`
    - `DevicePath`: `QueryDisplayConfig`로 경로를 얻고, `DisplayConfigGetDeviceInfo`로 SOURCE_NAME(GDI 이름)과 TARGET_NAME(`monitorDevicePath`)을 얻어 GDI 이름끼리 이어 붙인다. 실패하면 `DevicePath = GdiName`

- [ ] **Step 1: 통합 테스트 작성**
  - `GetMonitors_ReturnsExactlyOnePrimary`
  - `GetMonitors_DevicePathsNonEmpty`
  - `GetWindows_IncludesOwnTestHostOrAtLeastOneCandidate`
    - 후보 창이 최소 1개 있음. 테스트 실행 환경에는 보통 탐색기나 터미널 창이 있다.
  - `SetPlacement_OnOwnWindow_RoundTripsRect`
    - 테스트 안에서 WinForms `Form`을 하나 띄우고 (100,100,640,480)로 `SetPlacement`
    - 잠시 기다린 뒤 `GetWindow`로 읽은 `Rect`가 같음
- [ ] **Step 2: 실패 확인.** `dotnet test --filter Category=Integration` → FAIL
- [ ] **Step 3: `NativeMethods`와 `Win32WindowSystem` 구현.** P/Invoke는 `LibraryImport`(source-generated)를 쓴다.
- [ ] **Step 4: 통과 확인.** 같은 필터 → PASS
- [ ] **Step 5: 수동 확인.** 임시 콘솔 출력이나 테스트 로그로 `GetWindows(false)`를 찍어 본다.
  - Discord와 KakaoTalk 창이 exe 이름, 클래스, 제목과 함께 나오는지 본다.
  - **카카오톡 메인 창의 클래스 이름을 기록해서** Task 3 테스트의 `"EVA_Window_Dblclk"` 값이 실제와 맞는지 확인한다. 다르면 테스트 값을 실제 값으로 바꾼다.
- [ ] **Step 6: 커밋.** 메시지: `feat: Win32 window system`

---

### Task 5: 프리셋 저장과 적용

**Files:**
- Create: `src/WindowFix.Core/Apply/PresetCapture.cs`, `src/WindowFix.Core/Apply/PresetApplier.cs`
- Create: `tests/WindowFix.Core.Tests/Fakes/FakeWindowSystem.cs`
- Test: `tests/WindowFix.Core.Tests/PresetCaptureTests.cs`, `tests/WindowFix.Core.Tests/PresetApplierTests.cs`

**Interfaces:**
- Consumes: `IWindowSystem` (Task 4), `Placement` / `MonitorResolver` (Task 2), `WindowMatcher` (Task 3)
- Produces:
  - `static class PresetCapture`
    - `Preset Capture(string name, IReadOnlyList<WindowInfo> selected, IReadOnlyList<MonitorInfo> monitors)`
      - 매칭 조건은 `WindowMatcher.SuggestMatches`로 만든다.
      - 모니터는 `Placement.MonitorOf`로 찾는다.
      - `Rect`는 `ToRelative`로 바꾼다.
      - `Maximized = w.IsMaximized`
      - `LaunchPath = w.ExePath`
      - `LaunchIfMissing = false`
      - `DisplayName`은 exe 이름에서 `.exe`를 뗀 것이다.
    - `IReadOnlyList<WindowInfo> DefaultSelection(IReadOnlyList<WindowInfo> candidates, IReadOnlyList<MonitorInfo> monitors)`
      - 주 모니터가 아닌 모니터에 있는 후보만 고른다.
      - 모니터가 하나뿐이면 아무것도 고르지 않는다.
  - `enum ApplyOutcome`
    - `Applied`
    - `NotFound`
    - `MonitorMissing`
    - `Failed`
    - `LaunchedWaiting`
  - `record EntryResult(Guid EntryId, ApplyOutcome Outcome, nint Handle)`
  - `record ApplyReport(IReadOnlyList<EntryResult> Results)`
    - 속성: `bool AllDone`
      - 모든 결과가 `Applied`, `MonitorMissing`, `Failed` 중 하나면 true
  - `record ApplyOptions(bool Unhide, bool Launch)`
    - `static ApplyOptions Full = new(true, true)`
    - `static ApplyOptions PlaceOnly = new(false, false)`
  - `sealed class PresetApplier(IWindowSystem ws)`
    - `ApplyReport Apply(Preset p, ApplyOptions opts, ISet<Guid>? onlyEntries = null)`
    - 항목마다 처리하는 순서:
      1. 모니터를 찾는다. 없으면 `MonitorMissing`
      2. 보이는 창 중 `PickBest`로 고른다. 이미 쓴 창(taken)은 제외한다.
      3. 못 찾았고 항목의 `LaunchIfMissing`이 켜져 있으면:
         - `opts.Unhide`면 숨은 창 중에서 다시 고른다. 있으면 `Show` 한 뒤 배치한다.
         - 그래도 없고 `opts.Launch`이고 프로세스가 실행 중이 아니면 `Launch` 하고 `LaunchedWaiting`
         - 그 밖에는 `NotFound`
      4. `SetPlacement`가 false면 `Failed`

- [ ] **Step 1: 실패하는 테스트 작성** (`FakeWindowSystem`은 창과 모니터 목록, 호출 기록, 실패시킬 hwnd 목록을 가진다)
  - `Capture_StoresMonitorRelativeRectAndMatch`
  - `DefaultSelection_OnlyNonPrimaryMonitorWindows`
  - `Apply_MovesMatchingWindowToAbsoluteRect`
  - `Apply_MonitorMissing_SkipsAndReports`
  - `Apply_SameWindowNotUsedTwice`
    - 같은 매칭 조건의 항목 두 개, 창은 하나 → 하나는 `Applied`, 하나는 `NotFound`
  - `Apply_HiddenWindow_WithLaunchOption_ShowsThenPlaces`
  - `Apply_HiddenWindow_WithoutLaunchOption_NotFound`
  - `Apply_NotRunning_WithLaunchOption_LaunchesAndReportsWaiting`
    - `Launch`가 `LaunchPath`와 `LaunchArgs`로 한 번 호출됨
  - `Apply_LaunchNotAllowed_DoesNotLaunch`
    - `new ApplyOptions(Unhide: true, Launch: false)` → `Launch` 호출 없음, 결과 `NotFound`
  - `Capture_Discord_UsesUpdateExeLauncher`
    - 입력: `ExePath = C:\Users\u\AppData\Local\Discord\app-1.0.9200\Discord.exe`
    - 기대값:
      - `LaunchPath = C:\Users\u\AppData\Local\Discord\Update.exe`
      - `LaunchArgs = --processStart Discord.exe`
    - 이유: Discord의 exe 경로는 버전마다 바뀐다(스펙 7장).
    - 이 규칙은 `PresetCapture` 안의 private 메서드 하나로 둔다. 경로가 `\Discord\app-*\Discord.exe` 모양일 때만 적용한다.
  - `Apply_SetPlacementFails_ReportsAccessDenied`
    - 결과가 `Failed`이고, 다른 항목은 계속 적용됨
  - `Apply_MaximizedEntry_PassesMaximizeTrue`
  - `Apply_OnlyEntries_TouchesOnlyThose`
- [ ] **Step 2: 실패 확인.** `dotnet test --filter "PresetCaptureTests|PresetApplierTests"` → FAIL
- [ ] **Step 3: `PresetCapture`와 `PresetApplier` 구현.**
- [ ] **Step 4: 통과 확인.** 같은 필터 → PASS
- [ ] **Step 5: 커밋.** 메시지: `feat: capture and apply presets`

---

### Task 6: 잠금 판단 로직

**Files:**
- Create: `src/WindowFix.Core/Lock/SnapBackPolicy.cs`, `Lock/LockTracker.cs`, `Lock/LockController.cs`
- Create: `tests/WindowFix.Core.Tests/Fakes/FakeClock.cs`
- Test: `tests/WindowFix.Core.Tests/SnapBackPolicyTests.cs`, `LockTrackerTests.cs`, `LockControllerTests.cs`

**Interfaces:**
- Consumes: `Preset`, `WindowEntry`, `WindowInfo`, `Placement`, `MonitorResolver`, `WindowMatcher`, `PresetApplier`
- Produces:
  - `interface IClock`
    - `DateTime UtcNow { get; }`
  - `sealed class SnapBackPolicy(IClock clock, int maxReverts = 5, TimeSpan window = 10s, TimeSpan cooldown = 60s)`
    - `bool ShouldRevert(nint hwnd)`
      - 호출 자체를 되돌리기 1회로 센다.
      - `window` 안에서 `maxReverts`번을 넘으면 `cooldown` 동안 false를 돌려준다.
    - `void Forget(nint hwnd)`
  - `enum LockAction`
    - `None`
    - `Revert`
    - `PlaceNew`
  - `sealed class LockTracker(Preset preset, Func<IReadOnlyList<MonitorInfo>> monitors, SnapBackPolicy policy)`
    - `IReadOnlyDictionary<nint, Guid> Bound`
    - `void BindInitial(IEnumerable<EntryResult> applied)`
      - `Applied` 결과의 핸들을 항목에 붙잡는다.
    - `LockAction OnShown(WindowInfo w, out WindowEntry? entry)`
      - 아직 바인딩되지 않은 항목 중 매칭되는 게 있으면 바인딩하고 `PlaceNew`
      - 이미 바인딩된 창이 다시 보인 경우, 저장된 사각형과 다르면 `Revert`
    - `LockAction OnLocationChanged(WindowInfo w, out PixelRect target, out bool maximize)`
      - 바인딩되지 않은 창이면 `None`
      - `IsMinimized`면 `None`. 최소화는 허용한다.
      - 목표 사각형은 `ToAbsolute`로 구한다. 모니터가 없으면 `None`
      - 현재 상태가 목표와 같으면 `None`
        - 사각형이 같고, 최대화 여부가 항목의 `Maximized`와 같은 경우
        - 항목이 최대화로 저장돼 있으면 사각형은 비교하지 않고 최대화 여부만 본다.
      - 다르면 `policy.ShouldRevert`가 true일 때만 `Revert`
    - `void OnDestroyed(nint hwnd)`
      - 바인딩을 풀고 `policy.Forget`
  - `enum LockState`
    - `Off`
    - `Active`
    - `Paused`
  - `sealed class LockController`
    - `LockState State`
    - `event Action<LockState> StateChanged`
    - `void SetEnabled(bool on)`
      - 켜면 `Active`, 끄면 `Off`
    - `void TogglePause()`
      - `Active` ↔ `Paused`
      - `Off`일 때는 아무것도 안 한다.
      - `Paused`에서 `Active`로 돌아가면 `RequestReapply` 이벤트를 낸다.
    - `event Action RequestReapply`

- [ ] **Step 1: 실패하는 테스트 작성**
  - `SnapBackPolicy_AllowsUpToLimit`: 5번 true, 6번째 false
  - `SnapBackPolicy_GivesUpAfterLimit`
    - 한도를 넘은 뒤 59초까지 false
    - 61초 뒤 true
  - `SnapBackPolicy_WindowSlides`
    - 11초 간격 호출은 누적되지 않음
  - `Tracker_MovedBoundWindow_Reverts`
    - 결과가 `Revert`이고 `target`이 저장된 절대 사각형
  - `Tracker_Minimized_None`
  - `Tracker_MaximizedButSavedNormal_Reverts`
    - 최대화와 스냅을 되돌리는 경우
  - `Tracker_SavedMaximized_StaysMaximized_None`
  - `Tracker_UnboundWindow_None`
  - `Tracker_NewMatchingWindowShown_PlaceNew_AndBinds`
  - `Tracker_DestroyedThenNewShown_Rebinds`
  - `Tracker_SameRect_None`
    - `SetPlacement` 자기 자신이 일으킨 이벤트에 반응해 무한 루프가 생기지 않는지 확인
  - `Controller_TogglePause_ActiveToPausedAndBack_RequestsReapply`
  - `Controller_TogglePause_WhenOff_NoChange`
  - `Controller_Disable_FromPaused_GoesOff`
- [ ] **Step 2: 실패 확인.** `dotnet test --filter "SnapBackPolicyTests|LockTrackerTests|LockControllerTests"` → FAIL
- [ ] **Step 3: 세 클래스 구현.**
- [ ] **Step 4: 통과 확인.** 같은 필터 → PASS
- [ ] **Step 5: 커밋.** 메시지: `feat: lock decision logic with snap-back limiter and pause`

---

### Task 7: 잠금을 실제 창 이벤트에 연결

**Files:**
- Create: `src/WindowFix.Core/Lock/WinEventLockService.cs`
- Modify: `src/WindowFix.Core/Platform/NativeMethods.cs`
  - 추가: `SetWinEventHook`, `UnhookWinEvent`, 이벤트 상수

**Interfaces:**
- Consumes: `LockTracker`, `IWindowSystem` (Task 4, 6)
- Produces: `sealed class WinEventLockService(IWindowSystem ws) : IDisposable`
  - `void Start(LockTracker tracker)`
  - `void Stop()`
  - 반드시 UI(디스패처) 스레드에서 호출한다. 이 스레드에 메시지 루프가 있어야 한다.

- [ ] **Step 1: 구현**
  - 훅은 `WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS`로 건다. 받는 이벤트:
    - `EVENT_SYSTEM_MOVESIZEEND`
    - `EVENT_OBJECT_LOCATIONCHANGE`
    - `EVENT_OBJECT_SHOW`
    - `EVENT_SYSTEM_MINIMIZEEND`
    - `EVENT_OBJECT_DESTROY`
  - 공통 처리:
    - `idObject != OBJID_WINDOW`인 이벤트는 무시한다.
    - 콜백 델리게이트는 필드에 보관한다. 가비지 컬렉션에 수거되지 않게 하기 위해서다.
  - 사용자가 창을 끄는 중에는 되돌리지 않는다.
    - `EVENT_SYSTEM_MOVESIZESTART`를 받으면 그 hwnd를 "드래그 중"으로 표시한다.
    - 드래그 중인 창의 LOCATIONCHANGE는 무시하고, MOVESIZEEND에서 판단한다.
  - 그 밖의 LOCATIONCHANGE는 hwnd별로 300ms 동안 모았다가(debounce, `DispatcherTimer`) 한 번만 판단한다.
  - `Revert`와 `PlaceNew` 모두 `ws.SetPlacement`로 처리한다.
- [ ] **Step 2: 빌드 확인.** `dotnet build` → 오류 0
- [ ] **Step 3: 커밋.** 메시지: `feat: wire lock to WinEvent hooks`
  - 실제 동작은 Task 9의 앱이 생긴 뒤 수동 체크리스트(Task 12)로 확인한다.

---

### Task 8: 시작 시퀀스, 시작 프로그램 등록, 단일 실행

**Files:**
- Create: `src/WindowFix.Core/Startup/StartupSequence.cs`, `Startup/StartupRegistration.cs`, `Startup/SingleInstance.cs`
- Test: `tests/WindowFix.Core.Tests/StartupSequenceTests.cs`, `StartupRegistrationTests.cs`

**Interfaces:**
- Consumes: `PresetApplier`, `ApplyReport`, `IClock` (Task 5, 6)
- Produces:
  - `sealed class StartupSequence(PresetApplier applier, IClock clock, Func<TimeSpan, Task> delay)`
    - `Task<ApplyReport> RunAsync(Preset p, CancellationToken ct)`
    - 진행 순서:
      1. 처음 한 번은 `ApplyOptions.Full`로 적용한다.
      2. 그다음부터는 끝나지 않은 항목(`NotFound`, `LaunchedWaiting`)만 `onlyEntries`로 넘겨 2초마다 다시 적용한다. 이때 `new ApplyOptions(Unhide: true, Launch: false)`를 쓴다. 실행은 1단계에서 항목당 한 번만 한다.
      3. 모든 항목이 끝나거나 3분이 지나면 멈춘다.
      4. 끝나고 5초 뒤 전체를 `ApplyOptions.PlaceOnly`로 한 번 더 적용한다.
      5. 마지막 보고서를 돌려준다.
  - `interface IRunKey`
    - `string? Get()`
    - `void Set(string value)`
    - `void Delete()`
    - 실제 구현 `RegistryRunKey`
  - `static class StartupRegistration`
    - `void Sync(AppSettings s, string exePath, IRunKey key)`
      - `s.LockEnabled || s.StartupPresetId != null`이면 `"\"{exePath}\" --startup"`을 쓴다. 아니면 지운다.
  - `sealed class SingleInstance : IDisposable`
    - `static SingleInstance? TryAcquire(Action onShowRequested)`
      - Mutex 이름: `Local\WindowFix.SingleInstance`
      - 이벤트 이름: `Local\WindowFix.Show`
      - 이미 실행 중이면 이벤트에 신호를 보내고 null을 돌려준다.

- [ ] **Step 1: 실패하는 테스트 작성** (가짜 delay는 `FakeClock`을 앞으로 돌리고 바로 끝난다)
  - `Startup_AllPresentImmediately_AppliesThenReappliesAfter5s`
    - `SetPlacement` 호출 수가 항목 수 × 2
  - `Startup_WindowAppearsLater_RetriesEvery2sUntilFound`
  - `Startup_NeverAppears_StopsAfter3Minutes`
    - 마지막 보고서에 `NotFound`가 남아 있음
    - 경과 시간이 180초 이상 182초 이하
  - `Startup_LaunchesOnlyOncePerEntry`
  - `Startup_HiddenAtFirstThenAppears_IsShownAndPlaced`
  - `Registration_LockOnly_Writes`
  - `Registration_StartupPresetOnly_Writes`
  - `Registration_BothOff_Deletes`
  - `Registration_QuotesPathWithSpaces`
- [ ] **Step 2: 실패 확인.** `dotnet test --filter "StartupSequenceTests|StartupRegistrationTests"` → FAIL
- [ ] **Step 3: 구현.** `SingleInstance`는 단위 테스트 없이 Task 12에서 수동으로 확인한다.
- [ ] **Step 4: 통과 확인.** 같은 필터 → PASS
- [ ] **Step 5: 커밋.** 메시지: `feat: startup sequence, run-key registration, single instance`

---

### Task 9: 앱 생명주기와 메인 창 (B안: 모니터 미리보기)

**Files:**
- Modify: `src/WindowFix/WindowFix.csproj`
  - `UseWindowsForms`, `ApplicationManifest`, `AssemblyName=WindowFix`, 단일 파일 publish 속성
- Create: `src/WindowFix/app.manifest`
  - `PerMonitorV2`, `asInvoker`
- Modify: `src/WindowFix/App.xaml(.cs)`
  - `StartupUri`를 없애고 `AppHost`를 사용
- Create: `src/WindowFix/AppHost.cs`, `MainWindow.xaml(.cs)`, `Controls/MonitorPreview.cs`

**Interfaces:**
- Consumes: Task 1–8의 전부
- Produces: `sealed class AppHost`. Task 10–11이 쓴다.
  - 속성: `AppConfig Config`, `LockController Lock`
  - `void SaveConfig()`
    - 저장 후 `StartupRegistration.Sync`
  - `ApplyReport ApplyPreset(Guid id)`
    - `ApplyOptions.Full`로 적용한다.
    - `CurrentPresetId`를 갱신해 저장하고, 잠금이 `Active`면 `LockTracker`를 새로 만들어 `BindInitial`한 뒤 `WinEventLockService.Start`한다.
  - `LockController` 상태가 바뀌면 이렇게 반응한다.
    - `Active` → `Paused`: `WinEventLockService.Stop()`
    - `Off`: `WinEventLockService.Stop()`. 설정 저장은 잠금 토글 쪽(메인 창, 트레이)이 `LockEnabled`를 바꾼 뒤 `SaveConfig()`로 한다.
    - `RequestReapply`: 현재 프리셋을 `ApplyOptions.PlaceOnly`로 적용한 뒤 새 트래커로 `Start`한다. 창들이 원래 자리로 돌아가는 동작이 이것이다.
  - `void OnMainWindowClosing(CancelEventArgs e)`
    - `Lock.State != Off`면 창을 숨기고 `e.Cancel = true`
    - 아니면 `Shutdown`
  - `void ShowMainWindow()`
  - `event Action ConfigChanged`

- [ ] **Step 1: `AppHost` 시작 흐름 구현**
  1. `SingleInstance.TryAcquire(ShowMainWindow)`가 null이면 즉시 종료한다.
  2. 설정을 읽는다.
  3. 인자에 `--startup`이 있는 경우:
     - 창 없이 `StartupSequence`를 실행한다. 대상 프리셋은 `StartupPresetId`이고, 없으면 `CurrentPresetId`다.
     - 끝난 뒤 잠금이 켜져 있으면 `WinEventLockService`를 시작하고 남는다. 꺼져 있으면 종료한다.
  4. 인자가 없는 경우:
     - 메인 창을 띄운다.
     - 잠금이 켜져 있으면 현재 프리셋을 적용하고 잠금을 시작한다.
- [ ] **Step 2: `MonitorPreview` 구현.** `Canvas`를 상속한다.
  - 속성: `IReadOnlyList<MonitorInfo> Monitors`, `IReadOnlyList<PreviewRect> Rects`
    - `record PreviewRect(Guid Id, string Label, PixelRect Abs, bool Emphasized)`
  - 그리기:
    - 전체 모니터를 감싸는 사각형을 컨트롤 크기에 맞게 균일하게 축소해서 그린다.
    - 모니터는 어두운 사각형, 창은 반투명 색 사각형에 라벨을 붙인다.
    - 색은 Id 해시로 고정 팔레트(8색)에서 고른다.
  - `event Action<Guid> RectClicked`
- [ ] **Step 3: `MainWindow` 구현.** 스펙 5.2의 배치를 따른다.
  - 상단:
    - 프리셋 `ComboBox`
    - [적용]
    - [+ 지금 배치 저장] → Task 10 대화상자
    - [이름 변경]
    - [삭제] (확인 대화상자를 띄운다)
  - 가운데: `MonitorPreview`
  - 오른쪽 편집 패널 (사각형을 클릭하면 보인다):
    - 표시 이름
    - X, Y, 너비, 높이 (모니터 기준. 저장하면 `RelRect`에 반영)
    - 제목 조건
    - "없으면 실행, 숨어 있으면 꺼내기" 체크박스
    - 실행 경로와 인자
    - [이 창 빼기]
  - 하단 토글:
    - "🔒 잠금": 상태가 `Paused`면 문구를 "잠금 켜짐 · 일시 해제 중"으로 바꾼다.
    - "트레이 아이콘 표시"
    - "고정 버튼 표시"
    - "윈도우 시작 시 이 프리셋 적용": 켜면 `StartupPresetId`를 현재 프리셋으로 정한다.
  - 적용 결과: 건너뜀이나 실패가 있으면 하단 상태줄에 표시한다. 예: "카카오톡: 모니터 없음 → 건너뜀"
- [ ] **Step 4: 실행해서 확인.** `dotnet run --project src/WindowFix`
  - 메인 창이 뜨고, 모니터 두 개가 실제 배치대로 그려진다.
  - 잠금이 꺼진 상태에서 창을 닫으면 작업 관리자에서 프로세스가 사라진다.
- [ ] **Step 5: 커밋.** 메시지: `feat: app host and main window with monitor preview`

---

### Task 10: "지금 배치 저장" 대화상자

**Files:**
- Create: `src/WindowFix/SaveDialog.xaml(.cs)`

**Interfaces:**
- Consumes:
  - `IWindowSystem.GetWindows(false)`
  - `WindowMatcher.IsCandidate`
  - `PresetCapture.DefaultSelection`, `PresetCapture.Capture`
  - `MonitorPreview`
- Produces: `static Preset? SaveDialog.Show(Window owner, IWindowSystem ws)`

- [ ] **Step 1: 구현**
  - 왼쪽: 후보 창 목록. 항목마다 체크박스, 아이콘(`System.Drawing.Icon.ExtractAssociatedIcon(ExePath)`), 프로그램 이름, 창 제목, "모니터 N", "W×H"
  - 오른쪽: `MonitorPreview`. 체크한 창만 `Emphasized = true`
  - 아래: 이름 입력칸과 [저장]
    - 이름이 비어 있으면 [저장] 버튼을 끈다.
    - 기본 이름은 "프리셋 {n}"이다.
  - 처음 체크 상태는 `DefaultSelection`을 따른다.
  - WindowFix 자신의 창은 후보에서 뺀다.
- [ ] **Step 2: 실행해서 확인.**
  - 디스코드와 카톡을 서브모니터에 두고 대화상자를 연다.
  - 두 창이 미리 체크돼 있는지 본다.
  - 저장 후 메인 창 미리보기에 같은 배치가 나오는지 본다.
  - 창을 옮긴 뒤 [적용]을 누르면 원래 자리로 돌아오는지 본다.
- [ ] **Step 3: 커밋.** 메시지: `feat: save-current-layout dialog`

---

### Task 11: 떠 있는 고정 버튼과 트레이 아이콘

**Files:**
- Create: `src/WindowFix.Core/Layout/LockButtonPlacement.cs`
- Test: `tests/WindowFix.Core.Tests/LockButtonPlacementTests.cs`
- Create: `src/WindowFix/FloatingLockButton.xaml(.cs)`, `src/WindowFix/Tray/TrayIcon.cs`

**Interfaces:**
- Consumes: `AppHost`, `LockController` (Task 9, 6)
- Produces: `static class LockButtonPlacement`
  - `(int X, int Y) Resolve(int? savedX, int? savedY, IReadOnlyList<MonitorInfo> monitors, int size = 40, int margin = 24)`
    - 저장된 위치의 버튼 사각형이 어떤 모니터의 `WorkArea` 안에 완전히 들어가면 그 위치를 쓴다.
    - 아니면 기본 위치를 쓴다. 첫 번째 비주 모니터(없으면 주 모니터)의 `WorkArea` 오른쪽 아래에서 margin만큼 안쪽이다.
  - `bool IsClick(double dx, double dy)`
    - 마우스를 누른 지점에서 뗀 지점까지 이동이 `SystemParameters.MinimumHorizontalDragDistance`와 `MinimumVerticalDragDistance` 미만이면 클릭이다.
    - Core에서는 값으로 4px를 쓴다.

- [ ] **Step 1: 실패하는 테스트 작성**
  - `Resolve_NoSaved_DefaultsToSecondaryBottomRight`
    - 비주 모니터 WorkArea가 (1920,0,1920,1040)이면 결과는 (3816,976)
  - `Resolve_SavedOffscreen_FallsBackToDefault`
  - `Resolve_SavedOnScreen_Kept`
  - `Resolve_SingleMonitor_UsesPrimary`
  - `IsClick_SmallMove_True`, `IsClick_Drag_False`
- [ ] **Step 2: 실패 확인 → 구현 → 통과 확인.** `dotnet test --filter LockButtonPlacementTests`
- [ ] **Step 3: `FloatingLockButton` 구현.**
  - 창 속성:
    - `WindowStyle=None`, `AllowsTransparency=True`, `Topmost=True`, `ShowInTaskbar=False`
    - 40×40 원형
  - 확장 스타일: `SourceInitialized`에서 `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`를 건다. Alt+Tab에 나오지 않고, 포커스를 뺏지 않게 하기 위해서다.
  - 투명도: `Opacity`는 평소 0.4, `MouseEnter`에서 1.0
  - 아이콘: 잠김(🔒)과 풀림(🔓)을 `LockController.State`에 따라 바꾼다.
  - 마우스 동작:
    - `MouseLeftButtonDown`: 누른 위치를 기억한다.
    - 움직임이 `IsClick` 기준을 넘으면 `DragMove()`
    - 버튼을 뗐을 때 클릭이면 `Lock.TogglePause()`
    - 끌었으면 위치를 `LockButtonX/Y`에 저장한다.
  - 표시 조건: `ShowLockButton && Lock.State != Off`일 때만 보인다.
- [ ] **Step 4: `TrayIcon` 구현.** WinForms `NotifyIcon`을 쓰고 `ShowTrayIcon`이 켜져 있을 때만 보인다.
  - 메뉴:
    - 열기
    - 프리셋 적용 ▸ (프리셋 목록)
    - 잠금 켜기/끄기 (일시 해제 중이면 "잠금 켜짐 · 일시 해제 중")
    - 종료
  - "종료"는 설정을 바꾸지 않고 `Shutdown`만 한다.
- [ ] **Step 5: 실행해서 확인.**
  - 잠금을 켜면 버튼이 서브모니터 오른쪽 아래에 나타난다.
  - 버튼을 클릭하면 🔓로 바뀌고, 창을 옮길 수 있다.
  - 다시 클릭하면 🔒로 바뀌고, 창이 원래 자리로 돌아간다.
  - 버튼을 끌면 클릭으로 처리되지 않는다.
- [ ] **Step 6: 커밋.** 메시지: `feat: floating lock button and tray icon`

---

### Task 12: 배포 빌드와 수동 확인

**Files:**
- Create: `docs/manual-test-checklist.md` (아래 목록을 체크리스트로 옮긴다)
- Modify: `README.md` (기능 소개, 다운로드/실행 방법, 빌드 명령)

- [ ] **Step 1: 배포 빌드.** `dotnet publish src/WindowFix -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish`
  - 확인: `publish/WindowFix.exe` 하나가 생긴다.
- [ ] **Step 2: 전체 단위 테스트.** `dotnet test` → 전부 PASS
- [ ] **Step 3: 수동 체크리스트를 실제로 해 보고 결과를 적는다.**
  1. 디스코드와 카톡 메인 창을 서브모니터에 놓고 프리셋을 저장한다. 창을 흩뜨린 뒤 [적용]을 누르면 픽셀 단위로 원래대로 돌아온다.
  2. 잠금을 켜고 디스코드 테두리를 끈다. 손을 떼는 순간 원래 크기로 돌아온다.
  3. 디스코드 제목 표시줄을 더블클릭해 최대화한다. 원래 자리로 돌아온다.
  4. Win+← 로 스냅한다. 원래 자리로 돌아온다.
  5. 디스코드를 최소화한다. 그대로 최소화돼 있다. 다시 열면 원래 자리다.
  6. 카톡을 X로 닫아 트레이로 보낸 뒤 다시 연다. 원래 자리에 뜬다.
  7. 고정 버튼으로 일시 해제한 뒤 창을 옮기고, 다시 고정한다. 원래 자리로 돌아온다.
  8. 잠금을 켜고 메인 창을 닫는다. 프로세스가 남는다. exe를 다시 실행하면 새로 켜지지 않고 기존 창이 뜬다.
  9. 잠금을 끄고 메인 창을 닫는다. 프로세스가 사라진다.
  10. "윈도우 시작 시 적용"을 켜고 재부팅한다.
      - 디스코드와 카톡이 늦게 떠도 배치가 적용된다.
      - "없으면 실행"을 켠 창은 자동으로 실행된다.
  11. 잠금만 켜고(시작 시 적용은 끔) 재부팅한다. 잠금이 다시 동작한다.
  12. 트레이 아이콘을 끈 상태로 1–11을 다시 해 봐도 문제가 없다.
  13. 작업 관리자에서 CPU를 확인한다. 잠금 중 대기 상태에서 0%에 가까워야 한다.
  14. 서브모니터 배율(125% 등)을 주 모니터와 다르게 두고 1번을 반복한다.
  15. .NET이 설치되지 않은 PC(또는 친구 PC)에서 `WindowFix.exe`만으로 실행된다.
- [ ] **Step 4: 커밋.** 메시지: `chore: publish config, README, manual checklist`
