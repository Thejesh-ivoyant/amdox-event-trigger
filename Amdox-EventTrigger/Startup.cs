using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

[assembly: FunctionsStartup(typeof(Amdox_EventTrigger.Startup))]

namespace Amdox_EventTrigger
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            // Initialize configuration
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            IConfigurationRoot configuration = configBuilder.Build();

            // Add services to the container
            builder.Services.AddSingleton(configuration);

            // Add Application Insights telemetry
            builder.Services.AddApplicationInsightsTelemetry(configuration["APPINSIGHTS_INSTRUMENTATIONKEY"]);

            // Register other services as needed
            // Example: builder.Services.AddScoped<IMyService, MyService>();

            // Inject IConfiguration into the GetUpdateEvents class
            builder.Services.AddSingleton<IConfiguration>(configuration);
        }
    }
}
