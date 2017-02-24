using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GoldenHammer.Configuration;

namespace GoldenHammer.Caching
{
    public static class BuildCacheExtensions
    {
        public static async Task<IEnumerable<IProxyAsset>> FetchOrCreate(
            this IBuildCache cache,
            string pipeline, AssetMemoryManager mem, AssetSource source,
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

            var extraFiles = new string[0]; //TODO: record extra input files
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
