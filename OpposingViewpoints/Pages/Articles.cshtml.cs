using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpposingViewpoints.Models;
using System.Text.Json.Serialization;
using System.Text.Json;
using OpposingViewpoints.Enums;
using RestSharp;
using System.Collections.Specialized;
using Microsoft.Extensions.Caching.Memory;

namespace OpposingViewpoints.Pages
{
    public class ArticlesModel : PageModel
    {
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly IMemoryCache _memoryCache;
        private readonly IConfiguration _configuration;
        public List<SSApiPaper> Articles { get; set; }
        public string Topic { get; set; }
        public ArticlesModel(IHttpContextAccessor contextAccessor, IMemoryCache memoryCache, IConfiguration configuration)
        {
            _contextAccessor = contextAccessor;
            _memoryCache = memoryCache;
            _configuration = configuration;
        }
        public async Task OnGetAsync(string topic)
        {
            Topic = topic;
            List<SSApiPaper> articlesFromCache = await GetArticlesFromCache(topic);
            if (articlesFromCache.Count > 0)
            {
                Articles = articlesFromCache;
                return;
            }
            var articles = await GetArticlesAndBiasesAsync(topic);
            CacheSearchResults(articles.data, topic);
            if (articles != null)
            {
                Articles = articles.data;
            }
        }
        public async Task<SSApiResponse> GetScholarlyArticlesAsync(string topic)
        {
            var options = new RestClientOptions("http://api.semanticscholar.org")
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest($"/graph/v1/paper/search?query={topic.Replace(' ', '+')}&limit=15&fields=title,authors,abstract,url,journal,year,citationCount", Method.Get);
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
    }
}
