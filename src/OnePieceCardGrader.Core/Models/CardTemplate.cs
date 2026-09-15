namespace OnePieceCardGrader.Core.Models;

public sealed class CardTemplate
{
    public string CardNumber { get; init; } = string.Empty;
    public string Language { get; init; } = string.Empty;
    public string? ReferenceFrontPath { get; init; }
    public string? ReferenceBackPath { get; init; }
    public CenteringGuide? ExpectedFrontFrame { get; init; }
    public CenteringGuide? ExpectedBackFrame { get; init; }
    public IReadOnlyList<NormalizedRect> MaskRegions { get; init; } = [];
    public IReadOnlyList<NormalizedRect> FoilRegions { get; init; } = [];
    public IReadOnlyList<NormalizedRect> TextRegions { get; init; } = [];
    public IReadOnlyList<NormalizedRect> ArtworkRegions { get; init; } = [];
}
