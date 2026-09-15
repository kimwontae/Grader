using OnePieceCardGrader.Core.Interfaces;
using OnePieceCardGrader.Core.Models;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging;

public sealed class FileDebugImageSink : IDebugImageSink
{
    private readonly Dictionary<string, string> _paths = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _directory;
    private int _index;

    public FileDebugImageSink(string directory, bool enabled)
    {
        _directory = directory;
        Enabled = enabled;
        if (enabled)
        {
            Directory.CreateDirectory(directory);
        }
    }

    public bool Enabled { get; }

    public void Save(string name, OpenCvImage image)
    {
        if (!Enabled)
        {
            return;
        }

        _index++;
        var fileName = $"{_index:00}_{name}.jpg";
        var path = Path.Combine(_directory, fileName);
        var mat = MatAdapter.Unwrap(image);
        Cv2.ImWrite(path, mat);
        _paths[name] = path;
    }

    public IReadOnlyDictionary<string, string> SnapshotPaths() =>
        new Dictionary<string, string>(_paths, StringComparer.OrdinalIgnoreCase);
}
