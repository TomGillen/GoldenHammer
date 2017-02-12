using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GoldenHammer
{
    public static class HashingExtensions
    {
        public static string ToHexString(this byte[] ba)
        {
            var hex = BitConverter.ToString(ba);
            return hex.Replace("-","");
        }

        public static string ToShaString(this string text)
        {
            var bytes = Encoding.Unicode.GetBytes(text);

            var crypto = new SHA256Managed();
            var hash = crypto.ComputeHash(bytes);

            return hash.ToHexString();
        }
    }

    public interface IDataCache
    {
        Task<bool> HasContent(string hash);
        Task<Stream> Open(string hash);
        Task<string> Store(Func<Stream, Task> writer);
    }

    public class MemoryCas : IDataCache
    {
        private readonly Dictionary<string, byte[]> _files;

        public MemoryCas()
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

    public class FileCas : IDataCache
    {
        private readonly string _directory;

        public FileCas(string directory)
        {
            _directory = directory;
        }

        public string BaseDirectory => _directory;
        public string DataDirectory => Path.Combine(_directory, "data");
        public string TemporaryDirectory => Path.Combine(_directory, "temp");

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

    public interface IProxyAsset : IAsset
    {
        IProxyMeta GetMeta();
    }

    public interface IProxyMeta
    {
        string ContentSha { get; }
        IProxyAsset ConstructProxy(AssetMemoryManager mem);
    }

    public class AssetMemoryManager
    {
        private class ProxyAsset<T> : IAsset<T>, IProxyAsset
        {
            private readonly string _identifier;
            private readonly dynamic _config;
            private readonly string _contentSha;
            private readonly AssetMemoryManager _storage;

            public ProxyAsset(ProxyMeta<T> meta, AssetMemoryManager storage)
                : this(meta.Identifier, meta.Configuration, meta.ContentSha, storage)
            {
            }

            public ProxyAsset(string identifier, object config, string contentSha, AssetMemoryManager storage)
            {
                _identifier = identifier;
                _config = config;
                _contentSha = contentSha;
                _storage = storage;
            }

            public string Identifier => _identifier;
            public dynamic Configuration => _config;
            public Type AssetType => typeof(T);
            public Task<T> Load() => _storage.LoadContent<T>(_contentSha);

            public IProxyMeta GetMeta()
            {
                return new ProxyMeta<T> {
                    Identifier = _identifier,
                    ContentSha = _contentSha,
                    Configuration = _config
                };
            }
        }

        private class ProxyMeta<T> : IProxyMeta
        {
            public string Identifier { get; set; }
            public string ContentSha { get; set; }
            public object Configuration { get; set; }

            public IProxyAsset ConstructProxy(AssetMemoryManager mem) => new ProxyAsset<T>(this, mem);
        }

        private readonly IDataCache _storage;
        private readonly Dictionary<string, WeakReference> _cache;
        private readonly Dictionary<Type, Func<IAsset, Task<IProxyAsset>>> _invokerCache;

        public AssetMemoryManager(IDataCache storage)
        {
            _storage = storage;
            _cache = new Dictionary<string, WeakReference>();
            _invokerCache = new Dictionary<Type, Func<IAsset, Task<IProxyAsset>>>();
        }

        public Task<IProxyAsset> CreateProxy(IAsset asset)
        {
            Func<IAsset, Task<IProxyAsset>> invoker;

            lock (_invokerCache) {
                if (!_invokerCache.TryGetValue(asset.AssetType, out invoker)) {
                    invoker = GetStoreMethod(asset.AssetType);
                    _invokerCache[asset.AssetType] = invoker;
                }
            }

            return invoker(asset);
        }

        private Func<IAsset, Task<IProxyAsset>> GetStoreMethod(Type assetType)
        {
            var method = GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(m => m.Name == "CreateProxyInternal")
                .MakeGenericMethod(assetType);

            return asset => (Task<IProxyAsset>) method.Invoke(this, new object[] { asset });
        }

        // called via reflection
        // ReSharper disable once UnusedMember.Local
        private async Task<IProxyAsset> CreateProxyInternal<T>(IAsset<T> asset)
        {
            return await CreateProxy(asset);
        }

        public async Task<IProxyAsset> CreateProxy<T>(IAsset<T> asset)
        {
            var content = await asset.Load();
            var hash = await StoreContent(content);
            return new ProxyAsset<T>(asset.Identifier, (object)asset.Configuration, hash, this);
        }

        private Task<string> StoreContent<T>(T content)
        {
            return _storage.Store(stream => ProtoBuf.Serializer.Serialize(stream, content));
        }

        public async Task<T> LoadContent<T>(string hash)
        {
            lock (_cache) {
                T result;
                if (TryGetCached(hash, out result)) {
                    return result;
                }
            }

            using (var stream = await _storage.Open(hash)) {
                var content = ProtoBuf.Serializer.Deserialize<T>(stream);

                lock (_cache) {
                    T cached;
                    if (TryGetCached(hash, out cached)) {
                        return cached;
                    }

                    _cache[hash] = new WeakReference(content);
                    return content;
                }
            }
        }

        private bool TryGetCached<T>(string hash, out T result)
        {
            WeakReference reference;
            if (_cache.TryGetValue(hash, out reference)) {
                var cached = reference.Target;
                if (cached != null) {
                    result = (T) cached;
                    return true;
                }
            }

            result = default(T);
            return false;
        }

        public ArraySegment<byte> SerializeProxy(IProxyAsset asset)
        {
            var json = JsonConvert.SerializeObject(asset.GetMeta());
            var bytes = Encoding.Unicode.GetBytes(json);
            return new ArraySegment<byte>(bytes);
        }

        public async Task<IProxyAsset> DeserializeProxy(ArraySegment<byte> encoded)
        {
            var json = Encoding.Unicode.GetString(encoded.Array, encoded.Offset, encoded.Count);
            var meta = (IProxyMeta) JsonConvert.DeserializeObject(json);

            var contentCached = await _storage.HasContent(meta.ContentSha);
            if (!contentCached) {
                return null;
            }

            return meta.ConstructProxy(this);
        }
    }

    public enum CacheResult
    {
        NotCached,
        Cached,
        IncompleteKey
    }

    public struct CacheRecord
    {
        public CacheResult Type { get; }
        public List<ArraySegment<byte>> Assets { get; }
        public string[] MissingInputs { get; }

        private CacheRecord(CacheResult type, List<ArraySegment<byte>> assets, string[] missingInputs)
        {
            Type = type;
            Assets = assets;
            MissingInputs = missingInputs;
        }

        public static CacheRecord Found(List<ArraySegment<byte>> assets)
        {
            return new CacheRecord(CacheResult.Cached, assets, null);
        }

        public static CacheRecord NotFound()
        {
            return new CacheRecord(CacheResult.NotCached, null, null);
        }

        public static CacheRecord Incomplete(string[] missingInputs)
        {
            return new CacheRecord(CacheResult.IncompleteKey, null, missingInputs);
        }
    }

    internal static class BuildCache
    {
        public static string ComputeCacheKey(string pipeline, AssetSource asset, params string[] inputFiles)
        {
            var key = new StringBuilder();

            key.AppendLine(pipeline);
            key.AppendLine(asset.Path);
            key.AppendLine(JsonConvert.SerializeObject(asset.Configuration));

            foreach (var file in inputFiles) {
                key.AppendLine(HashFile(file));
            }

            return key.ToString().ToShaString();
        }

        private static string HashFile(string file)
        {
            try {
                using (var stream = File.OpenRead(file)) {
                    var sha = new SHA256Managed();
                    return sha.ComputeHash(stream).ToHexString();
                }
            }
            catch (Exception e) when (e is FileNotFoundException || e is DirectoryNotFoundException) {
                return "".ToShaString();
            }
        }
    }

    public interface IBuildCache
    {
        Task<CacheRecord> Fetch(string key);
        Task Store(string key, CacheRecord record);
    }

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

    public static class BuildCacheExtensions
    {
        public static async Task<IEnumerable<IProxyAsset>> FetchOrBuild(
            this IBuildCache cache, string pipeline, AssetMemoryManager mem, AssetSource source,
            Func<AssetSource, Task<IEnumerable<IProxyAsset>>> build)
        {
            var cacheKey = BuildCache.ComputeCacheKey(pipeline, source);

            while (true) {
                var cached = await cache.Fetch(cacheKey);
                if (cached.Type == CacheResult.Cached) {
                    var cachedAssets = await Task.WhenAll(cached.Assets.Select(mem.DeserializeProxy));
                    if (cachedAssets.All(a => a != null)) {
                        return cachedAssets;
                    }
                }

                if (cached.Type == CacheResult.IncompleteKey) {
                    cacheKey = BuildCache.ComputeCacheKey(pipeline, source, cached.MissingInputs);
                }
                else {
                    break;
                }
            }

            var assets = (await build(source)).ToList();

            string[] extraFiles = new string[0]; //todo record extra input files
            var serializedProxies = assets.Select(mem.SerializeProxy).ToList();

            if (extraFiles.Length == 0) {
                await cache.Store(cacheKey, CacheRecord.Found(serializedProxies));
            }
            else {
                await cache.Store(cacheKey, CacheRecord.Incomplete(extraFiles));
                await cache.Store(BuildCache.ComputeCacheKey(pipeline, source, extraFiles),
                                  CacheRecord.Found(serializedProxies));
            }

            return assets;
        }
    }
}