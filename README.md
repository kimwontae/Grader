# One Piece Card Grader

원피스 카드 사진을 항목별로 분석하여 **PSA 예상 등급**을 산출하는 Windows 데스크톱 애플리케이션입니다.

본 결과는 이미지 분석을 기반으로 한 PSA 예상 등급이며 실제 PSA 등급과 다를 수 있습니다. PSA 공식 판정을 대체하지 않습니다.

## 프로젝트 목적

- Front/Back 카드 사진에서 카드 영역을 검출하고 원근을 보정합니다.
- 내부 print frame을 기준으로 Centering(L/R, T/B)을 측정합니다.
- 공개된 PSA centering guideline을 설정 파일로 적용해 예상 등급 상한(Grade Cap)을 계산합니다.
- 분석 근거, 신뢰도, 결함 overlay를 사용자에게 설명 가능한 형태로 제공합니다.

## Architecture

```text
OnePieceCardGrader.App            WPF / MVVM / DI
OnePieceCardGrader.Core           Models, Enums, Interfaces, 순수 계산
OnePieceCardGrader.Imaging        OpenCvSharp 분석 파이프라인
OnePieceCardGrader.Grading        PSA rule engine, explanation
OnePieceCardGrader.Infrastructure SQLite, 파일 저장, 설정
```

UI와 이미지 분석 코드는 분리되어 있습니다. ViewModel은 `ICardAnalysisPipeline` / `ICardPreviewService`만 호출합니다.

향후 ONNX Runtime 결함 검출 모델을 붙일 수 있도록 `IDefectDetectionModel`과 `ICardTemplateProvider`를 준비했습니다. MVP에서는 null/empty 구현입니다.

## 사용 기술

- C# / .NET 8 / WPF / MVVM (CommunityToolkit.Mvvm)
- OpenCvSharp4
- Microsoft.Extensions.DependencyInjection / Logging
- Entity Framework Core + SQLite

## Build 방법

```bash
dotnet restore
dotnet build OnePieceCardGrader.sln -c Release
dotnet test OnePieceCardGrader.sln
dotnet run --project src/OnePieceCardGrader.App/OnePieceCardGrader.App.csproj
```

Visual Studio에서 `OnePieceCardGrader.sln`을 열고 App 프로젝트를 시작 프로젝트로 설정해도 됩니다. 대상은 Windows x64입니다.

## Grading Pipeline

```text
사진 입력
→ Image Quality 검사
→ Card Detection (또는 수동 네 점)
→ Perspective Correction → NormalizedCardImage
→ Centering 분석 (Auto / Manual Guide)
→ Corner / Edge / Surface (현재 Not Implemented)
→ PSA Rule Engine (Grade Cap, range, confidence)
→ 결과/근거 표시 및 SQLite 저장
```

이미지 품질이 기준 미달이거나 카드 검출이 실패하면 등급을 억지로 만들지 않습니다.

## Centering 계산 방식

Normalized 이미지에서 카드 외곽은 이미지 경계로 두고, 내부 print frame을 gradient projection으로 찾습니다.

```text
LeftPercent  = LeftMargin  / (LeftMargin + RightMargin) * 100
RightPercent = RightMargin / (LeftMargin + RightMargin) * 100
TopPercent / BottomPercent 동일
WorstCentering = max(max(L, R), max(T, B))
```

자동 검출 신뢰도가 낮으면 수동 가이드(Print Left/Right/Top/Bottom)를 사용합니다.

## Corner 분석 방식

Front 네 모서리(TL/TR/BL/BR) 확대 사진은 선택입니다. 있으면 매크로 이미지를 우선 분석하고, 없으면 정규화된 전체 사진에서 약 12% ROI를 잘라 분석합니다.

- 외곽 밴드와 안쪽 밴드를 비교해 상대 밝기/채도/질감으로 whitening 후보를 찾습니다. 절대 흰색 픽셀만으로 판정하지 않습니다.
- 신뢰도가 낮으면 "검출"이 아니라 "의심"으로 표시합니다.
- 가장 낮은 코너 점수와 결함 Grade Cap이 최종 예상에 반영됩니다.

## Edge 분석 방식

현재 **Not Implemented**.

## Surface 분석 방식

Front/Back 정면·사광 사진은 선택입니다. 전용 사진이 없으면 전체 정규화 이미지로 보수적으로 분석하고 신뢰도를 낮춥니다.

- 먼저 glare mask를 만들고 반사 영역은 scratch로 확정하지 않습니다.
- morphological tophat으로 가늘고 긴 선형 후보만 남깁니다. Canny/Hough line을 scratch로 바로 쓰지 않습니다.
- 사광 사진이 있으면 ORB+homography로 정렬한 뒤, 사광에서만 강해지는 선에 점수를 더합니다.
- 자신감이 낮으면 "스크래치 의심"으로만 표시합니다.

## Grading Engine 설명

최종 PSA 점수는 항목 단순 평균이 아닙니다.

1. Centering condition score 산출
2. `Profiles/psa.json`의 Front/Back max ratio로 Grade Cap 적용
3. Critical defect cap 적용 (향후 Corner/Edge/Surface 결함과 연결)
4. 미구현 카테고리 때문에 예상 범위를 보수적으로 확장
5. Confidence는 **사진에서 분석 가능한 정도**이며 PSA 적중 확률이 아닙니다

## 설정 파일 설명

- `src/OnePieceCardGrader.App/Config/analysis.json`  
  카드 실측(mm), canonical resolution, quality/detection/centering threshold
- `src/OnePieceCardGrader.App/Profiles/psa.json`  
  PSA centering cap, defect cap, score threshold
- `%LocalAppData%/OnePieceCardGrader/Data/settings.json`  
  Expert sensitivity, developer debug 이미지 저장

이미지는 SQLite Blob이 아니라 `%LocalAppData%/OnePieceCardGrader/Data/Images/{AnalysisId}/`에 저장됩니다.

## 현재 Limitations

자세한 내용은 [LIMITATIONS.md](LIMITATIONS.md)를 참고하세요.

## 향후 ML 계획

실제 PSA 피드백(예측 vs 실제 등급, 중간 metric JSON)을 축적한 뒤 Rule Engine + ONNX 모델을 결합할 예정입니다. 지금은 외부 AI API를 사용하지 않으며 모든 처리는 로컬에서 수행합니다.
