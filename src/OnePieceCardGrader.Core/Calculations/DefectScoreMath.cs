using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Models;

namespace OnePieceCardGrader.Core.Calculations;

public static class DefectScoreMath
{
    public static int CapFromSeverity(DefectSeverity severity) => severity switch
    {
        DefectSeverity.Severe => 5,
        DefectSeverity.Major => 6,
        DefectSeverity.Moderate => 8,
        DefectSeverity.Minor => 9,
        _ => 10
    };

    public static double Penalty(DefectSeverity severity) => severity switch
    {
        DefectSeverity.Severe => 40,
        DefectSeverity.Major => 28,
        DefectSeverity.Moderate => 16,
        DefectSeverity.Minor => 8,
        DefectSeverity.Trace => 3,
        _ => 0
    };

    public static double ConditionScore(IEnumerable<DetectedDefect> defects, double baseline = 100)
    {
        var score = baseline;
        foreach (var defect in defects.Where(d => d.ReviewStatus is not DefectReviewStatus.FalsePositive
                                                 and not DefectReviewStatus.Ignored))
        {
            var weight = defect.Confidence < 0.45 ? 0.55 : 1.0;
            score -= Penalty(defect.Severity) * weight;
        }

        return Math.Clamp(score, 0, 100);
    }

    public static int GradeCap(IEnumerable<DetectedDefect> defects)
    {
        var cap = 10;
        foreach (var defect in defects.Where(d => d.ReviewStatus is not DefectReviewStatus.FalsePositive
                                                 and not DefectReviewStatus.Ignored))
        {
            var candidate = defect.GradeCap ?? CapFromSeverity(defect.Severity);
            if (defect.Confidence < 0.45)
            {
                candidate = Math.Min(10, candidate + 1);
            }

            cap = Math.Min(cap, candidate);
        }

        return cap;
    }
}
