using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GoldenHammer.Caching
{
    public class LocalBuildCache : IBuildCache
    {
        private readonly string _directory;

        public LocalBuildCache(string directory = "build_cache")
        {
            _directory = directory;
        }

        public string BaseDirectory => _directory;

        private string DataFilename(string key)
        {
            return Path.Combine(BaseDirectory, key.Substring(0, 1), key);
        }

        public async Task<CacheRecord> Fetch(string key)
        {
            try {
                var path = DataFilename(key);
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
                using (var reader = new StreamReader(stream)) {
                    var json = await reader.ReadToEndAsync();
                    return JsonConvert.DeserializeObject<CacheRecord>(json);
                }
            }
            catch (Exception e) when (e is FileNotFoundException || e is DirectoryNotFoundException) {
                return CacheRecord.NotFound();
            }
        }

        public async Task Store(string key, CacheRecord record)
        {
            var path = DataFilename(key);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "");

            var env = Environment.CurrentDirectory;

            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            using (var writer = new StreamWriter(stream)) {
                var json = JsonConvert.SerializeObject(record);
                await writer.WriteAsync(json);
            }
        }
    }
}