using System;
using static Okr_Lambda.Constants;

namespace Okr_Lambda
{
    public interface IEnvironmentService
    {
        string EnvironmentName { get; set; }
    }
    public class EnvironmentService : IEnvironmentService
    {
        public EnvironmentService()
        {
            EnvironmentName = Environment.GetEnvironmentVariable(EnvironmentVariables.AspnetCoreEnvironment)
                ?? Environments.Qa;
        }

        public string EnvironmentName { get; set; }
    }

    public static class Constants
    {
        public static class EnvironmentVariables
        {
            public const string AspnetCoreEnvironment = "ASPNETCORE_ENVIRONMENT";
        }

        public static class Environments
        {
            public const string Dev = "dev";
            public const string Qa = "Qa";
            public const string Uat = "Uat";
        }
    }
}
