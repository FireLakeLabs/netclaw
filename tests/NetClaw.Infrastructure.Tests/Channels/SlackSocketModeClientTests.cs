using System.Net;
using System.Net.Http;
using System.Text;
using NetClaw.Infrastructure.Channels;
using NetClaw.Infrastructure.Configuration;

namespace NetClaw.Infrastructure.Tests.Channels;

public sealed class SlackSocketModeClientTests
{
    [Fact]
    public async Task AuthTestAsync_ParsesSlackEnvelope()
    {
        StubHttpMessageHandler handler = new(
            """
            {
              "ok": true,
              "url": "ignored",
              "user_id": "U-BOT"
            }
            """);
        HttpClient httpClient = new(handler);
        SlackSocketModeClient client = new(
            new SlackChannelOptions
            {
                Enabled = true,
                BotToken = "xoxb-test",
                AppToken = "xapp-test"
            },
            httpClient);

        SlackAuthInfo auth = await client.AuthTestAsync();

        Assert.Equal("U-BOT", auth.UserId);
        Assert.Equal("https://slack.com/api/auth.test", handler.RequestUri);
    }

    [Fact]
    public async Task GetConversationInfoAsync_ParsesConversationPayload()
    {
        StubHttpMessageHandler handler = new(
            """
            {
              "ok": true,
              "channel": {
                "id": "C12345",
                "name": "general",
                "is_im": false
              }
            }
            """);
        HttpClient httpClient = new(handler);
        SlackSocketModeClient client = new(
            new SlackChannelOptions
            {
                Enabled = true,
                BotToken = "xoxb-test",
                AppToken = "xapp-test"
            },
            httpClient);

        SlackConversationInfo info = await client.GetConversationInfoAsync("C12345");

        Assert.Equal("C12345", info.ConversationId);
        Assert.Equal("general", info.Name);
        Assert.True(info.IsGroup);
        Assert.Equal("https://slack.com/api/conversations.info?channel=C12345", handler.RequestUri);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string responseJson;

        public StubHttpMessageHandler(string responseJson)
        {
            this.responseJson = responseJson;
        }

        public string? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.ToString();
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}