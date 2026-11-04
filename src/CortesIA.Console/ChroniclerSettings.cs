using System.Globalization;
using CortesIA.Ai.Models;
using Microsoft.Extensions.Configuration;

namespace CortesIA.Console;

/// <summary>Reads the chronicler's model from appsettings.json (model, address, limits, prices) and user-secrets (key).</summary>
public static class ChroniclerSettings
{
    public static IConfiguration Load() => new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json")
        .AddUserSecrets(typeof(ChroniclerSettings).Assembly)
        .Build();

    public static ModelSettings For(IConfiguration configuration, Provider provider)
    {
        var section = configuration.GetSection($"Cronista:{provider}");
        return new ModelSettings(
            provider,
            section["Model"] ?? throw new InvalidOperationException($"Falta Cronista:{provider}:Model en appsettings.json."),
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
