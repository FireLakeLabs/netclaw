using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using FireLakeLabs.NetClaw.Application.Observability;
using FireLakeLabs.NetClaw.Dashboard.Hubs;
using FireLakeLabs.NetClaw.Dashboard.Services;

namespace FireLakeLabs.NetClaw.Dashboard;

public static class DashboardServiceExtensions
{
    public static IServiceCollection AddNetClawDashboard(this IServiceCollection services, string groupsDirectory, string dataDirectory)
    {
        services.AddSignalR();
        services.AddSingleton<DashboardStateService>();
        services.AddSingleton(new WorkspaceFileService(groupsDirectory, dataDirectory));
        services.AddSingleton<DashboardBroadcastService>();
        services.AddSingleton<IMessageNotifier>(sp => sp.GetRequiredService<DashboardBroadcastService>());
        services.AddHostedService(sp => sp.GetRequiredService<DashboardBroadcastService>());
        return services;
    }

    public static IEndpointRouteBuilder MapNetClawDashboard(this IEndpointRouteBuilder app)
    {
        app.MapDashboardApi();
        app.MapHub<DashboardHub>("/hubs/dashboard");
        return app;
    }

    public static WebApplication UseNetClawDashboardSpa(this WebApplication app)
    {
        string wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        if (Directory.Exists(wwwrootPath))
        {
            app.UseDefaultFiles(new DefaultFilesOptions
            {
                FileProvider = new PhysicalFileProvider(wwwrootPath)
            });
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(wwwrootPath)
            });
            app.MapFallbackToFile("index.html", new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(wwwrootPath)
            });
        }

        return app;
    }
}
