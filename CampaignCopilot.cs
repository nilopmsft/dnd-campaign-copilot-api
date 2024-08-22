using System.Text.Json;

namespace CampaignCopilot 
{
    public class AiModelInfo {
        public string ModelDeployment { get; set; }
        public string ModelEndpoint { get; set; }
    }

    public class AiModelPrompts {
        public string SystemPrompt { get; set; }
        public string UserPrompt { get; set; }
        public string ?StructurePrompt { get; set; }
        public AiModelPrompts() { }

        public AiModelPrompts(string promptFile)
        {
            // Load the JSON file content
            string jsonString = File.ReadAllText("resources/prompts/" + promptFile + ".json");

            // Deserialize the JSON into an instance of AiModelPrompts
            AiModelPrompts modelPrompts = JsonSerializer.Deserialize<AiModelPrompts>(jsonString);

            // Copy the deserialized properties to the current instance
            SystemPrompt = modelPrompts.SystemPrompt;
            UserPrompt = modelPrompts.UserPrompt;
            StructurePrompt = modelPrompts.StructurePrompt;
        }
    }
    
    public class WorldCompletion {
        public string name { get; set; }
        public string description { get; set; }
    }

    public class WorldObject {
        public string id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string imageUrl { get; set; }
        public string campaignId { get; set; }
        public AiModelInfo aimodelinfo { get; set; }
        public AiModelPrompts aimodelprompts { get; set; }
     }

     public class LocaleCompletion {
        public string name { get; set; }
        public string description { get; set; }
        public string localeType { get; set; }
     }

     public class LocaleObject {
        public string id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string localeType { get; set; }
        public string imageURL { get; set; }
        public string campaignId { get; set; }
        public string worldId { get; set; }
        public AiModelInfo aimodelinfo { get; set; }
        public AiModelPrompts aimodelprompts { get; set; }
     }   
}