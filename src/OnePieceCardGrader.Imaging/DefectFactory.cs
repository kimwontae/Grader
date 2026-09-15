using OnePieceCardGrader.Core.Calculations;
using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Explanations;
using OnePieceCardGrader.Core.Models;

namespace OnePieceCardGrader.Imaging;

internal static class DefectFactory
{
    public static DefectSeverity FromSeverity(double severity) => severity switch
    {
        >= 0.72 => DefectSeverity.Major,
        >= 0.48 => DefectSeverity.Moderate,
        >= 0.22 => DefectSeverity.Minor,
        >= 0.08 => DefectSeverity.Trace,
        _ => DefectSeverity.None
    };

    public static DetectedDefect? FromWhitening(
        WhiteningAnalysisResult result,
        CardSide side,
        string label,
        DefectType type,
        NormalizedRect? region)
    {
        var severity = FromSeverity(result.Severity);
        if (severity == DefectSeverity.None || result.DefectCount == 0)
        {
            return null;
        }

        var gradeCap = DefectScoreMath.CapFromSeverity(severity);
        var narration = DefectNarrator.ForWhitening(side, label, type, severity, result, gradeCap);
        return new DetectedDefect
        {
            Type = type,
            Severity = severity,
            Side = side,
            Region = result.Components.FirstOrDefault()?.BoundingBox ?? region,
            Confidence = Math.Clamp(result.Confidence / 100.0, 0, 1),
            Title = narration.Title,
            Description = narration.Description,
            Impact = narration.Impact,
            GradeCap = gradeCap,
            Metrics = new Dictionary<string, double>
            {
                ["severity"] = result.Severity,
                ["lengthRatio"] = result.WhiteningLengthRatio,
                ["areaRatio"] = result.WhiteningAreaRatio,
                ["meanDeltaE"] = result.MeanDeltaE,
                ["maxDeltaE"] = result.MaxDeltaE,
                ["defectCount"] = result.DefectCount
            }
        };
    }

    public static DetectedDefect? FromGeometry(CornerGeometryResult result, CardSide side, NormalizedRect? region)
    {
        var severity = FromSeverity(result.Severity);
        if (severity == DefectSeverity.None || result.Type == CornerGeometryType.Normal)
        {
            return null;
        }

        var type = result.Type switch
        {
            CornerGeometryType.Chipping or CornerGeometryType.MissingMaterial => DefectType.CornerChipping,
            CornerGeometryType.Rounding or CornerGeometryType.Blunting => DefectType.CornerRounding,
            CornerGeometryType.RoughCut or CornerGeometryType.FrayingCandidate => DefectType.CornerFraying,
            _ => DefectType.CornerRounding
        };

        var gradeCap = DefectScoreMath.CapFromSeverity(severity);
        var narration = DefectNarrator.ForGeometry(side, result, severity, gradeCap);
        return new DetectedDefect
        {
            Type = type,
            Severity = severity,
            Side = side,
            Region = region,
            Confidence = Math.Clamp(result.Confidence / 100.0, 0, 1),
            Title = narration.Title,
            Description = narration.Description,
            Impact = narration.Impact,
            GradeCap = gradeCap,
            Metrics = new Dictionary<string, double>
            {
                ["severity"] = result.Severity,
                ["missingAreaRatio"] = result.MissingAreaRatio,
                ["meanContourDeviation"] = result.MeanContourDeviation,
                ["radiusDeviation"] = result.RadiusDeviation,
                ["roughness"] = result.Roughness
            }
        };
    }

    public static DetectedDefect FromScratch(ScratchCandidate candidate, CardSide side)
    {
        var severity = FromSeverity(candidate.Severity);
        if (severity == DefectSeverity.None)
        {
            severity = DefectSeverity.Trace;
        }

        var gradeCap = DefectScoreMath.CapFromSeverity(severity);
        var narration = DefectNarrator.ForScratch(side, candidate, severity, gradeCap);
        return new DetectedDefect
        {
            Type = candidate.Type == ScratchCandidateType.PrintLineCandidate ? DefectType.PrintLine : DefectType.Scratch,
            Severity = severity,
            Side = side,
            Region = candidate.BoundingBox,
            Confidence = Math.Clamp(candidate.DetectionConfidence, 0, 1),
            Title = narration.Title,
            Description = narration.Description,
            Impact = narration.Impact,
            GradeCap = gradeCap,
            Metrics = new Dictionary<string, double>
            {
                ["severity"] = candidate.Severity,
                ["lengthRatio"] = candidate.LengthRatio,
                ["widthRatio"] = candidate.WidthRatio,
                ["localContrast"] = candidate.LocalContrast,
                ["lightingResponse"] = candidate.LightingResponse,
                ["detectionConfidence"] = candidate.DetectionConfidence
            }
        };
    }
}
