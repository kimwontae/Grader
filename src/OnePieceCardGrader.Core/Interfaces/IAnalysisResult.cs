namespace OnePieceCardGrader.Core.Interfaces;

public interface IAnalysisResult
{
    double Severity { get; }

    double ConditionScore { get; }

    double Confidence { get; }
}
