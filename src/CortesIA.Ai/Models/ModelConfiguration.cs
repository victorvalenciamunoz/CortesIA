using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace CortesIA.Ai.Models;

/// <summary>
/// Reads each model's settings from <c>models.json</c> (model, address, limits, prices) and its key from user-secrets.
/// Shared by every program that calls a model, so the console and the evals use the same models.
/// </summary>
public static class ModelConfiguration
{
    /// <summary>The user-secrets store, the one set with <c>dotnet user-secrets ... --project src/CortesIA.Console</c>.</summary>
    public const string SecretsId = "cortesia-console";

    public static IConfiguration Load() => new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("models.json")
        .AddUserSecrets(SecretsId)
        .Build();

    public static ModelSettings For(IConfiguration configuration, Provider provider)
    {
        var section = configuration.GetSection($"Cronista:{provider}");
        return new ModelSettings(
            provider,
            section["Model"] ?? throw new InvalidOperationException($"Falta Cronista:{provider}:Model en models.json."),
            section["Endpoint"] is { } endpoint ? new Uri(endpoint) : null,
            section["ApiKey"],
            Number(section["MaxCalls"]) is { } maxCalls ? (int)maxCalls : null,
            (int)(Number(section["MaxOutputTokens"]) ?? 200),
            Number(section["InputPricePerMillion"]) ?? 0,
            Number(section["OutputPricePerMillion"]) ?? 0);
    }

    /// <summary>The command that stores the key, to show when it is missing.</summary>
    public static string KeyCommand(Provider provider) =>
        $"dotnet user-secrets set \"Cronista:{provider}:ApiKey\" \"<tu clave>\" --project src/CortesIA.Console";

    private static decimal? Number(string? text) =>
        text is null ? null : decimal.Parse(text, CultureInfo.InvariantCulture);
}
