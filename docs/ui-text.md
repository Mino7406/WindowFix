# WindowFix 화면 문구 목록

앱에 보이는 한국어 문구를 파일별로 모은 목록이에요. 위치를 누르면 그 파일의 그 줄로 가요.

- **고치는 방법**: 바꾸고 싶은 문구와 새 문구를 알려 주시면 제가 바꿔 드려요. 직접 고치셔도 돼요. 그때는 따옴표(`"`) 안의 글자만 바꾸세요.
- `{...}` 부분은 실행할 때 채워지는 값(배치 이름, 창 개수 등)이라 그대로 두세요. `\n`은 줄바꿈이에요.
- 이 목록은 코드에서 자동으로 뽑은 것이라, 문구를 고친 뒤에는 줄 번호가 조금 달라질 수 있어요. 다시 뽑아 달라고 하시면 돼요.

## 메인 창 (배치 화면, 설정 화면, 알림 문구)

| 위치 | 종류 | 문구 |
|---|---|---|
| [MainWindow.xaml:12](../src/WindowFix/MainWindow.xaml#L12) | 메뉴/제목 | 이 배치로 맞추기 |
| [MainWindow.xaml:14](../src/WindowFix/MainWindow.xaml#L14) | 메뉴/제목 | 이름 바꾸기 |
| [MainWindow.xaml:15](../src/WindowFix/MainWindow.xaml#L15) | 메뉴/제목 | 삭제 |
| [MainWindow.xaml:80](../src/WindowFix/MainWindow.xaml#L80) | 마우스 올렸을 때 설명 | 테마, 시작 설정 등을 바꿔요. |
| [MainWindow.xaml:81](../src/WindowFix/MainWindow.xaml#L81) | 글자 | ⚙  설정 |
| [MainWindow.xaml:83](../src/WindowFix/MainWindow.xaml#L83) | 버튼/글자 | + 새 배치 생성 |
| [MainWindow.xaml:85](../src/WindowFix/MainWindow.xaml#L85) | 마우스 올렸을 때 설명 | 지금 화면에 놓인 창들의 위치와 크기를 새 배치로 저장해요. |
| [MainWindow.xaml:86](../src/WindowFix/MainWindow.xaml#L86) | 글자 | 저장한 배치 |
| [MainWindow.xaml:119](../src/WindowFix/MainWindow.xaml#L119) | 글자 | 배치 없음 |
| [MainWindow.xaml:129](../src/WindowFix/MainWindow.xaml#L129) | 마우스 올렸을 때 설명 | 고른 배치대로 창들을 지금 바로 옮겨요. |
| [MainWindow.xaml:130](../src/WindowFix/MainWindow.xaml#L130) | 글자 | ↺  이 배치로 맞추기 |
| [MainWindow.xaml:133](../src/WindowFix/MainWindow.xaml#L133) | 마우스 올렸을 때 설명 | 이 배치의 이름을 바꿔요. |
| [MainWindow.xaml:134](../src/WindowFix/MainWindow.xaml#L134) | 글자 | ✎  이름 바꾸기 |
| [MainWindow.xaml:138](../src/WindowFix/MainWindow.xaml#L138) | 마우스 올렸을 때 설명 | 이 배치를 지워요. 창이나 프로그램은 그대로 있어요. |
| [MainWindow.xaml:139](../src/WindowFix/MainWindow.xaml#L139) | 글자 | 🗑  삭제 |
| [MainWindow.xaml:148](../src/WindowFix/MainWindow.xaml#L148) | 버튼/글자 | 관리자 권한으로 다시 실행 |
| [MainWindow.xaml:150](../src/WindowFix/MainWindow.xaml#L150) | 글자 | ⚠ 관리자 권한 앱이 있어요 |
| [MainWindow.xaml:152](../src/WindowFix/MainWindow.xaml#L152) | 글자 | 일부 게임처럼 관리자 권한으로 켜진 창은 WindowFix도 관리자 권한이어야 옮길 수 있어요. |
| [MainWindow.xaml:159](../src/WindowFix/MainWindow.xaml#L159) | 마우스 올렸을 때 설명 | 켜 두면, 창을 실수로 움직이거나 크기를 바꿔도 저장한 자리로 바로 돌아가요. |
| [MainWindow.xaml:161](../src/WindowFix/MainWindow.xaml#L161) | 글자 | 🔒 창 고정 |
| [MainWindow.xaml:175](../src/WindowFix/MainWindow.xaml#L175) | 글자 | 이 배치의 창 — 누르면 그 창 설정을 열어요 |
| [MainWindow.xaml:198](../src/WindowFix/MainWindow.xaml#L198) | 글자 | 모니터를 누르면 이 배치를 고칠 수 있어요 |
| [MainWindow.xaml:201](../src/WindowFix/MainWindow.xaml#L201) | 글자 | 처음 오셨나요? 세 단계면 끝나요. |
| [MainWindow.xaml:206](../src/WindowFix/MainWindow.xaml#L206) | 글자 | 창을 원하는 자리에 놓으세요 |
| [MainWindow.xaml:207](../src/WindowFix/MainWindow.xaml#L207) | 글자 | 디스코드, 카톡처럼 늘 켜 두는 창을 평소 쓰는 위치와 크기로 맞춰 두세요. |
| [MainWindow.xaml:213](../src/WindowFix/MainWindow.xaml#L213) | 글자 | [+ 새 배치 생성]을 누르세요 |
| [MainWindow.xaml:214](../src/WindowFix/MainWindow.xaml#L214) | 글자 | 떠 있는 창 목록이 나오면, 고정할 창에 체크하고 저장하면 돼요. |
| [MainWindow.xaml:220](../src/WindowFix/MainWindow.xaml#L220) | 글자 | [🔒 창 고정]을 켜세요 |
| [MainWindow.xaml:221](../src/WindowFix/MainWindow.xaml#L221) | 글자 | 이제 창을 실수로 건드려도 제자리로 돌아와요. 컴퓨터를 껐다 켜도 유지돼요. |
| [MainWindow.xaml:224](../src/WindowFix/MainWindow.xaml#L224) | 버튼/글자 | + 새 배치 생성 |
| [MainWindow.xaml:237](../src/WindowFix/MainWindow.xaml#L237) | 마우스 올렸을 때 설명 | 닫기 |
| [MainWindow.xaml:238](../src/WindowFix/MainWindow.xaml#L238) | 글자 | 창 설정 |
| [MainWindow.xaml:240](../src/WindowFix/MainWindow.xaml#L240) | 글자 | 이름 |
| [MainWindow.xaml:242](../src/WindowFix/MainWindow.xaml#L242) | 마우스 올렸을 때 설명 | 배치를 맞출 때 이 프로그램이 꺼져 있으면 켜 주고, 트레이에 숨어 있으면 꺼내서 제자리에 놓아요. |
| [MainWindow.xaml:243](../src/WindowFix/MainWindow.xaml#L243) | 글자 | 꺼져 있으면 자동으로 켜기 (숨어 있으면 꺼내기) |
| [MainWindow.xaml:245](../src/WindowFix/MainWindow.xaml#L245) | 버튼/글자 | 최대화 상태로 두기 |
| [MainWindow.xaml:245](../src/WindowFix/MainWindow.xaml#L245) | 마우스 올렸을 때 설명 | 이 창을 모니터를 꽉 채운 상태로 고정해요. |
| [MainWindow.xaml:247](../src/WindowFix/MainWindow.xaml#L247) | 메뉴/제목 | 위치와 크기 직접 고치기 |
| [MainWindow.xaml:249](../src/WindowFix/MainWindow.xaml#L249) | 글자 | 보통은 손대지 않아도 돼요. 창을 원하는 자리에 놓고 새 배치를 만드는 게 더 쉬워요. 숫자는 모니터 왼쪽 위 모서리에서부터의 픽셀이에요. |
| [MainWindow.xaml:251](../src/WindowFix/MainWindow.xaml#L251) | 글자 | 가로 |
| [MainWindow.xaml:252](../src/WindowFix/MainWindow.xaml#L252) | 글자 | 세로 |
| [MainWindow.xaml:253](../src/WindowFix/MainWindow.xaml#L253) | 글자 | 너비 |
| [MainWindow.xaml:254](../src/WindowFix/MainWindow.xaml#L254) | 글자 | 높이 |
| [MainWindow.xaml:265](../src/WindowFix/MainWindow.xaml#L265) | 메뉴/제목 | 고급 설정 |
| [MainWindow.xaml:267](../src/WindowFix/MainWindow.xaml#L267) | 글자 | 같은 프로그램 창이 여러 개일 때 |
| [MainWindow.xaml:268](../src/WindowFix/MainWindow.xaml#L268) | 글자 | 제목에 이 글자가 들어간 창만 고정해요. 예: 크롬 창 중 'YouTube'만 |
| [MainWindow.xaml:270](../src/WindowFix/MainWindow.xaml#L270) | 버튼/글자 | 지금 창 제목 가져오기 |
| [MainWindow.xaml:271](../src/WindowFix/MainWindow.xaml#L271) | 마우스 올렸을 때 설명 | 지금 떠 있는 이 프로그램의 창 제목을 채워 넣어요. 필요 없는 부분은 지우세요. |
| [MainWindow.xaml:275](../src/WindowFix/MainWindow.xaml#L275) | 글자 | 자동으로 켤 때 실행할 파일 |
| [MainWindow.xaml:276](../src/WindowFix/MainWindow.xaml#L276) | 글자 | '꺼져 있으면 자동으로 켜기'를 쓸 때만 필요해요. 보통은 저장할 때 알아서 채워져요. |
| [MainWindow.xaml:278](../src/WindowFix/MainWindow.xaml#L278) | 버튼/글자 | 찾아보기… |
| [MainWindow.xaml:281](../src/WindowFix/MainWindow.xaml#L281) | 글자 | 실행 옵션 (선택) |
| [MainWindow.xaml:282](../src/WindowFix/MainWindow.xaml#L282) | 마우스 올렸을 때 설명 | 예: 디스코드는 --processStart Discord.exe |
| [MainWindow.xaml:287](../src/WindowFix/MainWindow.xaml#L287) | 버튼/글자 | 저장 |
| [MainWindow.xaml:288](../src/WindowFix/MainWindow.xaml#L288) | 버튼/글자 | 이 창 빼기 |
| [MainWindow.xaml:288](../src/WindowFix/MainWindow.xaml#L288) | 마우스 올렸을 때 설명 | 이 창을 배치에서 빼요. 프로그램이 꺼지지는 않아요. |
| [MainWindow.xaml:299](../src/WindowFix/MainWindow.xaml#L299) | 글자 | 설정 |
| [MainWindow.xaml:302](../src/WindowFix/MainWindow.xaml#L302) | 글자 | 화면 테마 |
| [MainWindow.xaml:303](../src/WindowFix/MainWindow.xaml#L303) | 글자 | 'Windows 설정'는 Windows의 밝게/어둡게 설정을 그대로 따라가요. |
| [MainWindow.xaml:306](../src/WindowFix/MainWindow.xaml#L306) | 버튼/글자 | Windows 설정 |
| [MainWindow.xaml:307](../src/WindowFix/MainWindow.xaml#L307) | 버튼/글자 | ☀ 라이트 |
| [MainWindow.xaml:308](../src/WindowFix/MainWindow.xaml#L308) | 버튼/글자 | ☾ 다크 |
| [MainWindow.xaml:317](../src/WindowFix/MainWindow.xaml#L317) | 글자 | 시작 프로그램으로 설정 |
| [MainWindow.xaml:318](../src/WindowFix/MainWindow.xaml#L318) | 글자 | 컴퓨터를 켜면 창들이 늦게 떠도 기다렸다가 제자리에 놓아 줘요. |
| [MainWindow.xaml:321](../src/WindowFix/MainWindow.xaml#L321) | 글자 | 어떤 배치로 맞출까요? |
| [MainWindow.xaml:342](../src/WindowFix/MainWindow.xaml#L342) | 글자 | 항상 관리자 권한으로 실행 |
| [MainWindow.xaml:343](../src/WindowFix/MainWindow.xaml#L343) | 글자 | 메이플처럼 관리자 권한으로 켜지는 게임 창도 고정하려면 켜세요. 컴퓨터를 켤 때도 관리자 권한으로 자동 실행돼요. 처음 켤 때 한 번만 확인 창이 떠요. |
| [MainWindow.xaml:353](../src/WindowFix/MainWindow.xaml#L353) | 글자 | 창 고정 위젯 사용 |
| [MainWindow.xaml:354](../src/WindowFix/MainWindow.xaml#L354) | 글자 | 창 고정 중일 때 모니터 가장자리에 작은 탭이 붙어요. 누르면 잠깐 풀고, 다시 누르면 원래 자리로 돌아가요. |
| [MainWindow.xaml:359](../src/WindowFix/MainWindow.xaml#L359) | 글자 | 시스템 트레이에 표시 |
| [MainWindow.xaml:360](../src/WindowFix/MainWindow.xaml#L360) | 글자 | 시계 옆에 WindowFix 아이콘을 보여 줘요. 숨겨도 WindowFix를 다시 실행하면 이 창이 열려요. |
| MainWindow.xaml (설정) | 글자 | Windows 알림 받기 |
| MainWindow.xaml (설정) | 글자 | 컴퓨터를 켠 뒤 배치를 맞춘 결과와 새 버전 소식을 화면 오른쪽 아래 Windows 알림으로 알려 줘요. |
| [MainWindow.xaml.cs:44](../src/WindowFix/MainWindow.xaml.cs#L44) | 코드 | 단, |
| [MainWindow.xaml.cs:44](../src/WindowFix/MainWindow.xaml.cs#L44) | 코드 | 못 |
| [MainWindow.xaml.cs:44](../src/WindowFix/MainWindow.xaml.cs#L44) | 코드 | 취소 |
| [MainWindow.xaml.cs:44](../src/WindowFix/MainWindow.xaml.cs#L44) | 코드 | 없어요 |
| [MainWindow.xaml.cs:84](../src/WindowFix/MainWindow.xaml.cs#L84) | 코드 | 배치 없음 |
| [MainWindow.xaml.cs:88](../src/WindowFix/MainWindow.xaml.cs#L88) | 코드 | 🔒 창 고정 중 |
| [MainWindow.xaml.cs:88](../src/WindowFix/MainWindow.xaml.cs#L88) | 코드 | '{_host.CurrentPreset?.Name}' 배치를 지키고 있어요. |
| [MainWindow.xaml.cs:89](../src/WindowFix/MainWindow.xaml.cs#L89) | 코드 | ● 고정 중 |
| [MainWindow.xaml.cs:90](../src/WindowFix/MainWindow.xaml.cs#L90) | 코드 | 🔓 잠깐 풀어 둠 |
| [MainWindow.xaml.cs:90](../src/WindowFix/MainWindow.xaml.cs#L90) | 코드 | 위젯을 다시 누르면 원래 자리로 돌아가요. |
| [MainWindow.xaml.cs:91](../src/WindowFix/MainWindow.xaml.cs#L91) | 코드 | 잠깐 풀어 둠 |
| [MainWindow.xaml.cs:92](../src/WindowFix/MainWindow.xaml.cs#L92) | 코드 | 🔒 창 고정 |
| [MainWindow.xaml.cs:92](../src/WindowFix/MainWindow.xaml.cs#L92) | 코드 | 먼저 배치를 만드세요. |
| [MainWindow.xaml.cs:92](../src/WindowFix/MainWindow.xaml.cs#L92) | 코드 | 켜면 창이 저장한 자리에서 움직이지 않아요. |
| [MainWindow.xaml.cs:93](../src/WindowFix/MainWindow.xaml.cs#L93) | 코드 | 고정 안 함 |
| [MainWindow.xaml.cs:108](../src/WindowFix/MainWindow.xaml.cs#L108) | 코드 | ↺  이 배치로 바꾸기 |
| [MainWindow.xaml.cs:108](../src/WindowFix/MainWindow.xaml.cs#L108) | 코드 | ↺  이 배치로 맞추기 |
| [MainWindow.xaml.cs:110](../src/WindowFix/MainWindow.xaml.cs#L110) | 코드 | 창들을 이 배치대로 옮기고, 창 고정도 이 배치로 바꿔요. |
| [MainWindow.xaml.cs:111](../src/WindowFix/MainWindow.xaml.cs#L111) | 코드 | 창들을 이 배치대로 지금 바로 옮겨요. 꺼진 프로그램은 ('자동으로 켜기'를 켜 뒀다면) 켜 줘요. |
| [MainWindow.xaml.cs:125](../src/WindowFix/MainWindow.xaml.cs#L125) | 코드 | ● 지금 관리자 권한으로 실행 중이에요. |
| [MainWindow.xaml.cs:125](../src/WindowFix/MainWindow.xaml.cs#L125) | 코드 | 지금은 일반 권한으로 실행 중이에요. |
| [MainWindow.xaml.cs:142](../src/WindowFix/MainWindow.xaml.cs#L142) | 코드 | ↩  돌아가기 |
| [MainWindow.xaml.cs:142](../src/WindowFix/MainWindow.xaml.cs#L142) | 코드 | ⚙  설정 |
| [MainWindow.xaml.cs:143](../src/WindowFix/MainWindow.xaml.cs#L143) | 코드 | 설정을 닫고 보던 배치로 돌아가요. |
| [MainWindow.xaml.cs:143](../src/WindowFix/MainWindow.xaml.cs#L143) | 코드 | 테마, 시작 설정 등을 바꿔요. |
| [MainWindow.xaml.cs:205](../src/WindowFix/MainWindow.xaml.cs#L205) | 코드 | 환영해요! 가운데 안내를 따라 첫 배치를 만들어 보세요. |
| [MainWindow.xaml.cs:239](../src/WindowFix/MainWindow.xaml.cs#L239) | 코드 | ✔ '{p.Name}' 배치로 맞췄어요 ({report.Results.Count}개 창). |
| [MainWindow.xaml.cs:240](../src/WindowFix/MainWindow.xaml.cs#L240) | 코드 | '{p.Name}' 배치로 맞췄어요. 단,\n  ·  |
| [MainWindow.xaml.cs:247](../src/WindowFix/MainWindow.xaml.cs#L247) | 코드 | 창이 안 보여서 건너뛰었어요 (프로그램이 켜져 있는지 확인하세요) |
| [MainWindow.xaml.cs:248](../src/WindowFix/MainWindow.xaml.cs#L248) | 코드 | 그 모니터가 연결돼 있지 않아 건너뛰었어요 |
| [MainWindow.xaml.cs:249](../src/WindowFix/MainWindow.xaml.cs#L249) | 코드 | 옮길 수 없었어요 |
| [MainWindow.xaml.cs:250](../src/WindowFix/MainWindow.xaml.cs#L250) | 코드 | 관리자 권한으로 켜진 창이라 옮길 수 없었어요 (위의 '관리자 권한으로 다시 실행'을 누르세요) |
| [MainWindow.xaml.cs:252](../src/WindowFix/MainWindow.xaml.cs#L252) | 코드 | 프로그램을 켜는 중이에요. 창이 뜨면 창 고정이 알아서 제자리에 놓아요 |
| [MainWindow.xaml.cs:253](../src/WindowFix/MainWindow.xaml.cs#L253) | 코드 | 프로그램을 켜는 중이에요. 창이 뜨면 [이 배치로 맞추기]를 한 번 더 눌러 주세요 |
| [MainWindow.xaml.cs:254](../src/WindowFix/MainWindow.xaml.cs#L254) | 코드 | 프로그램을 켤 수 없었어요 (창 설정 → 고급 설정의 실행할 파일을 확인하세요) |
| [MainWindow.xaml.cs:255](../src/WindowFix/MainWindow.xaml.cs#L255) | 코드 | 완료 |
| [MainWindow.xaml.cs:308](../src/WindowFix/MainWindow.xaml.cs#L308) | 코드 | 배치 이름 바꾸기 |
| [MainWindow.xaml.cs:308](../src/WindowFix/MainWindow.xaml.cs#L308) | 코드 | 새 이름을 입력하세요. |
| [MainWindow.xaml.cs:312](../src/WindowFix/MainWindow.xaml.cs#L312) | 코드 | ✔ 배치 이름을 '{name}'(으)로 바꿨어요. |
| [MainWindow.xaml.cs:322](../src/WindowFix/MainWindow.xaml.cs#L322) | 코드 | '{p.Name}' 배치를 삭제할까요? |
| [MainWindow.xaml.cs:323](../src/WindowFix/MainWindow.xaml.cs#L323) | 코드 | 창이나 프로그램은 그대로 있어요. 저장한 자리 정보만 지워져요. |
| [MainWindow.xaml.cs:323](../src/WindowFix/MainWindow.xaml.cs#L323) | 코드 | 삭제 |
| [MainWindow.xaml.cs:334](../src/WindowFix/MainWindow.xaml.cs#L334) | 코드 | 고정하던 '{p.Name}' 배치를 지워서 창 고정을 껐어요. |
| [MainWindow.xaml.cs:339](../src/WindowFix/MainWindow.xaml.cs#L339) | 코드 | '{p.Name}' 배치를 지웠어요. |
| [MainWindow.xaml.cs:361](../src/WindowFix/MainWindow.xaml.cs#L361) | 코드 | ✔ '{preset.Name}' 배치를 만들었어요. 이제 이 배치를 고정하고 있어요. |
| [MainWindow.xaml.cs:362](../src/WindowFix/MainWindow.xaml.cs#L362) | 코드 | ✔ '{preset.Name}' 배치를 만들었어요! 이제 [🔒 창 고정]을 켜면 창이 이 자리에 고정돼요. |
| [MainWindow.xaml.cs:370](../src/WindowFix/MainWindow.xaml.cs#L370) | 코드 | 배치 {n} |
| [MainWindow.xaml.cs:371](../src/WindowFix/MainWindow.xaml.cs#L371) | 코드 | 배치 {n} |
| [MainWindow.xaml.cs:390](../src/WindowFix/MainWindow.xaml.cs#L390) | 코드 | ✔ '{p.Name}' 배치를 고쳤어요 |
| [MainWindow.xaml.cs:390](../src/WindowFix/MainWindow.xaml.cs#L390) | 코드 | . 다시 고정했어요. |
| [MainWindow.xaml.cs:391](../src/WindowFix/MainWindow.xaml.cs#L391) | 코드 | 수정을 취소했어요. 창들을 원래 자리로 되돌리고 다시 고정했어요. |
| [MainWindow.xaml.cs:391](../src/WindowFix/MainWindow.xaml.cs#L391) | 코드 | 수정을 취소했어요. |
| [MainWindow.xaml.cs:397](../src/WindowFix/MainWindow.xaml.cs#L397) | 코드 | 관리자 권한 확인을 취소했어요. 관리자 권한 창은 옮기지 못해요. |
| [MainWindow.xaml.cs:407](../src/WindowFix/MainWindow.xaml.cs#L407) | 코드 | 🔒 '{_host.CurrentPreset?.Name}' 배치를 고정했어요. 이 창을 닫아도 계속 지켜요. 컴퓨터를 다시 켜도 자동으로 켜져요. |
| [MainWindow.xaml.cs:408](../src/WindowFix/MainWindow.xaml.cs#L408) | 코드 | 창 고정을 껐어요. 이제 창을 자유롭게 옮길 수 있어요. |
| [MainWindow.xaml.cs:452](../src/WindowFix/MainWindow.xaml.cs#L452) | 코드 | 관리자 권한 확인을 취소해서 옵션을 껐어요. |
| [MainWindow.xaml.cs:457](../src/WindowFix/MainWindow.xaml.cs#L457) | 코드 | ✔ 이제 항상 관리자 권한으로 실행돼요. 컴퓨터를 켤 때도 확인 창 없이 관리자 권한으로 켜져요. |
| [MainWindow.xaml.cs:458](../src/WindowFix/MainWindow.xaml.cs#L458) | 코드 | 다음 실행부터 일반 권한으로 켜져요. |
| [MainWindow.xaml.cs:476](../src/WindowFix/MainWindow.xaml.cs#L476) | 코드 | {entry.DisplayName} 설정 |
| [MainWindow.xaml.cs:487](../src/WindowFix/MainWindow.xaml.cs#L487) | 코드 | 프로그램: {entry.Match.ExeName} |
| [MainWindow.xaml.cs:487](../src/WindowFix/MainWindow.xaml.cs#L487) | 코드 |  · 창 종류: {c} |
| [MainWindow.xaml.cs:502](../src/WindowFix/MainWindow.xaml.cs#L502) | 코드 | 지금 떠 있는 {entry.DisplayName} 창이 없어요. 창을 띄운 뒤 다시 눌러 주세요. |
| [MainWindow.xaml.cs:507](../src/WindowFix/MainWindow.xaml.cs#L507) | 코드 | 창이 {live.Count}개라 첫 번째 제목을 넣었어요. 구분되는 단어만 남기고 지우세요. |
| [MainWindow.xaml.cs:508](../src/WindowFix/MainWindow.xaml.cs#L508) | 코드 | 제목을 넣었어요. 매번 바뀌는 부분은 지우고 구분되는 단어만 남기세요. |
| [MainWindow.xaml.cs:515](../src/WindowFix/MainWindow.xaml.cs#L515) | 코드 | 자동으로 켤 프로그램 고르기 |
| [MainWindow.xaml.cs:516](../src/WindowFix/MainWindow.xaml.cs#L516) | 코드 | 프로그램 (*.exe)\|*.exe\|모든 파일 (*.*)\|*.* |
| [MainWindow.xaml.cs:529](../src/WindowFix/MainWindow.xaml.cs#L529) | 코드 | 위치와 크기에는 숫자만 넣어 주세요 (너비·높이는 1 이상). |
| [MainWindow.xaml.cs:544](../src/WindowFix/MainWindow.xaml.cs#L544) | 코드 | ✔ '{entry.DisplayName}' 설정을 저장하고 바로 반영했어요. |
| [MainWindow.xaml.cs:545](../src/WindowFix/MainWindow.xaml.cs#L545) | 코드 | ✔ '{entry.DisplayName}' 설정을 저장했어요. [이 배치로 맞추기]를 누르면 바로 반영돼요. |

## 새 배치 만들기 / 배치 수정 창

| 위치 | 종류 | 문구 |
|---|---|---|
| [SaveDialog.xaml:5](../src/WindowFix/SaveDialog.xaml#L5) | 창 제목 | 새 배치 만들기 |
| [SaveDialog.xaml:12](../src/WindowFix/SaveDialog.xaml#L12) | 버튼/글자 | 저장하기 |
| [SaveDialog.xaml:13](../src/WindowFix/SaveDialog.xaml#L13) | 버튼/글자 | 취소 |
| [SaveDialog.xaml:16](../src/WindowFix/SaveDialog.xaml#L16) | 글자 | 배치 이름 |
| [SaveDialog.xaml:32](../src/WindowFix/SaveDialog.xaml#L32) | 버튼/글자 | ↻ 새로고침 |
| [SaveDialog.xaml:33](../src/WindowFix/SaveDialog.xaml#L33) | 마우스 올렸을 때 설명 | 창을 새로 띄웠거나 옮겼다면 눌러서 지금 화면을 다시 읽어 와요. (체크한 건 그대로 둬요) |
| [SaveDialog.xaml:34](../src/WindowFix/SaveDialog.xaml#L34) | 글자 | 어떤 창을 고정할까요? |
| [SaveDialog.xaml:37](../src/WindowFix/SaveDialog.xaml#L37) | 글자 | 지금 화면에 떠 있는 창이에요. 고정할 창에 체크하세요. 오른쪽 그림에서 창을 눌러도 돼요. (보조 모니터에 있는 창은 미리 체크해 뒀어요) |
| [SaveDialog.xaml:39](../src/WindowFix/SaveDialog.xaml#L39) | 글자 | 찾는 창이 없나요? 최소화했거나 트레이에 숨긴 창은 안 보여요. 그 창을 화면에 띄운 뒤 [↻ 새로고침]을 누르세요. |
| [SaveDialog.xaml:63](../src/WindowFix/SaveDialog.xaml#L63) | 글자 | 다른 모니터 — 누르면 크게 봐요 |
| [SaveDialog.xaml.cs:40](../src/WindowFix/SaveDialog.xaml.cs#L40) | 코드 | 변경 저장 |
| [SaveDialog.xaml.cs:95](../src/WindowFix/SaveDialog.xaml.cs#L95) | 코드 | ⚠ 관리자 권한 앱 (게임 등) ·  |
| [SaveDialog.xaml.cs:97](../src/WindowFix/SaveDialog.xaml.cs#L97) | 코드 | {warn}모니터 {ordered.IndexOf(m) + 1}{(m.IsPrimary ?  |
| [SaveDialog.xaml.cs:125](../src/WindowFix/SaveDialog.xaml.cs#L125) | 코드 | 이 모니터 |
| [SaveDialog.xaml.cs:125](../src/WindowFix/SaveDialog.xaml.cs#L125) | 코드 | 모니터 {i + 1}{(ordered[i].IsPrimary ?  |
| [SaveDialog.xaml.cs:134](../src/WindowFix/SaveDialog.xaml.cs#L134) | 코드 | '{_editing!.Name}' 배치 수정 · {label} |
| [SaveDialog.xaml.cs:135](../src/WindowFix/SaveDialog.xaml.cs#L135) | 코드 | {label}에 둘 창을 고르세요 |
| [SaveDialog.xaml.cs:136](../src/WindowFix/SaveDialog.xaml.cs#L136) | 코드 | 체크한 창들의 지금 위치와 크기로 배치가 바뀌어요. 수정하는 동안은 창 고정이 잠깐 풀려 있으니,  |
| [SaveDialog.xaml.cs:137](../src/WindowFix/SaveDialog.xaml.cs#L137) | 코드 | 창을 옮기고 이 창으로 돌아오면 알아서 다시 읽어요([↻ 새로고침]으로도 돼요). 아래 작은 모니터를 누르면 그 모니터로 바뀌어요. |
| [SaveDialog.xaml.cs:168](../src/WindowFix/SaveDialog.xaml.cs#L168) | 코드 | {Label(path)} 크게 보기 |
| [SaveDialog.xaml.cs:193](../src/WindowFix/SaveDialog.xaml.cs#L193) | 코드 | 전체 {n}개 창 선택됨 |
| [SaveDialog.xaml.cs:193](../src/WindowFix/SaveDialog.xaml.cs#L193) | 코드 | {n}개 창 선택됨 |

## 모니터 미리보기

| 위치 | 종류 | 문구 |
|---|---|---|
| [Controls/MonitorPreview.cs:83](../src/WindowFix/Controls/MonitorPreview.cs#L83) | 코드 | 카카오 |
| [Controls/MonitorPreview.cs:83](../src/WindowFix/Controls/MonitorPreview.cs#L83) | 코드 | 카톡 |
| [Controls/MonitorPreview.cs:156](../src/WindowFix/Controls/MonitorPreview.cs#L156) | 코드 | 모니터 {index++}{(m.IsPrimary ?  |

## 가장자리 고정 탭

| 위치 | 종류 | 문구 |
|---|---|---|
| [FloatingLockButton.xaml:4](../src/WindowFix/FloatingLockButton.xaml#L4) | 창 제목 | WindowFix 고정 버튼 |
| [FloatingLockButton.xaml.cs:76](../src/WindowFix/FloatingLockButton.xaml.cs#L76) | 코드 | 잠깐 풀어 둔 상태예요. 누르면 창들이 원래 자리로 돌아가고 다시 고정돼요. |
| [FloatingLockButton.xaml.cs:77](../src/WindowFix/FloatingLockButton.xaml.cs#L77) | 코드 | 창 고정 중이에요. 누르면 잠깐 풀려서 창을 옮길 수 있어요. (끌어서 다른 가장자리로 옮길 수 있어요) |

## 트레이 아이콘 메뉴

| 위치 | 종류 | 문구 |
|---|---|---|
| [Tray/TrayIcon.cs:37](../src/WindowFix/Tray/TrayIcon.cs#L37) | 코드 | WindowFix · 창 고정 중 |
| [Tray/TrayIcon.cs:38](../src/WindowFix/Tray/TrayIcon.cs#L38) | 코드 | WindowFix · 잠깐 풀어 둠 |
| [Tray/TrayIcon.cs:52](../src/WindowFix/Tray/TrayIcon.cs#L52) | 코드 | WindowFix 열기 |
| [Tray/TrayIcon.cs:54](../src/WindowFix/Tray/TrayIcon.cs#L54) | 코드 | WindowFix 설정 |
| [Tray/TrayIcon.cs:57](../src/WindowFix/Tray/TrayIcon.cs#L57) | 코드 | 이 배치로 맞추기 |
| [Tray/TrayIcon.cs:74](../src/WindowFix/Tray/TrayIcon.cs#L74) | 코드 | 다시 고정하기 (원래 자리로) |
| [Tray/TrayIcon.cs:77](../src/WindowFix/Tray/TrayIcon.cs#L77) | 코드 | 잠깐 풀기 |
| [Tray/TrayIcon.cs:80](../src/WindowFix/Tray/TrayIcon.cs#L80) | 코드 | 창 고정 |
| [Tray/TrayIcon.cs:87](../src/WindowFix/Tray/TrayIcon.cs#L87) | 코드 | WindowFix 끝내기 |

## 앱 전체 (시작, 관리자 권한, 에러 안내)

| 위치 | 종류 | 문구 |
|---|---|---|
| [AppHost.cs:68](../src/WindowFix/AppHost.cs#L68) | 코드 | 설정 파일을 읽을 수 없어요 |
| [AppHost.cs:69](../src/WindowFix/AppHost.cs#L69) | 코드 | 저장해 둔 배치를 지키기 위해 WindowFix를 닫을게요. 잠시 뒤 다시 실행해 주세요. |
| [App.xaml.cs:39](../src/WindowFix/App.xaml.cs#L39) | 코드 | 예상하지 못한 문제가 생겼어요 |
| [App.xaml.cs:40](../src/WindowFix/App.xaml.cs#L40) | 코드 | WindowFix는 계속 동작해요. 같은 문제가 반복되면 %APPDATA%\WindowFix\error.log 파일을 보내 주세요. |

## 확인 / 입력 창

| 위치 | 종류 | 문구 |
|---|---|---|
| [Dialogs/AppDialog.xaml:21](../src/WindowFix/Dialogs/AppDialog.xaml#L21) | 버튼/글자 | 취소 |
| [Dialogs/AppDialog.xaml:22](../src/WindowFix/Dialogs/AppDialog.xaml#L22) | 버튼/글자 | 확인 |
| [Dialogs/AppDialog.xaml.cs:37](../src/WindowFix/Dialogs/AppDialog.xaml.cs#L37) | 코드 | 확인 |
| [Dialogs/AppDialog.xaml.cs:37](../src/WindowFix/Dialogs/AppDialog.xaml.cs#L37) | 코드 | 취소 |
| [Dialogs/AppDialog.xaml.cs:52](../src/WindowFix/Dialogs/AppDialog.xaml.cs#L52) | 코드 | 저장 |

## 내부 로직에서 나오는 문구

| 위치 | 종류 | 문구 |
|---|---|---|
| [WindowFix.Core/Startup/StartupRegistration.cs:51](../src/WindowFix.Core/Startup/StartupRegistration.cs#L51) | 코드 | WindowFix 시작 (관리자) |
| [WindowFix.Core/Startup/StartupRegistration.cs:52](../src/WindowFix.Core/Startup/StartupRegistration.cs#L52) | 코드 | WindowFix 열기 (관리자) |
| [WindowFix.Core/Storage/ConfigStore.cs:67](../src/WindowFix.Core/Storage/ConfigStore.cs#L67) | 코드 | 설정 파일을 읽을 수 없어요: {path} |

