using System.Collections.Generic;
using System.Threading.Tasks;

namespace GoldenHammer
{
    public struct AssetBundle
    {
        public string Name { get; private set; }
        public IEnumerable<IAsset> Assets { get; private set; }

        public AssetBundle(string name, IEnumerable<IAsset> assets)
        {
            Name = name;
            Assets = assets;
        }
    }

    public interface IAssetPackager : IPipelineIdentity
    {
        Task Package(string name, IEnumerable<AssetBundle> bundles);
    }

    public abstract class AssetPackager : IAssetPackager
    {
        public virtual string Identity => GetType().AssemblyQualifiedName;
        public abstract Task Package(string name, IEnumerable<AssetBundle> bundles);
    }
}
