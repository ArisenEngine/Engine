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
