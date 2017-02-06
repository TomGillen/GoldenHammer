using System.Collections.Generic;
using System.Dynamic;

namespace GoldenHammer
{
    public class AssetSource
    {
        public string Path { get; set; }
        public dynamic Configuration { get; set; }

        public AssetSource()
        {
            Configuration = new ExpandoObject();
        }
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
