namespace NidDataExtractApi.Models
{
    public class ImageResult
    {
        public bool verified { get; set; } 
        public double distance { get; set; }
        public double threshold { get; set; }
        public string model { get; set; }
        public string detector_backend { get; set; }
        public string similarity_metric { get; set; }
        public double time { get; set; }
    }
}
