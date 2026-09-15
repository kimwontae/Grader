using OpenCvSharp;

namespace OnePieceCardGrader.Imaging;

public interface IAnalysisDebugSink
{
    bool Enabled { get; }
    void Save(string name, Mat image);
    IReadOnlyDictionary<string, string> SnapshotPaths();
}

public sealed class NullAnalysisDebugSink : IAnalysisDebugSink
{
    public static readonly NullAnalysisDebugSink Instance = new();
    public bool Enabled => false;
    public void Save(string name, Mat image) { }
    public IReadOnlyDictionary<string, string> SnapshotPaths() => new Dictionary<string, string>();
}

public sealed class DirectoryDebugSink : IAnalysisDebugSink
{
    private readonly Dictionary<string, string> _paths = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _directory;

    public DirectoryDebugSink(string directory, bool enabled)
    {
        _directory = directory;
        Enabled = enabled;
        if (enabled)
        {
            Directory.CreateDirectory(directory);
        }
    }

    public bool Enabled { get; }

    public void Save(string name, Mat image)
    {
        if (!Enabled || image.Empty())
        {
            return;
        }

        var relative = name.Replace('/', Path.DirectorySeparatorChar);
        if (!Path.HasExtension(relative))
        {
            relative += ".png";
        }

        var path = Path.Combine(_directory, relative);
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        Cv2.ImWrite(path, image);
        _paths[name] = path;
    }

    public IReadOnlyDictionary<string, string> SnapshotPaths() =>
        new Dictionary<string, string>(_paths, StringComparer.OrdinalIgnoreCase);

    public IAnalysisDebugSink Scope(string prefix) => new PrefixedAnalysisDebugSink(this, prefix);
}

public sealed class PrefixedAnalysisDebugSink : IAnalysisDebugSink
{
    private readonly IAnalysisDebugSink _inner;
    private readonly string _prefix;

    public PrefixedAnalysisDebugSink(IAnalysisDebugSink inner, string prefix)
    {
        _inner = inner;
        _prefix = prefix.TrimEnd('/', '\\') + "/";
    }

    public bool Enabled => _inner.Enabled;

    public void Save(string name, Mat image) => _inner.Save(_prefix + name, image);

    public IReadOnlyDictionary<string, string> SnapshotPaths() => _inner.SnapshotPaths();
}

public sealed class CompositeDebugSink : IAnalysisDebugSink
{
    private readonly IAnalysisDebugSink[] _sinks;

    public CompositeDebugSink(params IAnalysisDebugSink[] sinks)
    {
        _sinks = sinks;
        Enabled = sinks.Any(s => s.Enabled);
    }

    public bool Enabled { get; }

    public void Save(string name, Mat image)
    {
        foreach (var sink in _sinks)
        {
            sink.Save(name, image);
        }
    }

    public IReadOnlyDictionary<string, string> SnapshotPaths()
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sink in _sinks)
        {
            foreach (var pair in sink.SnapshotPaths())
            {
                merged[pair.Key] = pair.Value;
            }
        }

        return merged;
    }
}
