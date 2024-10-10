using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CampaignCopilot
{

    public class LocationChangeFeedProcessor(ILogger<LocationObject> logger, CosmosClient cosmosClient)
    {

        private readonly ILogger<LocationObject> _logger = logger;
        private readonly CosmosClient _cosmosClient = cosmosClient;
        string CosmosContainer = "Campaigns";

        [Function("LocationChangeFeedProcessor")]

        public async Task Run([CosmosDBTrigger(
          databaseName: "dnd",
          containerName: "Locations",
          LeaseContainerName = "Leases",
          CreateLeaseContainerIfNotExists = true,
          LeaseContainerPrefix = "locationsCFP_",
          FeedPollDelay = 1000
          )] IReadOnlyList<LocationObject> input)
        {

            if (input != null && input.Count > 0)
            {

                _logger.LogInformation("Documents modified: " + input.Count);
                
                foreach (LocationObject locationObject in input)
                {
      
                    _logger.LogInformation("Location Id: " + locationObject.id);
                    _logger.LogInformation("Campaign_ID: " + locationObject.campaignId);

                    string campaignId = locationObject.campaignId;
                    LocationReference location = new LocationReference
                    {
                        id = locationObject.id,
                        name = locationObject.name,
                        type = locationObject.locationType,
                        parentId = locationObject.localeId,
                        imageUrl = locationObject.imageUrl
                    };

                    ItemResponse<CampaignObject> response = await _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDB__database"), CosmosContainer).PatchItemAsync<CampaignObject>(
                        id: campaignId,
                        partitionKey: new PartitionKey(campaignId),
                        patchOperations: [
                            PatchOperation.Add("/locations/-", location)
                        ]
                    );

                    _logger.LogInformation("Patch Status: " + response.StatusCode);
                    _logger.LogInformation("Patch Cost: " + response.RequestCharge);
                }
            }
        }
    }

}