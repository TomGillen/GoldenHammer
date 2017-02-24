using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using GoldenHammer.Configuration;
using Newtonsoft.Json;

namespace GoldenHammer.Caching
{
    internal static class BuildCache
    {
        public static string ComputeCacheKey(string pipeline, AssetSource asset, params string[] inputFiles)
        {
            var key = new StringBuilder();

            key.AppendLine(pipeline);
            key.AppendLine(asset.Path);
            key.AppendLine(HashFile(asset.Path));
            key.AppendLine(JsonConvert.SerializeObject(asset.Configuration));

            foreach (var file in inputFiles) {
                key.AppendLine(HashFile(file));
            }

            return key.ToString().ToShaString();
        }

        private static string HashFile(string file)
        {
            try {
                using (var stream = File.OpenRead(file)) {
                    var sha = new SHA256Managed();
                    return sha.ComputeHash(stream).ToHexString();
                }
            }
            catch (Exception e) when (e is FileNotFoundException || e is DirectoryNotFoundException) {
                return "".ToShaString();
            }
        }
    }
}
