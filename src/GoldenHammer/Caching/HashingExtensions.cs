using System;
using System.Security.Cryptography;
using System.Text;

namespace GoldenHammer.Caching
{
    public static class HashingExtensions
    {
        public static string ToHexString(this byte[] ba)
        {
            var hex = BitConverter.ToString(ba);
            return hex.Replace("-", "");
        }

        public static string ToShaString(this string text)
        {
            var bytes = Encoding.Unicode.GetBytes(text);

            var crypto = new SHA256Managed();
            var hash = crypto.ComputeHash(bytes);

            return hash.ToHexString();
        }
    }
}
