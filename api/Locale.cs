using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using System.Text.Json;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampaignCopilot
{   

    public class Locale(ILogger<World> logger, CosmosClient cosmosClient, AzureOpenAIClient openaiClient)
    {

        private readonly ILogger<World> _logger = logger;
        private readonly CosmosClient _cosmosClient = cosmosClient;
        private readonly AzureOpenAIClient _openaiClient = openaiClient;
        string CosmosContainer = "Locales";

        [Function("Locale")]
        public async Task<IActionResult> LocaleAsync([HttpTrigger(AuthorizationLevel.Function, ["get","post"])] HttpRequest req)
        {

            if (req.Method == "GET")
            {
                return await GetLocaleAsync(req);
            }
            else if (req.Method == "POST")
            {
                return await CreateLocaleAsync(req);
                // return new BadRequestObjectResult("POST method not implemented yet");
            }
            else
            {
                return new BadRequestObjectResult("Please pass a GET or POST request");
            }
        }

        public async Task<IActionResult> GetLocaleAsync(HttpRequest req)
        {

            string localeId = req.Query["localeId"].ToString();

            if (localeId == null)
            {
                return new BadRequestObjectResult("Please provide an ID for the Locale");
            }
            else
            {

                Container cosmosContainer = _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDbDatabaseName"), CosmosContainer);
                try
                {

                    ItemResponse<Object> response = await cosmosContainer.ReadItemAsync<Object>(localeId, new PartitionKey(localeId));

                    LocaleObject locale = JsonSerializer.Deserialize<LocaleObject>(response.Resource.ToString());
                    return new OkObjectResult(locale);

                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("Locale not found for id: {0}", localeId);
                    return new NotFoundResult();
                }
            }

        }

        public async Task<IActionResult> CreateLocaleAsync(HttpRequest req)
        {

            string campaignId = req.Query["campaignId"].ToString();
            string worldId = req.Query["worldId"].ToString();

            if (string.IsNullOrEmpty(campaignId))
            {
                campaignId = "0";
            }

            if (string.IsNullOrEmpty(worldId))
            {
                return new BadRequestObjectResult("Please provide a World ID for the Locale");
            }

            // Get existing world from cosmos to provide the description to the AI Prompt
            WorldObject world;
            Container cosmosContainer = _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDbDatabaseName"), "Worlds");
            try
            {
                ItemResponse<Object> worldResponse = await cosmosContainer.ReadItemAsync<Object>(worldId, new PartitionKey(worldId));
                world = JsonSerializer.Deserialize<WorldObject>(worldResponse.Resource.ToString());

            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("World not found for id: {0}", worldId);
                return new NotFoundObjectResult("World not found for id: " + worldId);
            }

            // Get the description value from the worldResponse
            
            AiModelPrompts aiModelPrompts = new AiModelPrompts("locale");

            ChatClient chatClient = _openaiClient.GetChatClient(Environment.GetEnvironmentVariable("AzureAiCompletionDeployment")); 

            //Append the world description to the user prompt
            string userPrompt = aiModelPrompts.UserPrompt + "\nWorld Description:\n" + world.description;
            _logger.LogInformation("User Promp:\n" + userPrompt);

            ChatCompletion completion = chatClient.CompleteChat(
            [
                new SystemChatMessage(aiModelPrompts.SystemPrompt),
                new UserChatMessage(userPrompt),
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

            // Save the locale to CosmosDB
            LocaleObject newLocale = new LocaleObject
            {
                id = Guid.NewGuid().ToString("N").Substring(0, 8),
                name = localeCompletion.name,
                description = localeCompletion.description,
                localeType = localeCompletion.localeType,
                campaignId = campaignId,
                worldId = worldId,
                aimodelinfo = new AiModelInfo
                {
                    ModelDeployment = Environment.GetEnvironmentVariable("AzureAiCompletionDeployment"),
                    ModelEndpoint = Environment.GetEnvironmentVariable("AzureAiCompletionEndpoint")
                },
                aimodelprompts = aiModelPrompts
            };

            cosmosContainer = _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDbDatabaseName"), CosmosContainer);
            ItemResponse<LocaleObject> response = await cosmosContainer.CreateItemAsync(newLocale, new PartitionKey(newLocale.id));
            
            return new OkObjectResult(response.Resource);
   
        }
    }
     
}