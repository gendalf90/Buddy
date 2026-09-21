namespace Buddy;

internal static class Provider
{
    private static readonly AsyncLocal<IServiceProvider> CurrentProvider = new();

    public static IServiceProvider Current => CurrentProvider.Value;

    public static IDisposable UseProvider(IServiceProvider provider)
    {
        var current = CurrentProvider.Value;

        CurrentProvider.Value = provider;

        return new Disposing(current);
    }

    private class Disposing(IServiceProvider previous) : IDisposable
    {
        public void Dispose()
        {
            CurrentProvider.Value = previous;
        }
    }
}
