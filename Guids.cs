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
        public const string PowerArmCommandGroupId = "3c281d67-fe51-41e1-b138-b4385425efc5";

        public static readonly Guid PowerArmGroupGuid = new Guid(PowerArmCommandGroupId);
    }
}
