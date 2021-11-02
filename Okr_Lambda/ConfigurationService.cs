using Microsoft.Extensions.Configuration;

using System;

namespace Okr_Lambda
{
    public interface IConfigurationService
    {
        IConfiguration GetConfiguration();
    }
    public class ConfigurationService : IConfigurationService
    {
        public IEnvironmentService EnvService { get; }
        public ConfigurationService(IEnvironmentService envService)
        {
            EnvService = envService;
        }
        public IConfiguration GetConfiguration()
        {
            return new ConfigurationBuilder()
                ///.SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{EnvService.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables()
                .AddSystemsManager(configureSource =>
                {
                    /// Parameter Store prefix to pull configuration data from.
                    configureSource.Path = $"/compunnel/{EnvService.EnvironmentName}";

                    /// Reload configuration data every 15 minutes.
                    configureSource.ReloadAfter = TimeSpan.FromMinutes(15);
                })
                .Build();
        }
    }
}
