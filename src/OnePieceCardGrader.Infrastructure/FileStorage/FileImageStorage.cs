using OnePieceCardGrader.Core.Interfaces;

namespace OnePieceCardGrader.Infrastructure.FileStorage;

public sealed class FileImageStorage : IImageStorage
{
    public FileImageStorage()
    {
        AppPaths.EnsureCreated();
        RootPath = AppPaths.ImagesRoot;
    }

    public string RootPath { get; }

    public string GetAnalysisDirectory(Guid analysisId)
    {
        var directory = Path.Combine(RootPath, analysisId.ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    public async Task<string> SaveOriginalAsync(Guid analysisId, string slotName, string sourcePath, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".jpg";
        }

        var destination = Path.Combine(GetAnalysisDirectory(analysisId), slotName + extension);
        await using var source = File.OpenRead(sourcePath);
        await using var target = File.Create(destination);
        await source.CopyToAsync(target, cancellationToken);
        return destination;
    }

    public async Task<string> SaveProcessedAsync(Guid analysisId, string fileName, byte[] data, CancellationToken cancellationToken)
    {
        var destination = Path.Combine(GetAnalysisDirectory(analysisId), fileName);
        await File.WriteAllBytesAsync(destination, data, cancellationToken);
        return destination;
    }
}
