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

namespace OpposingViewpoints.Pages
{
    public class IndexModel : PageModel
    {
        [BindProperty]
        public string searchTerm { get; set; }
        public bool SearchesInCache { get; set; }
        [ViewData]
        public OrderedDictionary CachedSearches { get; set; } = new OrderedDictionary();

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

        public void OnGet()
        {
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

        public async Task CacheData(List<SSApiPaper> articles, string searchTerm)
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
            await CacheData(articles.data, searchTerm);
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

        //public async Task<SSResponse> GetScholarlyArticlesAsync(string topic)
        //{
        //    var options = new RestClientOptions("https://www.semanticscholar.org")
        //    {
        //        MaxTimeout = -1,
        //    };
        //    var client = new RestClient(options);
        //    var request = new RestRequest("/api/1/search", Method.Post);
        //    request.AddHeader("Content-Type", "application/json");
        //    request.AddHeader("Cookie", "tid=rBIABmQk/8lnMQAKBUXOAg==");
        //    var body = @"{""queryString"":""" + topic + @""",""page"":1,""pageSize"":10,""sort"":""relevance"",""authors"":[],""coAuthors"":[],""venues"":[],""yearFilter"":null,""requireViewablePdf"":false,""fieldsOfStudy"":[],""useFallbackRankerService"":false,""useFallbackSearchCluster"":false,""hydrateWithDdb"":true,""includeTldrs"":true,""performTitleMatch"":true,""includeBadges"":true,""tldrModelVersion"":""v2.0.0"",""getQuerySuggestions"":false,""useS2FosFields"":true}";
        //    request.AddStringBody(body, DataFormat.Json);
        //    RestResponse response = await client.ExecuteAsync(request);
        //    var responseObject = JsonSerializer.Deserialize<SSResponse>(response.Content);
        //    return responseObject;
        //}

        public async Task<SSApiResponse> GetScholarlyArticlesAsync(string topic)
        {
            var options = new RestClientOptions("http://api.semanticscholar.org")
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest($"/graph/v1/paper/search?query={topic.Replace(' ', '+')}&limit=10&fields=title,authors,abstract,url,journal,year,citationCount", Method.Get);
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

        //public async Task<object> TestAsync(string topic)
        //{
        //    var articles = await GetScholarlyArticlesAsync(topic);
        //    var text = articles.results.FirstOrDefault().paperAbstract.text;
        //    var bias = await AnalyzeTextChatGPTAsync(topic, text);
        //    var analysis = new
        //    {
        //        article = articles.results.FirstOrDefault(),
        //        bias = bias
        //    };
        //    return analysis;
        //}

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

        //public async Task<IActionResult> OnPostSearchArticlesChatGPTAsync()
        //{
        //    var prompt = "Give me a list of 9 articles, 3 of them with opposing viewpoints, 3 with moderate viewpoints and 3 supporting viewpoints about \"" + searchTerm + "\" in Json format. Please format it in the following way:\r\n{\r\n   \"articles\": [\r\n      {\r\n         \"title\": string,\r\n         \"author\": string,\r\n         \"publication\": string,\r\n         \"date\": date,\r\n         \"url\": string,\r\n         \"bias\": int\r\n      }\r\n   ]\r\n}\r\nfor the bias property use 0 as \"supporting\", 1 as \"moderate\", and 2 as \"against\".";
        //    var apiKey = _configuration["ChatGPT-Key"]; // Replace with your actual API key

        //    using var client = new HttpClient();
        //    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        //    var requestBody = new
        //    {
        //        messages = new List<object> { new { role = "user", content = prompt } },
        //        temperature = 0.5,
        //        max_tokens = 1000,
        //        model = "gpt-3.5-turbo"
        //    };

        //    var requestBodyJson = JsonSerializer.Serialize(requestBody);
        //    var content = new StringContent(requestBodyJson, System.Text.Encoding.UTF8, "application/json");

        //    var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);

        //    var responseJson = await response.Content.ReadAsStringAsync(); //testResponse; //
        //    var responseObject = JsonSerializer.Deserialize<OpenaiResponse>(responseJson);
        //    var openAiContent = JsonSerializer.Deserialize<OpenaiContent>(responseObject?.choices?.FirstOrDefault()?.message?.content);
        //    if (openAiContent != null)
        //    {
        //        var articles = new
        //        {
        //            For = openAiContent.articles.Where(x => x.bias == BiasEnum.For),
        //            Against = openAiContent.articles.Where(x => x.bias == BiasEnum.Against),
        //            Moderate = openAiContent.articles.Where(x => x.bias == BiasEnum.Neutral)
        //        };
        //        return new OkObjectResult(articles);
        //    }
        //    return new NoContentResult();
        //}

        //public string testResponse = "{\r\n  \"id\": \"chatcmpl-6zbObvSom0PxlgKbKnzYh3j4TKr3O\",\r\n  \"object\": \"chat.completion\",\r\n  \"created\": 1680139777,\r\n  \"model\": \"gpt-3.5-turbo-0301\",\r\n  \"usage\": {\r\n    \"prompt_tokens\": 127,\r\n    \"completion_tokens\": 751,\r\n    \"total_tokens\": 878\r\n  },\r\n  \"choices\": [\r\n    {\r\n      \"message\": {\r\n        \"role\": \"assistant\",\r\n        \"content\": \"{\\n   \\\"articles\\\": [\\n      {\\n         \\\"title\\\": \\\"Why Gun Control Is Still a Political Non-Starter\\\",\\n         \\\"author\\\": \\\"Jennifer Mascia\\\",\\n         \\\"publication\\\": \\\"The Trace\\\",\\n         \\\"date\\\": \\\"2020-02-07\\\",\\n         \\\"url\\\": \\\"https://www.thetrace.org/2020/02/gun-control-politics-2020-election/\\\",\\n         \\\"bias\\\": 1\\n      },\\n      {\\n         \\\"title\\\": \\\"The Case for Gun Control\\\",\\n         \\\"author\\\": \\\"Michael Waldman\\\",\\n         \\\"publication\\\": \\\"The Atlantic\\\",\\n         \\\"date\\\": \\\"2019-08-05\\\",\\n         \\\"url\\\": \\\"https://www.theatlantic.com/magazine/archive/2019/09/the-case-for-gun-control/594197/\\\",\\n         \\\"bias\\\": 0\\n      },\\n      {\\n         \\\"title\\\": \\\"The Second Amendment Is Not Absolute\\\",\\n         \\\"author\\\": \\\"David S. Cohen\\\",\\n         \\\"publication\\\": \\\"The New York Times\\\",\\n         \\\"date\\\": \\\"2019-08-09\\\",\\n         \\\"url\\\": \\\"https://www.nytimes.com/2019/08/09/opinion/guns-second-amendment.html\\\",\\n         \\\"bias\\\": 0\\n      },\\n      {\\n         \\\"title\\\": \\\"Why Gun Control Is Not the Answer\\\",\\n         \\\"author\\\": \\\"John R. Lott Jr.\\\",\\n         \\\"publication\\\": \\\"National Review\\\",\\n         \\\"date\\\": \\\"2019-08-13\\\",\\n         \\\"url\\\": \\\"https://www.nationalreview.com/2019/08/gun-control-not-answer-mass-shootings/\\\",\\n         \\\"bias\\\": 2\\n      },\\n      {\\n         \\\"title\\\": \\\"The Limits of Gun Control\\\",\\n         \\\"author\\\": \\\"Jeffrey Goldberg\\\",\\n         \\\"publication\\\": \\\"The Atlantic\\\",\\n         \\\"date\\\": \\\"2015-10-05\\\",\\n         \\\"url\\\": \\\"https://www.theatlantic.com/politics/archive/2015/10/the-limits-of-gun-control/409936/\\\",\\n         \\\"bias\\\": 1\\n      },\\n      {\\n         \\\"title\\\": \\\"Why Gun Control Is a Losing Issue for Democrats\\\",\\n         \\\"author\\\": \\\"David A. Graham\\\",\\n         \\\"publication\\\": \\\"The Atlantic\\\",\\n         \\\"date\\\": \\\"2019-08-05\\\",\\n         \\\"url\\\": \\\"https://www.theatlantic.com/politics/archive/2019/08/gun-control-democrats-2020-elections/595312/\\\",\\n         \\\"bias\\\": 1\\n      },\\n      {\\n         \\\"title\\\": \\\"A Modest Proposal for Gun Control\\\",\\n         \\\"author\\\": \\\"Nicholas Kristof\\\",\\n         \\\"publication\\\": \\\"The New York Times\\\",\\n         \\\"date\\\": \\\"2019-08-07\\\",\\n         \\\"url\\\": \\\"https://www.nytimes.com/2019/08/07/opinion/gun-control.html\\\",\\n         \\\"bias\\\": 0\\n      },\\n      {\\n         \\\"title\\\": \\\"The Case Against Gun Control\\\",\\n         \\\"author\\\": \\\"Jacob Sullum\\\",\\n         \\\"publication\\\": \\\"Reason\\\",\\n         \\\"date\\\": \\\"2019-08-05\\\",\\n         \\\"url\\\": \\\"https://reason.com/2019/08/05/the-case-against-gun-control/\\\",\\n         \\\"bias\\\": 2\\n      },\\n      {\\n         \\\"title\\\": \\\"Why We Need Gun Control Now More Than Ever\\\",\\n         \\\"author\\\": \\\"Sandy Phillips\\\",\\n         \\\"publication\\\": \\\"CNN\\\",\\n         \\\"date\\\": \\\"2018-02-14\\\",\\n         \\\"url\\\": \\\"https://www.cnn.com/2018/02/14/opinions/gun-control-now-opinion-phillips/index.html\\\",\\n         \\\"bias\\\": 0\\n      }\\n   ]\\n}\"\r\n      },\r\n      \"finish_reason\": \"stop\",\r\n      \"index\": 0\r\n    }\r\n  ]\r\n}\r\n";
    }
}