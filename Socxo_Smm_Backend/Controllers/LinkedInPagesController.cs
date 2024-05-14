using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Socxo_Smm_Backend.Infrastructure.Socxo_Smm_Backend.Infrastructure.Repository.Interface;
using Newtonsoft.Json;
using Socxo_Smm_Backend.Core.Model;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

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

        }

        [HttpPost("PostTextToLinkedin")]
        public async Task<ActionResult<HttpResponseMessage>> PostText([FromBody] PostContent content)
        {
            var client = _httpClientFactory.CreateClient();

            var postIds = new List<string>();

            foreach (string orgid in content.Orgids)
            {

                var requestBody = new
                {
                    author = orgid,
                    commentary = content.textcontent,
                    visibility = "PUBLIC",
                    distribution = new
                    {
                        feedDistribution = "MAIN_FEED",
                        targetEntities = new object[] { },
                        thirdPartyDistributionChannels = new object[] { }
                    },
                    lifecycleState = "PUBLISHED",
                    isReshareDisabledByAuthor = false
                };

                var jsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(requestBody);



                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.linkedin.com/rest/posts");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", content.accesstoken);
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("X-Restli-Protocol-Version", "2.0.0");
                client.DefaultRequestHeaders.Add("LinkedIn-Version", "202304");

                try
                {
                    var response = await client.SendAsync(request);
                    if (response.IsSuccessStatusCode && response.StatusCode == System.Net.HttpStatusCode.Created)
                    {
                        if (response.Headers.TryGetValues("x-restli-id", out var headerValues))
                        {
                            var postId = headerValues.FirstOrDefault();
                            if (postId != null)
                            {
                                postIds.Add(postId);
                            }
                        }
                    }
                    else
                    {
                        var responseMessage = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Failed to post for orgid {orgid}: {responseMessage}");
                    }

                }
                catch (Exception ex)
                {

                    Console.WriteLine($"Exception occurred for orgid {orgid}: {ex.Message}");
                }
            }

            return Ok(postIds);
        }


        [HttpPost("PostImageWithContentToLinkedin")]
        public async Task<ActionResult<HttpResponseMessage>> PostImage([FromBody] PostContent content)
        {
            var client = _httpClientFactory.CreateClient();

            var postIds = new List<string>();

            //var imageids = new List<string>();

            var imageuploadinfo = new List<uploadimageinitializeuploadobj>();
            //var uploadurls = new List<string>();

            foreach(string orgid in content.Orgids)
            {
                // intializing the image upload to the org assets 
                var requestBody = new
                {
                    initializeUploadRequest = new
                    {
                        owner = orgid,
                    }
                };

                var jsonbody = Newtonsoft.Json.JsonConvert.SerializeObject(requestBody);

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.linkedin.com/rest/images?action=initializeUpload");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", content.accesstoken);
                request.Content = new StringContent(jsonbody,Encoding.UTF8,"application/json");

                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("X-Restli-Protocol-Version", "2.0.0");
                client.DefaultRequestHeaders.Add("LinkedIn-Version", "202304");

                try
                {
                    var req = await client.SendAsync(request);

                    var responseString = await req.Content.ReadAsStringAsync();

                    if (req.IsSuccessStatusCode)
                    {
                        var uploadResponse = JsonConvert.DeserializeObject<LinkedInUploadRequestbody>(responseString);
                        if(uploadResponse?.Value != null)
                        {
                            var Uploadurl = uploadResponse.Value.uploadUrl;
                            var Image = uploadResponse.Value.image;

                            imageuploadinfo.Add(new uploadimageinitializeuploadobj
                            {
                                image = Image,
                                uploadUrl = Uploadurl,
                                orgid = orgid,

                            });


                        }

                    }

                }
                catch (FormatException ex)
                {
                    return BadRequest("Invalid base64 string");
                }
            }

            // semding the curl request and making the post request with image to linkedin api

                foreach (var imgobj in imageuploadinfo)
                {
                    byte[] imageBytes;

                    try
                    {
                        imageBytes = Convert.FromBase64String(content.base64img);
                    }
                    catch (FormatException)
                    {
                        return BadRequest("Invalid base64 string");
                    }

                    using var imageContent = new ByteArrayContent(imageBytes);

                    imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

                    var request = new HttpRequestMessage(HttpMethod.Put, imgobj.uploadUrl);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", content.accesstoken);

                    request.Content = imageContent;

                    try
                    {
                        // Send the request
                        var response = await client.SendAsync(request);

                        // Check the response status
                        if (response.IsSuccessStatusCode)
                        {

                            var requestbody = new
                            {
                                author = imgobj.orgid,
                                commentary = content.textcontent,
                                visibility = "PUBLIC",
                                distribution = new
                                {
                                    feedDistribution = "MAIN_FEED",
                                    targetEntities = new object[] { },
                                    thirdPartyDistributionChannels = new object[] { }
                                },
                                content = new
                                {
                                    media = new
                                    {
                                        altText = "",
                                        id = imgobj.image

                                    }
                                },
                                lifecycleState = "PUBLISHED",
                                isReshareDisabledByAuthor = false
                            };
                            var jsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(requestbody);
                            var requestforpost = new HttpRequestMessage(HttpMethod.Post, "https://api.linkedin.com/rest/posts");
                            requestforpost.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", content.accesstoken);
                            requestforpost.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                            client.DefaultRequestHeaders.Clear();
                            client.DefaultRequestHeaders.Add("X-Restli-Protocol-Version", "2.0.0");
                            client.DefaultRequestHeaders.Add("LinkedIn-Version", "202304");

                            try
                            {
                               var responsepost = await client.SendAsync(requestforpost);
                                if (responsepost.IsSuccessStatusCode && responsepost.StatusCode == System.Net.HttpStatusCode.Created)
                                {
                                    if (response.Headers.TryGetValues("x-restli-id", out var headerValues))
                                    {
                                        var postId = headerValues.FirstOrDefault();
                                        if (postId != null)
                                        {
                                            postIds.Add(postId);
                                        }
                                    }
                                }
                                else
                                {
                                    var responseMessage = await response.Content.ReadAsStringAsync();
                                    Console.WriteLine($"Failed to post for orgid {imgobj.orgid}: {responseMessage}");
                                }
                            }
                            catch (Exception ex)
                            {

                                Console.WriteLine($"Exception occurred for orgid {imgobj.orgid}: {ex.Message}");
                            }

                            //var responseBody = await response.Content.ReadAsStringAsync();
                            //return Ok(responseBody);

                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            return StatusCode((int)response.StatusCode, errorContent);
                        }
                    }
                    catch (Exception ex)
                    {
                        return StatusCode(500, $"Internal server error: {ex.Message}");
                    }


                }
            
            return Ok(postIds);

        }















    }
}
