using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using System.Text.Json;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using OpenAI.Images;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampaignCopilot
{   

    public class Locale(ILogger<LocaleObject> logger, CosmosClient cosmosClient, AzureOpenAIClient openaiClient,BlobServiceClient blobClient)
    {

        private readonly ILogger<LocaleObject> _logger = logger;
        private readonly CosmosClient _cosmosClient = cosmosClient;
        private readonly AzureOpenAIClient _openaiClient = openaiClient;
        private readonly BlobServiceClient _blobClient = blobClient;
        string CosmosContainer = "Locales";

        [Function("Locale")]
        public async Task<IActionResult> LocaleAsync([HttpTrigger(AuthorizationLevel.Function, ["get","post"])] HttpRequest req)
        {

            if (req.Method == "GET")
            {
                if (string.IsNullOrEmpty(req.Query["localeId"].ToString()) || string.IsNullOrEmpty(req.Query["campaignId"].ToString()))
                {
                    return new BadRequestObjectResult("Please provide a localeId and its campaignId");
                }
                else
                {
                    return await GetLocaleAsync(req);
                }
            }
            else if (req.Method == "POST")
            {
                if (string.IsNullOrEmpty(req.Query["campaignId"].ToString()) || string.IsNullOrEmpty(req.Query["worldId"].ToString()))
                {
                    return new BadRequestObjectResult("Please provide a campaignId and worldId to create a locale");
                }
                else
                {
                    return await CreateLocaleAsync(req);
                }
            }
            else
            {
                return new BadRequestObjectResult("Please pass a GET or POST request");
            }
        }

        public async Task<IActionResult> GetLocaleAsync(HttpRequest req)
        {

            string localeId = req.Query["localeId"].ToString();
            string campaignId = req.Query["campaignId"].ToString();

            Container cosmosContainer = _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDB__database"), CosmosContainer);
            try
            {

                ItemResponse<Object> response = await cosmosContainer.ReadItemAsync<Object>(localeId, new PartitionKey(campaignId));

                LocaleObject locale = JsonSerializer.Deserialize<LocaleObject>(response.Resource.ToString());
                return new OkObjectResult(locale);

            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Locale not found for id: {0}", localeId);
                return new NotFoundResult();
            }
        }

        public async Task<IActionResult> CreateLocaleAsync(HttpRequest req)
        {

            string campaignId = req.Query["campaignId"].ToString();
            string worldId = req.Query["worldId"].ToString();

            // Get existing world from cosmos to provide the description to the AI Prompt
            WorldObject world;
            Container cosmosContainer = _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDB__database"), "Worlds");
            try
            {
                ItemResponse<Object> worldResponse = await cosmosContainer.ReadItemAsync<Object>(worldId, new PartitionKey(campaignId));
                world = JsonSerializer.Deserialize<WorldObject>(worldResponse.Resource.ToString());

            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new NotFoundObjectResult("World not found for id: " + worldId);
            }

            // Get Existing Locations for the World
            cosmosContainer = _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDB__database"), CosmosContainer);
            QueryDefinition queryDefinition = new QueryDefinition("SELECT c.localeType FROM c WHERE c.campaignId = '" + campaignId + "' and c.worldId='" + worldId + "'");
            _logger.LogInformation("Query Definition: " + queryDefinition.QueryText);
            FeedIterator<Object> queryResultSetIterator = cosmosContainer.GetItemQueryIterator<Object>(queryDefinition);
            List<string> existingLocales = new List<string>();
            
            while (queryResultSetIterator.HasMoreResults)
            {
                FeedResponse<Object> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                foreach (var item in currentResultSet)
                {

                    try
                    {
                        LocaleType localeType = JsonSerializer.Deserialize<LocaleType>(item.ToString());
                        existingLocales.Add(localeType.localeType);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError("Error deserializing item: " + ex.Message);
                    }

                }
            }

            AiModelPrompts aiModelPrompts = new AiModelPrompts("locale");

            //Append the world description to the user prompt
            aiModelPrompts.UserPrompt += "\nWorld Description:\n" + world.description;
            
            //Append any existing locales if they exist
            if (existingLocales.Count > 0)
            {
                aiModelPrompts.UserPrompt += "\n\nExisting Locales:\n";
                foreach (string locale in existingLocales)
                {
                    aiModelPrompts.UserPrompt += locale + "\n";
                }
            }

            _logger.LogInformation("User Prompt:\n" + aiModelPrompts.UserPrompt);

            ChatClient chatClient = _openaiClient.GetChatClient(Environment.GetEnvironmentVariable("AzureAi__textDeployment"));

            ChatCompletion completion = chatClient.CompleteChat(
            [
                new SystemChatMessage(aiModelPrompts.SystemPrompt),
                new UserChatMessage(aiModelPrompts.UserPrompt)
            ]);

            LocaleCompletion localeCompletion;

            _logger.LogInformation("Locale Completion:\n" + completion.Content[0].Text);

            // Extract JSON content from the response
            string responseContent = completion.Content[0].Text;
            int startIndex = responseContent.IndexOf('{');
            int endIndex = responseContent.LastIndexOf('}');
            
            if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
            {
                string jsonResponse = responseContent.Substring(startIndex, endIndex - startIndex + 1);
                              
                try
                {
                    localeCompletion = JsonSerializer.Deserialize<LocaleCompletion>(jsonResponse);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error deserializing JSON content");
                    return new BadRequestObjectResult("Invalid Locale JSON format in response:" + jsonResponse);
                }
            }
            else
            {
                _logger.LogError("Invalid JSON format in response");
                return new StatusCodeResult(500);
            }

            aiModelPrompts.DallePrompt = String.Concat(localeCompletion.dalleprompt, " " , aiModelPrompts.DallePrompt);

            _logger.LogInformation("Dalle Prompt:\n" + aiModelPrompts.DallePrompt);
            
            // Generate Image 
            ImageClient imageClient = _openaiClient.GetImageClient(Environment.GetEnvironmentVariable("AzureAi__imageDeployment"));

            var imageCompletion = await imageClient.GenerateImageAsync(
                aiModelPrompts.DallePrompt,
                new ImageGenerationOptions()
                {
                    Size = GeneratedImageSize.W1024xH1024
                }
            );

            // Get a reference to a container and blob
            BlobContainerClient containerClient = _blobClient.GetBlobContainerClient(Environment.GetEnvironmentVariable("BlobStorage__container"));
            string localeId = Guid.NewGuid().ToString("N").Substring(0, 8);
            string blobName = $"campaigns/{campaignId}/{localeId}.png";
            BlobClient blobClient = containerClient.GetBlobClient(blobName);
            
            // Transform from uri to blob
            using (var httpClient = new HttpClient())
            {
                var imageStream = await httpClient.GetStreamAsync(imageCompletion.Value.ImageUri);
                await blobClient.UploadAsync(imageStream, true);
            }

            string blobUrl = blobClient.Uri.AbsoluteUri;

            // Save the locale to CosmosDB
            LocaleObject newLocale = new LocaleObject
            {
                id = localeId,
                name = localeCompletion.name,
                description = localeCompletion.description,
                localeType = localeCompletion.type,
                worldId = worldId,
                campaignId = campaignId,
                imageUrl = blobUrl,
                aimodelinfo = new AiModelInfo
                {
                    ModelDeployment = Environment.GetEnvironmentVariable("AzureAi__textDeployment"),
                    ModelEndpoint = Environment.GetEnvironmentVariable("AzureAi__accountEndpoint")
                },
                aimodelprompts = aiModelPrompts
            };

            cosmosContainer = _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDB__database"), CosmosContainer);
            ItemResponse<LocaleObject> response = await cosmosContainer.CreateItemAsync(newLocale, new PartitionKey(campaignId));
            
            return new OkObjectResult(response.Resource);
   
        }
    }
     
}

public class LocaleType
{
    public string localeType { get; set; }
}