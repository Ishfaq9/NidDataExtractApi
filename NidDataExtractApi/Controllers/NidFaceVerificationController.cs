using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using NidDataExtractApi.Models;
using System.Text;

namespace NidDataExtractApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NidFaceVerificationController : ControllerBase
    {
        #region variable
        private static readonly HttpClient client = new HttpClient();
        #endregion

        #region event
        [HttpPost("VerifyFace")]
        public async Task<ActionResult<Response>> VerifyFace([FromBody] FaceRequest request)
        
        {
            try
            {
                if(string.IsNullOrEmpty(request.imageBase641) || string.IsNullOrEmpty(request.imageBase642))
                    return new Response { IsSuccess = false, Status = "Failed", Message = "Image data cannot be null or empty." };


                var deepFaceResult = await faceVerifyResponse(request.imageBase641,request.imageBase642);

                if (deepFaceResult.IsSuccess)
                {
                    var imageResult = deepFaceResult.ObjResponse as ImageResult;
                    if(!imageResult!.verified)
                        return new Response { IsSuccess = false, Status = "Failed", Message = "Face not matched.", ObjResponse=imageResult };

                    return new Response { IsSuccess = true, Status = "Success", Message = "Face Matched Successfully", ObjResponse = imageResult };
                }
                else
                {
                    return new Response { IsSuccess = false, Status = "Failed",Message = deepFaceResult.Message };
                }


            }
            catch (Exception ex)
            {

                return new Response { IsSuccess = false, Status = "Exception", Message = ex.Message };
            }
        }

        #endregion

        #region method
        private async Task<Response> faceVerifyResponse(string imageBase641, string imageBase642)
        {
            try
            {
                var content = new StringContent(
                    $"{{\"image_base641\": \"{imageBase641}\", \"image_base642\": \"{imageBase642}\"}}",
                    Encoding.UTF8,
                    "application/json"
                );
                //var variable = new
                //{
                //    image_base641 = imageBase641,
                //    image_base642 = imageBase642
                //};

                //var json = JsonConvert.SerializeObject(variable);
                //var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("http://localhost:6000/compare-faces/", null);

                var responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    if (string.IsNullOrEmpty(responseString))
                        return new Response { IsSuccess = false, Status = "Failed", Message = "No response from face verification service." };

                    var imageResult = JsonConvert.DeserializeObject<ImageResult>(responseString);
                    if (imageResult == null)
                        return new Response { IsSuccess = false, Status = "Failed", Message = "Failed to face verification response." };

                    return new Response { IsSuccess = true, Status = "Success", Message = "Face Matched Successfully", ObjResponse = imageResult };
                }
                else
                {
                    var errorResponse = JsonConvert.DeserializeObject<FastAPIErrorResponse>(responseString);
                    string errorMessage = errorResponse.detail;
                    return new Response { IsSuccess = false, StatusCode = response.StatusCode.ToString(), Status = "Failed", Message = errorMessage };
                }

            }
            catch (Exception ex)
            {
                return new Response { IsSuccess = false, Status = "Exception", Message = ex.Message };
            }
        }

        #endregion

        #region model
        public class FastAPIErrorResponse
        {
            public string detail { get; set; }
        }
        public class FaceRequest
        {
            public string imageBase641 { get; set; }
            public string imageBase642 { get; set; }
        }
        #endregion
    }
}
