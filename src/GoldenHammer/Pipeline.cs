using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using MoreLinq;

namespace GoldenHammer
{
    public class PipelineBuilder
    {
        private class Processor<TIn, TOut> : IAssetProcessor
        {
            private readonly IAssetProcessor<TIn, TOut> _processor;

            public Processor(IAssetProcessor<TIn, TOut> processor)
            {
                _processor = processor;
            }

            public async Task<IAsset> Process(BuildContext context, IAsset asset)
            {
                return await _processor.Process(context, asset as IAsset<TIn>);
            }
        }

        private IAssetPackager _packager;
        private List<IAssetImporter> _importers;
        private Dictionary<Type, IAssetProcessor> _processors;

        public static PipelineBuilder Start(string name)
        {
            return new PipelineBuilder {
                _packager = null,
                _importers = new List<IAssetImporter>(),
                _processors = new Dictionary<Type, IAssetProcessor>()
            };
        }

        public PipelineBuilder Use(IAssetImporter importer)
        {
            _importers.Add(importer);
            return this;
        }

        public PipelineBuilder Use(IAssetPackager packager)
        {
            _packager = packager;
            return this;
        }

        public PipelineBuilder Use<TIn, TOut>(IAssetProcessor<TIn, TOut> processor)
        {
            var input = typeof(TIn);
            _processors[input] = new Processor<TIn, TOut>(processor);
            return this;
        }

        public BuildPipeline Build()
        {
            return new BuildPipeline(_packager, _importers, _processors);
        }
    }

    public class BuildPipeline
    {
        private readonly IAssetPackager _packager;
        private readonly List<IAssetImporter> _importers;
        private readonly Dictionary<Type, IAssetProcessor> _processors;

        internal BuildPipeline(IAssetPackager packager, List<IAssetImporter> importers,
                               Dictionary<Type, IAssetProcessor> processors)
        {
            _packager = packager;
            _importers = importers;
            _processors = processors;
        }

        public async Task Build(BuildConfiguration config)
        {
            var context = new BuildContext(this);

            foreach (var package in config.Packages) {
                await BuildPackage(context, package);
            }
        }

        private async Task BuildPackage(BuildContext context, PackageConfiguration package)
        {
            var bundles = new List<AssetBundle>();
            foreach (var bundle in package.Bundles) {
                bundles.Add(await BuildBundle(context, bundle));
            }

            await _packager.Package(package.Name, bundles);
        }

        private async Task<AssetBundle> BuildBundle(BuildContext context, BundleConfiguration bundle)
        {
            var bundleAssets = new List<IAsset>();
            foreach (var batch in bundle.Assets.Batch(Environment.ProcessorCount)) {
                var tasks = batch.Select(source => Task.Run(() => BuildAsset(context, source)));
                var assets = await Task.WhenAll(tasks);
                bundleAssets.AddRange(assets.SelectMany(a => a));
            }

            return new AssetBundle(bundle.Name, bundleAssets);
        }

        private async Task<IEnumerable<IAsset>> BuildAsset(BuildContext context, AssetSource source)
        {
            var imported = await ImportAsset(context, source);
            var processed = await Task.WhenAll(imported.Select(a => ProcessAsset(context, a)));

            return processed;
        }

        private Task<IEnumerable<IAsset>> ImportAsset(BuildContext context, AssetSource source)
        {
            var importer = _importers.FirstOrDefault(i => i.Filter.IsMatch(source.Path));
            return importer != null ? importer.Import(context, source) : Task.FromResult(Enumerable.Empty<IAsset>());
        }

        private Task<IAsset> ProcessAsset(BuildContext context, IAsset asset)
        {
            IAssetProcessor processor;
            if (_processors.TryGetValue(asset.AssetType, out processor)) {
                return processor.Process(context, asset);
            }

            return Task.FromResult(asset);
        }
    }

    public class BuildContext
    {
        private readonly BuildPipeline _pipeline;

        public BuildContext(BuildPipeline pipeline)
        {
            _pipeline = pipeline;
        }

        public Asset<T> Asset<T>(string identifier, AssetConfiguration config, T data)
        {
            return new Asset<T>(identifier, config, data);
        }
    }
}
