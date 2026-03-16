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
            // Note: In Arisen Editor, SceneSerializer directly writes to the file, but if mapped here,
            // we'd serialize the components into a DTO list.
        }

        public void OnAfterDeserialize()
        {
        }
    }
}