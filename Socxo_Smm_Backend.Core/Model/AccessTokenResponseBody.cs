using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Socxo_Smm_Backend.Core.Model
{
     public class AccessTokenResponseBody
    {
        public required string  code { get; set; }
        public required string client_id { get; set; }
        public required string client_secret { get; set; }
        public required string redirect_uri { get; set; }
    }
}
