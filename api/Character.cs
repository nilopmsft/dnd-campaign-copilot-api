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

    public class Character(ILogger<CharacterObject> logger, CosmosClient cosmosClient, AzureOpenAIClient openaiClient)
    {

        private readonly ILogger<CharacterObject> _logger = logger;
        private readonly CosmosClient _cosmosClient = cosmosClient;
        private readonly AzureOpenAIClient _openaiClient = openaiClient;
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

            Container cosmosContainer = _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDbDatabaseName"), CosmosContainer);
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
            Container cosmosContainer = _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDbDatabaseName"), CosmosContainer);
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

            ChatClient chatClient = _openaiClient.GetChatClient(Environment.GetEnvironmentVariable("AzureAiCompletionDeployment"));
            ChatCompletion completion = chatClient.CompleteChat(
            [
                new SystemChatMessage(aiModelPrompts.SystemPrompt),
                new UserChatMessage(aiModelPrompts.UserPrompt),
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
                    return new BadRequestObjectResult("Invalid Location JSON format in response:" + jsonResponse);
                }
            }
            else
            {
                _logger.LogError("Invalid JSON format in response");
                return new StatusCodeResult(500);
            }

            newCharacter.id = Guid.NewGuid().ToString("N").Substring(0, 8);
            newCharacter.campaignId = campaignId;
            newCharacter.aimodelinfo = new AiModelInfo
            {
                ModelDeployment = Environment.GetEnvironmentVariable("AzureAiCompletionDeployment"),
                ModelEndpoint = Environment.GetEnvironmentVariable("AzureAiCompletionEndpoint")
            };
            newCharacter.aimodelprompts = aiModelPrompts;

            _logger.LogInformation("Character Object:\n" + JsonSerializer.Serialize(newCharacter));

            cosmosContainer = _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDbDatabaseName"), CosmosContainer);
            ItemResponse<CharacterObject> response = await cosmosContainer.CreateItemAsync(newCharacter, new PartitionKey(campaignId));

            return new OkObjectResult(response.Resource);

        }
    }

    public class CharacterObject
    {
        public string id { get; set; }
        public string name { get; set; }
        public string backstory { get; set; }
        public string imageUrl { get; set; }
        public string campaignId { get; set; }
        public Definition definition { get; set; }
        public Stats stats { get; set; }
        public SavingThrows saving_throws { get; set; }
        public Skills skills { get; set; }
        public Health health { get; set; }
        public List<AttacksAndSpellCasting> attacks_and_spellcasting { get; set; }
        public Personality personality { get; set; }
        public List<string> features_and_traits { get; set; }
        public AiModelInfo aimodelinfo { get; set; }
        public AiModelPrompts aimodelprompts { get; set; }
    }

    public class Definition
    {
        public string character_class { get; set; }
        public string level { get; set; }
        public string race { get; set; }
        public string background { get; set; }
        public string alignment { get; set; }
        public string age { get; set; }
        public string height { get; set; }
        public string weight { get; set; }
        public string hair { get; set; }
    }

    public class Stats
    {
        public string strength { get; set; }
        public string dexterity { get; set; }
        public string constitution { get; set; }
        public string intelligence { get; set; }
        public string wisdom { get; set; }
        public string charisma { get; set; }
    }

    public class SavingThrows
    {
        public string strength { get; set; }
        public string dexterity { get; set; }
        public string constitution { get; set; }
        public string intelligence { get; set; }
        public string wisdom { get; set; }
        public string charisma { get; set; }
    }

    public class Skills
    {
        public string acrobatics { get; set; }
        public string animal_handling { get; set; }
        public string arcana { get; set; }
        public string athletics { get; set; }
        public string deception { get; set; }
        public string history { get; set; }
        public string insight { get; set; }
        public string intimidation { get; set; }
        public string investigation { get; set; }
        public string medicine { get; set; }
        public string nature { get; set; }
        public string perception { get; set; }
        public string performance { get; set; }
        public string persuasion { get; set; }
        public string religion { get; set; }
        public string sleight_of_hand { get; set; }
        public string stealth { get; set; }
        public string survival { get; set; }
    }

    public class Health
    {
        public string armor_class { get; set; }
        public string initiative { get; set; }
        public string speed { get; set; }
        public string maximum_hit_points { get; set; }
        public string current_hit_points { get; set; }
        public string hit_dice { get; set; }
        public string death_saves { get; set; }
    }

    public class AttacksAndSpellCasting
    {
        public string name { get; set; }
        public string attack_bonus { get; set; }
        public string damage_type { get; set; }
    }

    public class Personality
    {
        public string personality_traits { get; set; }
        public string ideals { get; set; }
        public string bonds { get; set; }
        public string flaws { get; set; }
    }

    public class CharacterInfo
    {
        public string name { get; set; }
        public string character_class { get; set; }
        public string race { get; set; }
    }

}