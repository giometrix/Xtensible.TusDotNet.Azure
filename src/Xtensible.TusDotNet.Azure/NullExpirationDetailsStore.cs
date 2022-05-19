using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xtensible.Time;

namespace Xtensible.TusDotNet.Azure
{
    public class NullExpirationDetailsStore : ITusExpirationDetailsStore
    {
        public Task SetExpirationAsync(string fileId, DateTimeOffset expires, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<DateTimeOffset?> GetExpirationAsync(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult((DateTimeOffset?)null);
        }

        public Task<IEnumerable<string>> GetExpiredFilesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Enumerable.Empty<string>());
        }
    }
}