using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Cosmos;
using Azure.AI.OpenAI;
using Azure;


var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services => {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddSingleton<CosmosClient>(serviceProvider =>
        {
            return new CosmosClient(Environment.GetEnvironmentVariable("CosmosDbEndpointUrl"),Environment.GetEnvironmentVariable("CosmosDbPrimaryKey"));
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