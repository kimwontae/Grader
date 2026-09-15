using OnePieceCardGrader.Core.Constants;
using OnePieceCardGrader.Core.Models;
using OnePieceCardGrader.Core.Options;

namespace OnePieceCardGrader.Core.Calculations;

public static class CenteringGradeCapCalculator
{
    public static int CalculateCap(CenteringMeasurement? front, CenteringMeasurement? back, GradingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var cap = AppConstants.MaxGrade;
        if (front is not null)
        {
            cap = Math.Min(cap, CapForSide(front.WorstRatio, isFront: true, profile));
        }

        if (back is not null)
        {
            cap = Math.Min(cap, CapForSide(back.WorstRatio, isFront: false, profile));
        }

        return cap;
    }

    public static bool PassesThreshold(double worstRatio, double maxAllowed) =>
        worstRatio <= maxAllowed + 1e-6;

    private static int CapForSide(double worstRatio, bool isFront, GradingProfile profile)
    {
        foreach (var grade in new[] { 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 })
        {
            if (!profile.Centering.TryGetValue(grade.ToString(), out var threshold))
            {
                continue;
            }

            var max = isFront ? threshold.FrontMax : threshold.BackMax;
            if (PassesThreshold(worstRatio, max))
            {
                return grade;
            }
        }

        return 1;
    }
}
