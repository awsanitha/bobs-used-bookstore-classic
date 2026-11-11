using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using BobsBookstoreClassic.Data;
using Bookstore.Common;
using Microsoft.Extensions.Configuration;

namespace Bookstore.Web
{
    public static class ConfigurationSetup
    {
        /// <summary>
        /// Initializes BookstoreConfiguration from IConfiguration, then optionally pulls additional
        /// settings from AWS Systems Manager Parameter Store based on the service configuration.
        /// </summary>
        public static void ConfigureConfiguration(IConfiguration configuration)
        {
            BookstoreConfiguration.Initialize(configuration);

            var rootPath = "/" + Constants.AppName;

            const string databasePath = "/Database";
            const string authenticationPath = "/Authentication";
            const string fileServicePath = "/Files";

            if (BookstoreConfiguration.GetSetting("Services/Database") == "aws")
            {
                using var client = new AmazonSimpleSystemsManagementClient();
                var request = new GetParameterRequest
                {
                    Name = $"{rootPath}{databasePath}/ConnectionStrings/BookstoreDatabaseConnection"
                };
                var response = client.GetParameterAsync(request).GetAwaiter().GetResult();
                BookstoreConfiguration.AddConnectionString(
                    response.Parameter.Name.Replace($"{rootPath}{databasePath}/ConnectionStrings/", string.Empty),
                    response.Parameter.Value);
            }

            if (BookstoreConfiguration.GetSetting("Services/Authentication") == "aws")
            {
                using var client = new AmazonSimpleSystemsManagementClient();
                var request = new GetParametersByPathRequest
                {
                    Path = $"{rootPath}{authenticationPath}/",
                    Recursive = true
                };
                var response = client.GetParametersByPathAsync(request).GetAwaiter().GetResult();
                foreach (var parameter in response.Parameters)
                {
                    BookstoreConfiguration.AddSetting(
                        parameter.Name.Replace($"{rootPath}/", string.Empty),
                        parameter.Value);
                }
            }

            if (BookstoreConfiguration.GetSetting("Services/FileService") == "aws")
            {
                using var client = new AmazonSimpleSystemsManagementClient();
                var request = new GetParametersByPathRequest
                {
                    Path = $"{rootPath}{fileServicePath}/",
                    Recursive = true
                };
                var response = client.GetParametersByPathAsync(request).GetAwaiter().GetResult();
                foreach (var parameter in response.Parameters)
                {
                    BookstoreConfiguration.AddSetting(
                        parameter.Name.Replace($"{rootPath}/", string.Empty),
                        parameter.Value);
                }
            }
        }
    }
}
