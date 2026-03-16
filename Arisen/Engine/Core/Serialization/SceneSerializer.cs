using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ArisenEngine.Core.ECS;
using YamlDotNet.Serialization;

namespace ArisenEngine.Core.Serialization;

/// <summary>
/// Handles reading and writing an EntityManager's state to a readable YAML format for the Editor.
/// </summary>
public static class SceneSerializer
{
    private class SceneData
    {
        public List<EntityData> Entities { get; set; } = new();
    }

    private class EntityData
    {
        public int Id { get; set; }
        public Dictionary<string, object> Components { get; set; } = new();
    }

    /// <summary>
    /// Serializes all entities and their components into a YAML file at the specified path.
    /// </summary>
    public static void SaveScene(string path, EntityManager entityManager)
    {
        var sceneData = new SceneData();
        var allPools = entityManager.GetType()
            .GetField("m_ComponentPools", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(entityManager) as Dictionary<Type, IComponentPool>;

        if (allPools == null) return;

        // Group component data by EntityID
        var entityDict = new Dictionary<int, EntityData>();

        foreach (var kvp in allPools)
        {
            var poolType = kvp.Key;
            var pool = kvp.Value;
            
            // To iterate through a generic pool safely without boxing the entire array, 
            // since this is Editor-time YAML export, we accept a bit of reflection overhead.
            var getRawEntityArrayMethod = pool.GetType().GetMethod("GetRawEntityArray");
            var getCountProperty = pool.GetType().GetProperty("Count");

            if (getRawEntityArrayMethod != null && getCountProperty != null)
            {
                var entities = (Entity[])getRawEntityArrayMethod.Invoke(pool, null);
                int count = (int)getCountProperty.GetValue(pool);

                var getMethod = pool.GetType().GetMethod("Get");

                for (int i = 0; i < count; i++)
                {
                    var entity = entities[i];
                    if (!entityDict.TryGetValue(entity.Id, out var eData))
                    {
                        eData = new EntityData { Id = entity.Id };
                        entityDict[entity.Id] = eData;
                    }

                    // Get the struct data using pool.Get(entity)
                    var componentData = getMethod.Invoke(pool, new object[] { entity });
                    
                    eData.Components[poolType.FullName] = componentData;
                }
            }
        }

        sceneData.Entities = entityDict.Values.OrderBy(e => e.Id).ToList();

        var serializer = new SerializerBuilder()
            .EnsureRoundtrip()
            .Build();

        var yaml = serializer.Serialize(sceneData);
        File.WriteAllText(path, yaml);
    }

    /// <summary>
    /// Loads a YAML scene file and populates the EntityManager.
    /// </summary>
    public static void LoadScene(string path, EntityManager entityManager)
    {
        if (!File.Exists(path)) return;

        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();
        var yaml = File.ReadAllText(path);
        
        var sceneData = deserializer.Deserialize<SceneData>(yaml);
        if (sceneData == null || sceneData.Entities == null) return;

        var addComponentMethodObj = typeof(EntityManager).GetMethod("AddComponent");
        
        foreach (var eData in sceneData.Entities)
        {
            var entity = entityManager.CreateEntity();

            foreach (var kvp in eData.Components)
            {
                var typeName = kvp.Key;
                var compDict = kvp.Value as Dictionary<object, object>;
                if (compDict == null) continue;

                var compType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .FirstOrDefault(t => t.FullName == typeName);

                if (compType != null && addComponentMethodObj != null)
                {
                    // Basic dictionary-to-object mapping for YamlDotNet dictionaries
                    object componentInstance = Activator.CreateInstance(compType);
                    
                    // Simple reflection to populate fields/properties
                    var fields = compType.GetFields(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var field in fields)
                    {
                        if (compDict.TryGetValue(field.Name, out var yamlVal))
                        {
                            try
                            {
                                var converted = Convert.ChangeType(yamlVal, field.FieldType);
                                field.SetValue(componentInstance, converted);
                            }
                            catch { /* Ignore conversion errors */ }
                        }
                    }

                    var properties = compType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var prop in properties)
                    {
                        if (compDict.TryGetValue(prop.Name, out var yamlVal) && prop.CanWrite)
                        {
                            try
                            {
                                var converted = Convert.ChangeType(yamlVal, prop.PropertyType);
                                prop.SetValue(componentInstance, converted);
                            }
                            catch { /* Ignore conversion errors */ }
                        }
                    }

                    var genericAdd = addComponentMethodObj.MakeGenericMethod(compType);
                    genericAdd.Invoke(entityManager, new object[] { entity, componentInstance });
                }
            }
        }
    }
}
