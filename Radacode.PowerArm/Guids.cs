using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PowerArm.Extension
{
    static class Guids
    {
        public const string PackageId = "859e6cf7-852b-4756-bef5-bacea93612d4";
        public const string PowerArmCommandGroupId = "3c281d67-fe51-41e1-b138-b4385425efc5";

        public static readonly Guid PowerArmGroupGuid = new Guid(PowerArmCommandGroupId);
    }
}
