using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine.Scripting;

namespace UnityEngine.Mobile.AdaptivePerformance
{
    [Preserve]
    public class TestAdaptivePerformanceSubsystem : AdaptivePerformanceSubsystem
    {
        override public event EventHandler<PerformanceWarningEventArgs> PerformanceWarning;
        override public event EventHandler PerformanceLevelDisabled;
        override public event EventHandler<TemperatureChangeEventArgs> TemperatureChange;

        public float gpuFrameTime { get; set; } = -1.0f;
        public bool acceptsPerformanceLevel { get; set; } = true;

        private Version m_Version = new Version(1, 0);

        public static TestAdaptivePerformanceSubsystem Initialize()
        {
            var desc = RegisterDescriptor();
            if (desc == null)
                return null;
            var subsystem = desc.Create();
            StartupSettings.preferredSubsystem = subsystem;
            return subsystem as TestAdaptivePerformanceSubsystem;
        }

        public TestAdaptivePerformanceSubsystem()
        {
            maxCpuPerformanceLevel = 4;
            maxGpuPerformanceLevel = 2;
        }

        public void EmitPerformanceWarning(PerformanceWarningEventArgs args)
        {
            PerformanceWarning?.Invoke(this, args);
        }

        public void EmitPerformanceLevelDisabled(PerformanceWarningEventArgs args)
        {
            PerformanceLevelDisabled?.Invoke(this, null);
        }

        public void EmitTemperatureChange(TemperatureChangeEventArgs args)
        {
            TemperatureChange?.Invoke(this, args);
        }

        override public void Start()
        {
            initialized = true;
        }

        override public void Stop()
        {
        }

        override public void Destroy()
        {
        }

        override public Version GetVersion()
        {
            return m_Version;
        }

        override public bool SetPerformanceLevel(int cpuLevel, int gpuLevel)
        {
            if (!acceptsPerformanceLevel)
                return false;

            return cpuLevel >= 0 && gpuLevel >= 0 && cpuLevel <= maxCpuPerformanceLevel && gpuLevel <= maxGpuPerformanceLevel;
        }

        override public float GetGpuFrameTime()
        {
            return gpuFrameTime;
        }

        static AdaptivePerformanceSubsystemDescriptor RegisterDescriptor()
        {
            return AdaptivePerformanceSubsystemDescriptor.RegisterDescriptor(new AdaptivePerformanceSubsystemDescriptor.Cinfo
            {
                id = "TestAdaptivePerformanceSubsystem",
                subsystemImplementationType = typeof(TestAdaptivePerformanceSubsystem)
            });
        }
    }
}
