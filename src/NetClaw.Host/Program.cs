using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetClaw.Dashboard;
using NetClaw.Host.Configuration;
using NetClaw.Infrastructure.Configuration;

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
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.Services.AddNetClawHostServices(builder.Configuration, builder.Environment);

        DashboardOptions dashboardOptions = CreateDashboardOptions(builder.Configuration);
        dashboardOptions.Validate();

        if (dashboardOptions.Enabled)
        {
            HostPathOptions hostPathOptions = HostPathOptions.Create(builder.Configuration, builder.Environment);
            StorageOptions storageOptions = StorageOptions.Create(hostPathOptions.ProjectRoot);
            builder.Services.AddNetClawDashboard(storageOptions.GroupsDirectory, storageOptions.DataDirectory);

            builder.WebHost.ConfigureKestrel(kestrel =>
            {
                kestrel.Listen(System.Net.IPAddress.Parse(dashboardOptions.BindAddress), dashboardOptions.Port);
            });

            WebApplication app = builder.Build();
            app.UseNetClawDashboardSpa();
            app.MapNetClawDashboard();
            await app.RunAsync();
        }
        else
        {
            IHost host = CreateHostBuilder(args).Build();
            await host.RunAsync();
        }
    }

    private static DashboardOptions CreateDashboardOptions(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        return new DashboardOptions
        {
            Port = int.TryParse(configuration["NetClaw:Dashboard:Port"], out int port) ? port : 5080,
            Enabled = bool.TryParse(configuration["NetClaw:Dashboard:Enabled"], out bool enabled) && enabled,
            BindAddress = configuration["NetClaw:Dashboard:BindAddress"] ?? "127.0.0.1"
        };
    }
}
