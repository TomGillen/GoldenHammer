using System.Threading.Tasks;

namespace GoldenHammer
{
    internal interface IAssetProcessor
    {
        Task<IAsset> Process(BuildContext context, IAsset asset);
    }

    public interface IAssetProcessor<TIn, TOut>
    {
        string Identifier { get; }
        Task<IAsset<TOut>> Process(BuildContext context, IAsset<TIn> asset);
    }

    internal class MergedAssetProcessor<TIn, TIntermediate, TOut> : IAssetProcessor<TIn, TOut>
    {
        private readonly IAssetProcessor<TIn, TIntermediate> _first;
        private readonly IAssetProcessor<TIntermediate, TOut> _second;

        public MergedAssetProcessor(IAssetProcessor<TIn, TIntermediate> first,
                                    IAssetProcessor<TIntermediate, TOut> second)
        {
            _first = first;
            _second = second;
        }

        public string Identifier => $"{_first.Identifier}-{_second.Identifier}";

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
