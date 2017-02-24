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
        private readonly Task<T> _value;

        public string Identifier { get; }

        public dynamic Configuration { get; }

        public Type AssetType => typeof(T);

        internal ValueAsset(string identifier, object config, T value)
        {
            Identifier = identifier;
            Configuration = config;
            _value = Task.FromResult(value);
        }

        public Task<T> Load() => _value;
    }
}
