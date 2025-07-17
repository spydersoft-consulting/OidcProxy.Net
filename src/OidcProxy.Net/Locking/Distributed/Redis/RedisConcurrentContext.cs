using System.Collections.Concurrent;
using System.Threading;
using Microsoft.AspNetCore.Http;
using RedLockNet;

namespace OidcProxy.Net.Locking.Distributed.Redis;

public class RedisConcurrentContext(IDistributedLockFactory redisLockFactory) : IConcurrentContext
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationToken = new();
    
    public async Task ExecuteOncePerSession(ISession session, string identifier, Func<bool> actionRequired, Func<Task> @delegate)
    {
        var expiryTime = TimeSpan.FromSeconds(15);
        var waitTime = TimeSpan.FromSeconds(10);
        var retryTime = TimeSpan.FromSeconds(1);
        
        if (!actionRequired())
        { 
            return;
        }
        
        var cacheKey = $"{typeof(RedisConcurrentContext).FullName}+{session.Id}+{identifier}";

        var cancellationTokenSource = _cancellationToken.GetOrAdd(cacheKey, _ => new CancellationTokenSource());

        IRedLock? resourceLock = null;

        try
        {
            resourceLock = await redisLockFactory.CreateLockAsync(cacheKey, expiryTime, waitTime, retryTime, cancellationTokenSource.Token);

            var isActionRequired = actionRequired();
            if (!resourceLock.IsAcquired && isActionRequired)
            {
                throw new ApplicationException($"Unable to renew the expired access_token. Unable to acquire a lock. " +
                                               $"Try again. Error: {resourceLock.InstanceSummary.ToString()}");
            }

            if (!isActionRequired)
            {
                return;
            }

            await @delegate();
        }
        catch (OperationCanceledException)
        {
            await ExecuteOncePerSession(session, identifier, actionRequired, @delegate);
        }
        finally
        {
            if (!cancellationTokenSource.IsCancellationRequested)
            {
                await cancellationTokenSource.CancelAsync();
            }

            _cancellationToken.TryRemove(cacheKey, out _);
            cancellationTokenSource.Dispose();
            if (resourceLock != null)
                await resourceLock.DisposeAsync();
        }
    }
}