# 개발 및 유지보수

패치를 설치하려면 [README의 다운로드·설치 안내](README.md)를 확인하세요.
## 저장소 구성

- `src/`: 런타임 한국어 패치 소스
- `package/`: 한국어 번역·이미지·폰트 및 배포 런타임
- `installer/`: 공용 규격 GUI 설치기 소스·이미지·지원 파일 해시
- `distribution/`: 설치·복구 안내, 라이선스와 변경 파일 목록
- `scripts/`: 빌드·패키징 스크립트

## 빌드

Windows의 .NET Framework C# 컴파일러와 정품 게임의 Managed DLL이 필요합니다.
`powershell -ExecutionPolicy Bypass -File scripts/build-distributions.ps1 -GamePath "게임 설치 폴더"`를 실행합니다.
게임 참조 DLL은 저장소에 넣지 않습니다. 결과는 `build` 아래 생성됩니다.

## 빌드 시 참고

- 저장소 루트에서 위 명령을 실행합니다.
- `build/`가 이미 있으면 빌드가 중단됩니다. 이전 결과를 별도로 보관한 뒤 실행하세요.
- 빌드는 `package/BepInEx/plugins/GalMasterKorean.dll`과 파일 해시 목록도 갱신합니다. 변경 내용을 확인한 뒤 커밋하세요.
- `build/`에 자동·수동 ZIP과 `SHA256SUMS.txt`가 생성됩니다.
- 버전명은 빌드 스크립트와 설치기·배포 문서에 지정되어 있으므로 새 버전을 만들 때 함께 확인해야 합니다.

## 검증 기록

v1.0.0에서 사용자 전체 플레이 검수와 종료·스킵·초기화 확인창 175프레임 검사를 진행했습니다. 검사 중 중국어 검출은 0건이었습니다.

최종 자동 설치기 화면·고배율 표시 및 최종 배포 ZIP의 전체 설치·복구 재검증은 미완료입니다.

- [배포 파일 및 SHA-256 목록](distribution/FILES.tsv)
- [변경 이력](CHANGELOG.md)
- [제3자 라이선스](distribution/제3자_라이선스_고지.txt)
