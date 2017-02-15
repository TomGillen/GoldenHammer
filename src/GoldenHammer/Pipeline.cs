using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using GoldenHammer.Caching;
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

            public string Identity => _processor.Identity;

            public async Task<IAsset> Process(BuildContext context, IAsset asset)
            {
                return await _processor.Process(context, asset as IAsset<TIn>);
            }
        }

        private IAssetPackager _packager;
        private IDataCache _storage;
        private IBuildCache _cache;
        private List<IAssetImporter> _importers;
        private Dictionary<Type, IAssetProcessor> _processors;

        public static PipelineBuilder Start(string name, IDataCache storage, IBuildCache cache, IAssetPackager packager)
        {
            return new PipelineBuilder {
                _packager = packager,
                _storage = storage,
                _importers = new List<IAssetImporter>(),
                _processors = new Dictionary<Type, IAssetProcessor>(),
                _cache = cache
            };
        }

        public PipelineBuilder Use(IAssetImporter importer)
        {
            _importers.Add(importer);
            return this;
        }

        public PipelineBuilder Use<TIn, TOut>(IAssetProcessor<TIn, TOut> processor)
        {
            var input = typeof(TIn);
            _processors[input] = new Processor<TIn, TOut>(processor);
            return this;
        }

        public BuildPipeline Create()
        {
            return new BuildPipeline(_storage, _cache, _packager, _importers, _processors);
        }
    }

    public interface IPipelineIdentity
    {
        string Identity { get; }
    }

    public class BuildPipeline : IPipelineIdentity
    {
        private readonly IDataCache _storage;
        private readonly IBuildCache _cache;
        private readonly IAssetPackager _packager;
        private readonly List<IAssetImporter> _importers;
        private readonly Dictionary<Type, IAssetProcessor> _processors;
        private readonly AssetMemoryManager _memoryManager;

        internal BuildPipeline(IDataCache storage, IBuildCache cache,
                               IAssetPackager packager, List<IAssetImporter> importers,
                               Dictionary<Type, IAssetProcessor> processors)
        {
            _storage = storage;
            _packager = packager;
            _importers = importers;
            _processors = processors;
            _cache = cache;
            _memoryManager = new AssetMemoryManager(_storage);
            Identity = CalculateId();
        }

        public string Identity { get; }

        private string CalculateId()
        {
            var types = new StringBuilder();
            types.AppendLine(GetType().AssemblyQualifiedName);
            types.AppendLine(_packager.Identity);

            foreach (var importer in _importers) {
                types.AppendLine(importer.Identity);
            }

            foreach (var processor in _processors.Values) {
                types.AppendLine(processor.Identity);
            }

            return types.ToString().ToShaString();
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

        private Task<IEnumerable<IProxyAsset>> BuildAsset(BuildContext context, AssetSource source)
        {
            return _cache.FetchOrBuild(Identity, _memoryManager, source, async s => {
                var imported = await ImportAsset(context, source);
                return await Task.WhenAll(imported.Select(a => ProcessAsset(context, a)));
            });
        }

        private Task<IEnumerable<IAsset>> ImportAsset(BuildContext context, AssetSource source)
        {
            var importer = _importers.FirstOrDefault(i => i.Filter.IsMatch(source.Path));
            return importer != null ? importer.Import(context, source) : Task.FromResult(Enumerable.Empty<IAsset>());
        }

        private async Task<IProxyAsset> ProcessAsset(BuildContext context, IAsset asset)
        {
            IAssetProcessor processor;
            if (_processors.TryGetValue(asset.AssetType, out processor)) {
                asset = await processor.Process(context, asset);
            }

            return await _memoryManager.CreateProxy(asset);
        }
    }

    public class BuildContext
    {
        private readonly BuildPipeline _pipeline;

        public BuildContext(BuildPipeline pipeline)
        {
            _pipeline = pipeline;
        }

        public IAsset<T> Asset<T>(string identifier, dynamic config, T data)
        {
            return new ValueAsset<T>(identifier, (object)config, data);
        }
    }
}
