using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GoldenHammer
{
    public interface IAssetImporter
    {
        Regex Filter { get; }
        Task<IEnumerable<IAsset>> Import(BuildContext context, AssetSource source);
    }
}
