using System.Threading.Tasks;

namespace GoldenHammer
{
    internal interface IAssetProcessor : IPipelineIdentity
    {
        Task<IAsset> Process(BuildContext context, IAsset asset);
    }

    public interface IAssetProcessor<TIn, TOut> : IPipelineIdentity
    {
        Task<IAsset<TOut>> Process(BuildContext context, IAsset<TIn> asset);
    }

    public abstract class AssetProcessor<TIn, TOut> : IAssetProcessor<TIn, TOut>
    {
        public virtual string Identity => GetType().AssemblyQualifiedName + GetHashCode();
        public abstract Task<IAsset<TOut>> Process(BuildContext context, IAsset<TIn> asset);
    }

    internal class MergedAssetProcessor<TIn, TIntermediate, TOut> : IAssetProcessor<TIn, TOut>
    {
        private readonly IAssetProcessor<TIn, TIntermediate> _first;
        private readonly IAssetProcessor<TIntermediate, TOut> _second;

        public string Identity => $"({_first.Identity}+{_second.Identity})";

        public MergedAssetProcessor(IAssetProcessor<TIn, TIntermediate> first, IAssetProcessor<TIntermediate, TOut> second)
        {
            _first = first;
            _second = second;
        }

        public async Task<IAsset<TOut>> Process(BuildContext context, IAsset<TIn> asset)
        {
            var intermediate = await _first.Process(context, asset);
            var result = await _second.Process(context, intermediate);

            return result;
        }
    }

    public static class AssetBuildPipelineExtensions
    {
        public static IAssetProcessor<TIn, TOut> Then<TIn, TIntermediate, TOut>(
            this IAssetProcessor<TIn, TIntermediate> first, IAssetProcessor<TIntermediate, TOut> second)
        {
            return new MergedAssetProcessor<TIn, TIntermediate, TOut>(first, second);
        }
    }
}
