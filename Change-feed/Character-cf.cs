using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CampaignCopilot
{

    public class CharacterChangeFedProcessor(ILogger<CharacterObject> logger, CosmosClient cosmosClient)
    {

        private readonly ILogger<CharacterObject> _logger = logger;
        private readonly CosmosClient _cosmosClient = cosmosClient;
        string CosmosContainer = "Campaigns";

        [Function("CharacterChangeFeedProcessor")]

        public async Task Run([CosmosDBTrigger(
          databaseName: "dnd",
          containerName: "Characters",
          LeaseContainerName = "Leases",
          CreateLeaseContainerIfNotExists = true,
          LeaseContainerPrefix = "charactersCFP_",
          FeedPollDelay = 1000
          )] IReadOnlyList<CharacterObject> input)
        {

            if (input != null && input.Count > 0)
            {

                _logger.LogInformation("Documents modified: " + input.Count);
                
                foreach (CharacterObject characterObject in input)
                {
      
                    _logger.LogInformation("Character Id: " + characterObject.id);
                    _logger.LogInformation("Campaign_ID: " + characterObject.campaignId);

                    string campaignId = characterObject.campaignId;
                    CharacterReference character = new CharacterReference
                    {
                        id = characterObject.id,
                        name = characterObject.name,
                        race = characterObject.definition.race,
                        character_class = characterObject.definition.character_class,
                        parentId = characterObject.campaignId,
                        imageUrl = characterObject.imageUrl
                    };

                    ItemResponse<CampaignObject> response = await _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDbDatabase"), CosmosContainer).PatchItemAsync<CampaignObject>(
                        id: campaignId,
                        partitionKey: new PartitionKey(campaignId),
                        patchOperations: [
                            PatchOperation.Add("/characters/-", character)
                        ]
                    );

                    _logger.LogInformation("Patch Status: " + response.StatusCode);
                    _logger.LogInformation("Patch Cost: " + response.RequestCharge);
                }
            }
        }
    }

}