using Microsoft.Extensions.Logging.Abstractions;
using OnePieceCardGrader.Core.Calculations;
using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Models;
using OnePieceCardGrader.Grading.Profiles;
using OnePieceCardGrader.Grading.Rules;
using OnePieceCardGrader.Grading.Services;

namespace OnePieceCardGrader.Grading.Tests;

public sealed class GradingEngineTests
{
    [Fact]
    public void GradingEngine_ShouldHonorCenteringCap()
    {
        var provider = new JsonGradingProfileProvider(Path.GetTempPath());
        var engine = new GradingEngine(provider, NullLogger<GradingEngine>.Instance);
        var front = CenteringMath.FromMargins(65, 35, 50, 50, 0.9);
        var analysis = new CardAnalysisResult
        {
            Centering = new CenteringResult
            {
                Status = AnalysisStatus.Completed,
                Front = front,
                EstimatedGradeCap = CenteringGradeCapCalculator.CalculateCap(front, null, provider.GetProfile("PSA"))
            },
            Coverage = new AnalysisCoverage { Overall = 35, Centering = 60, HasFront = true },
            Front = new SideImageAnalysis
            {
                Quality = new ImageQualityResult { OverallQualityScore = 90, IsAcceptable = true },
                Detection = new CardDetectionResult { Success = true, Confidence = 0.9 }
            }
        };

        var result = engine.Calculate(analysis);
        Assert.True(result.PredictedGrade <= 8);
        Assert.Equal(AnalysisStatus.NotImplemented, result.Corners.Status);
        Assert.InRange(result.AnalysisConfidence, 5, 95);
    }

    [Fact]
    public void CriticalDefect_ShouldLowerCap()
    {
        var provider = new JsonGradingProfileProvider(Path.GetTempPath());
        var evaluator = new CriticalDefectEvaluator(provider);
        var result = evaluator.Evaluate(
        [
            new DetectedDefect
            {
                Type = DefectType.Crease,
                Severity = DefectSeverity.Severe,
                Description = "Severe crease"
            }
        ]);

        Assert.Equal(5, result.MaxGrade);
    }

    [Fact]
    public void JsonProfile_ShouldLoadDefaultPsaWhenDirectoryMissing()
    {
        var provider = new JsonGradingProfileProvider(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var profile = provider.GetProfile("PSA");
        Assert.Equal("PSA", profile.Name);
        Assert.Equal(55, profile.Centering["10"].FrontMax);
        Assert.Equal(75, profile.Centering["10"].BackMax);
    }
}
