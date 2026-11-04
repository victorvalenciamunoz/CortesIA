using System.Text.Encodings.Web;
using System.Text.Json;

namespace CortesIA.Ai.Models;

/// <summary>Appends each entry (a call, a narration…) as one JSON line to a file, so later evals can read them back.</summary>
public sealed class JsonLinesLog(string path)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly Lock gate = new();

    public string Path { get; } = path;

    public void Append<T>(T entry)
    {
        var line = JsonSerializer.Serialize(entry, Json);
        lock (gate)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!);
            File.AppendAllText(Path, line + Environment.NewLine);
        }
    }
}
