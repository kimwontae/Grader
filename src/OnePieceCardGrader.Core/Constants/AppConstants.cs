namespace OnePieceCardGrader.Core.Constants;

public static class AppConstants
{
    public const string ApplicationName = "One Piece Card Grader";
    public const string ApplicationFolderName = "OnePieceCardGrader";
    public const string DatabaseFileName = "grader.db";
    public const string DefaultProfileName = "PSA";

    public const string DisclaimerKo =
        "본 결과는 이미지 분석을 기반으로 한 PSA 예상 등급이며 실제 PSA 등급과 다를 수 있습니다.";

    public const string ConfidenceMeaningKo =
        "분석 신뢰도는 입력 사진에서 결함을 분석할 수 있는 정도이며, PSA 적중 확률이 아닙니다.";

    public const int MinGrade = 1;
    public const int MaxGrade = 10;

    public const double PerfectCenteringPercent = 50.0;
}
