using CortesIA.Ai.Chronicle;
using CortesIA.Domain.Chronicle;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Pacts;
using Microsoft.Extensions.AI;

namespace CortesIA.Ai.Tests.Chronicle;

public class ChroniclerTests
{
    private static readonly GameEvent[] Turn3 =
    [
        new Fought(3, CourtId.North, CourtId.South, false, false, 5, 3, CourtId.North, 4, 0),
        new MessageSent(3, CourtId.North, CourtId.South, "Secreto entre Norte y Sur", null),
        new PactBroken(3, new Pact(PactKind.NonAggression, CourtId.North, CourtId.South, 2, 3), CourtId.North),
    ];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Tells_the_model_text_after_sending_the_prompt_and_the_facts()
    {
        var model = FakeChatClient.Saying("  El Norte cayó sobre el Sur.  ");

        var narration = await new Chronicler(model).NarrateAsync(3, Turn3, CourtId.Player, Token);

        Assert.Equal(Narration.Told("El Norte cayó sobre el Sur."), narration);
        var messages = Assert.Single(model.Calls);
        Assert.Equal(ChatRole.System, messages[0].Role);
        Assert.Contains("cronista", messages[0].Text);
        Assert.Equal(ChatRole.User, messages[1].Role);
        Assert.StartsWith("Turno 3:\n- Norte atacó a Sur", messages[1].Text);
    }

    [Fact]
    public void The_facts_only_hold_what_the_reader_may_see()
    {
        var facts = Chronicler.Facts(3, Turn3, CourtId.Player);

        Assert.Equal(
            "Turno 3:\n" +
            "- Norte atacó a Sur (fuerza 5 contra 3): gana Norte (+2 de prestigio) y se lleva 4 de grano y 0 de oro.\n" +
            "- Norte rompió el pacto de no agresión con Sur: −3 de prestigio.",
            facts);
    }

    [Fact]
    public async Task With_nothing_to_tell_the_model_is_not_called()
    {
        var model = FakeChatClient.Saying("No debería llamarse.");

        var narration = await new Chronicler(model).NarrateAsync(3, [Turn3[1]], CourtId.Player, Token);

        Assert.Null(narration.Text);
        Assert.Empty(model.Calls);
    }

    [Fact]
    public async Task A_failing_model_becomes_a_failed_narration()
    {
        var model = FakeChatClient.Failing(new HttpRequestException("No se puede establecer una conexión.\nDetalles largos"));

        var narration = await new Chronicler(model).NarrateAsync(3, Turn3, CourtId.Player, Token);

        Assert.Equal(Narration.Failed("No se puede establecer una conexión."), narration);
    }

    [Fact]
    public async Task A_model_that_hangs_is_cut_off_by_the_time_limit()
    {
        var narration = await new Chronicler(FakeChatClient.Hanging(), TimeSpan.FromMilliseconds(50)).NarrateAsync(3, Turn3, CourtId.Player, Token);

        Assert.Equal(Narration.Failed("Tardó más de 0 s."), narration);
    }

    [Fact]
    public async Task A_blank_answer_is_a_failure()
    {
        var narration = await new Chronicler(FakeChatClient.Saying("   ")).NarrateAsync(3, Turn3, CourtId.Player, Token);

        Assert.Equal(Narration.Failed("Respondió en blanco."), narration);
    }
}
