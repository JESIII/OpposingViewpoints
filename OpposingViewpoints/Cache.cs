using Microsoft.Extensions.Caching.Memory;
using OpposingViewpoints.Models;
using System.Text.Json;

namespace OpposingViewpoints
{
    public class Cache : ICache
    {
        private readonly IMemoryCache _memoryCache;
        public Cache(IMemoryCache memoryCache)
        { 
            _memoryCache = memoryCache;
        }
        public async Task CacheSearchResults(List<Article> articles, string searchTerm, int pageNo = 0)
        {
            var cacheKey = $"{searchTerm.ToLower().Trim()}_{pageNo}";
            var cacheValue = JsonSerializer.Serialize(articles);
            _memoryCache.Set(cacheKey, cacheValue, TimeSpan.FromHours(1));
        }

        public async Task<List<Article>> GetArticlesFromCache(string searchTerm, int pageNo = 0)
        {
            var cacheKey = $"{searchTerm.ToLower().Trim()}_{pageNo}";
            if (_memoryCache.TryGetValue(cacheKey, out string cachedSearches))
            {
                return JsonSerializer.Deserialize<List<Article>>(cachedSearches);
            }
            return new List<Article>();
        }

        public async Task CacheTodaysTopics(List<ControversialTopic> proconResponses)
        {
            _memoryCache.Set("TodaysTopics", proconResponses, TimeSpan.FromHours(1));
        }
        public async Task<List<ControversialTopic>> GetTodaysTopicsFromCache()
        {
            if (_memoryCache.TryGetValue("TodaysTopics", out List<ControversialTopic> topics))
            {
                return topics;

            }
            return new List<ControversialTopic>();
        }
    }
}
