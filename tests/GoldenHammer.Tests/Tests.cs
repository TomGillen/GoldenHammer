﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;
using Shouldly;

namespace GoldenHammer.Tests
{
    class FilenameImporter : IAssetImporter
    {
        public Regex Filter => new Regex(".*");
        public Task<IEnumerable<IAsset>> Import(BuildContext context, AssetSource source)
        {
            var asset = context.Asset(source.Path, source.Configuration, source.Path);
            return Task.FromResult(Enumerable.Repeat((IAsset) asset, 1));
        }
    }

    class ExclaimProcessor : IAssetProcessor<string, string>
    {
        public async Task<IAsset<string>> Process(BuildContext context, IAsset<string> asset)
        {
            var content = await asset.Load();
            var processed = $"{Path.GetFileNameWithoutExtension(content)}!!";
            return context.Asset(asset.Identifier, asset.Configuration, processed);
        }
    }

    class Packager : IAssetPackager
    {
        public List<AssetBundle> Bundles { get; } = new List<AssetBundle>();

        public Task Package(string name, IEnumerable<AssetBundle> bundles)
        {
            Bundles.AddRange(bundles);
            return Task.CompletedTask;
        }
    }

    public class ApiExample
    {
        [Fact]
        public void SetupPipeline()
        {
            var pipeline = PipelineBuilder
                .Start("TestPipeline")
                .Use(new FilenameImporter())
                .Use(new ExclaimProcessor())
                .Use(new Packager())
                .Build();
        }

        [Fact]
        public async Task ExecutePipeline()
        {
            var output = new Packager();

            var pipeline = PipelineBuilder
                .Start("TestPipeline")
                .Use(new FilenameImporter())
                .Use(new ExclaimProcessor())
                .Use(output)
                .Build();

            var config = new BuildConfiguration {
                Packages = {
                    new PackageConfiguration {
                        Name = "Test Package",
                        Bundles = {
                            new BundleConfiguration {
                                Name = "Bundle 1",
                                Assets = {
                                    new AssetSource {
                                        Path = "Test/File/1.txt",
                                        Configuration = {
                                            Foo = 5,
                                            Bar = 20
                                        }
                                    },
                                    new AssetSource {
                                        Path = "Test/File/2.txt"
                                    }
                                }
                            },
                            new BundleConfiguration {
                                Name = "Bundle 2",
                                Assets = {
                                    new AssetSource {
                                        Path = "Test/File/3.txt"
                                    },
                                    new AssetSource {
                                        Path = "Test/File/4.txt"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            await pipeline.Build(config);

            output.Bundles.ShouldSatisfyAllConditions(
                () => {
                    output.Bundles[0].Name.ShouldBe("Bundle 1");
                    output.Bundles[0].Assets.ShouldSatisfyAllConditions(
                        () => output.Bundles[0].Assets.ToList().ShouldContain(asset => asset.Identifier == "Test/File/1.txt" && ((Asset<string>)asset).Load().Result == "1!!"),
                        () => output.Bundles[0].Assets.ToList().ShouldContain(asset => asset.Identifier == "Test/File/2.txt" && ((Asset<string>)asset).Load().Result == "2!!")
                    );
                },
                () => {
                    output.Bundles[1].Name.ShouldBe("Bundle 2");
                    output.Bundles[1].Assets.ShouldSatisfyAllConditions(
                        () => output.Bundles[1].Assets.ToList().ShouldContain(asset => asset.Identifier == "Test/File/3.txt" && ((Asset<string>)asset).Load().Result == "3!!"),
                        () => output.Bundles[1].Assets.ToList().ShouldContain(asset => asset.Identifier == "Test/File/4.txt" && ((Asset<string>)asset).Load().Result == "4!!")
                    );
                }
            );
        }
    }
}
