using System;
using System.IO;
using System.Threading.Tasks;

namespace GoldenHammer.Caching
{
    public interface IDataCache
    {
        Task<bool> HasContent(string hash);
        Task<Stream> Open(string hash);
        Task<string> Store(Func<Stream, Task> writer);
    }
}
