namespace OidcProxy.Net.Locking.InMemory;

internal static class Semaphores
{
    private static readonly Dictionary<string, Entry> Collection = new();

    public static SemaphoreSlim GetInstance(string key)
    {
        lock (Collection)
        {
            if (!Collection.TryGetValue(key, out var entry))
            {
                entry = new Entry();
                Collection[key] = entry;
            }

            entry.References++;
            return entry.Semaphore;
        }
    }

    public static void ReleaseInstance(string key)
    {
        lock (Collection)
        {
            if (!Collection.TryGetValue(key, out var entry))
            {
                return;
            }

            if (--entry.References == 0)
            {
                Collection.Remove(key);
            }
        }
    }

    private sealed class Entry
    {
        public SemaphoreSlim Semaphore { get; } = new(1);

        public int References { get; set; }
    }
}
