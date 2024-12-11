using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Xtensible.TusDotNet.Azure.Tests
{
    /// <summary>
    /// A FileStream which triggers a <see cref="CancellationTokenSource"/> after an exact specified amount of bytes is read.
    /// </summary>
    internal class CancellationTriggeringReadingFileStream : FileStream
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly long _cancelAfterReadingBytes;

        // We have to track the position ourselves, the Position property throws when CanSeek == false.
        private long _position = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="CancellationTriggeringReadingFileStream"/> class.
        /// </summary>
        /// <param name="path"><inheritdoc cref="FileStream.FileStream(string, FileMode)" path="/param[@name='path']"/></param>
        /// <param name="cancellationTokenSource">The <see cref="CancellationTokenSource"/> which should be triggered.</param>
        /// <param name="cancelAfterReadingBytes">The amount of bytes after which the <paramref name="cancellationTokenSource"/> is triggered.</param>
        public CancellationTriggeringReadingFileStream(string path, CancellationTokenSource cancellationTokenSource, long cancelAfterReadingBytes)
            : base(path, FileMode.Open, FileAccess.Read, FileShare.Read)
        {
            _cancellationTokenSource = cancellationTokenSource;
            _cancelAfterReadingBytes = cancelAfterReadingBytes;
        }

        // CanSeek should return false to make sure this class works correctly.
        public override bool CanSeek => false;

        // This is the only "Stream.Read..." method which is used by the AzureBlobTusStorage.AppendDataAsync(...) method.
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_position >= _cancelAfterReadingBytes)
            {
                await _cancellationTokenSource.CancelAsync();
            }
            else if (_position + count > _cancelAfterReadingBytes)
            {
                count = (int)(_cancelAfterReadingBytes - _position);
            }

            var read = await base.ReadAsync(buffer, offset, count, cancellationToken);

            _position += read;

            return read;
        }
    }
}
