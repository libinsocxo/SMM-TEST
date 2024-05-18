using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Socxo_Smm_Backend.Infrastructure.Socxo_Smm_Backend.Infrastructure.Repository.Interface;
using Newtonsoft.Json;
using Socxo_Smm_Backend.Core.Model;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text;
using System.Text.Json;
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
            var redirecturl = "http://localhost:4200/linkedin/redirect";
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

            try
            {
                var response = await client.PostAsync("https://www.linkedin.com/oauth/v2/accessToken", formContent);
                
                if (response.IsSuccessStatusCode)
                {
              
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    return Ok(responseContent);
                }
                else
                {
                    var err_msg = response.Content.ReadAsStringAsync();
                    
                    return StatusCode((int)response.StatusCode, err_msg);
                }
            }
            catch(Exception ex)
            {
              return StatusCode(500, $"Internal server error  : {ex.Message}");
            }
            
        }


        [HttpPost("GetAdminPagesofUser")]
        public async Task<ActionResult<dynamic>> getAdminpages([FromBody] AccessTokenBody request)
        {

            var client = _httpClientFactory.CreateClient();

            //Console.WriteLine(request.accesstoken);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", request.accesstoken);
            client.DefaultRequestHeaders.Add("X-Restli-Protocol-Version", "2.0.0");
            client.DefaultRequestHeaders.Add("LinkedIn-Version", "202304");

            var apiUrl = "https://api.linkedin.com/rest/organizationAcls?q=roleAssignee&role=ADMINISTRATOR&projection=(elements*(*,roleAssignee~(localizedFirstName,localizedLastName),organization~(localizedName)))";

            try
            {
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
                    var msg_res = await response.Content.ReadAsStringAsync();
                    
                    return StatusCode((int)response.StatusCode, msg_res);
                }
                
                
            }
            catch (Exception ex)
            {
               return StatusCode(500, $"Internal server error  : {ex.Message}");
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
                client.DefaultRequestHeaders.Add("LinkedIn-Version", "202402");

                try
                {
                    var response = await client.SendAsync(request);
                    var msg_res = response.Content.ReadAsStringAsync();
                    
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
                      
                        Console.WriteLine($"Failed to post for orgid {orgid}: {msg_res}");
                        return StatusCode((int)response.StatusCode, msg_res);
                    }

                }
                catch (Exception ex)
                {

                    return StatusCode(500, $"Internal server error during posting the text : {ex.Message}");
                    
                }
            }

            return Ok(postIds);
        }


        [HttpPost("PostImageWithContentToLinkedin")]
        // optimized verision of the api 
        public async Task<ActionResult<HttpResponseMessage>> PostImage([FromBody] PostContent content)
        {
            var client = _httpClientFactory.CreateClient();
            var postIds = new List<string>();
            var imageuploadinfo = new List<uploadimageinitializeuploadobj>();

            // Centralize and configure client headers
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("X-Restli-Protocol-Version", "2.0.0");
            client.DefaultRequestHeaders.Add("LinkedIn-Version", "202402");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", content.accesstoken);

            // Validate and convert base64 image once
            byte[] imageBytes;
            try
            {
                imageBytes = Convert.FromBase64String(content.base64img);
            }
            catch (FormatException)
            {
                return BadRequest("Invalid base64 string");
            }

            // Initialize image upload for each org
            foreach (string orgid in content.Orgids)
            {
                var requestBody = new
                {
                    initializeUploadRequest = new
                    {
                        owner = orgid,
                    }
                };

                var jsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(requestBody);
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.linkedin.com/rest/images?action=initializeUpload")
                {
                    Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
                };

                try
                {
                    var req = await client.SendAsync(request);
                    var responseString = await req.Content.ReadAsStringAsync();

                    if (req.IsSuccessStatusCode)
                    {
                        var uploadResponse = JsonConvert.DeserializeObject<LinkedInUploadRequestbody>(responseString);
                        if (uploadResponse?.Value != null)
                        {
                            imageuploadinfo.Add(new uploadimageinitializeuploadobj
                            {
                                image = uploadResponse.Value.image,
                                uploadUrl = uploadResponse.Value.uploadUrl,
                                orgid = orgid
                            });
                        }
                    }
                    else
                    {
                        return StatusCode((int)req.StatusCode, responseString);
                    }
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Internal server error during initialization for orgid {orgid}: {ex.Message}");
                }
            }

            // Upload image and create posts
            foreach (var imgobj in imageuploadinfo)
            {
                using var imageContent = new ByteArrayContent(imageBytes)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("image/jpeg") }
                };

                var uploadRequest = new HttpRequestMessage(HttpMethod.Put, imgobj.uploadUrl)
                {
                    Content = imageContent
                };

                try
                {
                    var response = await client.SendAsync(uploadRequest);

                    if (response.IsSuccessStatusCode)
                    {
                        var postRequestBody = new
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

                        var postJsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(postRequestBody);
                        var postRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.linkedin.com/rest/posts")
                        {
                            Content = new StringContent(postJsonBody, Encoding.UTF8, "application/json")
                        };

                        var postResponse = await client.SendAsync(postRequest);

                        if (postResponse.IsSuccessStatusCode && postResponse.StatusCode == System.Net.HttpStatusCode.Created)
                        {
                            if (postResponse.Headers.TryGetValues("x-restli-id", out var headerValues))
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
                            var responseMessage = await postResponse.Content.ReadAsStringAsync();
                            Console.WriteLine($"Failed to post for orgid {imgobj.orgid}: {responseMessage}");
                        }
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        return StatusCode((int)response.StatusCode, errorContent);
                    }
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Internal server error during image upload for orgid {imgobj.orgid}: {ex.Message}");
                }
            }

            return Ok(postIds);
        }
        
        [HttpPost("GetUserProfile")]
        public async Task<ActionResult> getuserprofile([FromBody] AccessTokenBody token)
        {
            var client = _httpClientFactory.CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.accesstoken);
            var apiurl = "https://api.linkedin.com/v2/me";

            var response = await client.GetAsync(apiurl);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();

                return Ok(content);
            }
            else
            {
                return BadRequest();
            }
        }

        [HttpPost("GetAdminPagesofUser-v-202405")]
        public async Task<ActionResult<dynamic>> getadminpages_latest([FromBody] AccessTokenBody request)
        {
            var client = _httpClientFactory.CreateClient();
            var UserAccessPages = new List<AdminPagesModel>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", request.accesstoken);
            client.DefaultRequestHeaders.Add("X-Restli-Protocol-Version", "2.0.0");
            client.DefaultRequestHeaders.Add("LinkedIn-Version", "202405");

            var apiurl = "https://api.linkedin.com/rest/organizationAcls?q=roleAssignee&role=ADMINISTRATOR";
            
            // making the request to endpoint and retreaving the org id

            try
            {
                var response = await client.GetAsync(apiurl);
                var responsecontent = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    
                    using (JsonDocument doc = JsonDocument.Parse(responsecontent))
                    {
                        JsonElement root = doc.RootElement;
                        JsonElement elements = root.GetProperty("elements");
                        
                        foreach (JsonElement element in elements.EnumerateArray())
                        {
                            string organization = element.GetProperty("organization").GetString();
                            if (organization.Length>0)
                            {
                                //retreaving the id part from the urn 
                                string urn = organization;
                                string[] parts = urn.Split(':');
                                string urnid = parts[^1];
                                // sending the orgid for getting the org name

                                var orgnameUrl = $"https://api.linkedin.com/rest/organizations/{urnid}";
                                try
                                {
                                    var resp = await client.GetAsync(orgnameUrl);
                                    var content = await resp.Content.ReadAsStringAsync();
                                    if (resp.IsSuccessStatusCode)
                                    {
                                        
                                        using (JsonDocument doc1 = JsonDocument.Parse(content))
                                        {
                                            JsonElement root1 = doc1.RootElement;
                                            string localizedName = root1.GetProperty("localizedName").GetString();
                                            
                                            // seeting the org details
                                            AdminPagesModel adminpagemodel = new AdminPagesModel()
                                            {
                                                OrgId = urn,
                                                OrgName = localizedName
                                            };
                                            
                                            UserAccessPages.Add(adminpagemodel);

                                        }
                                    }
                                    else
                                    {
                                        return StatusCode((int)resp.StatusCode, content);
                                    }
                                    
                                }
                                catch (Exception ex)
                                {
                                    return StatusCode(500, $"Internal server error occured! : {ex.Message}");
                                }
                                
                            }
                            else
                            {
                                return NotFound();
                            }
                        }
                    }
                }
                else
                {
                    return StatusCode((int)response.StatusCode, responsecontent);
                }
            }

            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error occured! : {ex.Message}");
            }

            return Ok(UserAccessPages);
        }















    }
}
