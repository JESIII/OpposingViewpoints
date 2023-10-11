using Microsoft.AspNetCore.Mvc.RazorPages;
using OpposingViewpoints.Models;
using System.Text.Json;
using OpposingViewpoints.Enums;
using RestSharp;
using System.Collections.Generic;

namespace OpposingViewpoints.Pages
{
    public class ArticlesModel : PageModel
    {
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly IConfiguration _configuration;
        private readonly ICache _cache;
        public List<Article> Articles { get; set; }
        public int PageNo { get; set; }
        public string Topic { get; set; }
        public ArticlesModel(IHttpContextAccessor contextAccessor, IConfiguration configuration, ICache cache)
        {
            _contextAccessor = contextAccessor;
            _configuration = configuration;
            _cache = cache;
        }
        public async Task OnGetAsync(string topic, int pageNo = 0)
        {
            Topic = topic;
            PageNo = pageNo;
            var allArticles = new List<Article>();
            for (int i = 0; i <= PageNo; i++)
            {
                var articlesFromCache = await _cache.GetArticlesFromCache(Topic, i);
                if (articlesFromCache.Count > 0)
                {
                    allArticles.AddRange(articlesFromCache);
                    continue;
                }
                var articles = await GetArticlesAndBiasesAsync(Topic);
                _cache.CacheSearchResults(articles.results.ToList(), Topic, i);
                allArticles.AddRange(articles.results);
            }
            Articles = allArticles;
        }

        public async Task<Articles> GetScholarlyArticlesAsync(string topic)
        {
            var options = new RestClientOptions("https://api.core.ac.uk")
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest("/v3/search/outputs", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", "Bearer M1gk0Qlroajv5bJiFOH9xnLI2YzTPc6h");
            request.AddHeader("Cookie", "AWSALB=g/upfKkkuuCvzAOhFnnN1s23b3iuNlZ7oyy7fbPV42rdR5b1LRLu608AbjtAtGESfP/gAQwMaEymL38SngK4lEV4qCy3rYjty3V0jsDpbOt7AdKjs3hncgARIgE9; AWSALBCORS=g/upfKkkuuCvzAOhFnnN1s23b3iuNlZ7oyy7fbPV42rdR5b1LRLu608AbjtAtGESfP/gAQwMaEymL38SngK4lEV4qCy3rYjty3V0jsDpbOt7AdKjs3hncgARIgE9; AWSALBTG=KiWMtnEHXuokQZZ3BqWsCX7gTeJBfRPtVu1rSmqTJ6ffECzF0X3+YX38HHYFc2ZgbkD75bW7bccTjwgPHQ8RVRvE5paWCAKgfCoFfpYbyl3G37FhGT5PZk5CDKo7MXwDbddKrS2yUIQgJYkPTBYlHoVySFzddLYGh3h9mSRFxQmYj9n5HZ4=; AWSALBTGCORS=KiWMtnEHXuokQZZ3BqWsCX7gTeJBfRPtVu1rSmqTJ6ffECzF0X3+YX38HHYFc2ZgbkD75bW7bccTjwgPHQ8RVRvE5paWCAKgfCoFfpYbyl3G37FhGT5PZk5CDKo7MXwDbddKrS2yUIQgJYkPTBYlHoVySFzddLYGh3h9mSRFxQmYj9n5HZ4=");
            var param = new
            {
                q = topic,
                offset = PageNo,
                limit = 5,
                entity_type = "outputs",
                exclude = new string[]
                {
                    "acceptedDate",
                    "arxivId",
                    "outputs",
                    "depositedDate",
                    "fullText",
                    "magId",
                    "references"
                }
            };
            var body = JsonSerializer.Serialize(param);
            request.AddStringBody(body, DataFormat.Json);
            RestResponse response = await client.ExecuteAsync(request);
            return JsonSerializer.Deserialize<Articles>(response.Content);
        }


        public async Task<Articles> GetArticlesAndBiasesAsync(string topic)
        {
            var articles = await GetScholarlyArticlesAsync(topic);
            foreach (var article in articles.results)
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
            Enum.TryParse(responseObject?.choices?.FirstOrDefault()?.message?.content, out BiasEnum bias);
            return bias;
        }
    }
}
