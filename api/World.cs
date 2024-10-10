using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using System.Text.Json;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using OpenAI.Images;
using Microsoft.Extensions.Options;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;


namespace CampaignCopilot
{   

    public class World(ILogger<World> logger, CosmosClient cosmosClient, AzureOpenAIClient openaiClient,BlobServiceClient blobClient)
    {

        private readonly ILogger<World> _logger = logger;
        private readonly CosmosClient _cosmosClient = cosmosClient;
        private readonly AzureOpenAIClient _openaiClient = openaiClient;
        private readonly BlobServiceClient _blobClient = blobClient;
        
        string CosmosContainer = "Worlds";

        [Function("World")]
        public async Task<IActionResult> WorldAsync([HttpTrigger(AuthorizationLevel.Function, ["get","post"])] HttpRequest req)
        {

            if (req.Method == "GET")
            {

                if (string.IsNullOrEmpty(req.Query["campaignId"].ToString()) || string.IsNullOrEmpty(req.Query["worldId"].ToString()))
                {
                    return new BadRequestObjectResult("Please provide a worldId and its campaignId");
                }
                else
                {
                    return await GetWorldAsync(req);
                }
            }
            else if (req.Method == "POST")
            {

                if (string.IsNullOrEmpty(req.Query["campaignId"].ToString()))
                {
                    return new BadRequestObjectResult("Please provide a campaignId to create a world");
                } 
                else 
                {
                    return await CreateWorldAsync(req);
                }
            }
            else
            {
                return new BadRequestObjectResult("Please pass a GET or POST request");
            }
        }

        public async Task<IActionResult> GetWorldAsync(HttpRequest req)
        {

            string worldId = req.Query["worldId"].ToString();
            string campaignId = req.Query["campaignId"].ToString();

            Container cosmosContainer = _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDB__database"), CosmosContainer);
            try
            {

                ItemResponse<Object> response = await cosmosContainer.ReadItemAsync<Object>(worldId, new PartitionKey(campaignId));

                WorldObject world = JsonSerializer.Deserialize<WorldObject>(response.Resource.ToString());
                return new OkObjectResult(world);

            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("World not found for id: {0}", worldId);
                return new NotFoundResult();
            }
        }

        public async Task<IActionResult> CreateWorldAsync(HttpRequest req)
        {

            string campaignId = req.Query["campaignId"].ToString();

            AiModelPrompts aiModelPrompts = new AiModelPrompts("world");

            ChatClient chatClient = _openaiClient.GetChatClient(Environment.GetEnvironmentVariable("AzureAi_textDeployment"));

            ChatCompletion completion = chatClient.CompleteChat(
            [
                new SystemChatMessage(aiModelPrompts.SystemPrompt),
                new UserChatMessage(aiModelPrompts.UserPrompt)
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
            
            aiModelPrompts.DallePrompt = String.Concat(worldCompletion.dalleprompt," " , aiModelPrompts.DallePrompt);

            _logger.LogInformation("Dalle Prompt:\n" + aiModelPrompts.DallePrompt);
            
            // Generate Image 
            ImageClient imageClient = _openaiClient.GetImageClient(Environment.GetEnvironmentVariable("AzureAi_imageDeployment"));

            var imageCompletion = await imageClient.GenerateImageAsync(
                aiModelPrompts.DallePrompt,
                new ImageGenerationOptions()
                {
                    Size = GeneratedImageSize.W1024xH1024
                }
            );

            // Get a reference to a container and blob
            BlobContainerClient containerClient = _blobClient.GetBlobContainerClient(Environment.GetEnvironmentVariable("BlobStorage_container"));
            string worldId = Guid.NewGuid().ToString("N").Substring(0, 8);
            string blobName = $"campaigns/{campaignId}/{worldId}.png";
            BlobClient blobClient = containerClient.GetBlobClient(blobName);
            
            // Transform from uri to blob
            using (var httpClient = new HttpClient())
            {
                var imageStream = await httpClient.GetStreamAsync(imageCompletion.Value.ImageUri);
                await blobClient.UploadAsync(imageStream, true);
            }

            string blobUrl = blobClient.Uri.AbsoluteUri;


            // Save the world to CosmosDB
            WorldObject newWorld = new WorldObject
            {
                id = worldId,
                name = worldCompletion.name,
                description = worldCompletion.description,
                campaignId = campaignId,
                imageUrl = blobUrl,
                aimodelinfo = new AiModelInfo
                {
                    ModelDeployment = Environment.GetEnvironmentVariable("AzureAi_textDeployment"),
                    ModelEndpoint = Environment.GetEnvironmentVariable("AzureAi_accountEndpoint")
                },
                aimodelprompts = aiModelPrompts
            };

            Container cosmosContainer = _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDB__database"), CosmosContainer);
            ItemResponse<WorldObject> response = await cosmosContainer.CreateItemAsync(newWorld, new PartitionKey(newWorld.campaignId));

            return new OkObjectResult(response.Resource);

        }
    }
     
}