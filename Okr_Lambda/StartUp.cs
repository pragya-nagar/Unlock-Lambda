using Amazon.S3;
using Amazon.SimpleSystemsManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Okr_Lambda
{
    public class StartUp
    {
        private readonly IConfiguration _configuration;
        public StartUp(IConfiguration configuration)
        {
            this._configuration = configuration;
        }

        public static IServiceCollection Container => ConfigureServices(LambdaConfiguration.Configuration);


        private static IServiceCollection ConfigureServices(IConfigurationRoot root)
        {

            var services = new ServiceCollection();

            string envVariable = Environment.GetEnvironmentVariable("EnvironmentName");

            AmazonS3Config awsConfig = new AmazonS3Config();
            var regionEndPoint = Amazon.RegionEndpoint.USEast1;
            awsConfig.RegionEndpoint = regionEndPoint;
            awsConfig.ServiceURL = $"/compunnel/{envVariable}";

            services.AddTransient<ISsmParameters, SsmParameters>()
                    .AddSingleton<IAmazonSimpleSystemsManagement>(
                        new AmazonSimpleSystemsManagementClient(regionEndPoint))
                    .AddSingleton<IAmazonS3>(new AmazonS3Client(awsConfig));
            return services;

        }
    }
}
