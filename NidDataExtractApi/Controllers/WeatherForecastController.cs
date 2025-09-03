using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NidDataExtractApi.Models;
using System.Diagnostics;
using System.Text;

namespace NidDataExtractApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {

        #region Variale
        private readonly IWebHostEnvironment _env;
        private static readonly HttpClient client = new HttpClient();
        public WeatherForecastController(IWebHostEnvironment env)
        {
            _env = env;
        }
        #endregion

        #region Event

        [HttpPost("GetNidData")]
        public async Task<Response> GetNidData([FromBody] string imageBase64)
        {
            if (string.IsNullOrEmpty(imageBase64))
                return new Response { IsSuccess = false, Status = "Failed", Message = "Invalid Image Data" };

            try
            {
                var NidImageResult = new NidImageResult
                {
                    নাম = "",
                    পিতা = "",
                    মাতা = "",
                    স্বামী = "",
                    স্ত্রী = "",
                };
                var doctrNidResult = new NidImageResult();
                var paddleNidResult = new NidImageResult();
                var doctrResult = GetDataFromDoctrModel(imageBase64);
                var paddleResult = GetDataFromPaddleModel(imageBase64);

                // Wait for both tasks to complete
                await Task.WhenAll(doctrResult, paddleResult);

                if (!doctrResult.Result.IsSuccess && !paddleResult.Result.IsSuccess)
                    return new Response { IsSuccess = false, Status = "Failed", Message = doctrResult.Result.Message };

                if (doctrResult.Result.IsSuccess)
                {
                    doctrNidResult = doctrResult.Result.ObjResponse as NidImageResult;
                }
                else
                {
                    doctrNidResult = new NidImageResult
                    {
                        নাম = "",
                        পিতা = "",
                        মাতা = "",
                        স্বামী = "",
                        স্ত্রী = "",
                        DateOfBirth = "",
                        IDNO = "",
                        Name = ""
                    };
                }
                if (paddleResult.Result.IsSuccess)
                {
                    paddleNidResult = paddleResult.Result.ObjResponse as NidImageResult;
                }
                else
                {
                    paddleNidResult = new NidImageResult
                    {
                        নাম = "",
                        পিতা = "",
                        মাতা = "",
                        স্বামী = "",
                        স্ত্রী = "",
                        DateOfBirth = "",
                        IDNO = "",
                        Name = ""
                    };
                }

                if (paddleNidResult.Name.Replace(" ", "") == doctrNidResult.Name.Replace(" ", ""))
                {
                    if (paddleNidResult.Name.Count(c => c == ' ') > doctrNidResult.Name.Count(c => c == ' '))
                        NidImageResult.Name = paddleNidResult.Name;
                    else
                        NidImageResult.Name = doctrNidResult.Name;
                }
                else
                {
                    if (paddleNidResult.Name.Length == doctrNidResult.Name.Length)
                        NidImageResult.Name = paddleNidResult.Name;
                    else if (paddleNidResult.Name.Length > doctrNidResult.Name.Length)
                        NidImageResult.Name = paddleNidResult.Name;
                    else
                        NidImageResult.Name = doctrNidResult.Name;

                }

                NidImageResult.IDNO = !string.IsNullOrEmpty(doctrNidResult.IDNO) ? doctrNidResult.IDNO :
                    !string.IsNullOrEmpty(paddleNidResult.IDNO) ? paddleNidResult.IDNO : "";

                NidImageResult.DateOfBirth = !string.IsNullOrEmpty(doctrNidResult.DateOfBirth) ? doctrNidResult.DateOfBirth :
                    !string.IsNullOrEmpty(paddleNidResult.DateOfBirth) ? paddleNidResult.IDNO : "";


                return new Response { IsSuccess = true, Status = "Success", Message = "NID data extracted successfully.", ObjResponse = NidImageResult };
            }
            catch (Exception ex)
            {
                return new Response { IsSuccess = false, Status = "Exception", Message = $"An unexpected error occurred: {ex.Message}" };
            }
        }


        #endregion

        #region Method

        public class FastAPIErrorResponse
        {
            public string detail { get; set; }
        }

        private async Task<Response> GetDataFromDoctrModel(string imageBase64)
        {
            try
            {
                //var content = new StringContent(
                //    $"{{\"image_base64\": \"{imageBase64}\"}}",
                //    Encoding.UTF8,
                //    "application/json"
                //);

                //var response = await client.PostAsync("http://localhost:7060/extract-fields/", content);

                //// Read the response content REGARDLESS of the status code
                //var responseString = await response.Content.ReadAsStringAsync();

                //if (response.IsSuccessStatusCode)
                //{
                //    if (string.IsNullOrEmpty(responseString))
                //        return new Response { IsSuccess = false, Status = "Failed", Message = "No response from OCR service." };

                //    var nidImageResult = JsonConvert.DeserializeObject<NidImageResult>(responseString);
                //    if (nidImageResult == null)
                //        return new Response { IsSuccess = false, Status = "Failed", Message = "Failed to parse OCR response." };

                //    return new Response { IsSuccess = true, Status = "Success", Message = "NID data extracted successfully.", ObjResponse = nidImageResult };
                //}

                //else
                //{
                //    var errorResponse = JsonConvert.DeserializeObject<FastAPIErrorResponse>(responseString);
                //    string errorMessage = errorResponse.detail;
                //    return new Response { IsSuccess = false, StatusCode = response.StatusCode.ToString(), Status = "Failed", Message = errorMessage };
                //}
                var NidImageResult = new NidImageResult
                {
                    নাম = "",
                    পিতা = "",
                    মাতা = "",
                    স্বামী = "",
                    স্ত্রী = "",
                    DateOfBirth = "02-02-2025",
                    IDNO = "0123456789",
                    Name = "MD Ishfaq Rahman"
                };

                return new Response { IsSuccess = true, Status = "Success", Message = "NID data extracted successfully.", ObjResponse = NidImageResult };

            }
            catch (Exception ex)
            {
                return new Response { IsSuccess = false, Status = "123", Message = ex.Message };
            }
        }


        private async Task<Response> GetDataFromPaddleModel(string imageBase64)
        {
            try
            {
                var content = new StringContent(
                    $"{{\"image_base64\": \"{imageBase64}\"}}",
                    Encoding.UTF8,
                    "application/json"
                );

                //var response = await client.PostAsync("http://localhost:7060/extract-fields/", content);

                //// Read the response content REGARDLESS of the status code
                //var responseString = await response.Content.ReadAsStringAsync();

                //if (response.IsSuccessStatusCode)
                //{
                //    if (string.IsNullOrEmpty(responseString))
                //        return new Response { IsSuccess = false, Status = "Failed", Message = "No response from OCR service." };

                //    var nidImageResult = JsonConvert.DeserializeObject<NidImageResult>(responseString);
                //    if (nidImageResult == null)
                //        return new Response { IsSuccess = false, Status = "Failed", Message = "Failed to parse OCR response." };

                //    return new Response { IsSuccess = true, Status = "Success", Message = "NID data extracted successfully.", ObjResponse = nidImageResult };
                //}

                //else
                //{
                //    var errorResponse = JsonConvert.DeserializeObject<FastAPIErrorResponse>(responseString);
                //    string errorMessage = errorResponse.detail;
                //    return new Response { IsSuccess = false, StatusCode = response.StatusCode.ToString(), Status = "Failed", Message = errorMessage };
                //}
                var NidImageResult = new NidImageResult
                {
                    নাম = "",
                    পিতা = "",
                    মাতা = "",
                    স্বামী = "",
                    স্ত্রী = "",
                    DateOfBirth = "02-02-2025",
                    IDNO = "",
                    Name = "MDIshfaq Rahman"
                };
                return new Response { IsSuccess = true, Status = "Success", Message = "NID data extracted successfully.", ObjResponse = NidImageResult };

            }
            catch (Exception ex)
            {
                return new Response { IsSuccess = false, Status = "Failed", Message = ex.Message };
            }
        }

        [HttpGet("Test")]
        public ActionResult Test()
        {
            var a = "MSTMOHOSINA KHATUN";
            var b = "MST MOHOSINAKHATUN";
            if (a.Replace(" ", "") == b.Replace(" ", ""))
            {
            
                if (a.Count(c => c == ' ') > b.Count(c => c == ' '))
                    return Ok(a);
                else
                    return Ok(b);
            }
            else
            {
                if (a.Length == b.Length)
                {
                    if(a=="" && b =="")
                        return Ok(0);
                }
                    
                else if (a.Length > b.Length)
                    return Ok(a);
                else
                    return Ok(b);

                return Ok(0);
            }

        }


        #endregion


    }
}
