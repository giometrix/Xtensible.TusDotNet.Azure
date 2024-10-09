using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;

namespace Xtensible.TusDotNet.Azure
{
    internal static class ChecksumProvider
    {
        internal static byte[] GetChecksum(string algorithm, MemoryStream contents)
        {
            byte[] checksum;
            switch (algorithm)
            {
                case "md5":
                    using (var md5 = MD5.Create())
                    {
                        checksum = md5.ComputeHash(contents);
                    }
                    break;
                default:
                    throw new NotSupportedException();
            }
            contents.Seek(0, SeekOrigin.Begin);
            return checksum;
        }
    }
}