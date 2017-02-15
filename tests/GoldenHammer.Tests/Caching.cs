using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GoldenHammer.Caching;
using Shouldly;
using Xunit;

namespace GoldenHammer.Tests
{
    public class MemoryCasTests
    {
        [Fact]
        public void Construct()
        {
            var storage = new MemoryDataCache();
            storage.ShouldNotBeNull();
        }

        public static IEnumerable<object[]> TestValues => new[] {
            new object[] { "hello world" },
            new object[] { "a" },
            new object[] { "ab" },
            new object[] { "abc" },
            new object[] { "d" },
            new object[] { "e" }
        };

        [Theory]
        [MemberData(nameof(TestValues))]
        public async Task WrittenValuesCanBeRead(string value)
        {
            var storage = new MemoryDataCache();

            var sha = await storage.Store(async stream => {
                var bytes = Encoding.UTF8.GetBytes(value);
                await stream.WriteAsync(bytes, 0, bytes.Length);
            });

            using (var stream = await storage.Open(sha))
            using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                var read = await reader.ReadToEndAsync();
                read.ShouldBe(value);
            }
        }

        [Fact]
        public async Task MultipleWrittenValuesCanBeRead()
        {
            var storage = new MemoryDataCache();

            var shas = new Dictionary<string, string>();

            foreach (var value in TestValues.SelectMany(x => x).Cast<string>()) {
                var sha = await storage.Store(async stream => {
                    var bytes = Encoding.UTF8.GetBytes(value);
                    await stream.WriteAsync(bytes, 0, bytes.Length);
                });

                shas[value] = sha;
            }

            foreach (var value in shas.Keys) {
                using (var stream = await storage.Open(shas[value]))
                using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                    var read = await reader.ReadToEndAsync();
                    read.ShouldBe(value);
                }
            }
        }

        [Fact]
        public async Task UnknownShaThrows()
        {
            var storage = new MemoryDataCache();

            var exception = await Assert.ThrowsAsync<FileNotFoundException>(async () => {
                await storage.Open("unknown");
            });

            exception.FileName.ShouldBe("unknown");
        }
    }

    public class FileCasTests : IDisposable
    {
        private readonly string _temp = Path.Combine(Path.GetTempPath(), "gh_temp");

        [Fact]
        public void Construct()
        {
            var storage = new LocalDataCache(_temp);
            storage.ShouldNotBeNull();
        }

        public static IEnumerable<object[]> TestValues => new[] {
            new object[] { "hello world" },
            new object[] { "a" },
            new object[] { "ab" },
            new object[] { "abc" },
            new object[] { "d" },
            new object[] { "e" }
        };

        [Theory]
        [MemberData(nameof(TestValues))]
        public async Task WrittenValuesCanBeRead(string value)
        {
            var storage = new LocalDataCache(_temp);

            var sha = await storage.Store(async stream => {
                var bytes = Encoding.UTF8.GetBytes(value);
                await stream.WriteAsync(bytes, 0, bytes.Length);
            });

            using (var stream = await storage.Open(sha))
            using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                var read = await reader.ReadToEndAsync();
                read.ShouldBe(value);
            }
        }

        [Fact]
        public async Task MultipleWrittenValuesCanBeRead()
        {
            var storage = new LocalDataCache(_temp);

            var shas = new Dictionary<string, string>();

            foreach (var value in TestValues.SelectMany(x => x).Cast<string>()) {
                var sha = await storage.Store(async stream => {
                    var bytes = Encoding.UTF8.GetBytes(value);
                    await stream.WriteAsync(bytes, 0, bytes.Length);
                });

                shas[value] = sha;
            }

            foreach (var value in shas.Keys) {
                using (var stream = await storage.Open(shas[value]))
                using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                    var read = await reader.ReadToEndAsync();
                    read.ShouldBe(value);
                }
            }
        }

        [Fact]
        public async Task UnknownShaThrows()
        {
            var storage = new LocalDataCache(_temp);

            var exception = await Assert.ThrowsAsync<FileNotFoundException>(async () => {
                await storage.Open("unknown");
            });

            exception.FileName.ShouldBe("unknown");
        }

        public void Dispose()
        {
            if (Directory.Exists(_temp)) {
                Directory.Delete(_temp, true);
            }
        }
    }

//    public class AssetMemoryManagerTests
//    {
//        [Fact]
//        public async Task AssetProxiesLoadOriginalContent()
//        {
//            var mem = new AssetMemoryManager(new MemoryDataCache());
//            var asset = new ProxyAsset<string>("test", null, () => Task.FromResult("foo"));
//
//            var proxy = await mem.CreateProxy(asset);
//            var proxiedValue = await proxy.Load();
//
//            proxiedValue.ShouldBe("foo");
//        }
//    }
}