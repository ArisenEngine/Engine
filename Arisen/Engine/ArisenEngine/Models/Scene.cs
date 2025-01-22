//using Core.ECS.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serialization.Interface;

namespace ArisenEngine.Models
{
    public sealed class Scene : ISerializationCallbackReceiver
    {
        //public List<Entity> entities = new List<Entity>();
        public void OnBeforeSerialize()
        {
            throw new NotImplementedException();
        }

        public void OnAfterDeserialize()
        {
            throw new NotImplementedException();
        }
    }
}
