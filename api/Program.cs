using HaloPowerBiEmbed.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    // Use the overload that provides HostBuilderContext to access IConfiguration
    .ConfigureServices((context, services) => // This is the correct overload to use.
    {
        // Bind the "PowerBi" configuration section to the PowerBiOptions class
        services.AddOptions<PowerBiOptions>()
            // Bind directly to the IConfiguration provided by the HostBuilderContext.
            // The Functions host typically loads local.settings.json automatically.
            .Bind(context.Configuration.GetSection("PowerBi"))
            // Enable validation for PowerBiOptions, chained to the same options builder.
            .ValidateDataAnnotations()
            .ValidateOnStart();
    })
    .ConfigureLogging(logging =>
    {
        logging.AddConsole();
    })
    .Build();

host.Run();
