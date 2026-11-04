namespace CortesIA.Ai.Models;

public enum Provider
{
    /// <summary>Foundry Local on this machine: free, no key.</summary>
    Local,

    /// <summary>OpenAI's API, through the same OpenAI adapter as Foundry Local.</summary>
    OpenAI,

    /// <summary>Anthropic's API, through the official Anthropic library.</summary>
    Claude,
}

/// <summary>Which model to call and how. Read from configuration; the key never lives in git.</summary>
/// <param name="Endpoint">Only for the OpenAI adapter (Foundry Local, OpenAI); the Anthropic library knows its own.</param>
/// <param name="MaxCalls">Calls allowed per run; null means no limit (the local model costs nothing).</param>
/// <param name="MaxOutputTokens">
/// Cloud models think before answering and that thinking counts as output, so they need far more room than the
/// local model, which only writes the narration.
/// </param>
public sealed record ModelSettings(
    Provider Provider,
    string Model,
    Uri? Endpoint = null,
    string? ApiKey = null,
    int? MaxCalls = null,
    int MaxOutputTokens = 200,
    decimal InputPricePerMillion = 0,
    decimal OutputPricePerMillion = 0)
{
    public static readonly ModelSettings Local = new(Provider.Local, "qwen2.5-1.5b-instruct-openvino-gpu", new Uri("http://127.0.0.1:54311/v1"));

    public bool NeedsKey => Provider != Provider.Local;

    /// <summary>Estimated cost in dollars of the recorded calls, from the per-million-token prices.</summary>
    public decimal EstimatedCost(IEnumerable<CallRecord> calls) => calls.Sum(call =>
        (call.InputTokens ?? 0) * InputPricePerMillion / 1_000_000m + (call.OutputTokens ?? 0) * OutputPricePerMillion / 1_000_000m);
}
