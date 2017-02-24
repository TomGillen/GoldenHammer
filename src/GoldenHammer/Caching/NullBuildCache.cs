using System.Threading.Tasks;

namespace GoldenHammer.Caching
{
    public class NullBuildCache : IBuildCache
    {
        public Task<CacheRecord> Fetch(string key)
        {
            return Task.FromResult(CacheRecord.NotFound());
        }

        public Task Store(string key, CacheRecord record)
        {
            return Task.CompletedTask;
        }
    }
}
