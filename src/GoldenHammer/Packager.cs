using System.Collections.Generic;
using System.Threading.Tasks;

namespace GoldenHammer
{
    public struct AssetBundle
    {
        public AssetBundle(string name, IEnumerable<IAsset> assets)
        {
            Name = name;
            Assets = assets;
        }

        public string Name { get; private set; }
        public IEnumerable<IAsset> Assets { get; private set; }
    }

    public interface IAssetPackager
    {
        Task Package(string name, IEnumerable<AssetBundle> bundles);
    }
}
