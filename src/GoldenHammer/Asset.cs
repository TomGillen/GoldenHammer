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

    public class ValueAsset<T> : IAsset<T>
    {
        private readonly string _identifier;
        private readonly dynamic _config;
        private readonly Task<T> _value;

        internal ValueAsset(string identifier, object config, T value)
        {
            _identifier = identifier;
            _config = config;
            _value = Task.FromResult(value);
        }

        public string Identifier => _identifier;
        public dynamic Configuration => _config;
        public Type AssetType => typeof(T);
        public Task<T> Load() => _value;
    }
}
