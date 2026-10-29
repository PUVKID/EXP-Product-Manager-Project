# 로그 확인

**로그** 화면에는 프로그램이 한 일이 시간순으로 쌓입니다.

## 열
시간, Level(INFO/WARN/ERROR), Module, Action, Message, Result(OK/FAIL)

## 사용법
- 위쪽의 검색칸과 Level 선택으로 걸러 볼 수 있습니다.
- ERROR는 빨강, WARN은 주황으로 보입니다.
- 자동 스크롤을 켜면 새 로그가 올 때 맨 아래로 따라갑니다.
- **로그 폴더 열기**로 파일 위치를 바로 열 수 있습니다.

## 파일 위치
`%AppData%\BunjangAuto\logs` 에 날짜별 파일(`yyyyMMdd.log`)로 저장됩니다. 구조화된 기록(`yyyyMMdd.jsonl`)과 등록 실패 때의 화면 캡처(`screenshots` 폴더)도 있습니다.

문의할 때는 해당 날짜의 로그 파일을 보내 주시면 원인을 빨리 찾을 수 있습니다.
