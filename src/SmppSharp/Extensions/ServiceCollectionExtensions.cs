using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SmppSharp.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a single <see cref="ISmppClient"/> and starts it as a hosted service.
    /// </summary>
    public static IServiceCollection AddSmpp(
        this IServiceCollection services,
        Action<SmppOptions> configure)
    {
        services.Configure(configure);

        services.AddSingleton<ISmppClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SmppOptions>>().Value;
            var logger  = sp.GetRequiredService<ILogger<SmppClient>>();
            return new SmppClient(options, logger);
        });

        services.AddHostedService<SmppHostedService>();

        return services;
    }
}

/// <summary>Connects the SMPP client on app start and disconnects on stop.</summary>
internal sealed class SmppHostedService(ISmppClient client) : IHostedService
{
    public Task StartAsync(CancellationToken ct) => client.ConnectAsync(ct);

    public Task StopAsync(CancellationToken ct)  => client.DisconnectAsync(ct);
}
