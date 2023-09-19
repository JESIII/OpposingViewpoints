using System.Collections.Specialized;

namespace OpposingViewpoints.Models
{
    public class CacheModel
    {
        public DateTime Date { get;set; }
        public string SearchTerm { get; set; }
        public List<Article> Articles { get; set; }
    }

}
