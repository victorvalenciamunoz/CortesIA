using System.ClientModel;
using System.ClientModel.Primitives;
using Anthropic;
using Microsoft.Extensions.AI;
using OpenAI;

namespace CortesIA.Ai.Models;

public static class ChatModels
{
    /// <summary>Name under which calls are reported to OpenTelemetry (the Aspire dashboard listens to it).</summary>
    public const string TelemetrySource = "CortesIA";

    /// <summary>
    /// A chat client for the configured provider, with the project's middleware: every call is recorded, paid models
    /// have a call limit, and calls are traced. Whoever uses it only sees <see cref="IChatClient"/>.
    /// </summary>
    public static IChatClient Create(ModelSettings settings, Action<CallRecord> record)
    {
        var builder = Connect(settings)
            .AsBuilder()
            .Use(inner => new RecordingChatClient(inner, record));
        if (settings.MaxCalls is { } maxCalls)
        {
            builder = builder.Use(inner => new CallBudgetChatClient(inner, maxCalls));
        }

        return builder
            .ConfigureOptions(options =>
            {
                options.MaxOutputTokens ??= settings.MaxOutputTokens;

                // A narration needs little thinking; the cloud models default to more, which costs time and tokens.
                if (settings.Provider != Provider.Local)
                {
                    options.Reasoning ??= new ReasoningOptions { Effort = ReasoningEffort.Low };
                }
            })
            .UseOpenTelemetry(sourceName: TelemetrySource, configure: c => c.EnableSensitiveData = true)
            .Build();
    }

    private static IChatClient Connect(ModelSettings settings) => settings.Provider switch
    {
        // Retries are switched off: the OpenAI library would otherwise try 4 times (about 8 s) before failing,
        // and the game prefers to fail at once and carry on with its fixed text.
        Provider.Local or Provider.OpenAI => new OpenAIClient(
                new ApiKeyCredential(settings.ApiKey ?? "sin-clave"),
                new OpenAIClientOptions { Endpoint = settings.Endpoint, RetryPolicy = new ClientRetryPolicy(maxRetries: 0) })
            .GetChatClient(settings.Model)
            .AsIChatClient(),
        Provider.Claude => new AnthropicClient { ApiKey = settings.ApiKey, MaxRetries = 0, BaseUrl = settings.Endpoint?.ToString() ?? "https://api.anthropic.com" }
            .AsIChatClient(settings.Model),
        _ => throw new ArgumentOutOfRangeException(nameof(settings), settings.Provider, null),
    };
}
