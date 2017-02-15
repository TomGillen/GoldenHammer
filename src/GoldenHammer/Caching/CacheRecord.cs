using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GoldenHammer.Caching
{
    public enum CacheResult
    {
        NotCached,
        Cached,
        IncompleteKey
    }

    public struct CacheRecord
    {
        public CacheResult Type { get; }
        public List<byte[]> Assets { get; }
        public string[] MissingInputs { get; }

        [JsonConstructor]
        public CacheRecord(CacheResult type, List<byte[]> assets, string[] missingInputs)
        {
            Type = type;
            Assets = assets;
            MissingInputs = missingInputs;
        }

        public static CacheRecord Found(List<byte[]> assets)
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
}