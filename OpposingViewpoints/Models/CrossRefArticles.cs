using OpposingViewpoints.Enums;
using System.Text.RegularExpressions;

namespace OpposingViewpoints.Models
{

    public class CrossRefResponse
    {
        public string status { get; set; }
        public string messagetype { get; set; }
        public string messageversion { get; set; }
        public Message message { get; set; }
    }

    public class Message
    {
        public int totalresults { get; set; }
        public Article[] items { get; set; }
        public int itemsperpage { get; set; }
        public Query query { get; set; }
    }


    public class Query
    {
        public int startindex { get; set; }
        public string searchterms { get; set; }
    }

    public class Article
    {
        public string[] title { get; set; }
        public Author[] author { get; set; }
        public string @abstract { get; set; }
        public BiasEnum bias { get; set; }
        public string abstr
        {
            get 
            {
                var tmp = Regex.Replace(@abstract, "<.*?>", String.Empty);
                tmp = Regex.Unescape(tmp);
                return tmp;
            }
        }
        public string abstrTruncLong
        {
            get
            {
                if (this.abstrTruncShort.Length < 153) return "";
                return this.abstr?.Length > 500 ? "..." + this.abstr.Substring(150, 350) + "..." : this.abstr.Substring(150, abstr.Length - 150);
            }
        }
        public string abstrTruncShort
        {
            get
            {
                return this.abstr?.Length > 150 ? this.abstr.Substring(0, 150) + "..." : this.abstr;
            }
        }
        public DateTime? pubDate
        {
            get
            {
                if (published != null && published.dateparts != null && published.dateparts.Length > 0)
                {
                    if (DateTime.TryParse(published.dateparts[0] + "/" + published.dateparts[1], out var date))
                    {
                        return date;
                    }
                }
                return null;
            }
        }
        public string URL { get; set; }
        public Published published { get; set; }
        public Link[] link { get; set; }
        public List<string> links
        {
            get
            {
                var lnks = new List<string>();
                if (link != null)
                {
                    lnks.AddRange(link.Select(x => x.URL));
                }
                if (URL != null)
                {
                    lnks.Add(URL);
                }
                return lnks;
            }
        }
        public string page { get; set; }
    }

    public class Published
    {
        public int[][] dateparts { get; set; }
    }

    public class Author
    {
        public string given { get; set; }
        public string family { get; set; }
        public string sequence { get; set; }
        public object[] affiliation { get; set; }
    }

    public class Link
    {
        public string URL { get; set; }
        public string contenttype { get; set; }
        public string contentversion { get; set; }
        public string intendedapplication { get; set; }
    }


}

