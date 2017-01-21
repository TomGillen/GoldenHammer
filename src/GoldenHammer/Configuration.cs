using System.Collections.Generic;

namespace GoldenHammer
{
    public class AssetConfiguration
    {
        public T Get<T>()
        {
            return default(T);
        }
    }

    public struct AssetSource
    {
        public string Path { get; set; }
        public AssetConfiguration Configuration { get; set; }
    }

    public class BundleConfiguration
    {
        public string Name { get; set; }
        public List<AssetSource> Assets { get; set; }

        public BundleConfiguration()
        {
            Assets = new List<AssetSource>();
        }
    }

    public class PackageConfiguration
    {
        public string Name { get; set; }
        public List<BundleConfiguration> Bundles { get; set; }

        public PackageConfiguration()
        {
            Bundles = new List<BundleConfiguration>();
        }
    }

    public class BuildConfiguration
    {
        public List<PackageConfiguration> Packages { get; set; }

        public BuildConfiguration()
        {
            Packages = new List<PackageConfiguration>();
        }
    }
}
