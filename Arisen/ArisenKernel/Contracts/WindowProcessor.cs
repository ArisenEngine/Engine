using System;

namespace ArisenKernel.Contracts;

/// <summary>
/// Managed half of a native window procedure. The native HAL treats a result of -1 as "not handled"
/// and forwards the message to the platform default procedure, so an implementation must report every
/// message it does not consume as -1 instead of returning zero. Swallowing messages removes their
/// default behaviour for the whole window.
/// </summary>
public abstract class WindowProcessor
{
    protected WindowProcessor()
    {
    }

    protected IntPtr m_ProcPtr;
    public IntPtr ProcPtr => m_ProcPtr;

    protected IntPtr m_ResizeCallbackPtr;
    public IntPtr ResizeCallbackPtr => m_ResizeCallbackPtr;

    protected IntPtr m_ResizingCallbackPtr;
    public IntPtr ResizingCallbackPtr => m_ResizingCallbackPtr;

    protected abstract void OnResizing();
    protected abstract void OnResized();
    protected abstract void OnCreate();
    protected abstract void OnDestroy();
    protected abstract void OnClose();
}
