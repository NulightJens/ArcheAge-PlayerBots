using Newtonsoft.Json;

namespace AAEmu.Game.Bots.Content.Rotations;

public sealed class BotRotationDefinition
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("archetype")]
    public string Archetype { get; set; }

    [JsonProperty("meta")]
    public BotRotationMeta Meta { get; set; } = new();

    [JsonProperty("skills")]
    public Dictionary<string, uint> Skills { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonProperty("default")]
    public List<BotRotationRow> Default { get; set; } = [];

    [JsonProperty("rules")]
    public List<BotRotationRule> Rules { get; set; } = [];

    [JsonExtensionData]
    public IDictionary<string, Newtonsoft.Json.Linq.JToken> ExtensionData { get; set; } =
        new Dictionary<string, Newtonsoft.Json.Linq.JToken>(StringComparer.OrdinalIgnoreCase);

}

public sealed class BotRotationMeta
{
    [JsonProperty("role")]
    public string Role { get; set; }

    [JsonProperty("range")]
    public string Range { get; set; }

    [JsonProperty("homeAnchorSkill", NullValueHandling = NullValueHandling.Ignore)]
    public string HomeAnchorSkill { get; set; }
}

public sealed class BotRotationRow
{
    [JsonProperty("action")]
    public string Action { get; set; }

    [JsonProperty("skill")]
    public string Skill { get; set; }

    [JsonProperty("rel")]
    public float Relevance { get; set; }

    [JsonProperty("weight")]
    public float Weight { get; set; } = 1f;

    [JsonProperty("as")]
    public string As { get; set; }

    [JsonProperty("castWhileControlled")]
    public bool CastWhileControlled { get; set; }

    [JsonProperty("when", NullValueHandling = NullValueHandling.Ignore)]
    public BotRotationWhen When { get; set; }

    [JsonProperty("combo", NullValueHandling = NullValueHandling.Ignore)]
    public BotRotationCombo Combo { get; set; }

    [JsonProperty("ignoreGlobalDelay", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool IgnoreGlobalDelay { get; set; }

    [JsonProperty("chain", NullValueHandling = NullValueHandling.Ignore)]
    public BotRotationChain Chain { get; set; }

    [JsonExtensionData]
    public IDictionary<string, Newtonsoft.Json.Linq.JToken> ExtensionData { get; set; } =
        new Dictionary<string, Newtonsoft.Json.Linq.JToken>(StringComparer.OrdinalIgnoreCase);
}

public sealed class BotRotationCombo
{
    [JsonProperty("opener")]
    public string Opener { get; set; }

    [JsonProperty("followUp")]
    public string FollowUp { get; set; }
}

public sealed class BotRotationChain
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("stage")]
    public int Stage { get; set; }

    [JsonExtensionData]
    public IDictionary<string, Newtonsoft.Json.Linq.JToken> ExtensionData { get; set; } =
        new Dictionary<string, Newtonsoft.Json.Linq.JToken>(StringComparer.OrdinalIgnoreCase);
}

public sealed class BotRotationRule
{
    [JsonProperty("when")]
    public BotRotationWhen When { get; set; } = new();

    [JsonProperty("then")]
    public List<BotRotationRow> Then { get; set; } = [];
}

public sealed class BotRotationWhen
{
    [JsonProperty("kind")]
    public string Kind { get; set; }

    [JsonExtensionData]
    public IDictionary<string, Newtonsoft.Json.Linq.JToken> Arguments { get; set; } =
        new Dictionary<string, Newtonsoft.Json.Linq.JToken>(StringComparer.OrdinalIgnoreCase);

    [JsonProperty("children")]
    public List<BotRotationWhen> Children { get; set; } = [];
}

public sealed record BotRotationValidationError(string Code, string Message);
