using OnePieceCardGrader.Core.Enums;

namespace OnePieceCardGrader.Core.Models;

public sealed class CardInput
{
    public string? CardName { get; init; }
    public string? CardNumber { get; init; }
    public string? SetName { get; init; }
    public string? Language { get; init; }
    public string? Rarity { get; init; }
    public string? Memo { get; init; }

    public string? FrontImagePath { get; init; }
    public string? BackImagePath { get; init; }

    public QuadCorners? FrontManualCorners { get; init; }
    public QuadCorners? BackManualCorners { get; init; }
    public CenteringGuide? FrontCenteringGuide { get; init; }
    public CenteringGuide? BackCenteringGuide { get; init; }

    public IReadOnlyDictionary<ImageSlotKind, string> AdditionalImages { get; init; } =
        new Dictionary<ImageSlotKind, string>();

    public IReadOnlyDictionary<ImageSlotKind, QuadCorners> AdditionalImageCorners { get; init; } =
        new Dictionary<ImageSlotKind, QuadCorners>();
}
