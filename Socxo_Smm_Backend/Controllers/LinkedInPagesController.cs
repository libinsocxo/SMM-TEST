using System.Diagnostics;
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
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using static System.Net.Mime.MediaTypeNames;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

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
        public async Task<ActionResult<UserProfileModel>> getuserprofile([FromBody] AccessTokenBody token)
        {

            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("LinkedIn-Version", "202405");
            
            try
            {
                var req = await client.GetAsync($"https://api.linkedin.com/rest/me/?oauth2_access_token={token.accesstoken}");
                var response = await req.Content.ReadAsStringAsync();

                if (req.IsSuccessStatusCode)
                {
                    using ( JsonDocument doc = JsonDocument.Parse(response))
                    {
                        JsonElement root = doc.RootElement;
                        JsonElement firstNameElement =
                            root.GetProperty("firstName").GetProperty("localized").GetProperty("en_US");
                        string firstName = firstNameElement.ToString();

                        JsonElement secondNameElement =
                            root.GetProperty("lastName").GetProperty("localized").GetProperty("en_US");
                        string secondName = secondNameElement.ToString();

                        JsonElement aboutElement = root.GetProperty("localizedHeadline");
                        string about = aboutElement.ToString();

                        JsonElement profileUrnElement = root.GetProperty("profilePicture").GetProperty("displayImage");
                        string profileUrn = profileUrnElement.ToString();
                        
                        // req to get the profile url from the imageurn
                        var imageclient = new HttpClient();
                        imageclient.DefaultRequestHeaders.Authorization =
                            new AuthenticationHeaderValue("Bearer", token.accesstoken);
                        imageclient.DefaultRequestHeaders.Add("LinkedIn-Version", "202405");
                        string urn = profileUrn;
                        string[] parts = urn.Split(':');
                        string urnid = parts[^1];
                    
                        try
                        {
                            var url_req = await imageclient.GetAsync($"https://api.linkedin.com/rest/images/urn:li:image:{urnid}");
                            var url_response = await url_req.Content.ReadAsStringAsync();

                            if (url_req.IsSuccessStatusCode)
                            {
                                using (JsonDocument doc1 = JsonDocument.Parse(url_response))
                                {
                                    JsonElement root1 = doc1.RootElement;
                                    JsonElement DownloadUrlElement = root1.GetProperty("downloadUrl");
                                    string DownloadUrl = DownloadUrlElement.ToString();

                                    if (DownloadUrl.Length > 0)
                                    {
                                        UserProfileModel profilemodel = new UserProfileModel()
                                        {
                                            firstName =  firstName,
                                            secondName = secondName,
                                            about = about,
                                            profileUrl = DownloadUrl
                                        };

                                        return Ok(profilemodel);
                                    }
                                    else
                                    {
                                        return Content("Error while Parsing from the Json Response for ProfileUrn.");
                                    }
                                }
                            }
                            else
                            {
                                return StatusCode((int)url_req.StatusCode, url_response);
                            }
                            
                        }
                        catch (Exception ex)
                        {
                            return StatusCode(500, $"Internal server error occured! : {ex.Message}");
                        }

                    }
                }
                else
                {
                    return StatusCode((int)req.StatusCode, response);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error occured! : {ex.Message}"); 
            }


        }

        
        
        [HttpPost("GetAdminPagesofUser-v-202405")]
        public async Task<ActionResult<AdminPagesModel>> GetAdminPagesLatest([FromBody] AccessTokenBody request)
        {
            // var watch = new Stopwatch();
            // watch.Start();
            var client = _httpClientFactory.CreateClient();
            var client1 = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", request.accesstoken);
            client.DefaultRequestHeaders.Add("X-Restli-Protocol-Version", "2.0.0");
            client.DefaultRequestHeaders.Add("LinkedIn-Version", "202405");
            client1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", request.accesstoken);
            client1.DefaultRequestHeaders.Add("LinkedIn-Version","202405");
            
            var apiurl = "https://api.linkedin.com/rest/organizationAcls?q=roleAssignee&role=ADMINISTRATOR";
        
            try
            {
                var response = await client.GetAsync(apiurl);
                var responseContent = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    var userAccessPages = new List<AdminPagesModel>();
                    using (JsonDocument doc = JsonDocument.Parse(responseContent))
                    {
                        JsonElement elements = doc.RootElement.GetProperty("elements");
                        var tasks = elements.EnumerateArray().Select(async element =>
                        {
                            string organization = element.GetProperty("organization").GetString();
                            if (!string.IsNullOrEmpty(organization))
                            {
                                string urn = organization;
                                string[] parts = urn.Split(':');
                                string urnid = parts[^1];
                                var orgnameUrl = $"https://api.linkedin.com/rest/organizations/{urnid}";
                                var followerscountUrl =
                                    $"https://api.linkedin.com/rest/networkSizes/{urn}?edgeType=COMPANY_FOLLOWED_BY_MEMBER";
        
                                var orgNameTask = GetOrganizationName(client, orgnameUrl);
                           
                                var followersCountTask = GetFollowersCount(client1, followerscountUrl);
                           
                          
        
                                await Task.WhenAll(orgNameTask, followersCountTask);
        
                                var userProfileModel = new AdminPagesModel
                                {
                                    OrgId = urn,
                                    OrgName = orgNameTask.Result,
                                    FollowerCount = followersCountTask.Result
                                };
                                userAccessPages.Add(userProfileModel);
        
                            }
                        }).ToArray();
                        await Task.WhenAll(tasks);
                    }
      
                    return Ok(userAccessPages);
                }
                else
                {
                    return StatusCode((int)response.StatusCode, responseContent);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error occurred: {ex.Message}");
            }
        }
        
        private async Task<string> GetOrganizationName(HttpClient client, string orgnameUrl)
        {
            var response = await client.GetAsync(orgnameUrl);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                using (JsonDocument doc = JsonDocument.Parse(content))
                {
                    JsonElement root = doc.RootElement;
                    return root.GetProperty("localizedName").GetString();
                }
            }
            else
            {
                throw new Exception($"Failed to fetch organization name: {content}");
            }
        }
        
        
        private async Task<int> GetFollowersCount(HttpClient client1, string followerscountUrl)
        {
            var response = await client1.GetAsync(followerscountUrl);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                using (JsonDocument doc = JsonDocument.Parse(content))
                {
                    JsonElement root = doc.RootElement;
                    return root.GetProperty("firstDegreeSize").GetInt32();
                }
            }
            else
            {
                throw new Exception($"Failed to fetch followers count: {content}");
            }
        }

        [HttpPost("GetOrgPagesPosts")]
        public async Task<ActionResult<PageModelResponse>> GetOrgPagesPosts([FromBody] PostModelRequest postrequest)
        {
    
            var MainClient = new HttpClient();
            MainClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", postrequest.Accesstoken);
            MainClient.DefaultRequestHeaders.Add("LinkedIn-Version","202405");
            
            

            string urn = postrequest.Orgid;
            string[] parts = urn.Split(':');
            string urnid = parts[^1];
            var count = postrequest.Count;
            
            var MainUrl =
                $"https://api.linkedin.com/rest/posts?author=urn%3Ali%3Aorganization%3A{urnid}&q=author&count={count}&sortBy=LAST_MODIFIED";

            try
            {
                var response = await MainClient.GetAsync(MainUrl);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var postcontentlist = new List<PageModelResponse>();
                    

                    using (JsonDocument doc = JsonDocument.Parse(responseContent))
                    {
                        JsonElement elements = doc.RootElement.GetProperty("elements");
                        var tasks = elements.EnumerateArray().Select(async element =>
                        {
                            string PostTitle = element.GetProperty("commentary").ToString();
                            
                            var createdAtMillis = element.GetProperty("publishedAt").GetInt64();
                            string postid = element.GetProperty("id").ToString();
                            var createdAt = DateTimeOffset.FromUnixTimeMilliseconds(createdAtMillis).UtcDateTime;

                            string imageUrn = null;
                            var Content_type = "";
                            if (element.TryGetProperty("content", out JsonElement contentElement) &&
                                contentElement.TryGetProperty("media", out JsonElement mediaElement) &&
                                mediaElement.TryGetProperty("id", out JsonElement idElement))
                            {
                                imageUrn = idElement.ToString();
                                Content_type = "Image";
                            }
                            
                            if (element.TryGetProperty("content", out JsonElement contentElementMultiimage) &&
                                contentElementMultiimage.TryGetProperty("multiImage",out JsonElement multiimage))
                            {
                                Content_type = "MultiImage";
                            }
                            
                            var imageUrlTask = (string.IsNullOrEmpty(imageUrn))
                                ? Task.FromResult<string>(null)
                                : GetPostImageUrl(MainClient, $"https://api.linkedin.com/rest/images/{imageUrn}");
                            
                   

                            // var userprofileclient = new HttpClient();
                            // userprofileclient.DefaultRequestHeaders.Add("LinkedIn-Version", "202405");
                            // var authorprofileurl = $"https://api.linkedin.com/rest/me/?oauth2_access_token={postrequest.Accesstoken}";
                            // var authortask = GetUserProfile(userprofileclient,authorprofileurl);

                            var allTasks = new List<Task> {};
                            
                            if (imageUrlTask!=null)
                            {
                                allTasks.Add(imageUrlTask);
                            }
                            
                            await Task.WhenAll(allTasks);


                            var pagePost = new PageModelResponse
                            {
                               
                                Commentary = PostTitle,
                                CreatedAt = createdAt.ToString("o"),
                                PostUrl = imageUrlTask?.Result,
                                Postid = postid,
                                Ctntype = Content_type
                            };
                            
                            postcontentlist.Add(pagePost);
                            
                        }).ToArray();

                        await Task.WhenAll(tasks);
                    }

          


                    return Ok(postcontentlist);
                }
                else
                {
                    return StatusCode((int)response.StatusCode, responseContent);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal Server Error: {ex.Message}");
            }

        }

        // private async Task<string> GetUserProfile(HttpClient UserProfileClient, string userprofileurl)
        // {
        //     var response = await UserProfileClient.GetAsync(userprofileurl);
        //     var cnt = await response.Content.ReadAsStringAsync();
        //
        //     if (response.IsSuccessStatusCode)
        //     {
        //         using (JsonDocument doc = JsonDocument.Parse(cnt))
        //         {
        //             JsonElement root = doc.RootElement;
        //             return root.GetProperty("localizedFirstName").GetString();
        //         }
        //     }
        //     else
        //     {
        //         throw new Exception($"Failed to get the post image: {cnt}");
        //     }
        // }

    

        private async Task<string> GetPostImageUrl(HttpClient MainClient, string imgurl)
        {


            var response = await MainClient.GetAsync(imgurl);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to get the post image: {content}");
            }

            using var doc = JsonDocument.Parse(content);
            return doc.RootElement.GetProperty("downloadUrl").GetString();
            
        }


        [HttpPost("GetLikesCommentCount")]
        public async Task<ActionResult<PostanalyticsModel>> GetAllLikesComments([FromBody] PostanalyticsrequestModel postreqmodel)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", postreqmodel.Token);
            client.DefaultRequestHeaders.Add("LinkedIn-Version","202405");
            
            string urn = postreqmodel.ShareId;
            string[] parts = urn.Split(':');
            string urnid = parts[^1];
            
            var api = $"https://api.linkedin.com/rest/socialActions/urn%3Ali%3Ashare%3A{urnid}";

            try
            {
                var requestEnpoint = await client.GetAsync(api);
                var responseEndpoint = await requestEnpoint.Content.ReadAsStringAsync();

                if (requestEnpoint.IsSuccessStatusCode)
                {
                    var totallikescount = "";
                    var totalcommentscount = "";
                    using (JsonDocument doc = JsonDocument.Parse(responseEndpoint))
                    {
                        JsonElement root = doc.RootElement;
                        JsonElement likesSummary = root.GetProperty("likesSummary").GetProperty("totalLikes");
                        var totallikes = likesSummary.ToString();
                        totallikescount = totallikes;
                        JsonElement CommentSummary =
                            root.GetProperty("commentsSummary").GetProperty("aggregatedTotalComments");
                        var totalcomments = CommentSummary.ToString();
                        totalcommentscount = totalcomments;
                    }

                    PostanalyticsModel postanalytics = new PostanalyticsModel()
                    {
                        TotalLikes = totallikescount,
                        TotalComments = totalcommentscount
                    };

                    return Ok(postanalytics);
                }
                else
                {
                    return StatusCode((int)requestEnpoint.StatusCode, $"Something went wrong: {responseEndpoint}");
                }

            }
            catch (Exception ex)
            {
                return StatusCode(500,$"Internal Server Error: {ex.Message}");
            }

            
        }
        
        


    }
}
