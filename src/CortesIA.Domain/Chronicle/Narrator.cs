using CortesIA.Domain.Courts;
using CortesIA.Domain.Pacts;
using CortesIA.Domain.Resolution;

namespace CortesIA.Domain.Chronicle;

/// <summary>Turns chronicle events, proposals and pacts into plain Spanish sentences.</summary>
public static class Narrator
{
    /// <summary>Events of the diplomacy phase, which the player already sees before choosing an action.</summary>
    public static bool IsDiplomacy(GameEvent gameEvent) => gameEvent is MessageSent or ProposalRejected or PactSigned;

    public static string Tell(GameEvent gameEvent) => gameEvent switch
    {
        Cultivated e => $"{e.Court.Spanish()} cultivó: +{e.Grain} de grano{(e.Gold > 0 ? $" y +{e.Gold} de oro" : "")}.",
        Recruited e => $"{e.Court.Spanish()} reclutó {e.Soldiers} soldados.",
        Fortified e => $"{e.Court.Spanish()} se fortificó.",
        Traded e => $"{e.Court.Spanish()} dio {Amount(e.Deal.GiveAmount, e.Deal.Give)} a {e.Deal.Partner.Spanish()} a cambio de {Amount(e.Deal.AskAmount, e.Deal.Ask)}.",
        TradeFailed e => $"El intercambio de {e.Court.Spanish()} con {e.Deal.Partner.Spanish()} no se hizo: {e.Reason}",
        Fought e => Fight(e),
        MessageSent { Proposal: { } p } e => Describe(p) + (e.Text.Length > 0 ? $" «{e.Text}»" : ""),
        MessageSent e => $"{e.From.Spanish()} → {e.To.Spanish()}: «{e.Text}»",
        ProposalRejected { Reason: null } e => $"{e.Proposal.To.Spanish()} rechazó la propuesta de {e.Proposal.From.Spanish()}.",
        ProposalRejected e => $"La propuesta de {e.Proposal.From.Spanish()} a {e.Proposal.To.Spanish()} no vale: {e.Reason}",
        PlayerFaulted e => $"{e.Court.Spanish()}: {e.Reason}",
        PactSigned e => $"Pacto firmado: {Describe(e.Pact)}.",
        PactFulfilled e => $"Pacto cumplido: {Describe(e.Pact)}. +{Pact.FulfilReward} de prestigio para cada una.",
        PactBroken e => $"{e.Breaker.Spanish()} rompió el pacto de {e.Pact.Kind.Spanish()} con {e.Pact.Other(e.Breaker).Spanish()}: −{Pact.BreakPenalty} de prestigio.",
        PactCancelled e => $"Pacto anulado por eliminación: {Describe(e.Pact)}.",
        SoldiersDeserted e => $"Desertaron {e.Soldiers} soldados de {e.Court.Spanish()} por falta de grano.",
        CourtEliminated e => $"{e.Court.Spanish()} queda eliminada.",
        _ => gameEvent.ToString(),
    };

    public static string Describe(PactProposal proposal)
    {
        var text = $"{proposal.From.Spanish()} propone a {proposal.To.Spanish()} un pacto de {proposal.Kind.Spanish()} de {Turns(proposal.Turns)}";
        return proposal.Terms is { } terms
            ? $"{text}: cada turno {proposal.From.Spanish()} da {Amount(terms.GiveAmount, terms.Give)} y {proposal.To.Spanish()} da {Amount(terms.AskAmount, terms.Ask)}."
            : $"{text}.";
    }

    public static string Describe(Pact pact)
    {
        var text = $"{pact.Kind.Spanish()} entre {pact.Proposer.Spanish()} y {pact.Partner.Spanish()}, turnos {pact.StartTurn} a {pact.EndTurn}";
        return pact.Terms is { } terms
            ? $"{text} ({pact.Proposer.Spanish()} da {Amount(terms.GiveAmount, terms.Give)} y {pact.Partner.Spanish()} da {Amount(terms.AskAmount, terms.Ask)} cada turno)"
            : text;
    }

    public static string Amount(int amount, Resource resource) => $"{amount} de {resource.Spanish()}";

    private static string Turns(int turns) => turns == 1 ? "1 turno" : $"{turns} turnos";

    private static string Fight(Fought e)
    {
        var (attacker, defender) = (e.Attacker.Spanish(), e.Defender.Spanish());
        var forces = $"fuerza {e.AttackerForce} contra {e.DefenderForce}{(e.DefenderFortified ? $", {defender} fortificada" : "")}";
        var head = e.Mutual ? $"{attacker} y {defender} se atacaron a la vez ({forces})" : $"{attacker} atacó a {defender} ({forces})";
        if (e.Winner is not { } winner)
        {
            return $"{head}: empate, las dos pierden soldados.";
        }

        if (!e.Mutual && winner == e.Defender)
        {
            return $"{head}: {defender} resiste.";
        }

        var loot = e.StolenGrain + e.StolenGold > 0
            ? $" y se lleva {Amount(e.StolenGrain, Resource.Grain)} y {Amount(e.StolenGold, Resource.Gold)}"
            : "";
        return $"{head}: gana {winner.Spanish()} (+{Combat.PrestigeForVictory} de prestigio){loot}.";
    }
}
