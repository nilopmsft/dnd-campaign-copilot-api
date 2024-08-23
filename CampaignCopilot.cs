using System.Text.Json;

namespace CampaignCopilot
{

    public class CampaignClass
    {
        public string id { get; set; }
        public string status { get; set; }
        public string create_date { get; set; }
        public string name { get; set; }
        public string plot { get; set; }
        public List<WorldList> worlds { get; set; }
        public List<LocaleList> locales { get; set; }
        public List<LocationList> locations { get; set; }
        public List<CharacterList> characters { get; set; }

        public CampaignClass()
        {
            // Initialize lists to avoid null reference issues
            worlds = new List<WorldList>();
            locales = new List<LocaleList>();
            locations = new List<LocationList>();
            characters = new List<CharacterList>();
        }
    }

    public class WorldList
    {
        public string world_name { get; set; }
        public string world_image_url { get; set; }
    }

    public class LocaleList
    {
        public string locale_name { get; set; }
        public string locale_image_url { get; set; }
    }

    public class LocationList
    {
        public string location_id { get; set; }
        public string location_name { get; set; }
        public string location_image_url { get; set; }
    }

    public class CharacterList
    {
        public string character_id { get; set; }
        public string character_name { get; set; }
        public string character_class { get; set; }
        public string character_image_url { get; set; }
    }

    public class AiModelInfo
    {
        public string ModelDeployment { get; set; }
        public string ModelEndpoint { get; set; }
    }

    public class AiModelPrompts
    {
        public string SystemPrompt { get; set; }
        public string UserPrompt { get; set; }
        public string? StructurePrompt { get; set; }
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

    public class WorldCompletion
    {
        public string name { get; set; }
        public string description { get; set; }
    }

    public class WorldObject
    {
        public string id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string imageUrl { get; set; }
        public string campaignId { get; set; }
        public AiModelInfo aimodelinfo { get; set; }
        public AiModelPrompts aimodelprompts { get; set; }
    }

    public class LocaleCompletion
    {
        public string name { get; set; }
        public string description { get; set; }
        public string type { get; set; }
    }

    public class LocaleObject
    {
        public string id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string localeType { get; set; }
        public string imageURL { get; set; }
        public string worldId { get; set; }
        public string campaignId { get; set; }
        public AiModelInfo aimodelinfo { get; set; }
        public AiModelPrompts aimodelprompts { get; set; }
    }

    public class LocationCompletion
    {
        public string name { get; set; }
        public string description { get; set; }
        public string type { get; set; }
    }

    public class LocationObject
    {
        public string id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string locationType { get; set; }
        public string imageURL { get; set; }
        public string localeId { get; set; }
        public string campaignId { get; set; }
        public AiModelInfo aimodelinfo { get; set; }
        public AiModelPrompts aimodelprompts { get; set; }
    }
}