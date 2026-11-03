using CortesIA.Domain.Actions;
using CortesIA.Domain.Chronicle;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Game;
using CortesIA.Domain.Pacts;
using CortesIA.Domain.Players;
using CortesIA.Domain.Resolution;
using Spectre.Console;

namespace CortesIA.Console;

/// <summary>
/// A person at the keyboard, seeing only their <see cref="CourtView"/>. The menus only offer what the rules allow,
/// but the match still validates every answer, as it will for a model.
/// </summary>
public sealed class ConsolePlayer(IAnsiConsole console) : ICourtPlayer
{
    // Asking for more than this is never useful: no court gets near it in 15 turns.
    private const int MaxAsk = 30;

    private enum Diplomacy
    {
        Done,
        Pact,
        Text,
    }

    public Task<IReadOnlyList<Message>> WriteMessagesAsync(CourtView view, CancellationToken cancellationToken)
    {
        var messages = new List<Message>();
        while (true)
        {
            var recipients = view.Others
                .Where(c => !c.IsEliminated && messages.Count(m => m.To == c.Id) < Match.MaxMessagesPerRecipient)
                .Select(c => c.Id)
                .ToList();
            if (recipients.Count == 0)
            {
                break;
            }

            var choice = Choose("[bold]Diplomacia[/] (se envía todo al pasar a las acciones)",
            [
                (messages.Count == 0 ? "Pasar a las acciones" : "Enviar y pasar a las acciones", Diplomacy.Done),
                ("Proponer un pacto", Diplomacy.Pact),
                ("Escribir un mensaje (los rivales de esta fase no leen el texto)", Diplomacy.Text),
            ]);
            if (choice == Diplomacy.Done)
            {
                break;
            }

            var to = Choose("¿A quién?", recipients.Select(id => (id.Spanish(), id)));
            var message = choice == Diplomacy.Pact
                ? ProposePact(view, to)
                : new Message(to, console.Prompt(new TextPrompt<string>("Mensaje:")));
            messages.Add(message);
            console.MarkupLine($"[grey]Preparado: {Markup.Escape(message.Proposal is { } p ? Narrator.Describe(p) : $"«{message.Text}»")}[/]");
        }

        return Task.FromResult<IReadOnlyList<Message>>(messages);
    }

    public Task<bool> AnswerProposalAsync(CourtView view, PactProposal proposal, CancellationToken cancellationToken)
    {
        console.MarkupLine($"[yellow]{Markup.Escape(Narrator.Describe(proposal))}[/]");
        if (proposal.Terms is { } terms)
        {
            console.MarkupLine($"[grey]Si aceptas, cada turno tendrás que elegir Comerciar con {proposal.From.Spanish()} " +
                $"dando {Narrator.Amount(terms.AskAmount, terms.Ask)}, o romperás el pacto (−{Pact.BreakPenalty} de prestigio).[/]");
        }

        return Task.FromResult(console.Prompt(new ConfirmationPrompt("¿Aceptas?") { Yes = 's', No = 'n', DefaultValue = false }));
    }

    public Task<GameAction> ChooseActionAsync(CourtView view, CancellationToken cancellationToken)
    {
        Screen.ShowEvents(console, "Diplomacia de este turno", view.Chronicle.Where(e => e.Turn == view.Turn));

        var options = new List<(string Label, Func<GameAction> Build)>();
        foreach (var pact in view.Pacts.Where(p => p.Kind == PactKind.RecurringTrade))
        {
            var required = pact.RequiredTrade(view.Self.Id);
            var affordable = view.Self.Amount(required.Give) >= required.GiveAmount;
            console.MarkupLine(affordable
                ? $"[yellow]Pacto de comercio con {required.Partner.Spanish()}: si no eliges su intercambio, lo rompes (−{Pact.BreakPenalty}).[/]"
                : $"[red]No puedes pagar el comercio pactado con {required.Partner.Spanish()}: el pacto se romperá.[/]");
            if (affordable)
            {
                options.Add(($"Comerciar con {required.Partner.Spanish()} según el pacto: das {Narrator.Amount(required.GiveAmount, required.Give)}, " +
                    $"recibes {Narrator.Amount(required.AskAmount, required.Ask)}", () => required));
            }
        }

        var legal = view.Legal;
        options.Add(($"Cultivar: +{TurnResolver.CultivateGrain} de grano", () => new Cultivate()));
        if (legal.CanRecruit)
        {
            options.Add(($"Reclutar: −{ActionValidator.RecruitCost} de oro, +{TurnResolver.RecruitSoldiers} soldados", () => new Recruit()));
        }

        options.Add(("Fortificar: tu defensa cuenta doble este turno", () => new Fortify()));
        foreach (var target in legal.AttackTargets)
        {
            var soldiers = view.Others.First(c => c.Id == target).Soldiers;
            var breaks = view.Pacts.Any(p => p.Kind == PactKind.NonAggression && p.Binds(view.Self.Id, target))
                ? $" (rompe la no agresión: −{Pact.BreakPenalty} de prestigio)"
                : "";
            options.Add(($"Atacar a {target.Spanish()}: tus {view.Self.Soldiers} soldados contra sus {soldiers}{breaks}", () => new Attack(target)));
        }

        foreach (var partner in legal.TradePartners)
        {
            options.Add(($"Comerciar con {partner.Spanish()} (solo sale si elige el intercambio en espejo)", () => AskTrade(view, partner)));
        }

        return Task.FromResult(Choose("[bold]Tu acción[/] (secreta: las tres cortes eligen a la vez)", options)());
    }

    private Message ProposePact(CourtView view, CourtId to)
    {
        var kinds = new List<(string, PactKind)> { ("No agresión: ninguna ataca a la otra", PactKind.NonAggression) };
        if (view.Legal.MaxGrainToGive + view.Legal.MaxGoldToGive >= 1)
        {
            kinds.Add(("Comercio recurrente: el mismo intercambio cada turno (con 1 turno es un intercambio puntual)", PactKind.RecurringTrade));
        }

        var kind = Choose("¿Qué pacto?", kinds);
        var remaining = GameState.LastTurn - view.Turn + 1;
        var turns = AskNumber($"¿Cuántos turnos? Empieza en este (1 a {remaining}):", 1, remaining);
        var terms = kind == PactKind.RecurringTrade ? AskTrade(view, to) : null;
        return new Message(to, "", new PactProposal(kind, view.Self.Id, to, turns, terms));
    }

    private Trade AskTrade(CourtView view, CourtId partner)
    {
        var give = Choose("¿Qué das?", Enum.GetValues<Resource>()
            .Where(r => view.Self.Amount(r) >= 1)
            .Select(r => ($"{r.Spanish()} (tienes {view.Self.Amount(r)})", r)));
        var giveAmount = AskNumber($"¿Cuánto {give.Spanish()} das? (1 a {view.Self.Amount(give)})", 1, view.Self.Amount(give));

        var ask = give == Resource.Grain ? Resource.Gold : Resource.Grain;
        var partnerHas = view.Others.First(c => c.Id == partner).Amount(ask);
        var askAmount = AskNumber($"¿Cuánto {ask.Spanish()} pides a cambio? ({partner.Spanish()} tiene {partnerHas})", 1, MaxAsk);
        return new Trade(partner, give, giveAmount, ask, askAmount);
    }

    private T Choose<T>(string title, IEnumerable<(string Label, T Value)> options)
    {
        var list = options.ToList();
        var index = console.Prompt(new SelectionPrompt<int>()
            .Title(title)
            .AddChoices(Enumerable.Range(0, list.Count))
            .UseConverter(i => Markup.Escape(list[i].Label)));
        return list[index].Value;
    }

    private int AskNumber(string question, int min, int max) => console.Prompt(new TextPrompt<int>(Markup.Escape(question))
        .Validate(n => n >= min && n <= max
            ? Spectre.Console.ValidationResult.Success()
            : Spectre.Console.ValidationResult.Error($"[red]Tiene que estar entre {min} y {max}.[/]")));
}
