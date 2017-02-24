using System.Threading.Tasks;

namespace GoldenHammer.Caching
{
    public interface IBuildCache
    {
        Task<CacheRecord> Fetch(string key);
        Task Store(string key, CacheRecord record);
    }
}
