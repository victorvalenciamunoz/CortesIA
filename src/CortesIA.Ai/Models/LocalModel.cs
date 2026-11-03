using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using OpenAI;

namespace CortesIA.Ai.Models;

/// <summary>Where the local model lives. Foundry Local speaks the OpenAI API and needs no key.</summary>
public sealed record LocalModelSettings(Uri Endpoint, string Model)
{
    public static readonly LocalModelSettings Default = new(new Uri("http://127.0.0.1:54311/v1"), "qwen2.5-1.5b-instruct-openvino-gpu");
}

public static class LocalModel
{
    /// <summary>Name under which calls are reported to OpenTelemetry (the Aspire dashboard listens to it).</summary>
    public const string TelemetrySource = "CortesIA";

    /// <summary>
    /// A chat client for the local model with the project's middleware: every call is recorded and traced.
    /// Retries are switched off: the OpenAI library would otherwise try 4 times (about 8 s) before failing,
    /// and the game prefers to fail at once and carry on with its fixed text.
    /// </summary>
    public static IChatClient Create(LocalModelSettings settings, Action<CallRecord> record)
    {
        var options = new OpenAIClientOptions { Endpoint = settings.Endpoint, RetryPolicy = new ClientRetryPolicy(maxRetries: 0) };
        return new OpenAIClient(new ApiKeyCredential("sin-clave"), options)
            .GetChatClient(settings.Model)
            .AsIChatClient()
            .AsBuilder()
            .Use(inner => new RecordingChatClient(inner, record))
            .UseOpenTelemetry(sourceName: TelemetrySource, configure: c => c.EnableSensitiveData = true)
            .Build();
    }
}
