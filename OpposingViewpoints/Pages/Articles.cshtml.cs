using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpposingViewpoints.Models;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace OpposingViewpoints.Pages
{
    public class ArticlesModel : PageModel
    {
        private readonly IHttpContextAccessor _contextAccessor;
        public List<SSArticle> Articles { get; set; }
        public ArticlesModel(IHttpContextAccessor contextAccessor)
        {
            _contextAccessor = contextAccessor;
        }
        public void OnGet()
        {
            var articles = JsonSerializer.Deserialize<List<SSArticle>>(_contextAccessor.HttpContext.Session.GetString("Articles"));
            if (articles != null)
            {
                Articles = articles;
            }
        }
    }
}
