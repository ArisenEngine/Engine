using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serialization.Interface
{
    public interface ISerializationCallbackReceiver
    {
        public void OnBeforeSerialize();

        public void OnAfterDeserialize();

    }
}
