using CommunityToolkit.Mvvm.ComponentModel;
using OnePieceCardGrader.Core.Constants;

namespace OnePieceCardGrader.App.ViewModels;

public sealed class AboutViewModel : ObservableObject
{
    public string Title => AppConstants.ApplicationName;
    public string Disclaimer => AppConstants.DisclaimerKo;
    public string ConfidenceMeaning => AppConstants.ConfidenceMeaningKo;
    public IReadOnlyList<string> Implemented { get; } =
    [
        "WPF 메인 내비게이션 및 분석 마법사",
        "Front/Back 업로드, EXIF 회전, 로컬 이미지 저장",
        "Image Quality Analyzer (해상도/초점/노출/반사)",
        "Card Detection 및 Perspective Correction",
        "수동 네 점 보정",
        "Centering Analyzer (Auto + Manual Guide)",
        "PSA 센터링 Grade Cap 및 예상 범위",
        "SQLite 분석 기록, 실제 PSA 등급 입력, JSON Export"
    ];

    public IReadOnlyList<string> NotImplemented { get; } =
    [
        "Corner Analyzer",
        "Edge Analyzer",
        "Surface Analyzer (정면/사광 비교)",
        "Template Matching 센터링",
        "ONNX/YOLO 결함 검출 모델",
        "CGC/BGS/TAG 프로파일"
    ];
}
