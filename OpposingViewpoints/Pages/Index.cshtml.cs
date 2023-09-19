using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using OpposingViewpoints.Models;
using System.Collections.Generic;
using OpposingViewpoints.Enums;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using RestSharp;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Specialized;
using System.ComponentModel;
using HtmlAgilityPack;
using System.Web;

namespace OpposingViewpoints.Pages
{
    public class IndexModel : PageModel
    {
        [BindProperty]
        public string searchTerm { get; set; }

        public bool SearchesInCache { get; set; }

        public bool TopicsInCache { get; set; }
        public List<ControversialTopic> ControversialTopics { get; set; }

        private readonly ILogger<IndexModel> _logger;
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly IConfiguration _configuration;
        private readonly ICache _cache;

        public IndexModel(ILogger<IndexModel> logger, IHttpContextAccessor contextAccessor, IConfiguration configuration, ICache cache)
        {
            _logger = logger;
            _contextAccessor = contextAccessor;
            _configuration = configuration;
            _cache = cache;
        }

        public async Task OnGetAsync()
        {
            var topics = await _cache.GetTodaysTopicsFromCache();
            if (topics.Any())
            {
                TopicsInCache = true;
                ControversialTopics = topics;
            }
            else
            {
                topics = await GetControversialTopics();
                TopicsInCache = true;
                ControversialTopics = topics;
            }
        }

        public async Task<IActionResult> OnPostSearchArticlesAsync()
        {
            return RedirectToPage("Articles", new { topic = searchTerm });
        }

        public async Task<IActionResult> OnGetSearchArticlesAsync()
        {
            if (Request.Query.Keys.Contains("value")) searchTerm = Request.Query["value"].ToString();
            return RedirectToPage("Articles", new { topic = searchTerm });
        }


        public async Task<IActionResult> OnGetCachedArticlesAsync()
        {
            var searchTerm = Request.Query.FirstOrDefault().Value.ToString();
            List<Article> articlesFromCache = await _cache.GetArticlesFromCache(searchTerm);
            if (articlesFromCache.Count > 0)
            {
                _contextAccessor.HttpContext.Session.SetString($"Articles/{searchTerm}", JsonSerializer.Serialize(articlesFromCache));
                return RedirectToPage("Articles", new {topic = searchTerm});
            }
            return NotFound();
        }

        public async Task<List<ControversialTopic>> GetControversialTopics()
        {
            var url = "https://www.procon.org/";
            var httpClient = new HttpClient();
            var html = await httpClient.GetStringAsync(url);

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            var responses = new List<ControversialTopic>();
            var newTopicsList = htmlDoc.DocumentNode.SelectSingleNode("//h4[contains(text(),'NEW TOPICS')]/following-sibling::ul");
            var topics = newTopicsList.SelectNodes("./li/a");

            foreach (var topic in topics)
            {
                try
                {
                    var href = topic.Attributes["href"].Value;
                    var text = HttpUtility.HtmlDecode(topic.InnerHtml);
                    var topicHtml = await httpClient.GetStringAsync(href);
                    var topicHtmlDoc = new HtmlDocument();
                    topicHtmlDoc.LoadHtml(topicHtml);
                    var topicDescriptionNode = topicHtmlDoc.DocumentNode.SelectSingleNode("//meta[@property='og:description']");
                    var topicImageNode = topicHtmlDoc.DocumentNode.SelectSingleNode("//meta[@property='og:image']");
                    var topicDescription = HttpUtility.HtmlDecode(topicDescriptionNode.Attributes["content"].Value);
                    var topicImage = topicImageNode.Attributes["content"].Value;

                    responses.Add(new ControversialTopic
                    {
                        image = topicImage,
                        description = topicDescription,
                        topic = text,
                        link = href
                    });

                }
                catch { }
            }
            _cache.CacheTodaysTopics(responses);
            return responses;
        }
    }
}