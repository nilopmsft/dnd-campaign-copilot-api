using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CampaignCopilot
{

    public class LocaleChangeFeedProcessor(ILogger<LocaleObject> logger, CosmosClient cosmosClient)
    {

        private readonly ILogger<LocaleObject> _logger = logger;
        private readonly CosmosClient _cosmosClient = cosmosClient;
        string CosmosContainer = "Campaigns";

        [Function("LocaleChangeFeedProcessor")]

        public async Task Run([CosmosDBTrigger(
          databaseName: "dnd",
          containerName: "Locales",
          Connection = "CosmosDbFullConnectionURL",
          LeaseContainerName = "Leases",
          CreateLeaseContainerIfNotExists = true,
          LeaseContainerPrefix = "localesCFP_",
          FeedPollDelay = 1000
          )] IReadOnlyList<LocaleObject> input)
        {

            if (input != null && input.Count > 0)
            {

                _logger.LogInformation("Documents modified: " + input.Count);

                foreach (LocaleObject localeObject in input)
                {

                    _logger.LogInformation("Locale Id: " + localeObject.id);
                    _logger.LogInformation("Campaign_ID: " + localeObject.campaignId);

                    string campaignId = localeObject.campaignId;
                    LocaleReference locale = new LocaleReference
                    {
                        id = localeObject.id,
                        name = localeObject.name,
                        type = localeObject.localeType,
                        parentId = localeObject.worldId,
                        imageUrl = localeObject.imageUrl
                    };

                    ItemResponse<CampaignObject> response = await _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDbDatabase"), CosmosContainer).PatchItemAsync<CampaignObject>(
                        id: campaignId,
                        partitionKey: new PartitionKey(campaignId),
                        patchOperations: [
                            PatchOperation.Add("/locales/-", locale)
                        ]
                    );

                    _logger.LogInformation("Patch Status: " + response.StatusCode);
                    _logger.LogInformation("Patch Cost: " + response.RequestCharge);
                }
            }
        }
    }

}