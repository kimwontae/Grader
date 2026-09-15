using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Interfaces;
using OnePieceCardGrader.Core.Models;
using OnePieceCardGrader.Core.Options;

namespace OnePieceCardGrader.Grading.Rules;

public sealed class CriticalDefectEvaluator : ICriticalDefectEvaluator
{
    private readonly IGradingProfileProvider _profiles;

    public CriticalDefectEvaluator(IGradingProfileProvider profiles)
    {
        _profiles = profiles;
    }

    public GradeCapResult Evaluate(IReadOnlyCollection<DetectedDefect> defects)
    {
        var profile = _profiles.GetProfile("PSA");
        var maxGrade = 10;
        var reasons = new List<string>();
        var critical = new List<DetectedDefect>();

        foreach (var defect in defects.Where(d => d.ReviewStatus != DefectReviewStatus.FalsePositive
                                                 && d.ReviewStatus != DefectReviewStatus.Ignored))
        {
            var key = $"{defect.Type}.{defect.Severity}";
            if (profile.DefectRules.TryGetValue(key, out var cap) && cap < maxGrade)
            {
                maxGrade = cap;
                reasons.Add($"{defect.Description} → 최대 예상 등급 {cap}");
                critical.Add(defect);
            }
            else if (defect.GradeCap is int defectCap && defectCap < maxGrade)
            {
                maxGrade = defectCap;
                reasons.Add(defect.Description);
                critical.Add(defect);
            }
        }

        return new GradeCapResult
        {
            MaxGrade = maxGrade,
            Reasons = reasons,
            CriticalDefects = critical
        };
    }
}
