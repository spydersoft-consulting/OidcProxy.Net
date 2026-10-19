using FluentAssertions;
using OidcProxy.Net.Locking.InMemory;

namespace OidcProxy.Net.Tests.UnitTests.Locking;

public class InMemoryConcurrentContextTests
{
    [Fact]
    public async Task ItShouldExecuteCriticalSectionOnce()
    {
        var session = new TestSession();
        var isInUse = false;
        var somethingChanged = false;

        var tasks = new List<Func<Task>>();
        for (var i = 0; i < 2500; i++)
        {
            tasks.Add(async () =>
            {
                var concurrentContext = new InMemoryConcurrentContext();
                await concurrentContext.ExecuteOncePerSession(session, "foo",
                    () => !somethingChanged,
                    async () =>
                    {
                        if (isInUse)
                        {
                            Assert.Fail("If locking would have worked properly, this would never happen.");
                        }

                        isInUse = true;
                        await Task.Delay(50);
                        isInUse = false;

                        somethingChanged = true;
                    });
                });
        }

        await Task.WhenAll(tasks.Select(p => p()));
    }

    [Fact]
    public async Task ItShouldNeverRunTheCriticalSectionConcurrentlyForTheSameSession()
    {
        var session = new TestSession();
        var inside = 0;
        var maxInside = 0;
        var executions = 0;

        var tasks = Enumerable.Range(0, 2000).Select(_ => Task.Run(async () =>
        {
            await new InMemoryConcurrentContext().ExecuteOncePerSession(session, "foo",
                () => true,
                async () =>
                {
                    var now = Interlocked.Increment(ref inside);
                    InterlockedMax(ref maxInside, now);
                    await Task.Yield();
                    Interlocked.Decrement(ref inside);
                    Interlocked.Increment(ref executions);
                });
        }));

        await Task.WhenAll(tasks);

        maxInside.Should().Be(1);
        executions.Should().Be(2000);
    }

    [Fact]
    public async Task ItShouldNotThrowWhenManyRequestsCompeteForTheSameLock()
    {
        var session = new TestSession();

        var tasks = Enumerable.Range(0, 5000).Select(_ => Task.Run(() =>
            new InMemoryConcurrentContext().ExecuteOncePerSession(session, "foo", () => true, () => Task.CompletedTask)));

        await Task.WhenAll(tasks);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value &&
               Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }
}
