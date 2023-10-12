using Microsoft.AspNetCore.Mvc.RazorPages;
using OpposingViewpoints.Models;
using System.Text.Json;
using OpposingViewpoints.Enums;
using RestSharp;
using GroupDocs.Classification;
using GroupDocs.Classification.DTO;

namespace OpposingViewpoints.Pages
{
    public class ArticlesModel : PageModel
    {
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly IConfiguration _configuration;
        private readonly ICache _cache;
        public List<Article> Articles { get; set; }
        public int Offset { get; set; }
        public string Topic { get; set; }
        public ArticlesModel(IHttpContextAccessor contextAccessor, IConfiguration configuration, ICache cache)
        {
            _contextAccessor = contextAccessor;
            _configuration = configuration;
            _cache = cache;
        }
        public async Task OnGetAsync(string topic, int offset = 0)
        {
            Topic = topic;
            Offset = offset;
            var allArticles = new List<Article>();
            for (int i = 0; i <= Offset; i+=5)
            {
                var articlesFromCache = await _cache.GetArticlesFromCache(Topic, i);
                if (articlesFromCache.Count > 0)
                {
                    allArticles.AddRange(articlesFromCache);
                    continue;
                }
                var articles = await GetArticlesAndBiasesAsync(Topic);
                _cache.CacheSearchResults(articles, Topic, i);
                allArticles.AddRange(articles);
            }
            Articles = allArticles;
        }

        //public async Task<Articles> GetScholarlyArticlesAsync(string topic)
        //{
        //    var options = new RestClientOptions("https://api.core.ac.uk")
        //    {
        //        MaxTimeout = -1,
        //    };
        //    var client = new RestClient(options);
        //    var request = new RestRequest("/v3/search/outputs", Method.Post);
        //    request.AddHeader("Content-Type", "application/json");
        //    request.AddHeader("Authorization", "Bearer M1gk0Qlroajv5bJiFOH9xnLI2YzTPc6h");
        //    request.AddHeader("Cookie", "AWSALB=g/upfKkkuuCvzAOhFnnN1s23b3iuNlZ7oyy7fbPV42rdR5b1LRLu608AbjtAtGESfP/gAQwMaEymL38SngK4lEV4qCy3rYjty3V0jsDpbOt7AdKjs3hncgARIgE9; AWSALBCORS=g/upfKkkuuCvzAOhFnnN1s23b3iuNlZ7oyy7fbPV42rdR5b1LRLu608AbjtAtGESfP/gAQwMaEymL38SngK4lEV4qCy3rYjty3V0jsDpbOt7AdKjs3hncgARIgE9; AWSALBTG=KiWMtnEHXuokQZZ3BqWsCX7gTeJBfRPtVu1rSmqTJ6ffECzF0X3+YX38HHYFc2ZgbkD75bW7bccTjwgPHQ8RVRvE5paWCAKgfCoFfpYbyl3G37FhGT5PZk5CDKo7MXwDbddKrS2yUIQgJYkPTBYlHoVySFzddLYGh3h9mSRFxQmYj9n5HZ4=; AWSALBTGCORS=KiWMtnEHXuokQZZ3BqWsCX7gTeJBfRPtVu1rSmqTJ6ffECzF0X3+YX38HHYFc2ZgbkD75bW7bccTjwgPHQ8RVRvE5paWCAKgfCoFfpYbyl3G37FhGT5PZk5CDKo7MXwDbddKrS2yUIQgJYkPTBYlHoVySFzddLYGh3h9mSRFxQmYj9n5HZ4=");
        //    var param = new
        //    {
        //        q = topic,
        //        offset = Offset,
        //        limit = 5,
        //        entity_type = "outputs",
        //        exclude = new string[]
        //        {
        //            "acceptedDate",
        //            "arxivId",
        //            "outputs",
        //            "depositedDate",
        //            "fullText",
        //            "magId",
        //            "references"
        //        }
        //    };
        //    var body = JsonSerializer.Serialize(param);
        //    request.AddStringBody(body, DataFormat.Json);
        //    RestResponse response = await client.ExecuteAsync(request);
        //    return JsonSerializer.Deserialize<Articles>(response.Content);
        //}
        public async Task<List<Article>> GetScholarlyArticlesAsync(string topic)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.crossref.org/works?rows=10&offset={Offset}&select=title%2Cauthor%2Cabstract%2CURL%2Cpublished%2Clink%2Cpage&query={Topic}&filter=has-abstract%3A1%2Cfrom-pub-date%3A2000&mailto=zirjohn97+crossref@gmail.com");
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            var crossRefs = JsonSerializer.Deserialize<CrossRefResponse>(responseString);
            return crossRefs.message.items.ToList();
        }



        public async Task<List<Article>> GetArticlesAndBiasesAsync(string topic)
        {
            var articles = await GetScholarlyArticlesAsync(topic);
            foreach (var article in articles)
            {
                var text = article.abstr;
                //var bias = await AnalyzeTextSentiment(article.title + " " + text);
                var bias = await AnalyzeTextChatGPTAsync(topic, text);
                article.bias = bias;
            }
            return articles.Where(x => x.bias != BiasEnum.Irrelevant).ToList();
        }

        public async Task<BiasEnum> AnalyzeTextChatGPTAsync(string topic, string text)
        {
            string prompt = "Analyze the following text and tell me if the writer it supports, is against, or is neutral towards \"" + topic + "\". Respond with an integer 0 if \"in support\", 1 if \"against\", 2 if \"neutral or unopinionated\", and 3 if the text is irrelevant to the topic:\r\n\"" + text + "\"";
            var apiKey = _configuration["ChatGPT-Key"];
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var requestBody = new
            {
                messages = new List<object> { new { role = "user", content = prompt } },
                temperature = 0,
                max_tokens = 200,
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

        //public async Task<BiasEnum> AnalyzeTextSentiment(string text)
        //{
        //    var biasListTasks = new List<Task<BiasEnum>>();
        //    var biasList = new List<BiasEnum>();
        //    var hundredWordSlicees = text.Length / 100;
        //    if (hundredWordSlicees == 0)
        //    {
        //        var slice = text;
        //        return await ClassifyText(slice);
        //    }
        //    for (int i = 0; i < hundredWordSlicees; i++)
        //    {
        //        var slice = text.Substring(i * 100, 100);
        //        biasListTasks.Add(ClassifyText(slice));
        //    }
        //    biasList.AddRange(await Task.WhenAll(biasListTasks));
        //    return (BiasEnum)(biasList.Select(x => (int)x).Average() / 1);
        //}
        
        //private async Task<BiasEnum> ClassifyText(string text)
        //{
        //    var classification = await Task.Run(() => _classifier.Classify(text, taxonomy: Taxonomy.Sentiment3));
        //    switch (classification.BestClassName)
        //    {
        //        case "Negative":
        //            return BiasEnum.Against;
        //        case "Neutral":
        //            return BiasEnum.Neutral;
        //        case "Positive":
        //            return BiasEnum.For;
        //        default:
        //            return BiasEnum.Neutral;
        //    }
        //}
    }
}
