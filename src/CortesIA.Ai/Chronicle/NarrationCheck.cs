using System.Text.RegularExpressions;

namespace CortesIA.Ai.Chronicle;

/// <summary>
/// The model proposes, the code disposes: a narration is only shown if it passes these checks.
/// They are deliberately simple; they catch the failures seen with the small local model, not every possible one.
/// </summary>
public static partial class NarrationCheck
{
    /// <summary>Why the narration must be discarded, or null if it can be shown.</summary>
    public static string? Problem(string narration, string facts)
    {
        if (Markdown().IsMatch(narration))
        {
            return "Usó formato (negritas, listas o títulos).";
        }

        // Every figure must come from the facts: a number the model made up is a fact the game never had.
        var known = Number().Matches(facts).Select(m => m.Value).ToHashSet();
        var invented = Number().Matches(narration).Select(m => m.Value).Where(n => !known.Contains(n)).Distinct().ToList();
        return invented.Count > 0 ? $"Se inventó cifras ({string.Join(", ", invented)})." : null;
    }

    // Bold, or a line that starts like a list item or a heading.
    [GeneratedRegex(@"\*\*|^\s*([-*#]|\d+\.)\s", RegexOptions.Multiline)]
    private static partial Regex Markdown();

    [GeneratedRegex(@"\d+")]
    private static partial Regex Number();
}
