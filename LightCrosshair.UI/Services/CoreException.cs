namespace LightCrosshair.UI.Services;

/// <summary>
/// Failure from Core or the pipe. <see cref="Code"/> is an <see cref="IpcErrorCodes"/> value
/// or a client-only code (timeout, disconnected, core_missing). The server Message is never stored.
/// </summary>
public sealed class CoreException : Exception
{
    public CoreException(string code) : base(code) => Code = code;

    public string Code { get; }
}
