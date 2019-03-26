using System;

#if UNITY_2019_2_OR_NEWER
using UnityEngine;
#else
using UnityEngine.Experimental;
#endif

namespace UnityEngine.Mobile.AdaptivePerformance
{
    public class PerformanceWarningEventArgs
    {
        public PerformanceWarningLevel warningLevel { get; set; }
    }

    public class TemperatureChangeEventArgs
    {
        public float temperatureLevel { get; set; }
        public float temperatureTrend { get; set; }
    }

    public abstract class AdaptivePerformanceSubsystem : Subsystem<AdaptivePerformanceSubsystemDescriptor>
    {
        public bool initialized { get; protected set; } 

        public AdaptivePerformanceSubsystem()
        {
            
        }

        public abstract Version GetVersion();
        public abstract float GetGpuFrameTime();

        public int maxCpuPerformanceLevel { get; protected set; } = 0;
        public int maxGpuPerformanceLevel { get; protected set; } = 0;

        public abstract bool SetPerformanceLevel(int cpu, int gpu);

        public abstract event EventHandler<PerformanceWarningEventArgs> PerformanceWarning;
        public abstract event EventHandler PerformanceLevelDisabled;
        public abstract event EventHandler<TemperatureChangeEventArgs> TemperatureChange;

        public virtual void ApplicationPause() { }
        public virtual void ApplicationResume() { }
        public virtual void Update() { }

        public virtual string PrintStats() { return ""; }

#if UNITY_2019_2_OR_NEWER
        override public bool running { get { return initialized; } }
#endif
    }
}
