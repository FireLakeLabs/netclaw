using Microsoft.Extensions.Hosting;

namespace NetClaw.Host;

public static class Program
{
	public static IHostBuilder CreateHostBuilder(string[]? args = null)
	{
		return Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args ?? [])
			.ConfigureServices((context, services) =>
			{
				services.AddNetClawHostServices(context.Configuration, context.HostingEnvironment);
			});
	}

	public static async Task Main(string[] args)
	{
		using IHost host = CreateHostBuilder(args).Build();
		await host.RunAsync();
	}
}
