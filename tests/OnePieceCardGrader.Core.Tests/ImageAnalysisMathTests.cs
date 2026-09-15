using OnePieceCardGrader.Core.Calculations;
using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Options;

namespace OnePieceCardGrader.Core.Tests;

public sealed class ImageAnalysisMathTests
{
    [Fact]
    public void NormalizeWithReference_ShouldClampToUnitRange()
    {
        Assert.Equal(0, MetricNormalization.NormalizeWithReference(0, 0.08));
        Assert.Equal(0.5, MetricNormalization.NormalizeWithReference(0.04, 0.08), 3);
        Assert.Equal(1, MetricNormalization.NormalizeWithReference(0.2, 0.08));
    }

    [Fact]
    public void Normalize_ShouldUseMinMaxWindow()
    {
        Assert.Equal(0, MetricNormalization.Normalize(12, 12, 30));
        Assert.Equal(1, MetricNormalization.Normalize(30, 12, 30));
        Assert.InRange(MetricNormalization.Normalize(21, 12, 30), 0.49, 0.51);
    }

    [Fact]
    public void CombineComplement_ShouldIncreaseWhenBothDefectsPresent()
    {
        var combined = MetricNormalization.CombineComplement(0.3, 0.4);
        Assert.True(combined > 0.4);
        Assert.True(combined < 0.3 + 0.4);
        Assert.Equal(1, MetricNormalization.CombineComplement(1, 0.2));
    }

    [Fact]
    public void WhiteningSeverity_ShouldUseWeightedMetricsNotRawRatio()
    {
        var options = new WhiteningAnalysisOptions();
        var severity = WhiteningScoreCalculator.ComputeSeverity(0.0172, 0.0031, 22.4, 0.006, options);
        Assert.InRange(severity, 0.05, 0.55);
        Assert.NotEqual(0.0172, severity);
    }

    [Fact]
    public void WhiteningScore_ShouldApplyExtraPenaltyForLargeSingleDefect()
    {
        var options = new WhiteningAnalysisOptions();
        var mild = WhiteningScoreCalculator.ComputeConditionScore(0.2, 0.2, options);
        var spiked = WhiteningScoreCalculator.ComputeConditionScore(0.2, 0.9, options);
        Assert.True(spiked < mild);
    }

    [Fact]
    public void CornerFinalScore_ShouldNotHideWorstCornerInAverage()
    {
        var options = new CornerGeometryAnalysisOptions();
        var averaged = new[] { 90.0, 90.0, 90.0, 40.0 }.Average();
        var final = CornerGeometryScoreCalculator.ComputeFinalCornerScore([90, 90, 90, 40], options);
        Assert.True(final < averaged);
        Assert.InRange(final, 40, 85);
    }

    [Fact]
    public void GeometryClassification_ShouldPreferChippingWhenMissingAreaIsHigh()
    {
        var options = new CornerGeometryAnalysisOptions();
        var type = CornerGeometryScoreCalculator.Classify(0.8, 0.2, 0.1, options);
        Assert.Equal(CornerGeometryType.MissingMaterial, type);
    }

    [Fact]
    public void ScratchSurfaceSeverity_ShouldWeightWorstCandidate()
    {
        var options = new ScratchAnalysisOptions();
        var severity = ScratchScoreCalculator.ComputeSurfaceSeverity(0.8, 0.2, 0.1, 0.1, options);
        Assert.True(severity > 0.45);
        Assert.True(severity < 0.8);
    }

    [Fact]
    public void ScratchConfidence_ShouldDropWithoutAngledLight()
    {
        var options = new ScratchAnalysisOptions();
        var withAngled = ScratchScoreCalculator.ComputeConfidence(80, 80, 0.9, 0.05, 0.8, 1.0, options);
        var withoutAngled = ScratchScoreCalculator.ComputeConfidence(80, 40, 0.2, 0.05, 0.8, 0.3, options);
        Assert.True(withoutAngled < withAngled);
    }

    [Fact]
    public void WhiteningConfidence_IsIndependentFromConditionScore()
    {
        var options = new WhiteningAnalysisOptions();
        var score = WhiteningScoreCalculator.ComputeConditionScore(0.8, 0.2, options);
        var confidence = WhiteningScoreCalculator.ComputeConfidence(90, 0.9, 0.9, 0.9, options);
        Assert.True(score < 70);
        Assert.True(confidence > 80);
    }
}
