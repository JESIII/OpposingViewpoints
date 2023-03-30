using OpposingViewpoints.Enums;
using OpposingViewpoints.Models;

namespace OpposingViewpoints.Models
{
    public class OpenaiResponse
    {
        public OpenaiResponseChoice[] choices { get; set; }
    }

    public class OpenaiResponseChoice
    {
        public OpenaiMessage message { get; set; }
    }

    public class OpenaiMessage
    {
        public string content { get; set; } 
    }

    public class OpenaiContent
    {
        public List<OpenaiArticle> articles { get; set; }
    }

    public class OpenaiArticle
    {
        public string title { get; set; }
        public string author { get; set; }
        public string publication { get; set; }
        public DateTime date { get; set; }
        public string url { get; set; }
        public BiasEnum bias { get; set; }
    }
}
