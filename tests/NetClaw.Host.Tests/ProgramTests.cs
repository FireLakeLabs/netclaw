using Microsoft.Extensions.Hosting;

namespace NetClaw.Host.Tests;

public sealed class ProgramTests
{
    [Fact]
    public async Task CreateHostBuilder_BuildsAndStartsHost()
    {
        using IHost host = NetClaw.Host.Program.CreateHostBuilder([]).Build();

        await host.StartAsync();
        await host.StopAsync();
    }
}