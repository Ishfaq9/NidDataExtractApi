using System.ComponentModel;

namespace NidDataExtractApi.Models
{
    public class Response
    {
        [DefaultValue(false)]
        public bool IsSuccess { get; set; }
        public int? IsMissMatch { get; set; } = 0;
        public string? StatusCode { get; set; }
        public string? Status { get; set; }
        public string? Message { get; set; }
        public object? ObjResponse { get; set; }
    }
}
