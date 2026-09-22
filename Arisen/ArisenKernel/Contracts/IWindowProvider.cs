using System;

namespace ArisenKernel.Contracts;

public enum WindowSurfaceKind
{
    Unknown,
    Win32,
    Headless,
    EditorHosted,
    Virtual
}

public readonly record struct WindowCreateInfo(
    string Title,
    int Width,
    int Height,
    bool Visible = true);

/// <summary>
/// Native window/surface information used by runtime RHI backends.
/// NativeSurfaceId is an opaque provider-owned key; value 0 can be valid.
/// </summary>
public readonly record struct WindowSurfaceInfo(
    IntPtr NativeHandle,
    uint NativeSurfaceId,
    int Width,
    int Height,
    float DpiScale,
    WindowSurfaceKind SurfaceKind,
    bool CloseRequested);

public readonly record struct WindowResizeInfo(
    int Width,
    int Height,
    float DpiScale);

/// <summary>
/// Contract for providing native window handles to the engine for swapchain creation.
/// </summary>
[ServiceContract("Window Provider", "Provides an OS window handle to the engine for rendering SwapChains.")]
public interface IWindowProvider
{
    bool IsCloseRequested { get; }

    /// <summary>
    /// Shows or hides the provider-owned main window. A standalone runtime creates its main
    /// window hidden so frames that do not yet own world content are never displayed, and
    /// reveals it once such a frame has been presented. Providers that do not own a native
    /// window (editor hosts, headless surfaces) ignore the request.
    /// </summary>
    void SetMainWindowVisible(bool visible);

    /// <summary>
    /// True while the provider-owned main window is minimized. A minimized window reports a
    /// zero-extent native surface, so its swapchain can neither acquire nor present an image:
    /// every frame the runtime would produce in that state is invisible, and the runtime parks
    /// its frame loop instead of producing them. A window that was created hidden is not
    /// minimized; its frames are still rendered and presented, which the startup presentation
    /// gate relies on. Providers that do not own a native window always report false.
    /// </summary>
    bool IsMainWindowMinimized { get; }

    /// <summary>
    /// Blocks the calling thread until this thread's window message queue has a message, which is
    /// the earliest point the window state behind <see cref="IsMainWindowMinimized"/> can change.
    /// This is the synchronization primitive a runtime parks on while its host cannot present
    /// frames: the park ends on the message that changes the state (restore, close) instead of on
    /// a polling interval. Providers without a window return immediately.
    /// </summary>
    void WaitForWindowMessage();

    WindowSurfaceInfo EnsureMainWindow(WindowCreateInfo createInfo);

    WindowSurfaceInfo GetWindowInfo();

    IntPtr GetWindowHandle();

    (int Width, int Height) GetWindowSize();

    bool PumpEvents();

    /// <summary>
    /// Raised when the window size changes. Useful for notifying the SwapChain to resize.
    /// </summary>
    event EventHandler<(int Width, int Height)>? OnWindowResized;

    event Action<WindowResizeInfo>? WindowResized;

    event Action? CloseRequested;

    void Close();

    WindowProcessor CreateWindowProcessor();
}
