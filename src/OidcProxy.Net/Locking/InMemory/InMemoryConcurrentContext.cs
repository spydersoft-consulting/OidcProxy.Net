using Microsoft.AspNetCore.Http;

namespace OidcProxy.Net.Locking.InMemory;

internal class InMemoryConcurrentContext : IConcurrentContext
{   
    public async Task ExecuteOncePerSession(ISession session, string identifier, Func<bool> actionRequired, Func<Task> @delegate)
    {
        if (!actionRequired())
        {
            return;
        }

        var cacheKey = $"{typeof(InMemoryConcurrentContext).FullName}+{session.Id}+{identifier}";

        var semaphore = Semaphores.GetInstance(cacheKey);
        var acquired = false;

        try
        {
            acquired = await semaphore.WaitAsync(TimeSpan.FromSeconds(15));
            if (acquired && actionRequired())
            {
                await @delegate();
            }
        }
        finally
        {
            if (acquired)
            {
                semaphore.Release();
            }

            Semaphores.ReleaseInstance(cacheKey);
        }
    }
}
