using System.Runtime.InteropServices;

namespace LightCrosshair.Core;

/// <summary>
/// Windows.ApplicationModel.StartupTask through its WinRT ABI (vtable slots from the SDK's windows.applicationmodel.h).
/// The CsWinRT projection would add a 55 MB Microsoft.Windows.SDK.NET.dll for these three calls.
/// </summary>
internal static class StartupTaskAbi
{
    internal enum Action { Read, Enable, Disable }

    private static readonly Guid StaticsId = new("ee5b60bd-a148-41a7-b26e-e8b88a1e62f8"); // IStartupTaskStatics
    private static readonly Guid AsyncInfoId = new("00000036-0000-0000-c000-000000000046"); // IAsyncInfo
    private delegate int GetAsyncFn(IntPtr self, IntPtr taskId, out IntPtr operation);
    private delegate int OutPtrFn(IntPtr self, out IntPtr value);
    private delegate int OutIntFn(IntPtr self, out int value);
    private delegate int VoidFn(IntPtr self);

    /// <summary>Runs the action on the package's startup task and returns its StartupTaskState.</summary>
    internal static async Task<int> RunAsync(string taskId, Action action)
    {
        _ = RoInitialize(1); // MTA when the thread has no apartment yet; an STA caller keeps its own.
        IntPtr statics = IntPtr.Zero, task = IntPtr.Zero, id = IntPtr.Zero;
        try
        {
            const string ClassName = "Windows.ApplicationModel.StartupTask";
            Check(WindowsCreateString(ClassName, ClassName.Length, out IntPtr className));
            try { Guid iid = StaticsId; Check(RoGetActivationFactory(className, ref iid, out statics)); }
            finally { WindowsDeleteString(className); }
            Check(WindowsCreateString(taskId, taskId.Length, out id));
            Check(Slot<GetAsyncFn>(statics, 7)(statics, id, out IntPtr getOperation)); // GetAsync
            await CompleteAsync(getOperation);
            task = Result<OutPtrFn, IntPtr>(getOperation, (f, o) => { Check(f(o, out IntPtr value)); return value; });
            if (action == Action.Enable)
            {
                Check(Slot<OutPtrFn>(task, 6)(task, out IntPtr enableOperation)); // RequestEnableAsync
                await CompleteAsync(enableOperation);
                Result<OutIntFn, int>(enableOperation, (f, o) => { Check(f(o, out int value)); return value; });
            }
            else if (action == Action.Disable) Check(Slot<VoidFn>(task, 7)(task)); // Disable
            Check(Slot<OutIntFn>(task, 8)(task, out int state)); // get_State
            return state;
        }
        finally
        {
            if (task != IntPtr.Zero) Marshal.Release(task);
            if (statics != IntPtr.Zero) Marshal.Release(statics);
            if (id != IntPtr.Zero) WindowsDeleteString(id);
        }
    }

    /// <summary>Waits for an IAsyncOperation by polling IAsyncInfo.Status; releases it on failure.</summary>
    private static async Task CompleteAsync(IntPtr operation)
    {
        try
        {
            Check(Marshal.QueryInterface(operation, in AsyncInfoId, out IntPtr info));
            try
            {
                for (int waited = 0; ; waited += 10)
                {
                    Check(Slot<OutIntFn>(info, 7)(info, out int status)); // get_Status: 0 Started, 1 Completed, 2 Canceled, 3 Error
                    if (status == 1) return;
                    if (status != 0)
                    {
                        Check(Slot<OutIntFn>(info, 8)(info, out int error)); // get_ErrorCode
                        Check(error < 0 ? error : unchecked((int)0x800704C7)); // ERROR_CANCELLED
                    }
                    if (waited >= 10_000) throw new TimeoutException("StartupTask did not answer.");
                    await Task.Delay(10);
                }
            }
            finally { Marshal.Release(info); }
        }
        catch { Marshal.Release(operation); throw; }
    }

    /// <summary>IAsyncOperation&lt;T&gt;.GetResults (slot 8), then releases the operation.</summary>
    private static TResult Result<TFn, TResult>(IntPtr operation, Func<TFn, IntPtr, TResult> read) where TFn : Delegate
    {
        try { return read(Slot<TFn>(operation, 8), operation); }
        finally { Marshal.Release(operation); }
    }

    private static T Slot<T>(IntPtr instance, int slot) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size));

    private static void Check(int hr) { if (hr < 0) Marshal.ThrowExceptionForHR(hr); }

    [DllImport("combase.dll")] private static extern int RoInitialize(int type);
    [DllImport("combase.dll")] private static extern int RoGetActivationFactory(IntPtr classId, ref Guid iid, out IntPtr factory);
    [DllImport("combase.dll", CharSet = CharSet.Unicode)] private static extern int WindowsCreateString(string source, int length, out IntPtr value);
    [DllImport("combase.dll")] private static extern int WindowsDeleteString(IntPtr value);
}
