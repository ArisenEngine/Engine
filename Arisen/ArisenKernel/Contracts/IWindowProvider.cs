using System;

namespace ArisenKernel.Contracts;

/// <summary>
/// Contract for providing native window handles to the engine for swapchain creation.
/// </summary>
public interface IWindowProvider
{
    IntPtr GetWindowHandle();
    (int Width, int Height) GetWindowSize();
    
    /// <summary>
    /// Raised when the window size changes. Useful for notifying the SwapChain to resize.
    /// </summary>
    event EventHandler<(int Width, int Height)> OnWindowResized;
    
    void Close();

    WindowProcessor CreateWindowProcessor();
}
