# GalMaster 한국어 패치

Steam판 **GalMaster** 비공식 한국어 패치 저장소입니다.

첫 정식 배포 버전은 `v1.0.0`이며 Steam 앱 ID `4513880`, 빌드 `24775635`, Windows x64 환경을 대상으로 합니다.

<img width="1920" height="1080" alt="2000A0~1" src="https://github.com/user-attachments/assets/891ce82d-c03a-47b2-bb69-a785d5033252" />
<img width="1920" height="1080" alt="20A6F7~1" src="https://github.com/user-attachments/assets/3f2bd801-7293-4d07-97e8-55c8650d7fee" />
<img width="1920" height="1080" alt="202609~3" src="https://github.com/user-attachments/assets/509c37a3-c7f0-4a03-8b1d-b9d50629f3a1" />


## 배포본

[최신 릴리스](https://github.com/etashi04/GalMaster/releases/latest)에서 다운로드하세요.

- `GalMaster_Korean_Patch_v1.0.0.zip`: GUI 자동 설치·복구판
- `GalMaster_Korean_Patch_Manual_v1.0.0.zip`: 직접 복사하는 수동 설치판
- `SHA256SUMS.txt`: ZIP 무결성 확인용 SHA-256

게임을 종료한 상태에서 둘 중 하나만 설치하세요.

## 자동 설치

1. 자동 ZIP을 모두 압축 해제합니다.
2. `GalMaster 한국어 패치 v1.0.0.exe`를 실행합니다.
3. `GAL PRO MASTER.exe`가 있는 게임 폴더를 선택하고 **한국어 패치 설치**를 누릅니다.
4. 완료 후 Steam에서 게임을 실행합니다. 언어는 **중국어 간체**를 유지합니다.

## 수동 설치

1. 수동 ZIP을 모두 압축 해제합니다.
2. 기존 모드가 있다면 `BepInEx`, `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`을 별도 백업합니다.
3. `패치파일` **안의 내용 전체**를 `GAL PRO MASTER.exe`가 있는 폴더에 복사·병합·덮어씁니다.
4. Steam에서 게임을 실행합니다. 언어는 **중국어 간체**를 유지합니다.

## 복구

- 자동판: 동일 설치기의 **원본 복구**. 게임 폴더의 `_KR_PATCH_v1.0.0` 백업 폴더를 복구 전까지 보존하세요. 패치 또는 백업의 외부 변경이 감지되면 중단합니다.
- 수동판: 복사한 파일을 제거하고 설치 전 백업을 복원합니다. 다른 모드가 없는 순정 설치였다면 `BepInEx`, `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`을 제거합니다.
- 다른 모드가 있다면 `BepInEx` 전체를 삭제하지 마세요. 자동 복구 후 실행 중 생성된 로그·캐시·설정은 남을 수 있습니다.

## 주의

- 비공식 팬 번역입니다. 정품 게임이 필요하며 게임 및 편집 이미지의 권리는 원권리자에게 있습니다.
- 원본 영상 위에 한국어 자막을 추가하므로 영상의 중국어 문구는 그대로 남습니다.
- 다른 빌드·운영체제 및 다른 모드와의 병용은 미검증입니다. 기존 시험판은 먼저 복구하세요.
- 게임 업데이트 후 호환되지 않을 수 있습니다. 첫 실행은 초기화로 더 오래 걸릴 수 있습니다.
- [제3자 라이선스 전문](distribution/제3자_라이선스_고지.txt): BepInEx, HarmonyX, MonoMod, Mono.Cecil, Unity Doorstop, 나눔스퀘어라운드.
- [문제 제보](https://github.com/etashi04/GalMaster/issues)에 장면·재현 순서·게임 빌드를 남겨 주세요. 로그의 개인 경로는 지워 주세요.

저장소 구성과 빌드 방법은 [개발 문서](DEVELOPMENT.md)를 참고하세요.
