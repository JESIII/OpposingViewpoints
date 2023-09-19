using OpposingViewpoints.Enums;

namespace OpposingViewpoints.Models
{


    public class Articles
    {
        public int totalHits { get; set; }
        public int limit { get; set; }
        public int offset { get; set; }
        public object scrollId { get; set; }
        public Article[] results { get; set; }
        public object tooks { get; set; }
        public object esTook { get; set; }
    }

    public class Article
    {
        public Author[] authors { get; set; }
        public BiasEnum bias { get; set; }
        public DateTime? createdDate { get; set; }
        public string documentType { get; set; }
        public string doi { get; set; }
        public string downloadUrl { get; set; }
        public int id { get; set; }
        public string title { get; set; }
        public object publishedDate { get; set; }
        public string publisher { get; set; }
        public string[] sourceFulltextUrls { get; set; }
        public string updatedDate { get; set; }
        public string yearPublished { get; set; }
        public Link[] links { get; set; }
        public string @abstract { get; set; }
        public string abstr
        {
            get { return this.@abstract; }
        }
        public string abstrTrunc
        {
            get
            {
                return this.@abstract?.Length > 300 ? this.@abstract.Substring(0, 300) + "..." : this.@abstract;
            }
        }
        public string[] tags { get; set; }
        public string fulltextStatus { get; set; }
        public string[] subjects { get; set; }
        public string oai { get; set; }
        public string deleted { get; set; }
        public bool disabled { get; set; }
        public Journal[] journals { get; set; }
        public string[] urls { get; set; }
        public DateTime? lastUpdate { get; set; }
    }

    public class Journal
    {
        public string title { get; set; }
    }

    public class Author
    {
        public string name { get; set; }
    }

    public class Link
    {
        public string type { get; set; }
        public string url { get; set; }
    }

    //public class Articles
    //{
    //    public int totalHits { get; set; }
    //    public int limit { get; set; }
    //    public int offset { get; set; }
    //    public object scrollId { get; set; }
    //    public Article[] results { get; set; }
    //    public object tooks { get; set; }
    //    public object esTook { get; set; }
    //}

    //public class Article
    //{
    //    public Author[] authors { get; set; }
    //    public BiasEnum bias { get; set; }
    //    public object citationCount { get; set; }
    //    public string[] contributors { get; set; }
    //    public DateTime createdDate { get; set; }
    //    public string @abstract { get; set; }
    //    public string abstr
    //    {
    //        get { return this.@abstract; }
    //    }
    //    public string abstrTrunc
    //    {
    //        get
    //        {
    //            return this.@abstract?.Length > 300 ? this.@abstract.Substring(0, 300) + "..." : this.@abstract;
    //        }
    //    }
    //    public string documentType { get; set; }
    //    public string doi { get; set; }
    //    public string downloadUrl { get; set; }
    //    public object fieldOfStudy { get; set; }
    //    public int id { get; set; }
    //    public string title { get; set; }
    //    public DateTime? publishedDate { get; set; }
    //    public string publisher { get; set; }
    //    public string[] sourceFulltextUrls { get; set; }
    //    public DateTime? updatedDate { get; set; }
    //    public int? yearPublished { get; set; }
    //    public Journal[] journals { get; set; }
    //    public Link[] links { get; set; }
    //}

    //public class Author
    //{
    //    public string name { get; set; }
    //}

    //public class Journal
    //{
    //    public string title { get; set; }
    //    public string[] identifiers { get; set; }
    //}

    //public class Link
    //{
    //    public string type { get; set; }
    //    public string url { get; set; }
    //}
}
