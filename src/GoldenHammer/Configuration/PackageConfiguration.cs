using System.Collections.Generic;

namespace GoldenHammer.Configuration
{
    public class PackageConfiguration
    {
        public string Name { get; set; }
        public List<BundleConfiguration> Bundles { get; set; }

        public PackageConfiguration()
        {
            Bundles = new List<BundleConfiguration>();
        }
    }
}
