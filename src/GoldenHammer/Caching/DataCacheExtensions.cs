using System;
using System.IO;
using System.Threading.Tasks;

namespace GoldenHammer.Caching
{
    public static class DataCacheExtensions
    {
        public static Task<string> Store(this IDataCache storage, Action<Stream> writer)
        {
            return storage.Store(stream => {
                writer(stream);
                return Task.CompletedTask;
            });
        }
    }
}
