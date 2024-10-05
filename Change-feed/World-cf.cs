using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CampaignCopilot
{

    public class WorldChangeFeedProcessor(ILogger<WorldObject> logger, CosmosClient cosmosClient)
    {

        private readonly ILogger<WorldObject> _logger = logger;
        private readonly CosmosClient _cosmosClient = cosmosClient;
        string CosmosContainer = "Campaigns";

        [Function("WorldChangeFeedProcessor")]

        public async Task Run([CosmosDBTrigger(
          databaseName: "dnd",
          containerName: "Worlds",
          Connection = "CosmosDbFullConnectionURL",
          LeaseContainerName = "Leases",
          CreateLeaseContainerIfNotExists = true,
          LeaseContainerPrefix = "worldsCFP_",
          FeedPollDelay = 1000
          )] IReadOnlyList<WorldObject> input)
        {

            if (input != null && input.Count > 0)
            {

                _logger.LogInformation("Documents modified: " + input.Count);
                
                foreach (WorldObject worldObject in input)
                {
      
                    _logger.LogInformation("World Id: " + worldObject.id);
                    _logger.LogInformation("Campaign_ID: " + worldObject.campaignId);

                    string campaignId = worldObject.campaignId;
                    WorldReference world = new WorldReference
                    {
                        id = worldObject.id,
                        name = worldObject.name,
                        parentId = worldObject.campaignId,
                        imageUrl = worldObject.imageUrl
                    };

                    ItemResponse<CampaignObject> response = await _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDbDatabase"), CosmosContainer).PatchItemAsync<CampaignObject>(
                        id: campaignId,
                        partitionKey: new PartitionKey(campaignId),
                        patchOperations: [
                            PatchOperation.Add("/worlds/-", world)
                        ]
                    );

                    _logger.LogInformation("Patch Status: " + response.StatusCode);
                    _logger.LogInformation("Patch Cost: " + response.RequestCharge);
                }
            }
        }
    }

}