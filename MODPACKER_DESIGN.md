## 프로젝트 개요

이 문서는 `NeFSedit` GUI 중심 워크플로우를 자동화 가능한 CLI/배치 기반 툴로 확장하기 위한 요구사항, 설계, 구현 계획을 정리합니다. 목표는 매니페스트 파일을 입력으로 받아 대상 `.nefs` 아카이브에 다수의 파일을 일괄 교체/삽입하고, 백업과 롤백이 가능한 자동 패커(`modpacker.exe` 또는 `.bat`)를 제공하는 것입니다.

### 배경 / 현재 문제
- **현행**: GUI에서 `.nefs` 파일 오픈 → 파일을 하나씩 수동 교체 → 대량 변경이 비효율적
- **문제점**: 일괄처리 부재, 반복 작업 많음, 실수 가능성 증가, 자동화 파이프라인 미지원

### 목표
- **매니페스트 기반 일괄 교체**: 텍스트/CSV/JSON 등 간편한 포맷으로 매핑 정의
- **원본 백업/복구**: 작업 전 자동 백업, 실패 시 자동 복구 옵션
- **헤드리스 실행**: GUI 없이 한 번에 수행 가능한 `modpacker.exe` 또는 `.bat`
- **로그/리포트**: 상세 결과 로그와 요약 리포트 출력
- **재현성**: 동일 매니페스트로 반복 실행 시 동일 결과 보장

## 요구사항

### 기능 요구사항
- **입력**: 대상 `.nefs` 경로, 매니페스트 경로, 출력/백업 경로, 옵션(덮어쓰기 정책, 실패시 중단/계속 등)
- **작업**:
  - 매니페스트의 각 항목(원본 교체 경로 → 삽입 파일 경로)을 순회
  - 대상 항목 존재 시 교체, 미존재 시 삽입(옵션) 또는 스킵
  - 중복/여러 항목 처리, 경로 대소문자/슬래시 규칙 통일
- **출력**:
  - 성공/실패 요약, 상세 로그 파일(`.log`) 저장
  - 최종 `.nefs` 저장(원본 파일명 유지, 백업은 별도 확장자 또는 폴더)
- **안전성**:
  - 필수 프리체크(경로 유효성, 파일 존재, 권한, 여유 공간)
  - 원자적 저장(임시 파일 → 교체), 에러 발생 시 롤백

### 비기능 요구사항
- **성능**: 수백~수천 항목 처리 시에도 실용적 시간 내 완료
- **호환성**: 기존 `VictorBush.Ego.NefsLib` API를 재사용해 안정성 확보
- **확장성**: 포맷/옵션 추가가 용이하도록 모듈화
- **사용성**: 명령행 도움말, 예제 매니페스트 제공

## 아키텍처 개요

### 구성 요소
- **CLI 엔트리 포인트(`modpacker.exe`)**: 인자 파싱, 실행 플로우 제어
- **매니페스트 파서**: 지정 포맷(TSV/CSV/JSON 등) 읽어 표준 모델로 변환
- **작업 실행기**: 백업 → `.nefs` 열기 → 항목 교체/삽입 → 저장 → 검증/로그
- **로그/리포팅**: 콘솔/파일 출력, 실패 항목 재시도 옵션

### 코드베이스 매핑(기존 재사용 포인트)
- `VictorBush.Ego.NefsLib`
  - 아카이브 모델/IO/헤더 파싱/아이템 조작 API 재사용
  - 특히 `NefsArchive`, `Item`, `IO` 하위 API 참고
- `VictorBush.Ego.NefsEdit`
  - 현재 GUI 커맨드(`Commands/ReplaceFileCommand.cs`, `RemoveFileCommand.cs`)의 로직/흐름 참고
  - `Workspace/NefsEditWorkspace.cs`의 아카이브 열기/저장 흐름 참고
- 신규 **Headless** 서비스 계층 추가 제안: UI 의존 제거, 동일 동작을 프로그램적으로 수행

## 매니페스트 포맷 제안

간결성과 범용성, 에디팅 편의성을 고려하여 **TSV(탭 구분 텍스트)**를 기본으로 제안합니다. CSV/JSON도 옵션으로 지원 가능.

### TSV 기본 포맷
```text
# targetPath	localFilePath	[action]
# 주석과 빈 줄은 무시
vehicles/car_a/body.dds	mods/car_a/body_v2.dds	replace
ui/icons/car_a.png	mods/ui/car_a.png	replace
audio/engine/bank.bnk	mods/audio/new_engine.bnk	insert
```

- **targetPath**: `.nefs` 내부 경로(가상 경로)
- **localFilePath**: 교체/삽입할 로컬 파일 경로
- **action**: `replace` | `insert` | 생략 시 기본 `replace`

### JSON 예시(선택)
```json
{
  "items": [
    { "targetPath": "vehicles/car_a/body.dds", "localFilePath": "mods/car_a/body_v2.dds", "action": "replace" },
    { "targetPath": "ui/icons/car_a.png", "localFilePath": "mods/ui/car_a.png" }
  ]
}
```

## CLI 사용 시나리오

### 기본 사용
```bash
modpacker.exe \
  --archive "C:\\path\\to\\archive.nefs" \
  --manifest "C:\\mods\\manifest.tsv" \
  --backup-dir "C:\\mods\\backup" \
  --output "C:\\path\\to\\archive.nefs" \
  --on-missing insert \
  --continue-on-error false
```

### 옵션 설명(제안)
- `--archive`: 대상 `.nefs` 경로(필수)
- `--manifest`: 매니페스트 파일 경로(필수)
- `--backup-dir`: 백업 저장 폴더(미지정 시 `<archive>.bak/` 폴더 자동)
- `--output`: 결과 저장 경로(미지정 시 원본 덮어쓰기)
- `--on-missing`: 대상 항목 미존재 시 `insert|skip|fail` 중 선택(기본 `skip`)
- `--continue-on-error`: 개별 실패 시 계속 진행 여부(기본 `true`)
- `--dry-run`: 실제 저장 없이 검증/리포트만 수행
- `--log`: 로그 파일 경로(기본 `modpacker.log`)

## 구현 계획

### 1) Headless 실행 기반 만들기
- `VictorBush.Ego.NefsEdit`와 분리된 콘솔 프로젝트 신규 생성: `ModPacker.Cli`
- `VictorBush.Ego.NefsLib`에 직접 의존하여 아카이브 조작
- 공용 서비스 인터페이스 정의: `IArchivePatcher`, `IManifestParser`, `ILogService`

### 2) 매니페스트 파서
- 기본 TSV 파서 구현(주석/빈 줄/필드 수 유효성 검사)
- 확장: CSV/JSON 선택 지원

### 3) 패치 실행기
- 프리체크(경로, 파일 존재, 쓰기 가능, 디스크 용량)
- 백업 절차: 원본 복사 → 임시 작업 파일 사용 → 성공 시 스왑
- 항목 처리: `replace`/`insert`/`remove(옵션)`
- 저장 및 무결성 검증(옵션)

### 4) 로깅/리포팅
- 콘솔 + 파일 로그 동시 출력
- 결과 요약 테이블(총/성공/실패/스킵)
- 실패 항목 재실행 지원(선택)

### 5) 배포/사용성
- 단일 실행 파일 배포(셀프컨테인드) 또는 `.bat` 래퍼 제공
- 예제 `manifest.tsv` 템플릿, 샘플 `.bat` 제공

## 코드 변경 영향도

- 신규 프로젝트: `ModPacker.Cli` (Console)
- 재사용: `VictorBush.Ego.NefsLib` 전반, `VictorBush.Ego.NefsEdit`의 커맨드 로직을 참고하되 UI 의존 제거
- 가능하면 `VictorBush.Ego.NefsLib`에 **헤드리스 친화** 유틸 메서드(안전 저장, 경로 정규화) 추가

## 에러 처리/엣지 케이스

- 매니페스트 항목의 `localFilePath` 미존재 → 실패 기록, 정책에 따라 계속/중단
- `targetPath` 미존재 & `on-missing=insert` 아님 → 스킵 또는 실패
- 아카이브 포맷/버전 불일치 → 즉시 중단
- 파일 잠김/권한 문제 → 백업 복구 시도 후 종료
- 부분 성공 시 리포트로 재시도 가이드 제공

## 테스트 전략

- 단위 테스트: TSV/JSON 파싱, 경로 정규화, 정책별 분기
- 통합 테스트: 소형 테스트 아카이브로 replace/insert/save 검증
- 회귀 테스트: 동일 매니페스트 반복 실행 시 동일 바이너리 출력(해시 비교)

## 작업 항목(마일스톤)

1. 콘솔 프로젝트 스캐폴딩(`ModPacker.Cli`) 생성
2. 매니페스트 파서(TSV) 구현 및 테스트
3. 아카이브 패처 초안(`replace/insert`) + 백업/저장 플로우
4. CLI 옵션/도움말/로그 출력 정리
5. 통합 테스트(샘플 아카이브/매니페스트) + 안정화
6. 배포 번들 및 예제 스크립트/매니페스트 제공

## 예제: 매니페스트 + 배치 스크립트

### `manifest.tsv`
```text
# targetPath	localFilePath	[action]
ui/icons/car_a.png	mods/ui/car_a.png	replace
vehicles/car_a/body.dds	mods/car_a/body_v2.dds	replace
```

### `run_modpack.bat`
```bat
@echo off
set ARCHIVE="C:\\path\\to\\archive.nefs"
set MANIFEST="%~dp0manifest.tsv"
set BACKUP_DIR="%~dp0backup"
set OUT="C:\\path\\to\\archive.nefs"

modpacker.exe --archive %ARCHIVE% --manifest %MANIFEST% --backup-dir %BACKUP_DIR% --output %OUT% --on-missing insert --continue-on-error true --log "%~dp0modpacker.log"

if %ERRORLEVEL% NEQ 0 (
  echo Mod pack failed. See log for details.
  exit /b 1
)
echo Done.
```

## 향후 확장

- `remove`/`rename` 액션 지원, 조건부 교체(타임스탬프/해시 비교)
- 병렬 처리 최적화, 대용량 파일 스트리밍, 압축/암호화 대응
- GUI에서 매니페스트 생성/검증 도우미 제공


