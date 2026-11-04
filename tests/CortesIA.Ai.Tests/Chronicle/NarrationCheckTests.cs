using CortesIA.Ai.Chronicle;

namespace CortesIA.Ai.Tests.Chronicle;

public class NarrationCheckTests
{
    private const string Facts =
        "Turno 15:\n" +
        "- Norte cultivó: +3 de grano.\n" +
        "- Jugador atacó a Sur (fuerza 4 contra 0): gana Jugador (+2 de prestigio) y se lleva 3 de grano y 1 de oro.";

    [Fact]
    public void A_plain_narration_with_known_figures_passes()
    {
        var narration = "En el turno 15 Norte sembró sus campos, y Jugador cayó sobre Sur, cuatro contra nadie, llevándose 3 de grano.";

        Assert.Null(NarrationCheck.Problem(narration, Facts));
    }

    // Real answers from qwen2.5-1.5b during a game.
    [Theory]
    [InlineData("**Crónica del Turno 15:**\n\nNorte cultivó.")]
    [InlineData("Esto pasó:\n- Norte cultivó +3 de grano.")]
    [InlineData("# Turno 15\nNorte cultivó.")]
    [InlineData("1. Norte cultivó.\n2. Jugador atacó.")]
    public void Markdown_is_discarded(string narration)
    {
        Assert.Equal("Usó formato (negritas, listas o títulos).", NarrationCheck.Problem(narration, Facts));
    }

    [Fact]
    public void Figures_that_are_not_in_the_facts_are_discarded()
    {
        var narration = "Jugador atacó a Sur con fuerza 0 contra 1 y entregó 7 de oro a Norte.";

        Assert.Equal("Se inventó cifras (7).", NarrationCheck.Problem(narration, Facts));
    }

    [Fact]
    public void A_dash_inside_a_sentence_is_not_a_list()
    {
        Assert.Null(NarrationCheck.Problem("Norte cultivó - y Jugador venció a Sur.", Facts));
    }
}
