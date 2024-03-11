using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amdox_EventTrigger
{
    public class DataStatus
    {
        public Dictionary<string, string> Status { get; set; }

        public DataStatus()
        {
            Status = new Dictionary<string, string>();
        }
    }
}
