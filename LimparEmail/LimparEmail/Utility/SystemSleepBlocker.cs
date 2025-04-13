using System.Runtime.InteropServices;

namespace LimparEmail.Utility;

public class SystemSleepBlocker : IDisposable
{
    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint esFlags);

    private const uint ES_CONTINUOUS = 0x80000000;
    private const uint ES_SYSTEM_REQUIRED = 0x00000001;
    private const uint ES_DISPLAY_REQUIRED = 0x00000002;

    private bool _disposed;

    // Ativa a prevenção de suspensão do sistema e desligamento da tela
    public SystemSleepBlocker() =>
        SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);

    public void Dispose()
    {
        if (!_disposed)
        {
            // Restaura o comportamento normal do sistema
            SetThreadExecutionState(ES_CONTINUOUS);
            _disposed = true;
        }
    }
}
