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

## 오류 해결

### 스마트 앱 컨트롤이 설치기 또는 게임 실행을 차단할 때

이 패치의 설치 EXE와 일부 DLL은 디지털 서명이 없어 Windows 스마트 앱 컨트롤에서 차단될 수 있습니다. 오류 안내에서 스마트 앱 컨트롤에 의한 차단인지 먼저 확인하세요.

해당 기능이 차단 원인이라면 아래 설정을 끈 뒤 다시 실행할 수 있습니다. **이 설정은 PC 전체의 앱 실행 보호 수준을 낮추므로 변경 여부는 사용자가 판단해 주세요.** 변경 전 화면에 표시되는 재활성화 관련 안내도 확인하세요.

1. **Windows 보안**을 엽니다.
2. **앱 및 브라우저 컨트롤 → 스마트 앱 컨트롤 설정**으로 이동합니다.
3. 설정 변경을 선택한 경우 **끄기**로 바꾼 뒤, 차단됐던 설치기 또는 게임을 다시 실행합니다.

설치 EXE만 차단된다면 **수동 설치판**을 이용할 수 있습니다. 다만 게임이 불러오는 패치 DLL 자체가 차단되면 **수동 설치로도 해결되지 않습니다.**

차단 원인이 다르거나 문제가 계속되면 오류 문구와 차단된 파일명을 확인한 뒤, 저장소의 [이슈(Issues) 탭](https://github.com/etashi04/GalMaster/issues)에 제보해 주세요. 로그·스크린샷을 공개할 때는 사용자 이름과 개인 경로를 가려 주세요.

## 주의

- 원작자·개발사·배급사와 무관한 비공식 한국어 팬 패치입니다.
- 게임 업데이트 후 호환되지 않을 수 있습니다.
- 배포 파일은 `SHA256SUMS.txt`로 무결성을 확인할 수 있습니다.
- 정품 게임이 필요하며 게임 및 편집 이미지의 권리는 원권리자에게 있습니다.
- 원본 영상 위에 한국어 자막을 추가하므로 영상의 중국어 문구는 그대로 남습니다.
- 다른 빌드·운영체제 및 다른 모드와의 병용은 미검증입니다. 기존 시험판은 먼저 복구하세요.
- 첫 실행은 초기화로 더 오래 걸릴 수 있습니다.
- 제3자 구성요소의 라이선스는 [제3자 라이선스 전문](distribution/제3자_라이선스_고지.txt)을 확인하세요. 포함 구성요소: BepInEx, HarmonyX, MonoMod, Mono.Cecil, Unity Doorstop, 나눔스퀘어라운드.
