using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NidDataExtractApi.Models;
using System.Diagnostics;
using System.Text;

namespace NidDataExtractApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NidDataExtractController : ControllerBase
    {
        #region Variale
        private readonly IWebHostEnvironment _env;
        private static readonly HttpClient client = new HttpClient();
        public NidDataExtractController(IWebHostEnvironment env)
        {
            _env = env;
        }
        #endregion

        #region Event

        //[HttpPost("GetNidData")]
        //public async Task<ActionResult<Response>> GetNidData([FromBody] string imageBase64)
        //{
        //    try
        //    {
        //        if (string.IsNullOrEmpty(imageBase64))
        //            return new Response { IsSuccess = false, Status = "Failed", Message = "Invalid Image Data" };

        //        byte[] imageBytes = Convert.FromBase64String(imageBase64);

        //        string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        //        Directory.CreateDirectory(uploadsFolder);

        //        string fileName = Guid.NewGuid() + ".jpg";
        //        string filePath = Path.Combine(uploadsFolder, fileName);

        //        await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);

        //        Response result = await RunOCRWithImagePath(filePath);
        //        if (!result.IsSuccess)
        //            return new Response { IsSuccess = false, Status = "Failed", Message = "OCR processing failed." };

        //        return result;
        //    }
        //    catch (Exception ex)
        //    {
        //        return new Response { IsSuccess = false, Status = "Failed", Message = ex.Message };
        //    }
        //}



        //[HttpPost("GetNidData")]
        //public async Task<Response> GetNidData([FromBody] string imageBase64)
        //{
        //        if (string.IsNullOrEmpty(imageBase64))
        //            return new Response { IsSuccess = false, Status = "Failed", Message = "Invalid Image Data" };

        //        try
        //    {
        //        // Convert file to base64 string
        //        //using var stream = new MemoryStream();
        //        //await file.CopyToAsync(stream);
        //        //var base64String = Convert.ToBase64String(stream.ToArray());

        //        // Send base64 string to Python API
        //        var content = new StringContent(
        //            $"{{\"image_base64\": \"{imageBase64}\"}}",
        //            Encoding.UTF8,
        //            "application/json"
        //        );

        //        var response = await client.PostAsync("http://localhost:8000/extract-fields/", content);
        //        response.EnsureSuccessStatusCode();

        //        var responseString = await response.Content.ReadAsStringAsync();
        //        if (string.IsNullOrEmpty(responseString))
        //            return new Response { IsSuccess = false, Status = "Failed", Message = "No response from OCR service." };

        //        // Deserialize the response
        //        var nidImageResult = JsonConvert.DeserializeObject<NidImageResult>(responseString);
        //        if (nidImageResult == null)
        //            return new Response { IsSuccess = false, Status = "Failed", Message = "Failed to parse OCR response." };

        //        return new Response { IsSuccess = true, Status = "Success", Message = "NID data extracted successfully.", ObjResponse = nidImageResult };
        //            //return Ok(responseString);
        //        }
        //        catch (Exception ex)
        //        {
        //            return new Response { IsSuccess = false, Status = "Exception", Message = ex.Message };

        //        }
        //}


        //[HttpPost("GetNidData")]
        //public async Task<Response> GetNidData([FromBody] string imageBase64)
        //{
        //    if (string.IsNullOrEmpty(imageBase64))
        //        return new Response { IsSuccess = false, Status = "Failed", Message = "Invalid Image Data" };

        //    try
        //    {
        //        var content = new StringContent(
        //            $"{{\"image_base64\": \"{imageBase64}\"}}",
        //            Encoding.UTF8,
        //            "application/json"
        //        );

        //        var response = await client.PostAsync("http://localhost:7060/extract-fields/", content);

        //        // Read the response content REGARDLESS of the status code
        //        var responseString = await response.Content.ReadAsStringAsync();

        //        if (response.IsSuccessStatusCode)
        //        {
        //            if (string.IsNullOrEmpty(responseString))
        //                return new Response { IsSuccess = false, Status = "Failed", Message = "No response from OCR service." };

        //            var nidImageResult = JsonConvert.DeserializeObject<NidImageResult>(responseString);
        //            if (nidImageResult == null)
        //                return new Response { IsSuccess = false, Status = "Failed", Message = "Failed to parse OCR response." };

        //            return new Response { IsSuccess = true, Status = "Success", Message = "NID data extracted successfully.", ObjResponse = nidImageResult };
        //        }

        //        else
        //        {
        //            var errorResponse = JsonConvert.DeserializeObject<FastAPIErrorResponse>(responseString);
        //            string errorMessage = errorResponse.detail;
        //            return new Response { IsSuccess = false, StatusCode = response.StatusCode.ToString(), Status = "Failed", Message = errorMessage };
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return new Response { IsSuccess = false, Status = "Exception", Message = $"An unexpected error occurred: {ex.Message}" };
        //    }
        //}

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
                var doctrResult =  GetDataFromDoctrModel(imageBase64);
                var paddleResult =  GetDataFromPaddleModel(imageBase64);

                // Wait for both tasks to complete
                await Task.WhenAll(doctrResult,paddleResult);

                if(!doctrResult.Result.IsSuccess && !paddleResult.Result.IsSuccess)
                    return new Response { IsSuccess = false, Status = "Failed", Message =doctrResult.Result.Message  };

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
                
                int isMissMatch = 0;

                if (paddleNidResult.Name.Replace(" ", "") == doctrNidResult.Name.Replace(" ", ""))
                {
                    if (paddleNidResult.Name == doctrNidResult.Name)
                    {
                        NidImageResult.Name = doctrNidResult.Name;
                    }
                    else
                    {
                        NidImageResult.Name = paddleNidResult.Name.Count(c => c == ' ') > doctrNidResult.Name.Count(c => c == ' ')?
                            paddleNidResult.Name:doctrNidResult.Name;
                        isMissMatch = 1;
                    }

                }
                else
                {
                    if (paddleNidResult.Name.Length == doctrNidResult.Name.Length)
                        NidImageResult.Name = paddleNidResult.Name;

                    else
                    {
                        NidImageResult.Name = paddleNidResult.Name.Length > doctrNidResult.Name.Length? paddleNidResult.Name: doctrNidResult.Name;
                        isMissMatch = 1;
                    }

                }

                //////-----------------------------------

                if (!string.IsNullOrEmpty(doctrNidResult.IDNO) && !string.IsNullOrEmpty(paddleNidResult.IDNO) && doctrNidResult.IDNO == paddleNidResult.IDNO)
                {
                    NidImageResult.IDNO = doctrNidResult.IDNO;
                }
                else
                {
                    NidImageResult.IDNO = !string.IsNullOrEmpty(doctrNidResult.IDNO) ? doctrNidResult.IDNO
                      : !string.IsNullOrEmpty(paddleNidResult.IDNO) ? paddleNidResult.IDNO : "";
                    isMissMatch = 1;
                }

                if (!string.IsNullOrEmpty(doctrNidResult.DateOfBirth) && !string.IsNullOrEmpty(paddleNidResult.DateOfBirth) && doctrNidResult.DateOfBirth == paddleNidResult.DateOfBirth)
                {
                    NidImageResult.DateOfBirth = doctrNidResult.DateOfBirth;
                }
                else
                {
                    NidImageResult.DateOfBirth = !string.IsNullOrEmpty(paddleNidResult.DateOfBirth) ? paddleNidResult.DateOfBirth :
                        !string.IsNullOrEmpty(doctrNidResult.DateOfBirth) ? doctrNidResult.DateOfBirth : "";
                    isMissMatch = 1;
                }

                //NidImageResult.IDNO = !string.IsNullOrEmpty(doctrNidResult.IDNO) ? doctrNidResult.IDNO
                //    :!string.IsNullOrEmpty(paddleNidResult.IDNO) ? paddleNidResult.IDNO:"";

                //NidImageResult.DateOfBirth = !string.IsNullOrEmpty(paddleNidResult.DateOfBirth) ? paddleNidResult.DateOfBirth:
                //    !string.IsNullOrEmpty(doctrNidResult.DateOfBirth) ? doctrNidResult.DateOfBirth :"";


                return new Response { IsSuccess = true, Status = "Success", Message = "NID data extracted successfully.", ObjResponse = NidImageResult,IsMissMatch=isMissMatch };
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
                var content = new StringContent(
                    $"{{\"image_base64\": \"{imageBase64}\"}}",
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await client.PostAsync("http://localhost:7000/extract-fields/", content);

                // Read the response content REGARDLESS of the status code
                var responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    if (string.IsNullOrEmpty(responseString))
                        return new Response { IsSuccess = false, Status = "Failed", Message = "No response from OCR service." };

                    var nidImageResult = JsonConvert.DeserializeObject<NidImageResult>(responseString);
                    if (nidImageResult == null)
                        return new Response { IsSuccess = false, Status = "Failed", Message = "Failed to parse OCR response." };

                    return new Response { IsSuccess = true, Status = "Success", Message = "NID data extracted successfully.", ObjResponse = nidImageResult };
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

                var response = await client.PostAsync("http://localhost:8000/extract-fields/", content);

                // Read the response content REGARDLESS of the status code
                var responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    if (string.IsNullOrEmpty(responseString))
                        return new Response { IsSuccess = false, Status = "Failed", Message = "No response from OCR service." };

                    var nidImageResult = JsonConvert.DeserializeObject<NidImageResult>(responseString);
                    if (nidImageResult == null)
                        return new Response { IsSuccess = false, Status = "Failed", Message = "Failed to parse OCR response." };

                    return new Response { IsSuccess = true, Status = "Success", Message = "NID data extracted successfully.", ObjResponse = nidImageResult };
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
                return new Response { IsSuccess = false, Status = "Failed", Message = ex.Message };
            }
        }

        private async Task<Response> RunOCRWithImagePath(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return new Response { IsSuccess = false, Status = "Failed", Message = "Image path is required." };

            string scriptPath = Path.Combine(_env.ContentRootPath, "Scripts", "combine_three_codes_(paddle_and_teseeract).py");
            string pythonExe = "C:\\Program Files\\Python312\\python.exe";

            var start = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"\"{scriptPath}\" \"{imagePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            try
            {
                using var process = Process.Start(start)!;
                string error = await process.StandardError.ReadToEndAsync();
                string output = await process.StandardOutput.ReadToEndAsync();

                var result = JsonConvert.DeserializeObject<NidImageResult>(output);
                if (result == null)
                    return new Response { IsSuccess = false, Status = "Failed", Message = error };

                return new Response
                {
                    IsSuccess = true,
                    Status = "Success",
                    Message = "NID data extracted.",
                    ObjResponse = result
                };


            }
            catch (Exception ex)
            {
                return new Response { IsSuccess = false, Status = "Exception", Message = ex.Message };
            }
        }

        #endregion


    }
}
