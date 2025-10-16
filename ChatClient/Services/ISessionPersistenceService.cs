using System.Threading;
using System.Threading.Tasks;
using ChatClient.Models;

namespace ChatClient.Services;

public interface ISessionPersistenceService
{
    Task<SessionStoreSnapshot> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(SessionStoreSnapshot snapshot, CancellationToken cancellationToken = default);

    Task PurgeAsync(CancellationToken cancellationToken = default);
}
