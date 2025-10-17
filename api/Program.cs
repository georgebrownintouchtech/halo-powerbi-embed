using HaloPowerBiEmbed.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // Bind the "PowerBi" configuration section to the PowerBiOptions class
        services.AddOptions<PowerBiOptions>()
            .Configure<IHostEnvironment>((settings, environment) =>
            {
                new ConfigurationBuilder()
                    .SetBasePath(environment.ContentRootPath)
                    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build()
                    .GetSection("Values:PowerBi") // In Azure, settings are flat. In local.settings.json, they are under "Values".
                    .Bind(settings);
            });
    })
    .ConfigureLogging(logging =>
    {
        logging.AddConsole();
    })
    .Build();

host.Run();
