namespace Vernier.App.Platform;

/// <summary>Runs work that needs an STA thread (WPF imaging, RenderTargetBitmap) without blocking the UI thread.</summary>
public static class StaThread
{
    public static Task<T> RunAsync<T>(Func<T> work)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { done.SetResult(work()); }
            catch (Exception e) { done.SetException(e); } // rethrown to the awaiting caller, which handles specific types
        }) { IsBackground = true, Name = "Vernier capture" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return done.Task;
    }
}
