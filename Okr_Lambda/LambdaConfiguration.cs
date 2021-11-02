using Microsoft.Extensions.Configuration;
using System;
using System.IO;


namespace Okr_Lambda
{
    public class LambdaConfiguration : ILambdaConfiguration
    {
        static string envName = Environment.GetEnvironmentVariable("EnvironmentName");
        public static IConfigurationRoot Configuration => new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddSystemsManager(configureSource =>
            {
                // Parameter Store prefix to pull configuration data from.
                configureSource.Path = $"/compunnel/{envName}";

                // Reload configuration data every 15 minutes.
                configureSource.ReloadAfter = TimeSpan.FromMinutes(15);
            })
            .Build();

        IConfigurationRoot ILambdaConfiguration.Configuration => Configuration;
    }
    public interface ILambdaConfiguration
    {
        IConfigurationRoot Configuration { get; }
    }
}
