using CortesIA.Ai.Models;
using Microsoft.Extensions.AI;

namespace CortesIA.Ai.Tests.Models;

public class RecordingChatClientTests
{
    private static readonly ChatMessage[] Hello = [new(ChatRole.User, "Hola")];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_good_call_is_recorded_with_model_tokens_and_finish_reason()
    {
        var records = new List<CallRecord>();
        var client = new RecordingChatClient(FakeChatClient.Saying("Hola", inputTokens: 68, outputTokens: 59), records.Add);

        await client.GetResponseAsync(Hello, cancellationToken: Token);

        var call = Assert.Single(records);
        Assert.True(call.Succeeded);
        Assert.Equal(("modelo-falso", 68, 59, "stop"), (call.Model, call.InputTokens, call.OutputTokens, call.FinishReason));
    }

    [Fact]
    public async Task A_failed_call_is_recorded_and_the_error_still_reaches_the_caller()
    {
        var records = new List<CallRecord>();
        var client = new RecordingChatClient(FakeChatClient.Failing(new HttpRequestException("Servidor apagado")), records.Add);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetResponseAsync(Hello, cancellationToken: Token));

        var call = Assert.Single(records);
        Assert.False(call.Succeeded);
        Assert.Equal("HttpRequestException: Servidor apagado", call.Error);
    }

    [Fact]
    public void The_call_log_writes_one_json_line_per_call()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cortesia-{Guid.NewGuid():N}", "llamadas.jsonl");
        var log = new CallLog(path);

        log.Append(new CallRecord(DateTimeOffset.UnixEpoch, "m", 1, 2, 0.5, "stop", null));
        log.Append(new CallRecord(DateTimeOffset.UnixEpoch, "m", null, null, 0.1, null, "Falló"));

        var lines = File.ReadAllLines(path);
        Assert.Equal(2, lines.Length);
        Assert.Contains("\"inputTokens\":1", lines[0]);
        Assert.Contains("\"error\":\"Falló\"", lines[1]);
        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
    }
}
