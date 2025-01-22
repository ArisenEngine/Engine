using Serialization.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SerializationTest
{
    internal class SerializableObjectB : ISerializationCallbackReceiver
    {
        public SerializableObjectA serializableObjectA = new SerializableObjectA();

        public void OnAfterDeserialize()
        {
           
        }

        public void OnBeforeSerialize()
        {
            
        }
        public override string ToString()
        {
            var str = "";
            if (serializableObjectA != null)
            {
                str = serializableObjectA.ToString();
            } else
            {
                str = "null";
            }
            return str;
        }
    }
}
