using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;

namespace Xtensible.TusDotNet.Azure
{
    internal static class ChecksumProvider
    {
        internal static readonly IEnumerable<string> SupportedChecksumAlgorithms = new ReadOnlyCollection<string>(new[] { "md5" });
        private static readonly MD5 MD5CryptoServiceProvider = MD5.Create();
        internal static byte[] GetChecksum(string algorithm, MemoryStream contents)
        {
            byte[] checksum;
            switch (algorithm)
            {
                case "md5":
                    checksum = MD5CryptoServiceProvider.ComputeHash(contents);
                    break;
                default:
                    throw new NotSupportedException();
            }
            contents.Seek(0, SeekOrigin.Begin);
            return checksum;
        }
    }
}