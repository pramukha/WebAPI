using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebAPI.Models
{
    public class AccountAuthResponse
    {
        public string AccessToken { get; set; }
        public string ResultCode { get; set; }
        public string ResultDescription { get; set; }
    }
}
