using CortesIA.Ai.Models;
using Microsoft.Extensions.AI;

namespace CortesIA.Ai.Tests.Models;

public class CallBudgetTests
{
    private static readonly ChatMessage[] Hello = [new(ChatRole.User, "Hola")];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Calls_beyond_the_limit_are_refused_without_reaching_the_model()
    {
        var model = FakeChatClient.Saying("Hola");
        var client = new CallBudgetChatClient(model, maxCalls: 2);

        await client.GetResponseAsync(Hello, cancellationToken: Token);
        await client.GetResponseAsync(Hello, cancellationToken: Token);
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetResponseAsync(Hello, cancellationToken: Token));

        Assert.Equal("Límite de 2 llamadas por partida alcanzado.", refused.Message);
        Assert.Equal(2, model.Calls.Count);
    }

    [Fact]
    public void The_estimated_cost_adds_input_and_output_tokens_at_their_prices()
    {
        var settings = new ModelSettings(Provider.OpenAI, "m", InputPricePerMillion: 0.20m, OutputPricePerMillion: 1.20m);
        CallRecord[] calls =
        [
            new(DateTimeOffset.UnixEpoch, "m", 400, 100, 1, "stop", null),
            new(DateTimeOffset.UnixEpoch, "m", 600, 300, 1, "stop", null),
            new(DateTimeOffset.UnixEpoch, "m", null, null, 1, null, "Falló"),
        ];

        // 1,000 input tokens at 0.20 $/M + 400 output tokens at 1.20 $/M.
        Assert.Equal(0.00068m, settings.EstimatedCost(calls));
    }
}
