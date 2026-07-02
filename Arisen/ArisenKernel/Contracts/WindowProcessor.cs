using System;

namespace ArisenKernel.Contracts;

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
