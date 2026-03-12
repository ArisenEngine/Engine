using ArisenEngine.Core.ECS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArisenEngine.Core.Serialization;

namespace ArisenEngine.Models
{
    public sealed class Scene : ISerializationCallbackReceiver
    {
        public string Name { get; set; } = "New Scene";
        public EntityManager Registry { get; private set; } = new();

        public Entity CreateEntity()
        {
            return Registry.CreateEntity();
        }

        public void DestroyEntity(Entity entity)
        {
            Registry.DestroyEntity(entity);
        }

        public void OnBeforeSerialize()
        {
            // Custom Yaml serialization logic will go here to parse out the EntityManager contiguous arrays later
        }

        public void OnAfterDeserialize()
        {
            // Re-inflate EntityManager arrays upon load
        }
    }
}