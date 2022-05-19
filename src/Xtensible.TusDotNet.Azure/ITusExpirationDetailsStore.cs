using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Xtensible.TusDotNet.Azure
{
    public interface ITusExpirationDetailsStore
    {
        Task SetExpirationAsync(string fileId, DateTimeOffset expires, CancellationToken cancellationToken);
        Task<DateTimeOffset?> GetExpirationAsync(string fileId, CancellationToken cancellationToken);
        Task<IEnumerable<string>> GetExpiredFilesAsync(CancellationToken cancellationToken);
    }
}