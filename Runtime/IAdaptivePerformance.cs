using System;

namespace UnityEngine.Mobile.AdaptivePerformance
{
    public enum PerformanceWarningLevel
    {
        NoWarning,

        /// <summary>
        /// Adjustments are required to avoid thermal throttling
        /// </summary>
        ThrottlingImminent,

        /// <summary>
        /// Thermal throttling state.
        /// The application should make adjustments to go back to normal temperature levels.
        /// </summary>
        Throttling,
    }

    public static class Constants
    {
        /// <summary>
        /// The minimum temperature level
        /// see IAdaptivePerformance.temperatureLevel
        /// </summary>
        public const float minTemperatureLevel = 0.0f;

        /// <summary>
        /// The maximum temperature level
        /// see IAdaptivePerformance.temperatureLevel
        /// </summary>
        public const float maxTemperatureLevel = 1.0f;

        /// <summary>
        /// The minimum CPU level, see
        /// see IAdaptivePerformance.cpuLevel, IAdaptivePerformance.currentCpuLevel
        /// </summary>
        public const int minCpuPerformanceLevel = 0;

        /// <summary>
        /// The minimum GPU level, see
        /// see IAdaptivePerformance.gpuLevel, IAdaptivePerformance.currentGpuLevel
        /// </summary>
        public const int minGpuPerformanceLevel = 0;

        /// <summary>
        /// The value of IAdaptivePerformance.gpuLevel, IAdaptivePerformance.currentGpuLevel, IAdaptivePerformance.cpuLevel, IAdaptivePerformance.currentCpuLevel
        /// if the current performance level is unknown.
        /// This may happen when AdaptivePerformance is not supported or when int throttling state (PerformanceWarningLevel.Throttling). 
        /// </summary>
        public const int unknownPerformceLevel = -1;
    }

    public class ThermalEventArgs
    {
        public PerformanceWarningLevel warningLevel { get; set; }

        /// <summary>
        /// Temperature trend value in the range of [-1; 1]
        /// A value of 1 describes a rapid increase in temperature
        /// A value of 0 describes a constant temperature
        /// A value of -1 describes a rapid decrease in temperature
        /// Please note that it takes at least 10s until the temperature trend may reflect any changes.
        /// </summary>
        public float temperatureTrend { get; set; }

        /// <summary>
        /// Temperature level in the range of [0-1] or -1.0f if not available
        /// A higher temperature level means higher device temperature
        /// See Constants.minTemperatureLevel, Constants.maxTemperatureLevel
        /// </summary>
        public float temperatureLevel { get; set; }
    }

    public enum PerformanceBottleneck
    {
        /// <summary>
        /// Framerate bottleneck is unknown
        /// </summary>
        Unknown,

        /// <summary>
        /// Framerate is limited by CPU processing
        /// </summary>
        CPU,

        /// <summary>
        /// Framerate is limited by GPU processing
        /// </summary>
        GPU,

        /// <summary>
        /// Framerate is limited by Application.targetFrameRate
        /// In this case the application should consider lowering performance requirements (see IAdaptivePerformance.SetPerformanceRequirements)
        /// </summary>
        TargetFrameRate
    }

    public class PerformanceBottleneckChangeEventArgs
    {
        /// <summary>
        /// Indicates the current performance bottleneck
        /// </summary>
        public PerformanceBottleneck bottleneck { get; set; }
    }

    public interface IAdaptivePerformance
    {
        /// <summary>
        ///  Returns the frame time of the last frame (in seconds)
        ///  Returns -1.0f if this is not available.
        /// </summary>
        float currentFrameTime { get; }

        /// <summary>
        ///  Returns the overall frame time as an average over the past 100 frames (in seconds)
        ///  Returns -1.0f if this is not available.
        ///  </summary>
        float averageFrameTime { get; }

        /// <summary>
        ///  Returns the GPU time of the last completely rendered frame (in seconds).
        ///  Returns -1.0f if this is not available.
        /// </summary>
        float currentGpuFrameTime { get; }

        /// <summary>
        ///  Returns the overall frame time as an average over the past 100 frames (in seconds)
        ///  Returns -1.0f if this is not available.
        ///  </summary>
        float averageGpuFrameTime { get; }

        /// <summary>
        /// The maximum valid CPU performance level to be passed to SetPerformanceRequirements
        /// The minimum is Constant.minCpuPerformanceLevel
        /// This value does not change after startup of the AdaptivePerformance system is complete
        /// </summary>
        int maxCpuPerformanceLevel { get; }

        /// <summary>
        /// The maximum valid GPU performance level to be passed to SetPerformanceRequirements
        /// The minimum is Constant.minGpuPerformanceLevel.
        /// This value does not change after startup of the AdaptivePerformance system is complete.
        /// </summary>
        int maxGpuPerformanceLevel { get; }

        /// <summary>
        /// Current CPU performance level.
        /// This value is updated once per frame when the any changes to IAdaptivePerformance.cpuLevel are applied.
        /// </summary>
        int currentCpuLevel { get; }

        /// <summary>
        /// Current GPU performance level.
        /// This value is updated once per frame when the any changes to IAdaptivePerformance.gpuLevel are applied.
        /// </summary>
        int currentGpuLevel { get;  }

        /// <summary>
        /// The requested CPU performance level.
        /// Higher levels typically allow CPU cores to run at higher clock speeds.
        /// The consequence is that thermal warnings and throttling may happen sooner when the high clock speeds cannot be sustained.
        /// Changes are applied once per frame.
        /// It is recommended to set the cpuLevel as low as possible.
        /// The valid value range is [Constants.minCpuPerformanceLevel; IAdaptivePerformance.maxCpuPerformanceLevel]
        /// </summary>
        int cpuLevel { get; set; }

        /// <summary>
        /// The requested GPU performance level.
        /// Higher levels typically allows the GPU to run at higher clock speeds.
        /// The consequence is that thermal warnings and throttling may happen sooner when the high clock speeds cannot be sustained.
        /// Changes are applied once per frame.
        /// It is recommended to set the gpuLevel as low as possible.
        /// The valid value range is [Constants.minGpuPerformanceLevel; IAdaptivePerformance.maxGpuPerformanceLevel]
        /// </summary>
        int gpuLevel { get; set; }

        /// <summary>
        /// Current performance bottleneck
        /// </summary>
        PerformanceBottleneck performanceBottleneck { get; }

        /// <summary>
        /// Current warning level
        /// </summary>
        PerformanceWarningLevel warningLevel { get; }

        /// <summary>
        /// Current temperature level in the range of [0-1]
        /// </summary>
        float temperatureLevel { get; }

        /// <summary>
        /// Temperature trend value in the range of [-1; 1]
        /// A value of 1 describes a rapid increase in temperature
        /// A value of 0 describes a constant temperature
        /// A value of -1 describes a rapid decrease in temperature
        /// Please note that it takes at least 10s until the temperature trend may reflect any changes.
        /// </summary>
        float temperatureTrend { get; }

        /// <summary>
        /// Subscribe to thermal events
        /// </summary>
        event EventHandler<ThermalEventArgs> ThermalEvent;

        /// <summary>
        /// Subscribe to performance events
        /// </summary>
        event EventHandler<PerformanceBottleneckChangeEventArgs> PerformanceBottleneckChangeEvent;

        /// <summary>
        /// Returns true if logging was enabled in StartupSettings
        /// </summary>
        bool logging { get; set; }

        /// <summary>
        /// Returns true if AdaptivePerformance was initialized successfully.
        /// This means that the system was enabled in StartupSettings and runs on a platform that is supports by an Adaptive Performance subsystem.
        /// </summary>
        bool active { get; }
    }

    /// <summary>
    /// Global access to the default AdaptivePerformance interface
    /// </summary>
    public static class Holder
    {
        static public IAdaptivePerformance instance { get; internal set; }
    }

    /// <summary>
    /// Changes to the startup settings are only respected when they are made before the AdaptivePerformance system starts
    /// (e.g. from a method with the attribute [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]).
    /// </summary>
    public static class StartupSettings
    {
        /// <summary>
        ///  Enable debug logging
        /// </summary>
        static public bool logging { get; set; } = false;

        /// <summary>
        ///  Enable the Adaptive Performance system
        /// </summary>
        static public bool enable { get; set; } = true;

        /// <summary>
        /// Can be used to override the automatic selection of an AdaptivePerformance subsystem
        /// Should primarily be used for testing.
        /// </summary>
        static public AdaptivePerformanceSubsystem preferredSubsystem = null;
    }

}
