using System.Text.Json;

namespace CampaignCopilot
{

    public class CampaignObject
    {
        public string id { get; set; }
        public string status { get; set; }
        public string create_date { get; set; }
        public string name { get; set; }
        public string plot { get; set; }
        public List<WorldReference> worlds { get; set; }
        public List<LocaleReference> locales { get; set; }
        public List<LocationReference> locations { get; set; }
        public List<CharacterReference> characters { get; set; }

        public CampaignObject()
        {
            // Initialize lists to avoid null reference issues
            worlds = new List<WorldReference>();
            locales = new List<LocaleReference>();
            locations = new List<LocationReference>();
            characters = new List<CharacterReference>();
        }
    }
    public class WorldReference
    {
        public string id { get; set; }
        public string name { get; set; }
        public string parentId { get; set; }
        public string imageUrl { get; set; }

    }

    public class LocaleReference
    {
        public string id { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public string parentId { get; set; }
        public string imageUrl { get; set; }

    }
    public class LocationReference
    {
        public string id { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public string parentId { get; set; }
        public string imageUrl { get; set; }

    }
    public class CharacterReference
    {
        public string id { get; set; }
        public string name { get; set; }
        public string race { get; set; }
        public string character_class { get; set; }
        public string parentId { get; set; }
        public string imageUrl { get; set; }

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
        public string DallePrompt { get; set; }

        public string? StructurePrompt { get; set; }
        public AiModelPrompts() { }

        public AiModelPrompts(string promptFile)
        {
            // Load the JSON file content
            // string jsonString = File.ReadAllText("resources/prompts/" + promptFile + ".json");
            string jsonString = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "resources/prompts", promptFile + ".json"));

            // Deserialize the JSON into an instance of AiModelPrompts
            AiModelPrompts modelPrompts = JsonSerializer.Deserialize<AiModelPrompts>(jsonString);

            // Copy the deserialized properties to the current instance
            SystemPrompt = modelPrompts.SystemPrompt;
            UserPrompt = modelPrompts.UserPrompt;
            StructurePrompt = modelPrompts.StructurePrompt;
            DallePrompt = modelPrompts.DallePrompt;

        }
    }

    public class WorldCompletion
    {
        public string name { get; set; }
        public string description { get; set; }
        public string dalleprompt { get; set; }
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
        public string dalleprompt { get; set; }

    }

    public class LocaleObject
    {
        public string id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string localeType { get; set; }
        public string imageUrl { get; set; }
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
        public string dalleprompt { get; set; }

    }

    public class LocationObject
    {
        public string id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string locationType { get; set; }
        public string imageUrl { get; set; }
        public string localeId { get; set; }
        public string campaignId { get; set; }
        public AiModelInfo aimodelinfo { get; set; }
        public AiModelPrompts aimodelprompts { get; set; }
    }

    public class CharacterObject
    {
        public CharacterObject() {
            id = "";
            name = "";
            dalleprompt = "";
            backstory = "";
            imageUrl = "";
            campaignId = "";

            definition = new Definition();
            stats = new Stats();
            saving_throws = new SavingThrows();
            skills = new Skills();
            health = new Health();
            attacks_and_spellcasting = new List<AttacksAndSpellCasting>();
            personality = new Personality();
            features_and_traits = new List<string>();
            aimodelinfo = new AiModelInfo();
            aimodelprompts = new AiModelPrompts();
        }
        public string id { get; set; }
        public string name { get; set; }
        public string dalleprompt { get; set; }
        public string backstory { get; set; }
        public string imageUrl { get; set; }
        public string campaignId { get; set; }
        public Definition definition { get; set; }
        public Stats stats { get; set; }
        public SavingThrows saving_throws { get; set; }
        public Skills skills { get; set; }
        public Health health { get; set; }
        public List<AttacksAndSpellCasting> attacks_and_spellcasting { get; set; }
        public Personality personality { get; set; }
        public List<string> features_and_traits { get; set; }
        public AiModelInfo aimodelinfo { get; set; }
        public AiModelPrompts aimodelprompts { get; set; }
    }

    public class Definition
    {
        public Definition() { 
            character_class = "";
            level = "";
            race = "";
            background = "";
            alignment = "";
            age = "";
            height = "";
            weight = "";
            hair = "";
        }

        public string character_class { get; set; }
        public string level { get; set; }
        public string race { get; set; }
        public string background { get; set; }
        public string alignment { get; set; }
        public string age { get; set; }
        public string height { get; set; }
        public string weight { get; set; }
        public string hair { get; set; }
    }

    public class Stats
    {
        public Stats() { 
            strength = "";
            dexterity = "";
            constitution = "";
            intelligence = "";
            wisdom = "";
            charisma = "";
        }
        public string strength { get; set; }
        public string dexterity { get; set; }
        public string constitution { get; set; }
        public string intelligence { get; set; }
        public string wisdom { get; set; }
        public string charisma { get; set; }
    }

    public class SavingThrows
    {
        public SavingThrows() {
            strength = "";
            dexterity = "";
            constitution = "";
            intelligence = "";
            wisdom = "";
            charisma = "";
        }
        public string strength { get; set; }
        public string dexterity { get; set; }
        public string constitution { get; set; }
        public string intelligence { get; set; }
        public string wisdom { get; set; }
        public string charisma { get; set; }
    }

    public class Skills
    {
        public Skills() {
            acrobatics = "";
            animal_handling = "";
            arcana = "";
            athletics = "";
            deception = "";
            history = "";
            insight = "";
            intimidation = "";
            investigation = "";
            medicine = "";
            nature = "";
            perception = "";
            performance = "";
            persuasion = "";
            religion = "";
            sleight_of_hand = "";
            stealth = "";
            survival = "";
        }
        public string acrobatics { get; set; }
        public string animal_handling { get; set; }
        public string arcana { get; set; }
        public string athletics { get; set; }
        public string deception { get; set; }
        public string history { get; set; }
        public string insight { get; set; }
        public string intimidation { get; set; }
        public string investigation { get; set; }
        public string medicine { get; set; }
        public string nature { get; set; }
        public string perception { get; set; }
        public string performance { get; set; }
        public string persuasion { get; set; }
        public string religion { get; set; }
        public string sleight_of_hand { get; set; }
        public string stealth { get; set; }
        public string survival { get; set; }
    }

    public class Health
    {
        public Health() {
            armor_class = "";
            initiative = "";
            speed = "";
            maximum_hit_points = "";
            current_hit_points = "";
            hit_dice = "";
            death_saves = "";
        }
        public string armor_class { get; set; }
        public string initiative { get; set; }
        public string speed { get; set; }
        public string maximum_hit_points { get; set; }
        public string current_hit_points { get; set; }
        public string hit_dice { get; set; }
        public string death_saves { get; set; }
    }

    public class AttacksAndSpellCasting
    {
        public AttacksAndSpellCasting() { 
            name = "";
            attack_bonus = "";
            damage_type = "";
        }

        public string name { get; set; }
        public string attack_bonus { get; set; }
        public string damage_type { get; set; }
    }

    public class Personality
    {
        public Personality() {
            personality_traits = "";
            ideals = "";
            bonds = "";
            flaws = "";
        }
        public string personality_traits { get; set; }
        public string ideals { get; set; }
        public string bonds { get; set; }
        public string flaws { get; set; }
    }

    public class CharacterInfo
    {
        public CharacterInfo() {
            name = "";
            character_class = "";
        }
        public string name { get; set; }
        public string character_class { get; set; }
        public string race { get; set; }
    }

}
