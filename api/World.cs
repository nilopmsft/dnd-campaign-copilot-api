using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using System.Text.Json;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace CampaignCopilot
{   

    public class World(ILogger<World> logger, CosmosClient cosmosClient, AzureOpenAIClient openaiClient)
    {

        private readonly ILogger<World> _logger = logger;
        private readonly CosmosClient _cosmosClient = cosmosClient;
        private readonly AzureOpenAIClient _openaiClient = openaiClient;
        string CosmosContainer = "Worlds";

        [Function("World")]
        public async Task<IActionResult> WorldAsync([HttpTrigger(AuthorizationLevel.Function, ["get","post"])] HttpRequest req)
        {

            if (req.Method == "GET")
            {
                return await GetWorldAsync(req);
            }
            else if (req.Method == "POST")
            {
                return await CreateWorldAsync(req);
                // return new BadRequestObjectResult("POST method not implemented yet");
            }
            else
            {
                return new BadRequestObjectResult("Please pass a GET or POST request");
            }
        }

        public async Task<IActionResult> GetWorldAsync(HttpRequest req)
        {

            string worldId = req.Query["id"].ToString();

            if (worldId == null)
            {
                return new BadRequestObjectResult("Please provide an ID for the World");
            }
            else
            {

                Container cosmosContainer = _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDbDatabaseName"), CosmosContainer);
                try
                {

                    ItemResponse<Object> response = await cosmosContainer.ReadItemAsync<Object>(worldId, new PartitionKey(worldId));

                    WorldObject world = JsonSerializer.Deserialize<WorldObject>(response.Resource.ToString());
                    return new OkObjectResult(world);

                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("World not found for id: {0}", worldId);
                    return new NotFoundResult();
                }
            }

        }

        public async Task<IActionResult> CreateWorldAsync(HttpRequest req)
        {

            string campaignId = req.Query["campaignId"].ToString();

            if (string.IsNullOrEmpty(campaignId))
            {
                campaignId = "0";
            }

            AiModelPrompts aiModelPrompts = new AiModelPrompts("world");

            ChatClient chatClient = _openaiClient.GetChatClient(Environment.GetEnvironmentVariable("AzureAiCompletionDeployment")); 

            ChatCompletion completion = chatClient.CompleteChat(
            [
                new SystemChatMessage(aiModelPrompts.SystemPrompt),
                new UserChatMessage(aiModelPrompts.UserPrompt),
            ]);

            WorldCompletion worldCompletion;

            // Extract JSON content from the response
            string responseContent = completion.Content[0].Text;
            int startIndex = responseContent.IndexOf('{');
            int endIndex = responseContent.LastIndexOf('}');
            
            if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
            {
                string jsonResponse = responseContent.Substring(startIndex, endIndex - startIndex + 1);
                              
                try
                {
                    worldCompletion = JsonSerializer.Deserialize<WorldCompletion>(jsonResponse);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error deserializing JSON content");
                    return new BadRequestObjectResult("Invalid World JSON format in response:" + jsonResponse);
                }
            }
            else
            {
                _logger.LogError("Invalid JSON format in response");
                return new StatusCodeResult(500);
            }

            // Save the world to CosmosDB
            WorldObject newWorld = new WorldObject
            {
                id = Guid.NewGuid().ToString("N").Substring(0, 8),
                name = worldCompletion.name,
                description = worldCompletion.description,
                campaignId = campaignId,
                aimodelinfo = new AiModelInfo
                {
                    ModelDeployment = Environment.GetEnvironmentVariable("AzureAiCompletionDeployment"),
                    ModelEndpoint = Environment.GetEnvironmentVariable("AzureAiCompletionEndpoint")
                },
                aimodelprompts = aiModelPrompts
            };

            Container cosmosContainer = _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDbDatabaseName"), CosmosContainer);
            ItemResponse<WorldObject> response = await cosmosContainer.CreateItemAsync(newWorld, new PartitionKey(newWorld.id));
            
            return new OkObjectResult(response.Resource);
   
        }
    }
     
}