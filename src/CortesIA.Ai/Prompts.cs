namespace CortesIA.Ai;

/// <summary>Prompts live in <c>*.prompt.md</c> files next to the code that uses them, embedded in the assembly.</summary>
public static class Prompts
{
    public static string Load(string fileName)
    {
        using var stream = typeof(Prompts).Assembly.GetManifestResourceStream(fileName)
            ?? throw new InvalidOperationException($"No prompt named {fileName}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }
}
