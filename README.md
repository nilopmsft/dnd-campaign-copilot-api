This is an openhack project where we are leveraging Azure OpenAI (LLM's) to help us generate a D&D Campaign and be our Dungeon Master Copilot! This is the API portion of the project that will manage the backend experience of doing AI Generation and just managing the campaign for players.

For local (and cloud deployment), will want to ensure that these properties are provided in your local.settings.json in your project, or provided in an Azure Function App deployment.

```
{
  "IsEncrypted": false,
  "Values": {
    ...
    "CosmosDbEndpointUrl": "https://<Cosmos Endpoint>.azure.com:443/",
    "CosmosDbPrimaryKey": "<Cosmos Key>",
    "CosmosDbDatabaseName": "<Cosmos DB>",
    "AzureAiCompletionEndpoint": "https://<Azure OpenAI Endpoint>.azure.com",
    "AzureAiCompletionApiKey": "<Azure OpenAI Deployment Key>",
    "AzureAiCompletionDeployment": "<Deployment Name>"
  }
}
```