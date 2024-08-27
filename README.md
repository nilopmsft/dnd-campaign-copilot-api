## D&D CampAIgn Copilot

This project is for an openhack to learn Azure Cosmos DB and Azure OpenAI services and configurations. We are using the various LLM capabilities in Azure OpenAI to help us create a campaign for Dungeons and Dragons with all details, images and storing them in Cosmos for retention. At the end it could assist a Dungeon Master to take a campaign to a group and begin their quest.

This repo holds all the API logic and changefeed processors for cosmos that are related to building (click on each for examples):

- [Campaign](./resources/examples/campaign.json)
  - This is the main object created that will hold the campaign ID and high level reference data of each child object. That data is populated by a change feed processor.
- [Character](./resources/examples/character.json)
  - A character of a unique class and potentially race. This will include common stats, abilities and traits you would see in a D&D 5th Edition campaign. This is a child of a campaign
- [World](./resources/examples/world.json)
  - A Full world (think planet earth) that has some level of history/lore and maybe some descriptions of locales. This is a child of a campaign
- [Locale](./resources/examples/locale.json)
  - A greater area in the world (think major city) that has various unique locations. This is a child of a world
- [Location](./resources/examples/location.json)
  - This is a location within a locale (think church, graveyard, tavern, cathedral, lagoon, etc). Typically a locale will have a few unique locations. This is a child of a locale.


### Cosmos Configuration

The project expects a single database, **dnd** with a collection for each object type and a Leases container for the Change Feed Processors:

- **Campaigns**- Partition Key: /id
- **Characters**- Partition Key: /campaignId
- **Worlds**- Partition Key: /campaignId
- **Locales**- Partition Key: /campaignId
- **Locations**- Partition Key: /campaignId
- **Leases**- Partition Key: /id

We chose to use a shared provisioned resources due to traffice levels (i.e. RU's at the database level)


### App Configuration

For local (and cloud deployment), will want to ensure that these properties are provided in your local.settings.json in your project, or provided in an Azure Function App deployment.

```
{
  "Values": {
    ...
    "CosmosDbEndpointUrl": "https://<Cosmos Endpoint>.azure.com:443/",
    "CosmosDbPrimaryKey": "<Cosmos Key>",
    "CosmosDbDatabaseName": "<Cosmos DB>",
    "CosmosDbFullConnectionURL": "AccountEndpoint=https://<Cosmos Endpoint>.azure.com:443/;AccountKey=<Some Account Key>==;",
    "AzureAiCompletionEndpoint": "https://<Azure OpenAI Endpoint>.azure.com",
    "AzureAiCompletionApiKey": "<Azure OpenAI Deployment Key>",
    "AzureAiCompletionDeployment": "<Deployment Name>"
  }
}
```

### Note

This is a fun project and understand that the accuracy and effectiveness of characters or locations are simply from the mind of the AI LLM. In no way are we suggesting the existing D&D resources are inferior and is simply for fun and learning. Who knows the results of building this could lead to an exciting adventure.