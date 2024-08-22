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
                return await GetCampaignAsync(req);
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

            string campaignId = req.Query["id"].ToString();

            if (campaignId == null)
            {
                return new BadRequestObjectResult("Please provide an ID for the campaign");
            }
            else
            {

                Container cosmosContainer = _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDbDatabaseName"), "Campaigns");
                try
                {

                    ItemResponse<Object> response = await cosmosContainer.ReadItemAsync<Object>(campaignId, new PartitionKey(campaignId));

                    CampaignClass campaign = JsonSerializer.Deserialize<CampaignClass>(response.Resource.ToString());
                    return new OkObjectResult(campaign);

                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("Campaign not found for id: {0}", campaignId);
                    return new NotFoundResult();
                }
            }

        }

        public async Task<IActionResult> CreateCampaignAsync(HttpRequest req)
        {

            CampaignClass newCampaign = new CampaignClass
            {
                // Generate a unique ID for the campaign
                id = Guid.NewGuid().ToString("N").Substring(0, 8),
                status = "creating",
                create_date = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                world = {},
                locale = {},
            };

            // Save the campaign to the Cosmos DB container
            Container cosmosContainer = _cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDbDatabaseName"), "Campaigns");
            ItemResponse<CampaignClass> response = await cosmosContainer.CreateItemAsync(newCampaign, new PartitionKey(newCampaign.id));

            return new OkObjectResult(response.Resource);

        }

    }
}

public class CampaignClass
{
    public string id { get; set; }
    public string status { get; set; }
    public string create_date { get; set; }
    public string name { get; set; }
    public string plot { get; set; }
    public World world { get; set; }
    public Locale locale { get; set; }
    public List<Location> locations { get; set; }
    public List<Character> characters { get; set; }

    public CampaignClass()
    {
        // Initialize lists to avoid null reference issues
        locations = new List<Location>();
        characters = new List<Character>();
    }
}

public class World
{
    public string world_name { get; set; }
    public string world_image_url { get; set; }
}

public class Locale
{
    public string locale_name { get; set; }
    public string locale_image_url { get; set; }
}

public class Location
{
    public string location_id { get; set; }
    public string location_name { get; set; }
    public string location_image_url { get; set; }
}

public class Character
{
    public string character_id { get; set; }
    public string character_name { get; set; }
    public string character_class { get; set; }
    public string character_image_url { get; set; }
}