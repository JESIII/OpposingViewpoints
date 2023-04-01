using OpposingViewpoints.Enums;
using System.Collections.Generic;

namespace OpposingViewpoints.Models
{
    public class SSApiResponse
    {
        public List<SSApiPaper> data { get; set; }
        
    }

    public class SSApiPaper
    {
        public string title { get; set; }
        public string @abstract { get; set; }
        public string abstr
        {
            get { return this.@abstract; }
        }
        public string url { get; set; }
        public List<SSApiAuthor> authors { get; set; }
        public BiasEnum bias { get; set; }
        public SSApiJournal journal { get; set; }
    }

    public class SSApiJournal
    {
        public string name { get; set; } = "N/A";
    }

    public class SSApiAuthor
    {
        public string name { get; set; }
    }

    //public class SSResponse
    //{
    //    public List<SSArticle> results { get; set; }
    //}
    //public class SSArticle
    //{
    //    public SSTitle title { get; set; }
    //    public SSAbstract paperAbstract { get; set; }
    //    public string paperAbstractTruncated { get; set; }
    //    public SSCitationStats citationStats { get; set; }
    //    public List<SSLink> links { get; set; }
    //    public List<SSLink> alternatePaperLinks { get; set; }
    //    public BiasEnum bias { get; set; }
    //    public List<List<SSAuthor>> authors { get; set; }
    //    public SSJournal journal { get; set; }

    //}
    //public class SSTitle
    //{
    //    public string text { get; set; }
    //}
    //public class SSAbstract
    //{
    //    public string text { get; set; }
    //}
    //public class SSAuthors
    //{
    //    public List<SSAuthor> authors { get; set; }
    //}
    //public class SSAuthor
    //{
    //    public string name { get; set; }
    //}
    //public class SSYear 
    //{
    //    public string text { get; set; }
    //}
    //public class SSJournal
    //{
    //    public string name { get; set; }
    //}
    //public class SSLink
    //{
    //    public string url { get; set; }
    //}
    //public class SSCitationStats
    //{
    //    public int numCitations { get; set; }
    //}
}
