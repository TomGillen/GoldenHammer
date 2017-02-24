using System.Collections.Generic;

namespace GoldenHammer.Configuration
{
    public class BuildConfiguration
    {
        public List<PackageConfiguration> Packages { get; set; }

        public BuildConfiguration()
        {
            Packages = new List<PackageConfiguration>();
        }
    }
}
