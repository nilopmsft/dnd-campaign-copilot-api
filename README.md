# D&D CampAIgn Copilot

![Deploy to Azure](https://aka.ms/deploytoazurebutton)

This project started as an openhack to learn Azure Cosmos DB and Azure OpenAI services and their respective configurations. We are using the various LLM capabilities in Azure OpenAI to help us create a campaign for Dungeons and Dragons with all details, images and storing details in Cosmos for retention. At the end it could assist a Dungeon Master to take a campaign to a group and begin their quest.

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


## Cosmos Configuration

The project expects a single database, **dnd** with a collection for each object type and a Leases container for the Change Feed Processors:

- **Campaigns**- Partition Key: /id
- **Characters**- Partition Key: /campaignId
- **Worlds**- Partition Key: /campaignId
- **Locales**- Partition Key: /campaignId
- **Locations**- Partition Key: /campaignId
- **Leases**- Partition Key: /id

We chose to use a shared provisioned resources due to traffice levels (i.e. RU's at the database level)


## App Configuration

### Authentication

For security purposes, we use Managed Identities to authenticate against the various services (Cosmos, Blob Storage, Azure AI). For local development you can utilize your own login to test.
Review [here](https://learn.microsoft.com/en-us/dotnet/azure/sdk/authentication/local-development-dev-accounts?tabs=azure-portal%2Csign-in-visual-studio%2Ccommand-line#3---sign-in-to-azure-using-developer-tooling) for your dev environment. For a deployed Function App, you can utilize System Assigned or User Assigned Managed Identities, both having their own benefits. The permissions needed for both user/group and Managed Identity authentication of services are

### Permissions 
**Cosmos DB**: [Data Plane Access](https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/security/how-to-grant-data-plane-role-based-access?tabs=built-in-definition%2Ccsharp&pivots=azure-interface-cli#permission-model)

**Blob Storage**: 'Storage Blob Data Contributor' Role

**Azure AI Service**: 'Cognitive Services OpenAI User' Role (this is at the Azure Service level where the model deployments exist)

### Environment Variables

For local (and cloud deployment), will want to ensure that these properties are provided in your local.settings.json in your project, or provided in an Azure Function App deployment.

```
{
  "Values": {
    ...
    "CosmosDB__accountEndpoint": "https://<CosmosDbAccount>.documents.azure.com:443/",
    "CosmosDB__database": "<CosmosDatabase>",
    "BlobStorage__accountEndpoint":"https://<StorageAccount>.blob.core.windows.net/",
    "BlobStorage__container":"<Container>",
    "AzureAi__accountEndpoint": "https://<AiServiceUri>.openai.azure.com/",
    "AzureAi__textDeployment": "<TextDeploymentName>",
    "AzureAi__imageDeployment":"<ImageDeploymentName>"
  }
}
```

- Published Function

The below Az CLI can make it easy to update the function app to have the environment variables needed as local settings are not published. You will need to adjust the values accordingly. Running this does take a bit of time so be patient.

```
APP_NAME=<app name>
RESOURCE_GROUP=<resource group>
# REPEAT BELOW FOR EACH SETTING AND VALUE
az functionapp config appsettings set --name $APP_NAME --resource-group $RESOURCE_GROUP --settings "<SettingName>=<Value>"
```

- User Assigned Managed Identity (optional)

If you are using User Assigned Managed Identities for the authentication (see above), you will need to provide the AZURE_CLIENT_ID environment variable on the function (not needed for local testing). Ensure you provide the **Client ID** of the Managed Identity. Otherwise you might see an error, "Unable to load the proper Managed Identity Azure Function App"

- Github Actions with MI (optional)

This expands upon the User Assigned Managed Identity above where rather than using a Shared Access Key for the storage account publishing builds to, we can use the Managed Identity for authorization. Useful information on this topic is provided [here](https://github.com/azure/functions-action?tab=readme-ov-file#manged-identities-for-storage-account-access-and-package-deployments-on-linux-consumption-sku)

## API Calls

Every object has a parent object, at the very least its the Campaign itself (unless it is the campaigign being created). This can effect how you lookup or create an object. Each object requires the campaignId. This means:

- To look up an object, you need the objectId and the campaignId. It is a GET request to `/api/<object>?'object'Id=<objectId>&camapaignId=<campaignId>`
  - EXAMPLE OF Locale lookup: GET `/api/Locale?localeId=abc&campaignId=123`
- To create an object, you need the parentId and the campaignId. For a world the Campaign is the parent. For a campaign there is no parent. It is a POST request to `/api/<object>?'parent'Id=<parentId>&camapaignId=<campaignId>` 
  - EXAMPLE Of Locale creation: POST `/api/Locale?worldId=def&campaignId=123`

The reason for the campaignID is that we need that to tie back to the campaign when updating the campaign sheet upon object creation as well as all collections are partitioned by campaignId so its easy to identify which objects exist for a campaign.

## Note

This is a fun project and understand that the accuracy and realistic nature of characters or locations are simply from the mind of an AI LLM. In no way are we suggesting the existing D&D resources are inferior or this is a replacement. This project is simply for fun and learning. Who knows the results of building this could lead to an exciting adventure.
