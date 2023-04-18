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

namespace OpposingViewpoints.Pages
{
    public class IndexModel : PageModel
    {
        [BindProperty]
        public string searchTerm { get; set; }

        public bool SearchesInCache { get; set; }

        [ViewData]
        public OrderedDictionary CachedSearches { get; set; } = new OrderedDictionary();

        public bool TopicsInCache { get; set; }
        public List<ControversialTopic> ControversialTopics { get; set; }

        private readonly ILogger<IndexModel> _logger;
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _memoryCache;

        public IndexModel(ILogger<IndexModel> logger, IHttpContextAccessor contextAccessor, IConfiguration configuration, IMemoryCache memoryCache)
        {
            _logger = logger;
            _contextAccessor = contextAccessor;
            _configuration = configuration;
            _memoryCache = memoryCache;
        }

        public async Task OnGetAsync()
        {
            var topics = new List<ControversialTopic>();
            if (_memoryCache.TryGetValue("TodaysTopics", out topics))
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
            var cachedSearches = new OrderedDictionary();
            if (_memoryCache.TryGetValue("CachedSearches", out cachedSearches))
            {
                if (cachedSearches.Count > 0) 
                {
                    CachedSearches = cachedSearches;
                    SearchesInCache = true;
                }
            }
        }

        public void CacheTodaysTopics(List<ControversialTopic> proconResponses)
        {
            _memoryCache.Set("TodaysTopics", proconResponses, TimeSpan.FromDays(1));
        }

        public async Task CacheSearchResults(List<SSApiPaper> articles, string searchTerm)
        {
            OrderedDictionary cachedSearches;
            if (_memoryCache.TryGetValue("CachedSearches", out cachedSearches))
            {
                if (cachedSearches.Count > 0)
                {
                    if (cachedSearches.Contains(searchTerm.ToLower()))
                    {
                        return;
                    }
                    if (cachedSearches.Count > 10)
                    {
                        cachedSearches.RemoveAt(cachedSearches.Count - 1);
                    }
                }
            }
            if (cachedSearches == null)
            {
                cachedSearches = new OrderedDictionary();
            }
            cachedSearches.Insert(0, searchTerm.ToLower(), new CacheModel
            {
                Date = DateTime.Now,
                SearchTerm = searchTerm,
                Articles = articles
            });
            _memoryCache.Set("CachedSearches", cachedSearches, TimeSpan.FromDays(1));
        }

        public async Task<List<SSApiPaper>> GetArticlesFromCache(string searchTerm)
        {
            OrderedDictionary cachedSearches;
            if (_memoryCache.TryGetValue("CachedSearches", out cachedSearches))
            {
                if (cachedSearches.Contains(searchTerm.ToLower()))
                {
                    var cachedSearch = cachedSearches[searchTerm.ToLower()] as CacheModel;
                    return cachedSearch.Articles;
                }
            }
            return new List<SSApiPaper>();
        }

        public async Task<IActionResult> OnPostSearchArticlesAsync()
        {
            List<SSApiPaper> articlesFromCache = await GetArticlesFromCache(searchTerm);
            if (articlesFromCache.Count > 0) 
            {
                _contextAccessor.HttpContext.Session.SetString("Articles", JsonSerializer.Serialize(articlesFromCache));
                return RedirectToPage("Articles");
            }
            var articles = await GetArticlesAndBiasesAsync(searchTerm);
            var articlesJson = JsonSerializer.Serialize(articles.data);
            _contextAccessor.HttpContext.Session.SetString("Articles", articlesJson);
            await CacheSearchResults(articles.data, searchTerm);
            return RedirectToPage("Articles");
        }
        public async Task<IActionResult> OnGetCachedArticlesAsync()
        {
            var searchTerm = Request.Query.FirstOrDefault().Value.ToString();
            List<SSApiPaper> articlesFromCache = await GetArticlesFromCache(searchTerm);
            if (articlesFromCache.Count > 0)
            {
                _contextAccessor.HttpContext.Session.SetString("Articles", JsonSerializer.Serialize(articlesFromCache));
                return RedirectToPage("Articles");
            }
            return NotFound();
        }

        public async Task<SSApiResponse> GetScholarlyArticlesAsync(string topic)
        {
            var options = new RestClientOptions("http://api.semanticscholar.org")
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest($"/graph/v1/paper/search?query={topic.Replace(' ', '+')}&limit=12&fields=title,authors,abstract,url,journal,year,citationCount", Method.Get);
            RestResponse response = await client.ExecuteAsync(request);
            var responseObject = JsonSerializer.Deserialize<SSApiResponse>(response.Content);
            return responseObject;
        }

        public async Task<SSApiResponse> GetArticlesAndBiasesAsync(string topic)
        {
            var articles = await GetScholarlyArticlesAsync(topic);
            foreach (var article in articles.data)
            {
                var text = article.@abstract;
                var bias = await AnalyzeTextChatGPTAsync(topic, text);
                article.bias = bias;
            }
            return articles;
        }

        public async Task<BiasEnum> AnalyzeTextChatGPTAsync(string topic, string text)
        {
            string prompt = "Analyze the following text and tell me if for, against, or is neutral towards the topic \"" + topic + "\". respond with an integer 0 for \"for\", 1 for \"against\", and 2 for \"neutral\" and no other text:\r\n\"" + text + "\"";
            var apiKey = _configuration["ChatGPT-Key"];
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var requestBody = new
            {
                messages = new List<object> { new { role = "user", content = prompt } },
                temperature = 0.5,
                max_tokens = 1000,
                model = "gpt-3.5-turbo"
            };
            var requestBodyJson = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(requestBodyJson, System.Text.Encoding.UTF8, "application/json");

            var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);

            var responseJson = await response.Content.ReadAsStringAsync(); //testResponse; //
            var responseObject = JsonSerializer.Deserialize<OpenaiResponse>(responseJson);
            BiasEnum bias;
            Enum.TryParse<BiasEnum>(responseObject?.choices?.FirstOrDefault()?.message?.content, out bias);
            return bias;
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
                    var text = topic.InnerHtml;
                    var topicHtml = await httpClient.GetStringAsync(href);
                    var topicHtmlDoc = new HtmlDocument();
                    topicHtmlDoc.LoadHtml(topicHtml);
                    var topicDescriptionNode = topicHtmlDoc.DocumentNode.SelectSingleNode("//meta[@property='og:description']");
                    var topicImageNode = topicHtmlDoc.DocumentNode.SelectSingleNode("//meta[@property='og:image']");
                    var topicDescription = topicDescriptionNode.Attributes["content"].Value;
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
            CacheTodaysTopics(responses);
            return responses;
        }
    }
}