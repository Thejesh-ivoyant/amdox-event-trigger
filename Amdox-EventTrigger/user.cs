
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amdox_EventTrigger
{
    public class User
    {
        public string UserGuid { get; set; }
        public string B2BUserld { get; set; }
        public string B2BAccessCode { get; set; }
        public string UttUID { get; set; }
        public string Type { get; set; }
        public string UserName { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public bool IsSynced { get; set; }
        public string SyncStatus { get; set; }
        public string ModificationDate { get; set; }
        public string ModificationBatch { get; set; }
        public string SyncDate { get; set; }
    }
}
