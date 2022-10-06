using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dlubal
{
    /// <summary>
    /// This interface is for handling and logging all stuffs that happen in WS.
    /// Object that implements this interface should be prepared that code of these method
    /// might be execute in different threads than main app. (So for UI Dispatcher is needed)
    /// </summary>
    public interface IMainApp
    {
        public static readonly string ErrorMessageType = "Error";
        public static readonly string InfoMessageType = "Info";
        public void Log(string messageType, string message);
    }
}
