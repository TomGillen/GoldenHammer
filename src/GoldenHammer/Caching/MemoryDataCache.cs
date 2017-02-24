using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace GoldenHammer.Caching
{
    public class MemoryDataCache : IDataCache
    {
        private readonly Dictionary<string, byte[]> _files;

        public MemoryDataCache()
        {
            _files = new Dictionary<string, byte[]>();
        }

        public Task<bool> HasContent(string hash)
        {
            lock (_files) {
                return Task.FromResult(_files.ContainsKey(hash));
            }
        }

        public Task<Stream> Open(string hash)
        {
            byte[] data;
            lock (_files) {
                if (!_files.TryGetValue(hash, out data)) {
                    throw new FileNotFoundException("No content with the expected Sha256 found.", hash);
                }
            }

            var stream = new MemoryStream(data, false);
            return Task.FromResult<Stream>(stream);
        }

        public async Task<string> Store(Func<Stream, Task> writer)
        {
            using (var memory = new MemoryStream())
            using (var hash = new SHA256Managed())
            using (var crypto = new CryptoStream(memory, hash, CryptoStreamMode.Write)) {
                await writer(crypto);

                crypto.FlushFinalBlock();
                var name = hash.Hash.ToHexString();

                lock (_files) {
                    _files[name] = memory.ToArray();
                }

                return name;
            }
        }
    }
}
