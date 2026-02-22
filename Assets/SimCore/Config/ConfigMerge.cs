using System.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

public static class ConfigMerge
{
    private static readonly JsonSerializer SafeSerializer = JsonSerializer.Create(new JsonSerializerSettings
    {
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        ContractResolver = new WritableMembersOnlyContractResolver()
    });

    public static ScenarioConfig CreateBaseDefaults()
    {
        var config = new ScenarioConfig();
        config.NormalizeAliases();
        return config;
    }

    public static ScenarioConfig Merge(ScenarioConfig defaults, string presetJson)
    {
        var defaultsObject = JObject.FromObject(defaults ?? CreateBaseDefaults(), SafeSerializer);

        if (!string.IsNullOrWhiteSpace(presetJson))
        {
            var presetObject = JObject.Parse(presetJson);
            DeepMerge(defaultsObject, presetObject);
        }

        var merged = defaultsObject.ToObject<ScenarioConfig>() ?? CreateBaseDefaults();
        merged.NormalizeAliases();
        return merged;
    }

    public static string ToPrettyJson(ScenarioConfig config)
    {
        config ??= CreateBaseDefaults();
        config.NormalizeAliases();
        return JsonConvert.SerializeObject(config, Formatting.Indented, new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            ContractResolver = new WritableMembersOnlyContractResolver()
        });
    }

    private static void DeepMerge(JObject target, JObject source)
    {
        foreach (var property in source.Properties())
        {
            if (property.Value is JObject sourceObject)
            {
                if (target[property.Name] is not JObject targetObject)
                {
                    targetObject = new JObject();
                    target[property.Name] = targetObject;
                }

                DeepMerge(targetObject, sourceObject);
            }
            else
            {
                target[property.Name] = property.Value;
            }
        }
    }

    private sealed class WritableMembersOnlyContractResolver : DefaultContractResolver
    {
        protected override System.Collections.Generic.IList<JsonProperty> CreateProperties(System.Type type, MemberSerialization memberSerialization)
        {
            var properties = base.CreateProperties(type, memberSerialization);
            return properties.Where(property => property.Writable || !property.Readable).ToList();
        }
    }
}
