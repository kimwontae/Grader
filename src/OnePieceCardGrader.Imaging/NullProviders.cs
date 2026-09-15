using OnePieceCardGrader.Core.Interfaces;
using OnePieceCardGrader.Core.Models;

namespace OnePieceCardGrader.Imaging;

public sealed class NullCardTemplateProvider : ICardTemplateProvider
{
    public Task<CardTemplate?> GetTemplateAsync(string cardNumber, string language) =>
        Task.FromResult<CardTemplate?>(null);
}

public sealed class NullDefectDetectionModel : IDefectDetectionModel
{
    public Task<IReadOnlyList<DetectedDefect>> DetectAsync(OpenCvImage image) =>
        Task.FromResult<IReadOnlyList<DetectedDefect>>([]);
}
