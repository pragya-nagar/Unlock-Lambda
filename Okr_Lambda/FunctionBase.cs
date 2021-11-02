using Amazon.SimpleSystemsManagement;
using Okr_Lambda.Repository;
using Okr_Lambda.Repository.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;


namespace Okr_Lambda
{
    public class Global
    {
        public static string envVariable = Environment.GetEnvironmentVariable("EnvironmentName") ?? Constants.Environments.Dev;
        public static Amazon.RegionEndpoint _regionEndpoint = Amazon.RegionEndpoint.USEast1;
    }
    public class FunctionBase : Global
    {
        protected readonly IServiceProvider _serviceProvider;
        public static string EnvironmentName = envVariable;

        public FunctionBase()
        {
            _serviceProvider = ConfigureServices();
        }


        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables()
                .AddSystemsManager(configureSource =>
                {
                    // Parameter Store prefix to pull configuration data from.
                    configureSource.Path = $"/compunnel/{EnvironmentName}";

                    // Reload configuration data every 15 minutes.
                    configureSource.ReloadAfter = TimeSpan.FromMinutes(15);
                    configureSource.AwsOptions = new Amazon.Extensions.NETCore.Setup.AWSOptions { Region = _regionEndpoint };
                })
                .Build();

            services.AddSingleton(config);
            services.AddSingleton<IAmazonSimpleSystemsManagement>(new AmazonSimpleSystemsManagementClient(_regionEndpoint));

            services.AddTransient<IEnvironmentService, EnvironmentService>();
            services.AddTransient<IAdminRepository, AdminRepository>();
            services.AddTransient<INotificationsRepository, NotificationsRepository>();
            services.AddTransient<IOkrServiceRepository, OkrServiceRepository>();

            var newServiceProvider = services.BuildServiceProvider();

            return newServiceProvider;
        }
    }
}
