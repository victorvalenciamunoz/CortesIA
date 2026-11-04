using System.Text.Encodings.Web;
using System.Text.Json;

namespace CortesIA.Ai.Models;

/// <summary>Appends each call as one JSON line to a file, so later evals can read them back.</summary>
public sealed class CallLog(string path)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly Lock gate = new();

    public string Path { get; } = path;

    public void Append(CallRecord call)
    {
        var line = JsonSerializer.Serialize(call, Json);
        lock (gate)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!);
            File.AppendAllText(Path, line + Environment.NewLine);
        }
    }
}
