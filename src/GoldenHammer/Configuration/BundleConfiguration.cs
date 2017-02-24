using System.Collections.Generic;

namespace GoldenHammer.Configuration
{
    public class BundleConfiguration
    {
        public string Name { get; set; }
        public List<AssetSource> Assets { get; set; }

        public BundleConfiguration()
        {
            Assets = new List<AssetSource>();
        }
    }
}
