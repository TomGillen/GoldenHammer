using System;
using System.Threading.Tasks;

namespace GoldenHammer
{
    public interface IAsset
    {
        string Identifier { get; }
        Type AssetType { get; }
        dynamic Configuration { get; }
    }

    public interface IAsset<T> : IAsset
    {
        Task<T> Load();
    }

    public class Asset<T> : IAsset<T>
    {
        private readonly string _identifier;
        private readonly dynamic _config;
        private readonly T _value;

        internal Asset(string identifier, dynamic config, T value)
        {
            _identifier = identifier;
            _config = config;
            _value = value;
        }

        public string Identifier => _identifier;
        public dynamic Configuration => _config;
        public Type AssetType => typeof(T);
        public Task<T> Load() => Task.FromResult(_value);
    }
}
