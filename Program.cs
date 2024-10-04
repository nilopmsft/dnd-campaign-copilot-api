using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Cosmos;
using Azure;
using Azure.AI.OpenAI;
using Azure.Storage.Blobs;
using Azure.Identity;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services => {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddSingleton<CosmosClient>(serviceProvider =>
        {
            return new CosmosClient(Environment.GetEnvironmentVariable("CosmosDbEndpointUrl"),Environment.GetEnvironmentVariable("CosmosDbPrimaryKey"));
            // return new CosmosClient(accountEndpoint: Environment.GetEnvironmentVariable("CosmosDbEndpointUrl"),tokenCredential: new DefaultAzureCredential());
        });
        services.AddSingleton<BlobServiceClient>(_ =>
        {
            return new BlobServiceClient(
                new Uri(Environment.GetEnvironmentVariable("BlobStorageUri")),
                new DefaultAzureCredential()
            );
        });
        services.AddSingleton<AzureOpenAIClient>(serviceProvider =>
        {
            var endpoint = new Uri(Environment.GetEnvironmentVariable("AzureAiCompletionEndpoint"));
            var apiKey = new AzureKeyCredential(Environment.GetEnvironmentVariable("AzureAiCompletionApiKey"));
            return new AzureOpenAIClient(endpoint, apiKey);
        });
    })
    .Build();


host.Run();