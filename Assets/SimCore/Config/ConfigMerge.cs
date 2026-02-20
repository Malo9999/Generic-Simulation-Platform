using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class ConfigMerge
{
    public static ScenarioConfig CreateBaseDefaults()
    {
        var config = new ScenarioConfig();
        config.NormalizeAliases();
        return config;
    }

    public static ScenarioConfig Merge(ScenarioConfig defaults, string presetJson)
    {
        var defaultsObject = JObject.FromObject(defaults ?? CreateBaseDefaults());

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
        return JsonConvert.SerializeObject(config, Formatting.Indented);
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
}
