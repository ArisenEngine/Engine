using Serialization.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SerializationTest
{
    internal class SerializableObjectA : ISerializationCallbackReceiver
    {
        private List<string> m_PrivateStringList = new List<string>()
        {
            "Private A",
            "Private B",
            "Private C"
        };

        public List<string> m_PublicStringList = new List<string>()
        {
            "public A",
            "public B",
            "public C"
        };

        public override string ToString()
        {
            var str = "";
            if (m_PublicStringList != null && m_PublicStringList.Count > 0)
            {
                foreach(var val in m_PublicStringList)
                {
                    str += val;
                    str += ", ";
                }

            } else
            {
                str = "null";
            }
            return str;
        }

        public SerializableObjectA()
        {
            
        }

        public void OnAfterDeserialize()
        {
            
        }

        public void OnBeforeSerialize()
        {
            
        }
    }
}
