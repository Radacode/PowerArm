using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace PowerArm.Extension.Helpers
{
    public static class LocalIISChecker
    {
        public static bool LocalIISIsInstalled()
        {
            RegistryKey IISKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\InetStp", false);
            return IISKey != null;
        }
    }
}
