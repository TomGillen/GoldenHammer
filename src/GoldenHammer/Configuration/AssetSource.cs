using System.Dynamic;

namespace GoldenHammer.Configuration
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
}
