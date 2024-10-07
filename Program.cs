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
            return new CosmosClient(
                Environment.GetEnvironmentVariable("CosmosDbUri"),
                new DefaultAzureCredential()
            );
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
            return new AzureOpenAIClient(
                new Uri(Environment.GetEnvironmentVariable("AzureAiUri")),
                new DefaultAzureCredential()
            );
        });
    })
    .Build();


host.Run();