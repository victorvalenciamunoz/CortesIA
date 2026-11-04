using CortesIA.Ai.Chronicle;
using CortesIA.Domain.Actions;
using CortesIA.Domain.Chronicle;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Game;
using CortesIA.Domain.Pacts;
using CortesIA.Domain.Resolution;
using Spectre.Console;

namespace CortesIA.Console;

/// <summary>Draws the state of the game. When playing, only what your court may know: the public resources and your pacts.</summary>
public static class Screen
{
    /// <summary>The rules in one screen, built from the domain's constants so it cannot drift from them.</summary>
    public static void ShowIntro(IAnsiConsole console)
    {
        var goal = new Markup(
            $"[bold]Objetivo:[/] acabar el turno {GameState.LastTurn} con más [bold]prestigio[/] que las otras dos cortes.\n" +
            $"  • Ganar un ataque: +{Combat.PrestigeForVictory}.\n" +
            $"  • Cumplir un pacto hasta el final: +{Pact.FulfilReward} para cada una.\n" +
            $"  • Romper un pacto: −{Pact.BreakPenalty}, y se entera todo el mundo.\n\n" +
            "[bold]Recursos:[/]\n" +
            "  • Soldados: para atacar y defenderte.\n" +
            "  • Grano: da de comer a los soldados (cada 2 comen 1 por turno). Si falta, desertan. Sin soldados y sin grano, quedas eliminado.\n" +
            "  • Oro: para reclutar.\n\n" +
            "[bold]Cada turno:[/] primero hablas (proponer pactos, opcional); luego eliges una acción, a ciegas y a la vez que las otras.");

        var actions = new Table().Border(TableBorder.Simple)
            .AddColumn("Acción")
            .AddColumn("Qué hace")
            .AddRow("Cultivar", $"+{TurnResolver.CultivateGrain} de grano")
            .AddRow("Reclutar", $"gastas {ActionValidator.RecruitCost} de oro, ganas {TurnResolver.RecruitSoldiers} soldados")
            .AddRow("Fortificar", "si te atacan este turno, tus soldados cuentan doble")
            .AddRow("Atacar", $"gana quien tenga más soldados (±1 de suerte; si empatan, el defensor). Si ganas, le robas la mitad de su grano y oro y +{Combat.PrestigeForVictory} de prestigio")
            .AddRow("Comerciar", "solo sale si el otro elige justo el intercambio contrario el mismo turno");

        var hint = new Markup(
            "[grey]Norte juega así: si le falta grano, cultiva; si le saca 2 soldados a alguien, le ataca; si no, recluta. " +
            "Sur juega al azar. En esta fase los rivales no leen el texto de los mensajes.[/]");

        console.Write(new Panel(new Rows(goal, actions, hint)).Header("Cómo se juega").Border(BoxBorder.Rounded).Expand());
    }

    public static void ShowTurn(IAnsiConsole console, GameState state, CourtId? you)
    {
        console.WriteLine();
        console.Write(new Rule($"[bold]Turno {state.Turn} de {GameState.LastTurn}[/]").LeftJustified());

        var table = new Table().Border(TableBorder.Rounded)
            .AddColumn("Corte")
            .AddColumn(new TableColumn("Grano").RightAligned())
            .AddColumn(new TableColumn("Oro").RightAligned())
            .AddColumn(new TableColumn("Soldados").RightAligned())
            .AddColumn(new TableColumn("Prestigio").RightAligned())
            .AddColumn(new TableColumn("Comen por turno").RightAligned());

        foreach (var court in state.Courts.Values.OrderBy(c => c.Id))
        {
            var name = court.Id == you ? $"[bold]{court.Id.Spanish()} (tú)[/]" : court.Id.Spanish();
            if (court.IsEliminated)
            {
                table.AddRow($"[strikethrough grey]{court.Id.Spanish()}[/]", "", "", "", court.Prestige.ToString(), "[grey]eliminada[/]");
                continue;
            }

            table.AddRow(
                name,
                court.Grain.ToString(),
                court.Gold.ToString(),
                court.Soldiers.ToString(),
                court.Prestige.ToString(),
                $"{TurnResolver.UpkeepCost(court.Soldiers)} de grano");
        }

        console.Write(table);

        var pacts = state.Pacts.Where(p => you is not { } viewer || p.Parties.Contains(viewer)).ToList();
        console.MarkupLine(pacts.Count == 0 ? "[grey]Sin pactos en vigor.[/]" : "[bold]Pactos en vigor:[/]");
        foreach (var pact in pacts)
        {
            console.MarkupLine($"  • {Markup.Escape(Narrator.Describe(pact))}");
        }
    }

    public static void ShowEvents(IAnsiConsole console, string title, IEnumerable<GameEvent> events)
    {
        var lines = events.Select(Narrator.Tell).ToList();
        if (lines.Count == 0)
        {
            return;
        }

        console.MarkupLine($"[bold]{Markup.Escape(title)}[/]");
        foreach (var line in lines)
        {
            console.MarkupLine($"  • {Markup.Escape(line)}");
        }
    }

    /// <summary>The model's retelling under the facts, or a grey line saying why there is none (it failed or was discarded).</summary>
    public static void ShowChronicle(IAnsiConsole console, Narration narration)
    {
        if (narration.Text is { } text)
        {
            console.Write(new Panel(new Markup($"[italic]{Markup.Escape(text)}[/]")).Header("Crónica").Border(BoxBorder.Rounded));
            return;
        }

        console.MarkupLine($"[grey]Sin crónica: {Markup.Escape(narration.Failure ?? "")}[/]");
    }

    public static void ShowStandings(IAnsiConsole console, GameState state)
    {
        var standings = Standings.Of(state);
        console.WriteLine();
        console.Write(new Rule("[bold]Fin de la partida[/]").LeftJustified());

        var table = new Table().Border(TableBorder.Rounded)
            .AddColumn("Puesto")
            .AddColumn("Corte")
            .AddColumn(new TableColumn("Prestigio").RightAligned())
            .AddColumn(new TableColumn("Grano + oro + soldados").RightAligned());
        foreach (var (court, place) in standings.Ranking.Select((c, i) => (c, i + 1)))
        {
            table.AddRow(
                place.ToString(),
                court.Id.Spanish() + (court.IsEliminated ? " (eliminada)" : ""),
                court.Prestige.ToString(),
                Standings.Tiebreak(court).ToString());
        }

        console.Write(table);
        console.MarkupLine(standings.Winners.Count switch
        {
            0 => "No gana nadie: todas las cortes quedaron eliminadas.",
            1 => $"[bold]Gana {standings.Winners[0].Spanish()}.[/]",
            _ => $"[bold]Victoria compartida: {string.Join(" y ", standings.Winners.Select(w => w.Spanish()))}.[/]",
        });
    }
}
