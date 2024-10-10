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

    public class Character(ILogger<CharacterObject> logger, CosmosClient cosmosClient, AzureOpenAIClient openaiClient,BlobServiceClient blobClient)
    {

        private readonly ILogger<CharacterObject> _logger = logger;
        private readonly CosmosClient _cosmosClient = cosmosClient;
        private readonly AzureOpenAIClient _openaiClient = openaiClient;
        private readonly BlobServiceClient _blobClient = blobClient;
        string CosmosContainer = "Characters";

        [Function("Character")]
        public async Task<IActionResult> CharacterAsync([HttpTrigger(AuthorizationLevel.Function, ["get", "post"])] HttpRequest req)
        {

            if (req.Method == "GET")
            {
                if (string.IsNullOrEmpty(req.Query["characterId"].ToString()) || string.IsNullOrEmpty(req.Query["campaignId"].ToString()))
                {
                    return new BadRequestObjectResult("Please provide a characterId and its campaignId");
                }
                else
                {
                    return await GetCharacterAsync(req);
                }
            }
            else if (req.Method == "POST")
            {
                if (string.IsNullOrEmpty(req.Query["campaignId"].ToString()))
                {
                    return new BadRequestObjectResult("Please provide a campaignId to create a character");
                }
                else
                {
                    return await CreateCharacterAsync(req);
                }
            }
            else
            {
                return new BadRequestObjectResult("Please pass a GET or POST request");
            }
        }

        public async Task<IActionResult> GetCharacterAsync(HttpRequest req)
        {
            string characterId = req.Query["characterId"].ToString();
            string campaignId = req.Query["campaignId"].ToString();

            Container cosmosContainer = _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDB__database"), CosmosContainer);
            try
            {

                ItemResponse<Object> response = await cosmosContainer.ReadItemAsync<Object>(characterId, new PartitionKey(campaignId));

                CharacterObject character = JsonSerializer.Deserialize<CharacterObject>(response.Resource.ToString());
                return new OkObjectResult(character);

            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Character not found for id: {0}", characterId);
                return new NotFoundResult();
            }
        }

        public async Task<IActionResult> CreateCharacterAsync(HttpRequest req)
        {

            string campaignId = req.Query["campaignId"].ToString();

            AiModelPrompts aiModelPrompts = new AiModelPrompts("character");

            // Get Existing Characters for the Campaign
            Container cosmosContainer = _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDB__database"), CosmosContainer);
            QueryDefinition queryDefinition = new QueryDefinition("SELECT c.name, c.definition.character_class, c.definition.race FROM c WHERE c.campaignId = '" + campaignId + "'");
            _logger.LogInformation("Query Definition: " + queryDefinition.QueryText);
            FeedIterator<Object> queryResultSetIterator = cosmosContainer.GetItemQueryIterator<Object>(queryDefinition);
            List<string> existingCharacters = new List<string>();

            while (queryResultSetIterator.HasMoreResults)
            {
                FeedResponse<Object> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                foreach (var item in currentResultSet)
                {

                    try
                    {
                        CharacterInfo characterInfo = JsonSerializer.Deserialize<CharacterInfo>(item.ToString());
                        existingCharacters.Add(characterInfo.name + " - " + characterInfo.character_class + " - " + characterInfo.race);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError("Error deserializing item: " + ex.Message);
                    }

                }
            }

            
            //Append any existing locales if they exist
            if (existingCharacters.Count > 0)
            {
                aiModelPrompts.UserPrompt += "\n\nExisting Characters:\n";
                foreach (string character in existingCharacters)
                {
                    aiModelPrompts.UserPrompt += character + "\n";
                }
            }

            // _logger.LogInformation("User Prompt:\n" + aiModelPrompts.UserPrompt);

            ChatClient chatClient = _openaiClient.GetChatClient(Environment.GetEnvironmentVariable("AzureAi__textDeployment"));
            ChatCompletion completion = chatClient.CompleteChat(
            [
                new SystemChatMessage(aiModelPrompts.SystemPrompt),
                new UserChatMessage(aiModelPrompts.UserPrompt)
            ]);

            _logger.LogInformation("Completion:\n" + completion.Content[0].Text);

            CharacterObject newCharacter = new CharacterObject();

            // Extract JSON content from the response
            string responseContent = completion.Content[0].Text;
            int startIndex = responseContent.IndexOf('{');
            int endIndex = responseContent.LastIndexOf('}');

            if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
            {
                string jsonResponse = responseContent.Substring(startIndex, endIndex - startIndex + 1);

                try
                {
                    newCharacter = JsonSerializer.Deserialize<CharacterObject>(jsonResponse);

                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error deserializing JSON content");
                    return new BadRequestObjectResult("Invalid Character JSON format in response:" + jsonResponse);
                }
            }
            else
            {
                _logger.LogError("Invalid JSON format in response");
                return new StatusCodeResult(500);
            }

            aiModelPrompts.DallePrompt = String.Concat(newCharacter.dalleprompt," " , aiModelPrompts.DallePrompt);
            
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
            string characterId = Guid.NewGuid().ToString("N").Substring(0, 8);
            string blobName = $"campaigns/{campaignId}/{characterId}.png";
            BlobClient blobClient = containerClient.GetBlobClient(blobName);
            
            // Transform from uri to blob
            using (var httpClient = new HttpClient())
            {
                var imageStream = await httpClient.GetStreamAsync(imageCompletion.Value.ImageUri);
                await blobClient.UploadAsync(imageStream, true);
            }

            string blobUrl = blobClient.Uri.AbsoluteUri;

            newCharacter.id = characterId;
            newCharacter.campaignId = campaignId;
            newCharacter.imageUrl = blobUrl;
            newCharacter.aimodelinfo = new AiModelInfo
            {
                ModelDeployment = Environment.GetEnvironmentVariable("AzureAi__textDeployment"),
                ModelEndpoint = Environment.GetEnvironmentVariable("AzureAi__accountEndpoint")
            };
            newCharacter.aimodelprompts = aiModelPrompts;

            _logger.LogInformation("Character Object:\n" + JsonSerializer.Serialize(newCharacter));

            cosmosContainer = _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDB__database"), CosmosContainer);
            ItemResponse<CharacterObject> response = await cosmosContainer.CreateItemAsync(newCharacter, new PartitionKey(campaignId));

            return new OkObjectResult(response.Resource);

        }
    }

}