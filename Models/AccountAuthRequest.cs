using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebAPI.Models
{
    public class AccountAuthRequest
    {
        public string AccountId { get; set; }
        public string AccessSecret { get; set; }
        public string ApiKey { get; set; }
    }
}
