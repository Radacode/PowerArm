using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PowerArm.Extension
{
    static class Guids
    {
        public const string PackageId = "79717230-8601-4566-be1b-8c8831f92931";
        public const string RestartElevatedCommandGroupId = "15dc28d5-04f4-4698-90e0-e3e16bc6894f";

        public const string TopLevelMenuGroupId = "D2FB6644-0147-4FDB-8F35-22B5F0AA8594";

        public static readonly Guid RestartElevatedGroupGuid = new Guid(RestartElevatedCommandGroupId);
        public static readonly Guid TopLevelMenuGuid = new Guid(TopLevelMenuGroupId);
    }
}
