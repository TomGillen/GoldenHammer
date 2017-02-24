using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GoldenHammer.Configuration;

namespace GoldenHammer
{
    public interface IAssetImporter : IPipelineIdentity
    {
        Regex Filter { get; }
        Task<IEnumerable<IAsset>> Import(BuildContext context, AssetSource source);
    }

    public abstract class AssetImporter : IAssetImporter
    {
        public virtual string Identity => GetType().AssemblyQualifiedName + GetHashCode();
        public abstract Regex Filter { get; }
        public abstract Task<IEnumerable<IAsset>> Import(BuildContext context, AssetSource source);
    }
}
