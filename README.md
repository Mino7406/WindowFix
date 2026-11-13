# WindowFix

서브모니터에 디스코드, 카카오톡처럼 늘 켜 두는 창을 **원하는 자리에 고정**해 주는 Windows 앱이에요.

- **배치 저장**: 지금 창들의 위치와 크기를 저장해 두고, 언제든 버튼 하나로 되돌려요.
- **창 고정**: 테두리를 실수로 건드리거나 최대화해도 저장한 자리로 바로 돌아와요.
- **재부팅해도 유지**: 컴퓨터를 켜면 창들이 늦게 떠도 기다렸다가 제자리에 놓아 줘요.
- **고정 버튼**: 고정 중에는 모니터 가장자리에 작은 탭이 붙어요. 누르면 잠깐 풀리고, 다시 누르면 원래 자리로 돌아가요.

## 사용법

1. `WindowFix.exe`를 실행하세요. 설치나 .NET이 필요 없어요.
2. 디스코드, 카톡 같은 창을 원하는 자리에 놓으세요.
3. **[+ 지금 배치 저장]** → 고정할 창에 체크 → **저장하기**
4. 아래 **[🔒 창 고정]**을 켜면 끝이에요.

> 창 고정이 꺼져 있을 때 WindowFix 창을 닫으면 프로그램이 완전히 꺼져요.
> 창 고정이 켜져 있으면 창을 닫아도 뒤에서 계속 지키고, 컴퓨터를 다시 켜도 자동으로 켜져요.
> 숨어 있는 WindowFix를 다시 열려면 `WindowFix.exe`를 한 번 더 실행하세요.

설정은 `%APPDATA%\WindowFix\config.json`에 저장돼요.

## 빌드

[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)가 필요해요.

```bash
dotnet test
```

```bash
dotnet publish src/WindowFix -c Release -r win-x64 -o publish
```

`publish/WindowFix.exe` 하나만 배포하면 돼요.

## 구조

- `src/WindowFix.Core` — 창 찾기, 좌표 변환, 배치 적용, 고정 판단, 부팅 시 복원 (테스트 가능한 로직 + Win32 호출)
- `src/WindowFix` — WPF 화면 (메인 창, 저장 창, 고정 탭, 트레이 아이콘)
- `tests/WindowFix.Core.Tests` — xUnit 테스트
- `docs/superpowers/specs` — 설계 문서, `docs/manual-test-checklist.md` — 직접 확인할 목록

## 새 버전 배포(릴리스) 방법

WindowFix는 켤 때와 하루 한 번 GitHub의 최신 릴리스를 확인하고, 새 버전이 있으면 화면 위에 알려 줘요. [업데이트]를 누르면 새 exe를 받아 바꾼 뒤 다시 켜요. 저장한 배치와 설정은 그대로 남아요.

1. `src/WindowFix/WindowFix.csproj`의 `<Version>`을 올려요. 예: `0.1.0` → `0.2.0`
2. exe를 만들어요.

```bash
dotnet publish src/WindowFix -c Release -r win-x64 -o publish
```

3. GitHub에서 **Releases → Draft a new release**를 열어요.
   - 태그: `v0.2.0` (`<Version>`과 같은 숫자, 앞에 `v`)
   - 파일: `publish/WindowFix.exe`를 **이름 그대로** 올려요.
   - "Set as a pre-release"는 끄고 **Publish release**를 눌러요. 임시 저장이나 미리보기 릴리스는 업데이트로 안내되지 않아요.
