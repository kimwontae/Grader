namespace OnePieceCardGrader.Core.Models;

public static class AnalysisStageNames
{
    public const string Prepare = "분석 준비";
    public const string Front = "Front 이미지 처리";
    public const string Back = "Back 이미지 처리";
    public const string Centering = "센터링 분석";
    public const string Corners = "코너 분석";
    public const string Edges = "엣지 분석";
    public const string Surface = "표면 분석";
    public const string Grading = "예상 등급 계산";
    public const string Done = "완료";

    public static readonly string[] All =
    [
        Prepare,
        Front,
        Back,
        Centering,
        Corners,
        Edges,
        Surface,
        Grading,
        Done
    ];
}
