using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

namespace SamaBot.Api.Common.Extensions;

public static class AwsConfigurationExtensions
{
    public static void AddAwsSecureConfiguration(this IHostApplicationBuilder builder)
    {
        var martenArn = Environment.GetEnvironmentVariable("SECRET_ARN_MARTEN");
        var ssmPath = Environment.GetEnvironmentVariable("SSM_PATH_WHATSAPP");

        if (string.IsNullOrEmpty(martenArn) && string.IsNullOrEmpty(ssmPath))
        {
            return;
        }

        if (Environment.GetCommandLineArgs().Contains("codegen"))
        {
            return;
        }

        using var secretsClient = new AmazonSecretsManagerClient();
        using var ssmClient = new AmazonSimpleSystemsManagementClient();

        builder.AddAwsSecureConfigurationCore(secretsClient, ssmClient);
    }

    public static void AddAwsSecureConfigurationCore(
        this IHostApplicationBuilder builder,
        IAmazonSecretsManager secretsClient,
        IAmazonSimpleSystemsManagement ssmClient)
    {
        var secureConfig = new Dictionary<string, string?>();

        // 1. Fetch Marten connection string from Secrets Manager
        var martenArn = Environment.GetEnvironmentVariable("SECRET_ARN_MARTEN");
        if (!string.IsNullOrEmpty(martenArn))
        {
            var response = secretsClient.GetSecretValueAsync(new GetSecretValueRequest { SecretId = martenArn })
                                        .GetAwaiter()
                                        .GetResult();

            secureConfig["ConnectionStrings:Marten"] = response.SecretString;
        }

        // 2. Fetch WhatsApp tokens from SSM Parameter Store
        var ssmPath = Environment.GetEnvironmentVariable("SSM_PATH_WHATSAPP");
        if (!string.IsNullOrEmpty(ssmPath))
        {
            var response = ssmClient.GetParametersByPathAsync(new GetParametersByPathRequest
            {
                Path = ssmPath,
                WithDecryption = true
            }).GetAwaiter().GetResult();

            foreach (var param in response.Parameters)
            {
                // Map AWS path to .NET IOptions structure 
                // e.g., "/chatbot/dev/whatsapp/app-secret" -> "WhatsApp:AppSecret"
                var keyName = param.Name.Split('/').Last();
                var pascalCaseKey = string.Join("", keyName.Split('-').Select(p => char.ToUpper(p[0]) + p[1..]));

                secureConfig[$"WhatsApp:{pascalCaseKey}"] = param.Value;
            }
        }

        // 3. Inject fetched secrets into .NET configuration
        if (secureConfig.Count > 0)
        {
            builder.Configuration.AddInMemoryCollection(secureConfig);
        }
    }
}