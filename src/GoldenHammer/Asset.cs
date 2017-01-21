using System;
using System.Threading.Tasks;

namespace GoldenHammer
{
    public interface IAsset
    {
        string Identifier { get; }
        Type AssetType { get; }
        AssetConfiguration Configuration { get; }
    }

    public interface IAsset<T> : IAsset
    {
        Task<T> Load();
    }

    public class Asset<T> : IAsset<T>
    {
        private readonly string _identifier;
        private readonly AssetConfiguration _config;
        private readonly T _value;

        internal Asset(string identifier, AssetConfiguration config, T value)
        {
            _identifier = identifier;
            _config = config;
            _value = value;
        }

        public string Identifier => _identifier;
        public AssetConfiguration Configuration => _config;
        public Type AssetType => typeof(T);
        public Task<T> Load() => Task.FromResult(_value);
    }
}
