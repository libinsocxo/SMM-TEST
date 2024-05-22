using Amazon.Runtime;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Socxo_Smm_Backend.Core.Model
{
     public class PostContent
    {
        [BsonId]
        public ObjectId Id { get; set; }

        public required List<string> Orgids { get; set; } 

        public string? textcontent { get; set; }

        public string? accesstoken { get; set; }

        public string? base64img { get; set; }
        
        public string? PdfFile { get; set; }
        
        public string? DocTitle { get; set; }

    }
}
