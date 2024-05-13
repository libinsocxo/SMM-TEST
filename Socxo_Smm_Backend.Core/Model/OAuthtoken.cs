using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Socxo_Smm_Backend.Core.Model
{
    public class OAuthtoken
    {
        [BsonElement]
        public string? OAuthToken { get; set; }
        
    }
}
