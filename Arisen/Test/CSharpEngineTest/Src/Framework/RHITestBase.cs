using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Arisen.Native.RHI;
using Arisen.Native.HAL;
using ArisenEngine.Core.Diagnostics;
using ArisenEngine.Platform.Desktop;
using ArisenEngine.Core.RHI;

namespace CSharpEngineTest.Framework
{
    public abstract class RHITestBase : ITest
    {
        private Stopwatch _stopwatch = new Stopwatch();
        protected double _frameTime = 0.0;
        protected double _fps = 0.0;
        private float _timeAccumulator = 0.0f;

        protected RHIInstance? _instance;
        protected RHIDevice? _device;
        protected uint _windowId = uint.MaxValue;
        protected uint _maxFramesInFlight = 2;
        protected uint _frameIndex = 0;

        public abstract string GetName();
        public virtual TestCategory GetCategory() => TestCategory.Misc;

        public virtual bool Setup()
        {
            if (!InitializeRHI(GetName()))
            {
                return false;
            }

            if (!IsHeadless())
            {
                if (!CreateAppWindow())
                {
                    Logger.Error("Failed to create window");
                    return false;
                }
            }

            if (!InitializeDevice())
            {
                Logger.Error("Failed to initialize device");
                return false;
            }

            return SetupTest();
        }

        public virtual void Teardown()
        {
            TeardownTest();

            if (_instance != null)
            {
                _instance.Dispose();
                _instance = null;
            }

            if (_windowId != uint.MaxValue)
            {
                RenderWindowAPI.RemoveRenderSurface(_windowId);
                _windowId = uint.MaxValue;
            }
            
            RHILoader.Unload();
        }

        public virtual bool Run()
        {
            bool isRunning = true;
            
            _stopwatch.Start();
            _lastTicks = _stopwatch.ElapsedTicks;

            Win32Native.NativeMessage msg;
            
            while (isRunning)
            {
                while (Win32Native.PeekMessage(out msg, IntPtr.Zero, 0, 0, Win32Native.PM_REMOVE) != 0)
                {
                    if (msg.msg == Win32Native.WM_QUIT)
                    {
                        isRunning = false;
                        break;
                    }
                    Win32Native.TranslateMessage(ref msg);
                    Win32Native.DispatchMessage(ref msg);
                }

                if (!isRunning) break;
                
                RenderFrame();
                NextFrame();
            }

            return true;
        }

        protected virtual bool IsHeadless() => false;
        protected virtual bool SetupTest() => true;
        protected virtual void TeardownTest() { }
        protected virtual void RenderFrame() { }
        protected virtual void OnResize(uint width, uint height) { }

        private bool InitializeRHI(string appName)
        {
            var appInfo = new RHIInstanceInfo
            {
                Name = appName,
                EngineName = "Arisen Engine",
                ValidationLayer = true,
                Major = 1, Minor = 3, Patch = 0, // Vulkan 1.3
                AppMajor = 1, AppMinor = 0, AppPatch = 0,
                EngineMajor = 1, EngineMinor = 0, EnginePatch = 0,
                MaxFramesInFlight = 2
            };

            RHILoader.SetCurrentGraphicsAPI(GraphicsAPI.Vulkan);
            _instance = RHILoader.CreateInstance(appInfo);

            if (_instance == null)
            {
                Logger.Error("Failed to create RHI instance");
                return false;
            }

            // _maxFramesInFlight = _instance.MaxFramesInFlight; // Native has GetMaxFramesInFlight, check binding
            return true;
        }

        private bool CreateAppWindow(uint width = 1200, uint height = 800)
        {
            // HAL expects function pointers for callbacks. C# delegates can be marshaled.
            // But we need to keep them alive.
            _windowId = RenderWindowAPI.CreateRenderWindow(IntPtr.Zero, IntPtr.Zero, (int)width, (int)height);
            return _windowId != uint.MaxValue;
        }

        private bool InitializeDevice()
        {
            if (_instance == null) return false;

            if (!IsHeadless())
            {
                if (_windowId == uint.MaxValue) return false;
                _instance.CreateSurface(_windowId);
            }

            _instance.PickPhysicalDevice(!IsHeadless());
            _instance.InitLogicDevices();
            _instance.CreateLogicDevice(_windowId);

            _device = _instance.GetLogicalDevice(_windowId);
            if (_device != null)
            {
                ArisenEngine.Core.RHI.RHISystem.SetLogicDevice(_device);
                return true;
            }
            return false; 
        }

        protected void NextFrame()
        {
            _frameIndex++;
            
            long currentTicks = _stopwatch.ElapsedTicks;
            double deltaSeconds = (double)(currentTicks - _lastTicks) / Stopwatch.Frequency;
            _lastTicks = currentTicks;

            _frameTime = deltaSeconds;
            _fps = (1.0 / _frameTime) * 0.1 + _fps * 0.9;
            _timeAccumulator += (float)_frameTime;

            if (_timeAccumulator >= 1.0f)
            {
                _timeAccumulator = 0.0f;
                Console.WriteLine($"FPS: {_fps:F2}, Delta Time: {_frameTime:F4}s");
            }
        }

        private long _lastTicks = 0;

        protected uint GetCurrentFrameIndex() => _frameIndex % _maxFramesInFlight;
    }
}
