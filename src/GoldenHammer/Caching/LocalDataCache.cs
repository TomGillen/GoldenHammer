using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace GoldenHammer.Caching
{
    public class LocalDataCache : IDataCache
    {
        public string BaseDirectory { get; }

        public string DataDirectory => Path.Combine(BaseDirectory, "data");
        public string TemporaryDirectory => Path.Combine(BaseDirectory, "temp");

        public LocalDataCache(string directory)
        {
            BaseDirectory = directory;
        }

        public Task<bool> HasContent(string hash)
        {
            var path = DataFilename(hash);
            return Task.FromResult(File.Exists(path));
        }

        public Task<Stream> Open(string hash)
        {
            try {
                var path = DataFilename(hash);
                var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                return Task.FromResult<Stream>(stream);
            }
            catch (Exception e) when (e is FileNotFoundException || e is DirectoryNotFoundException) {
                throw new FileNotFoundException("No content with the expected Sha256 found.", hash, e);
            }
        }

        public async Task<string> Store(Func<Stream, Task> writer)
        {
            Directory.CreateDirectory(TemporaryDirectory);
            var tempName = Path.Combine(TemporaryDirectory, Guid.NewGuid().ToString());

            string sha;

            using (var stream = new FileStream(tempName, FileMode.CreateNew, FileAccess.Write, FileShare.Write, 4096, true))
            using (var hash = new SHA256Managed())
            using (var crypto = new CryptoStream(stream, hash, CryptoStreamMode.Write)) {
                await writer(crypto);

                crypto.FlushFinalBlock();
                sha = hash.Hash.ToHexString();
            }

            var path = DataFilename(sha);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "");
            File.Move(tempName, path);

            return sha;
        }

        private string DataFilename(string hash)
        {
            return Path.Combine(DataDirectory, hash.Substring(0, 1), hash);
        }
    }
}
