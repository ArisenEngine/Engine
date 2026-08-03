using ArisenKernel.Contracts;

namespace ArisenKernel.Lifecycle;

/// <summary>
/// Default-context owner for render surfaces registered by package-loaded rendering code.
/// </summary>
public sealed class RenderSurfaceRegistry : IDisposable
{
    private readonly object m_Gate = new();
    private readonly Dictionary<IntPtr, SurfaceInfo> m_Surfaces = new();
    private ulong m_NextGeneration;
    private bool m_Disposed;

    public int Count
    {
        get
        {
            lock (m_Gate)
            {
                return m_Surfaces.Count;
            }
        }
    }

    public bool IsDisposed
    {
        get
        {
            lock (m_Gate)
            {
                return m_Disposed;
            }
        }
    }

    public RenderSurfaceRegistration Register(
        IntPtr host,
        string name,
        SurfaceType surfaceType,
        IRenderSurface surface)
    {
        if (host == IntPtr.Zero)
        {
            throw new ArgumentException("A render surface requires a non-zero host identity.", nameof(host));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(surface);

        lock (m_Gate)
        {
            ThrowIfDisposed();
            if (m_Surfaces.ContainsKey(host))
            {
                throw new InvalidOperationException(
                    $"Render surface host 0x{host.ToInt64():X} is already registered.");
            }
            if (m_NextGeneration == ulong.MaxValue)
            {
                throw new InvalidOperationException("Render surface registration generation is exhausted.");
            }

            var registration = new RenderSurfaceRegistration(host, ++m_NextGeneration);
            m_Surfaces.Add(
                host,
                new SurfaceInfo(registration, name, surface, surfaceType));
            return registration;
        }
    }

    public bool TryGet(RenderSurfaceRegistration registration, out SurfaceInfo surfaceInfo)
    {
        if (!registration.IsValid)
        {
            surfaceInfo = default;
            return false;
        }

        lock (m_Gate)
        {
            if (m_Surfaces.TryGetValue(registration.Host, out surfaceInfo) &&
                surfaceInfo.Registration == registration)
            {
                return true;
            }
        }

        surfaceInfo = default;
        return false;
    }

    public SurfaceInfo[] Snapshot()
    {
        lock (m_Gate)
        {
            var snapshot = new SurfaceInfo[m_Surfaces.Count];
            m_Surfaces.Values.CopyTo(snapshot, 0);
            return snapshot;
        }
    }

    public bool Unregister(RenderSurfaceRegistration registration)
    {
        SurfaceInfo removed;
        lock (m_Gate)
        {
            if (!registration.IsValid ||
                !m_Surfaces.TryGetValue(registration.Host, out removed) ||
                removed.Registration != registration)
            {
                return false;
            }

            m_Surfaces.Remove(registration.Host);
        }

        DisposeSurface(removed);
        return true;
    }

    public int Drain()
    {
        SurfaceInfo[] surfaces;
        lock (m_Gate)
        {
            surfaces = new SurfaceInfo[m_Surfaces.Count];
            m_Surfaces.Values.CopyTo(surfaces, 0);
            m_Surfaces.Clear();
        }

        DisposeSurfaces(surfaces);
        return surfaces.Length;
    }

    public void Dispose()
    {
        SurfaceInfo[] surfaces;
        lock (m_Gate)
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;
            surfaces = new SurfaceInfo[m_Surfaces.Count];
            m_Surfaces.Values.CopyTo(surfaces, 0);
            m_Surfaces.Clear();
        }

        DisposeSurfaces(surfaces);
    }

    private static void DisposeSurfaces(IReadOnlyList<SurfaceInfo> surfaces)
    {
        List<Exception>? failures = null;
        for (int index = 0; index < surfaces.Count; index++)
        {
            try
            {
                DisposeSurface(surfaces[index]);
            }
            catch (Exception error)
            {
                failures ??= new List<Exception>();
                failures.Add(error);
            }
        }

        if (failures != null)
        {
            throw new AggregateException(
                "One or more render surfaces failed to dispose.",
                failures);
        }
    }

    private static void DisposeSurface(in SurfaceInfo surfaceInfo)
    {
        try
        {
            surfaceInfo.Surface.DisposeSurface();
        }
        catch (Exception error)
        {
            throw new InvalidOperationException(
                $"Render surface '{surfaceInfo.Name}' at host 0x{surfaceInfo.Parent.ToInt64():X} " +
                $"(registration generation {surfaceInfo.Registration.Generation}) failed to dispose.",
                error);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(m_Disposed, this);
    }
}
