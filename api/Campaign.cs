using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using System.Text.Json;

namespace CampaignCopilot
{
    public class Campaign(ILogger<Campaign> logger, CosmosClient cosmosClient)
    {

        private readonly ILogger<Campaign> _logger = logger;
        private readonly CosmosClient _cosmosClient = cosmosClient;

        [Function("Campaign")]
        public async Task<IActionResult> CampaignAsync([HttpTrigger(AuthorizationLevel.Function, ["get","post"])] HttpRequest req)
        {

            if (req.Method == "GET")
            {
                if (string.IsNullOrEmpty(req.Query["campaignId"].ToString()))
                {
                    return new BadRequestObjectResult("Please provide a campaignId for the campaign");
                }
                else
                {
                    return await GetCampaignAsync(req);
                }
            }
            else if (req.Method == "POST")
            {
                return await CreateCampaignAsync(req);
            }
            else
            {
                return new BadRequestObjectResult("Please pass a GET or POST request");
            }
        }
        public async Task<IActionResult> GetCampaignAsync(HttpRequest req)
        {

            string campaignId = req.Query["campaignId"].ToString();


                Container cosmosContainer = _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDbDatabaseName"), "Campaigns");
                try
                {

                    ItemResponse<Object> response = await cosmosContainer.ReadItemAsync<Object>(campaignId, new PartitionKey(campaignId));

                    CampaignObject campaign = JsonSerializer.Deserialize<CampaignObject>(response.Resource.ToString());
                    return new OkObjectResult(campaign);

                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("Campaign not found for id: {0}", campaignId);
                    return new NotFoundResult();
                }
            }

        public async Task<IActionResult> CreateCampaignAsync(HttpRequest req)
        {

            CampaignObject newCampaign = new CampaignObject
            {
                // Generate a unique ID for the campaign
                id = Guid.NewGuid().ToString("N").Substring(0, 8),
                status = "creating",
                create_date = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                worlds = {},
                locales = {},
                locations = {},
                characters = {}
            };

            // Save the campaign to the Cosmos DB container
            Container cosmosContainer = _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDbDatabaseName"), "Campaigns");
            ItemResponse<CampaignObject> response = await cosmosContainer.CreateItemAsync(newCampaign, new PartitionKey(newCampaign.id));

            return new OkObjectResult(response.Resource);

        }

    }
}