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

    public class Location(ILogger<LocationObject> logger, CosmosClient cosmosClient, AzureOpenAIClient openaiClient)
    {

        private readonly ILogger<LocationObject> _logger = logger;
        private readonly CosmosClient _cosmosClient = cosmosClient;
        private readonly AzureOpenAIClient _openaiClient = openaiClient;
        string CosmosContainer = "Locations";

        [Function("Location")]
        public async Task<IActionResult> LocationAsync([HttpTrigger(AuthorizationLevel.Function, ["get", "post"])] HttpRequest req)
        {

            if (req.Method == "GET")
            {
                if (string.IsNullOrEmpty(req.Query["locationId"].ToString()) || string.IsNullOrEmpty(req.Query["campaignId"].ToString()))
                {
                    return new BadRequestObjectResult("Please provide a locationId and its campaignId");
                }
                else
                {
                    return await GetLocationAsync(req);
                }
            }
            else if (req.Method == "POST")
            {
                if (string.IsNullOrEmpty(req.Query["campaignId"].ToString()) || string.IsNullOrEmpty(req.Query["localeId"].ToString()))
                {
                    return new BadRequestObjectResult("Please provide a campaignId and localeId to create a location");
                }
                else
                {
                    return await CreateLocationAsync(req);
                }
            }
            else
            {
                return new BadRequestObjectResult("Please pass a GET or POST request");
            }
        }

        public async Task<IActionResult> GetLocationAsync(HttpRequest req)
        {

            string locationId = req.Query["locationId"].ToString();
            string campaignId = req.Query["campaignId"].ToString();

            Container cosmosContainer = _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDbDatabaseName"), CosmosContainer);
            try
            {

                ItemResponse<Object> response = await cosmosContainer.ReadItemAsync<Object>(locationId, new PartitionKey(campaignId));

                LocaleObject locale = JsonSerializer.Deserialize<LocaleObject>(response.Resource.ToString());
                return new OkObjectResult(locale);

            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Location not found for id: {0}", locationId);
                return new NotFoundResult();
            }
        }

        public async Task<IActionResult> CreateLocationAsync(HttpRequest req)
        {

            string campaignId = req.Query["campaignId"].ToString();
            string localeId = req.Query["localeId"].ToString();

            // Get existing Locale from cosmos to provide the description to the AI Prompt
            LocaleObject locale;
            Container cosmosContainer = _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDbDatabaseName"), "Locales");
            try
            {
                ItemResponse<Object> localeResponse = await cosmosContainer.ReadItemAsync<Object>(localeId, new PartitionKey(campaignId));
                locale = JsonSerializer.Deserialize<LocaleObject>(localeResponse.Resource.ToString());

            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new NotFoundObjectResult("Locale not found for id: " + localeId);
            }

            AiModelPrompts aiModelPrompts = new AiModelPrompts("location");

            //Append the Locale description to the user prompt to generate locations
            aiModelPrompts.UserPrompt += "\nLocale Description:\n" + locale.description;

            // Get Existing Locations for the Locale
            cosmosContainer = _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDbDatabaseName"), CosmosContainer);
            QueryDefinition queryDefinition = new QueryDefinition("SELECT c.locationType FROM c WHERE c.campaignId = '" + campaignId + "' and c.localeId='" + localeId + "'");
            _logger.LogInformation("Query Definition: " + queryDefinition.QueryText);
            FeedIterator<Object> queryResultSetIterator = cosmosContainer.GetItemQueryIterator<Object>(queryDefinition);
            List<string> existingLocations = new List<string>();

            while (queryResultSetIterator.HasMoreResults)
            {
                FeedResponse<Object> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                foreach (var item in currentResultSet)
                {

                    try
                    {
                        LocationType locationType = JsonSerializer.Deserialize<LocationType>(item.ToString());
                        existingLocations.Add(locationType.locationType);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError("Error deserializing item: " + ex.Message);
                    }

                }
            }

            //Append any existing locales if they exist
            if (existingLocations.Count > 0)
            {
                aiModelPrompts.UserPrompt += "\n\nExisting Locations:\n";
                foreach (string location in existingLocations)
                {
                    aiModelPrompts.UserPrompt += location + "\n";
                }
            }

            _logger.LogInformation("User Prompt:\n" + aiModelPrompts.UserPrompt);

            ChatClient chatClient = _openaiClient.GetChatClient(Environment.GetEnvironmentVariable("AzureAiCompletionDeployment"));
            ChatCompletion completion = chatClient.CompleteChat(
            [
                new SystemChatMessage(aiModelPrompts.SystemPrompt),
                new UserChatMessage(aiModelPrompts.UserPrompt),
            ]);

            LocationCompletion locationCompletion;

            _logger.LogInformation("Location Completion:\n" + completion.Content[0].Text);

            // Extract JSON content from the response
            string responseContent = completion.Content[0].Text;
            int startIndex = responseContent.IndexOf('{');
            int endIndex = responseContent.LastIndexOf('}');

            if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
            {
                string jsonResponse = responseContent.Substring(startIndex, endIndex - startIndex + 1);

                try
                {
                    locationCompletion = JsonSerializer.Deserialize<LocationCompletion>(jsonResponse);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error deserializing JSON content");
                    return new BadRequestObjectResult("Invalid Location JSON format in response:" + jsonResponse);
                }
            }
            else
            {
                _logger.LogError("Invalid JSON format in response");
                return new StatusCodeResult(500);
            }

            // Save the locale to CosmosDB
            LocationObject newLocation = new LocationObject
            {
                id = Guid.NewGuid().ToString("N").Substring(0, 8),
                name = locationCompletion.name,
                description = locationCompletion.description,
                locationType = locationCompletion.type,
                localeId = localeId,
                campaignId = campaignId,
                aimodelinfo = new AiModelInfo
                {
                    ModelDeployment = Environment.GetEnvironmentVariable("AzureAiCompletionDeployment"),
                    ModelEndpoint = Environment.GetEnvironmentVariable("AzureAiCompletionEndpoint")
                },
                aimodelprompts = aiModelPrompts
            };

            cosmosContainer = _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDbDatabaseName"), CosmosContainer);
            ItemResponse<LocationObject> response = await cosmosContainer.CreateItemAsync(newLocation, new PartitionKey(campaignId));

            return new OkObjectResult(response.Resource);

        }
    }

}

public class LocationType
{
    public string locationType { get; set; }
}