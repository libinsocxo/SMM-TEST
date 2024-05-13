using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Socxo_Smm_Backend.Infrastructure.Socxo_Smm_Backend.Infrastructure.Repository.Interface;
using Newtonsoft.Json;
using Socxo_Smm_Backend.Core.Model;
using System.Net.Http.Headers;

namespace Socxo_Smm_Backend.Controllers
{

    [Route("api/[controller]")]
    [ApiController]
    public class LinkedInPagesController : ControllerBase
    {

        private readonly ILinkedIn _ilinkedin;

        private readonly IHttpClientFactory _httpClientFactory;

        public LinkedInPagesController(ILinkedIn ilinkedin, IHttpClientFactory httpClientFactory)
        {
            _ilinkedin = ilinkedin;
            _httpClientFactory = httpClientFactory;

        }



        /// <summary>
        /// To Get the OAuth_Token from Linkedin
        /// </summary>
        /// <param name="auth"></param>
        /// <response code="200">Success Return of a OAuth token</response>
        /// <response code="400"> Bad Request</response>
        /// <response code="500">Internal Server Error</response>

        [HttpGet("GetOAuthToken")]
        public IActionResult OAuthToken()
        {
            var redirecturl = "http://localhost:4200/home";
            var clientid = "86dsonuq146byy";
            var apiUrl = $"https://www.linkedin.com/oauth/v2/authorization?response_type=code&client_id=${clientid}&redirect_uri={Uri.EscapeDataString(redirecturl)}&state=foobar&scope=rw_organization_admin%20r_organization_social%20r_organization_social_feed%20w_organization_social";

            return Ok(new { url = apiUrl });

        }

        [HttpPost("GetAccessToken")]
        public async Task<ActionResult<dynamic>> GetAccessToken([FromBody] AccessTokenResponseBody request)
        {
          
            var client = _httpClientFactory.CreateClient();

            var parameters = new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "code", request.code },
            { "client_id", request.client_id },
            { "client_secret", request.client_secret },
            { "redirect_uri", request.redirect_uri }
        };

           
            var formContent = new FormUrlEncodedContent(parameters);

            formContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");

           
            var response = await client.PostAsync("https://www.linkedin.com/oauth/v2/accessToken", formContent);

       
            if (response.IsSuccessStatusCode)
            {
              
                var responseContent = await response.Content.ReadAsStringAsync();
             
                //dynamic responseData = JsonConvert.DeserializeObject<dynamic>(responseContent);

            
                return Ok(responseContent);
            }
            else
            {
               
                return BadRequest($"Error: {response.StatusCode} - {response.ReasonPhrase}");
            }
        }


        [HttpPost("GetAdminPagesofUser")]
        public async Task<ActionResult<dynamic>> getAdminpages([FromBody] AccessTokenBody request)
        {

            var client = _httpClientFactory.CreateClient();

            Console.WriteLine(request.accesstoken);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", request.accesstoken);
            client.DefaultRequestHeaders.Add("X-Restli-Protocol-Version", "2.0.0");
            client.DefaultRequestHeaders.Add("LinkedIn-Version", "202304");

            var apiUrl = "https://api.linkedin.com/rest/organizationAcls?q=roleAssignee&role=ADMINISTRATOR&projection=(elements*(*,roleAssignee~(localizedFirstName,localizedLastName),organization~(localizedName)))";

            var response = await client.GetAsync(apiUrl);

            if (response.IsSuccessStatusCode)
            {
                // Read content
                var content = await response.Content.ReadAsStringAsync();
                return Ok(content);
            }
            else
            {
                Console.WriteLine($"Error: {response.StatusCode}");

                return BadRequest();
            }

            //var parameters = new Dictionary<string, string>
            //{
            //{ "q", "roleAssignee" },
            //{ "role", "ADMINISTRATOR" },
            //{ "projection", "(elements*(*,roleAssignee~(localizedFirstName , localizedLastName), organization~(localizedName)))" },
            //};

            //var formContent = new FormUrlEncodedContent(parameters);

            //formContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",request.accesstoken);

            //client.DefaultRequestHeaders.Add("X-Restli-Protocol-Version", "2.0.0");
            //client.DefaultRequestHeaders.Add("LinkedIn-Version", "202304");






        }









    }
}
