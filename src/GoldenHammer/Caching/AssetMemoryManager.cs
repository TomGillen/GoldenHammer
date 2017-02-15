using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GoldenHammer.Caching
{
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

        public byte[] SerializeProxy(IProxyAsset asset)
        {
            var json = JsonConvert.SerializeObject(asset.GetMeta(), new JsonSerializerSettings {
                TypeNameHandling = TypeNameHandling.Objects
            });

            return Encoding.Unicode.GetBytes(json);
        }

        public Task<IProxyAsset> DeserializeProxy(byte[] encoded)
        {
            return DeserializeProxy(new ArraySegment<byte>(encoded));
        }

        public async Task<IProxyAsset> DeserializeProxy(ArraySegment<byte> encoded)
        {
            var json = Encoding.Unicode.GetString(encoded.Array, encoded.Offset, encoded.Count);
            var deserialized = JsonConvert.DeserializeObject(json, new JsonSerializerSettings {
                TypeNameHandling = TypeNameHandling.Objects
            });

            var meta = (IProxyMeta) deserialized;

            var contentCached = await _storage.HasContent(meta.ContentSha);
            if (!contentCached) {
                return null;
            }

            return meta.ConstructProxy(this);
        }
    }
}