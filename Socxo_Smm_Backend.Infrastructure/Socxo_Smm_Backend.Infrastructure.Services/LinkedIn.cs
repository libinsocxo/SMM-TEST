using Amazon.Runtime;
using Socxo_Smm_Backend.Core.Model;
using Socxo_Smm_Backend.Infrastructure.Socxo_Smm_Backend.Infrastructure.Repository.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;




namespace Socxo_Smm_Backend.Infrastructure.Socxo_Smm_Backend.Infrastructure.Services
{
    public class LinkedIn : ILinkedIn
    {

        private readonly HttpClient _httpClient;

        public LinkedIn(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }


        public Task<List<Page>> GetallPages()
        {
            throw new NotImplementedException();
        }
    }
}
