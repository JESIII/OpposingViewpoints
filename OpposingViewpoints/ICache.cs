using OpposingViewpoints.Models;

namespace OpposingViewpoints
{
    public interface ICache
    {
        Task CacheSearchResults(List<Article> articles, string searchTerm, int pageNo = 0);
        Task<List<Article>> GetArticlesFromCache(string searchTerm, int pageNo = 0);
        Task CacheTodaysTopics(List<ControversialTopic> proconResponses);
        Task<List<ControversialTopic>> GetTodaysTopicsFromCache();
    }
}
