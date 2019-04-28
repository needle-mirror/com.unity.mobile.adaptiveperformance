using System;
using System.Collections;
using System.Collections.Generic;

#if UNITY_2019_2_OR_NEWER
using UnityEngine;
#else
using UnityEngine.Experimental;
#endif

namespace UnityEngine.Mobile.AdaptivePerformance
{
    public static class APLog
    {
        public static bool enabled = false;

        public static void Debug(string str)
        {
            if (enabled)
                UnityEngine.Debug.Log($"Adaptive Performance: {str}");
        }
    }

    internal class RunningAverage
    {
        public RunningAverage(int sampleWindowSize = 100)
        {
            m_Values = new float[sampleWindowSize];
        }

        public int GetNumValues()
        {
            return m_NumValues;
        }

        public int GetSampleWindowSize()
        {
            return m_Values.Length;
        }

        public float GetAverage()
        {
            return m_AverageValue;
        }

        public float GetMostRecentValue()
        {
            return (m_NumValues > 0) ? m_Values[m_LastIndex] : 0.0f;
        }

        public void AddValue(float NewValue)
        {
            // Temporarily remember the oldest value, which will overwritten by the new value
            int oldestIndex = (m_LastIndex + 1) % m_Values.Length;
            float oldestValue = m_Values[oldestIndex];

            // Store the new value in the array, overwriting the 100th oldest value
            m_LastIndex = oldestIndex;
            m_Values[m_LastIndex] = NewValue;

            // Update average value over the past numValues (removing oldest and adding newest value)
            float totalValue = m_AverageValue * m_NumValues + NewValue - oldestValue;
            m_NumValues = Mathf.Min(m_NumValues + 1, m_Values.Length);
            m_AverageValue = totalValue / m_NumValues;
        }

        public void Reset()
        {
            m_NumValues = 0;
            m_LastIndex = -1;
            m_AverageValue = 0.0f;
            Array.Clear(m_Values, 0, m_Values.Length);
        }

        private float[] m_Values = null;
        private int m_NumValues = 0;
        private int m_LastIndex = -1;
        private float m_AverageValue = 0.0f;
    }

    public class AdaptivePerformanceManager : MonoBehaviour, IAdaptivePerformance
    {
        public event EventHandler<ThermalEventArgs> ThermalEvent;
        public event EventHandler<PerformanceBottleneckChangeEventArgs> PerformanceBottleneckChangeEvent;

        private AdaptivePerformanceSubsystem m_Subsystem = null;

        private bool m_JustResumed = false;

        private PerformanceWarningLevel m_ActiveWarningLevel;
        private int m_ActiveCpuLevel = Constants.unknownPerformceLevel;
        private int m_ActiveGpuLevel = Constants.unknownPerformceLevel;
        private object m_ActiveSustainedPerformanceLock = new object();

        private PerformanceWarningLevel m_UserWarningLevel;
        private int m_RequestedCpuLevel = Constants.unknownPerformceLevel;
        private int m_RequestedGpuLevel = Constants.unknownPerformceLevel;

        private float m_TemperatureLevel = -1.0f;
        private float m_ThermalTrend = 0.0f;
        private bool m_TemperatureChanged = false;

        private PerformanceBottleneck? m_Bottleneck = null;

        public PerformanceBottleneck performanceBottleneck => m_Bottleneck ?? PerformanceBottleneck.Unknown;

        public bool logging
        {
            get { return APLog.enabled; }
            set { APLog.enabled = value; }
        }

        public bool active => m_Subsystem != null;

        public int maxCpuPerformanceLevel => m_Subsystem != null ? m_Subsystem.maxCpuPerformanceLevel : Constants.unknownPerformceLevel;

        public int maxGpuPerformanceLevel => m_Subsystem != null ? m_Subsystem.maxGpuPerformanceLevel : Constants.unknownPerformceLevel;

        public int currentCpuLevel { get; private set; } = Constants.unknownPerformceLevel;

        public int currentGpuLevel { get; private set; } = Constants.unknownPerformceLevel;

        public int cpuLevel
        {
            get => m_RequestedCpuLevel;
            set => m_RequestedCpuLevel = value;
        }

        public int gpuLevel
        {
            get => m_RequestedGpuLevel;
            set => m_RequestedGpuLevel = value;
        }

        public PerformanceWarningLevel warningLevel => m_UserWarningLevel;

        public float temperatureLevel => m_TemperatureLevel;

        public float temperatureTrend => m_ThermalTrend;

        private bool InitializeSubsystem(AdaptivePerformanceSubsystem subsystem)
        {
            if (subsystem == null)
                return false;

            subsystem.PerformanceWarning += OnThermalWarning;
            subsystem.TemperatureChange += OnTemperatureChange;

            subsystem.Start();

            if (subsystem.initialized)
            {
                m_Subsystem = subsystem;

                subsystem.PerformanceLevelDisabled += OnPerformanceLevelDisabled;
                APLog.Debug($"version={m_Subsystem.GetVersion()}");

                return true;
            }
            else
            {
                subsystem.PerformanceWarning -= OnThermalWarning;
                subsystem.TemperatureChange -= OnTemperatureChange;
                subsystem.Destroy();

                return false;
            }
        }

        public void Start()
        {
            APLog.enabled = StartupSettings.logging;
            if (!StartupSettings.enable)
                return;

            if (InitializeSubsystem(StartupSettings.preferredSubsystem))
            {
                m_Subsystem = StartupSettings.preferredSubsystem;
            }
            else
            {
                var perfDescriptors = new List<AdaptivePerformanceSubsystemDescriptor>();
                SubsystemManager.GetSubsystemDescriptors<AdaptivePerformanceSubsystemDescriptor>(perfDescriptors);

                foreach (var perfDesc in perfDescriptors)
                {
                    var subsystem = perfDesc.Create();
                    if (InitializeSubsystem(subsystem))
                    {
                        m_Subsystem = subsystem;
                        break;
                    }
                }
            }

            if (m_Subsystem != null)
            {
                APLog.Debug($"temperature level: {m_TemperatureLevel}");
                ThermalEvent += (object obj, ThermalEventArgs ev) => APLog.Debug($"[thermal event] temperature level: {ev.temperatureLevel}, warning level: {ev.warningLevel}, thermal trend: {ev.temperatureTrend}");
                PerformanceBottleneckChangeEvent += (object obj, PerformanceBottleneckChangeEventArgs ev) => APLog.Debug($"[perf event] bottleneck: {ev.bottleneck}");

                if (m_RequestedCpuLevel == Constants.unknownPerformceLevel)
                    m_RequestedCpuLevel = m_Subsystem.maxCpuPerformanceLevel;

                if (m_RequestedGpuLevel == Constants.unknownPerformceLevel)
                    m_RequestedGpuLevel = m_Subsystem.maxGpuPerformanceLevel;
            }
        }

        private void AddNonNegativeValue(RunningAverage runningAverage, float value)
        {
            if (value >= 0.0f && value < 1.0f) // don't add frames that took longer than 1s
                runningAverage.AddValue(value);
        }

        public void Update()
        {
            if (m_Subsystem == null)
                return;

            var timestamp = Time.time;

            m_Subsystem.Update();

            if (!m_JustResumed)
            {
                // Update overall frame time
                m_OverallFrameTime.AddValue(Time.unscaledDeltaTime);
                if (m_Subsystem != null)
                {
                    AddNonNegativeValue(m_GpuFrameTime, m_Subsystem.GetGpuFrameTime());
                }
            }
            else
            {
                m_JustResumed = false;
            }

            var averageOverallFrametime = m_OverallFrameTime.GetAverage();
            var averageGpuFrametime = m_GpuFrameTime.GetAverage();

            if (m_OverallFrameTime.GetNumValues() == m_OverallFrameTime.GetSampleWindowSize() &&
                m_GpuFrameTime.GetNumValues() == m_GpuFrameTime.GetSampleWindowSize())
            {
                PerformanceBottleneck bottleneck = DetermineBottleneck(m_Bottleneck ?? PerformanceBottleneck.Unknown, averageGpuFrametime, averageOverallFrametime);

                if (bottleneck != m_Bottleneck)
                {
                    m_Bottleneck = bottleneck;
                    var args = new PerformanceBottleneckChangeEventArgs();
                    args.bottleneck = bottleneck;

                    PerformanceBottleneckChangeEvent?.Invoke(this, args);
                }
            }

            var activeCpuLevel = Constants.unknownPerformceLevel;
            var activeGpuLevel = Constants.unknownPerformceLevel;
            var currentWarningLevel = PerformanceWarningLevel.NoWarning;
            lock (m_ActiveSustainedPerformanceLock)
            {
                currentWarningLevel = m_ActiveWarningLevel;
                activeCpuLevel = m_ActiveCpuLevel;
                activeGpuLevel = m_ActiveGpuLevel;
            }

            //              TODO: Allow ThrottlingImminent earlier than subsystem tells us
            //                    this should either be removed or moved into the subsystem
            //                 if (warningLevel == PerformanceWarningLevel.NoWarning && m_ThermalTrend > 0.0 && m_TemperatureLevel > 0.5f)
            //                 {
            //                     // we want PerformanceWarningLevel.ThrottlingImminent a bit earlier
            //                     warningLevel = PerformanceWarningLevel.ThrottlingImminent;
            //                 }

            bool warningLevelChanged = (currentWarningLevel != m_UserWarningLevel);

            if (warningLevelChanged || m_TemperatureChanged)
            { 
                m_UserWarningLevel = currentWarningLevel;
                var args = new ThermalEventArgs();
                args.warningLevel = currentWarningLevel;
                args.temperatureLevel = m_TemperatureLevel;
                args.temperatureTrend = m_ThermalTrend;
                ThermalEvent?.Invoke(this, args);
                m_TemperatureChanged = false;
            }

            if (m_UserWarningLevel != PerformanceWarningLevel.Throttling)
            {
                if (m_RequestedCpuLevel != Constants.unknownPerformceLevel && m_RequestedGpuLevel != Constants.unknownPerformceLevel)
                {
                    if (m_RequestedCpuLevel != activeCpuLevel || m_RequestedGpuLevel != activeGpuLevel)
                    {
                        lock (m_ActiveSustainedPerformanceLock)
                        {
                            m_ActiveCpuLevel = m_RequestedCpuLevel;
                            m_ActiveGpuLevel = m_RequestedGpuLevel;
                        }

                        if (m_Subsystem.SetPerformanceLevel(m_RequestedCpuLevel, m_RequestedGpuLevel))
                        {
                            this.currentCpuLevel = m_RequestedCpuLevel;
                            this.currentGpuLevel = m_RequestedGpuLevel;
                        }
                        else
                        {
                            lock (m_ActiveSustainedPerformanceLock)
                            {
                                m_ActiveCpuLevel = Constants.unknownPerformceLevel;
                                m_ActiveGpuLevel = Constants.unknownPerformceLevel;
                            }
                            this.currentCpuLevel = Constants.unknownPerformceLevel;
                            this.currentGpuLevel = Constants.unknownPerformceLevel;
                        }
                    }
                }
            }
            else
            {
                this.currentCpuLevel = Constants.unknownPerformceLevel;
                this.currentGpuLevel = Constants.unknownPerformceLevel;
            }

            if (APLog.enabled)
            {
                m_FrameCount++;
                if (m_FrameCount % 50 == 0)
                {
                    APLog.Debug(m_Subsystem.PrintStats());
                    APLog.Debug($"Performance level CPU={currentCpuLevel}/{m_Subsystem.maxCpuPerformanceLevel} GPU={currentGpuLevel}/{m_Subsystem.maxGpuPerformanceLevel} warn={m_UserWarningLevel}({(int)m_UserWarningLevel})");
                    APLog.Debug($"Average GPU frametime = {averageGpuFrameTime * 1000.0f} ms (Current = {currentGpuFrameTime * 1000.0f} ms)");
                    APLog.Debug($"Average frametime = {averageFrameTime * 1000.0f} ms (Current = {currentFrameTime * 1000.0f} ms)");
                    if (m_Bottleneck != null)
                        APLog.Debug($"Bottleneck {m_Bottleneck}");
                    APLog.Debug($"FPS = {1.0f / averageFrameTime}");
                }
            }
        }

        public void OnDestroy()
        {
            m_Subsystem?.Destroy();
        }

        public void OnApplicationPause(bool pause)
        {
            if (m_Subsystem != null)
            {
                if (pause)
                {
                    m_Subsystem.ApplicationPause();
                    m_OverallFrameTime.Reset();
                }
                else
                {
                    m_ActiveWarningLevel = PerformanceWarningLevel.NoWarning;
                    m_Subsystem.ApplicationResume();
                    m_JustResumed = true;
                }
            }
        }

        private int m_FrameCount = 0;

        private RunningAverage m_OverallFrameTime = new RunningAverage();   // In seconds
        private RunningAverage m_GpuFrameTime = new RunningAverage();   // In seconds

        public float averageFrameTime => m_OverallFrameTime.GetAverage();
        public float currentFrameTime => (m_OverallFrameTime.GetNumValues() > 0) ? m_OverallFrameTime.GetMostRecentValue() : 0.0f;
        public float averageGpuFrameTime => m_GpuFrameTime.GetAverage();
        public float currentGpuFrameTime => (m_GpuFrameTime.GetNumValues() > 0) ? m_GpuFrameTime.GetMostRecentValue() : 0.0f;

        private static PerformanceBottleneck DetermineBottleneck(PerformanceBottleneck prevBottleneck, float averageGpuFrametime, float averageOverallFrametime)
        {
            if (HittingFrameRateLimit(averageOverallFrametime, prevBottleneck == PerformanceBottleneck.TargetFrameRate ? 0.03f : 0.02f))
                return PerformanceBottleneck.TargetFrameRate;

            // very brittle..
            // TODO: consider data such as GPU and CPU utilization, vsync wait time, ...
            if (averageGpuFrametime < averageOverallFrametime)
            {
                // GPU has plenty of idle time => probably limited by CPU
                var delta = averageOverallFrametime - averageGpuFrametime;

                float cpuFactor = prevBottleneck == PerformanceBottleneck.CPU ? 0.18f : 0.2f;
                if (delta > cpuFactor * averageOverallFrametime)
                    return PerformanceBottleneck.CPU;

                // GPU is active almost all the time? It's probably the bottleneck
                float gpuFactor = prevBottleneck == PerformanceBottleneck.GPU ? 0.12f : 0.1f;
                if (delta < gpuFactor * averageOverallFrametime)
                    return PerformanceBottleneck.GPU;
            }
            else
            {
                // GPU is active all the time? It's probably the bottleneck
                return PerformanceBottleneck.GPU;
            }

            return PerformanceBottleneck.Unknown;
        }

        static int EffectiveTargetFrameRate()
        {
            int vsyncCount = QualitySettings.vSyncCount;
            if (vsyncCount == 0)
            {
                var targetFrameRate = Application.targetFrameRate;
                if (targetFrameRate >= 0)
                    return targetFrameRate;

#if UNITY_ANDROID
                // see https://docs.unity3d.com/ScriptReference/Application-targetFrameRate.html 
                return 30;
#else
                return -1;
#endif
            }

            int displayRefreshRate = Screen.currentResolution.refreshRate;
            if (displayRefreshRate <= 0)
                displayRefreshRate = 60;

            return displayRefreshRate / vsyncCount;
        }

        static bool HittingFrameRateLimit(float actualFrameTime, float thresholdFactor)
        {
            var targetFrameRate = EffectiveTargetFrameRate();
            if (targetFrameRate < 0)
                return false;

            float targetFrameTime = 1.0f / targetFrameRate;

            if (actualFrameTime <= targetFrameTime)
            {
                return true;
            }

            if (actualFrameTime - targetFrameTime < thresholdFactor * targetFrameTime)
            {
                return true;
            }

            return false;
        }

        public void SetPerformanceRequirements(int cpuLevel, int gpuLevel)
        {
            m_RequestedCpuLevel = cpuLevel;
            m_RequestedGpuLevel = gpuLevel;
            APLog.Debug($"!!!!!!!!!!!!! m_RequestedCpuLevel={m_RequestedCpuLevel} m_RequestedGpuLevel={m_RequestedGpuLevel}");
        }

        private static float ClampToValidRange(float value)
        {
            if (value > 1.0f)
                return 1.0f;
            else if (value >= 0.0f)
                return value;
            else // < 0.0 or NaN
                return -1.0f;
        }

        private void OnThermalWarning(object sender, PerformanceWarningEventArgs args)
        {
            if (APLog.enabled)
            {
                APLog.Debug($"Thermal warning {args.warningLevel}");
            }

            lock(m_ActiveSustainedPerformanceLock)
            {
                m_ActiveWarningLevel = args.warningLevel;
                if (m_ActiveWarningLevel == PerformanceWarningLevel.Throttling)
                {
                    m_ActiveCpuLevel = Constants.unknownPerformceLevel;
                    m_ActiveGpuLevel = Constants.unknownPerformceLevel;
                }
            }
        }


        private void OnTemperatureChange(object sender, TemperatureChangeEventArgs args)
        {
            if (APLog.enabled)
            {
                APLog.Debug($"Temperature change {args.temperatureLevel} {args.temperatureTrend}");
            }

            m_TemperatureLevel = args.temperatureLevel;
            m_ThermalTrend = args.temperatureTrend;
            m_TemperatureChanged = true;
        }

        private void OnPerformanceLevelDisabled(object sender, EventArgs args)
        {
            if (APLog.enabled)
            {
                APLog.Debug($"Sustained Performance reset");
            }

            lock (m_ActiveSustainedPerformanceLock)
            {
                m_ActiveCpuLevel = Constants.unknownPerformceLevel;
                m_ActiveGpuLevel = Constants.unknownPerformceLevel;
            }
        }
    }

    public class AdaptivePerformanceManagerSpawner : ScriptableObject
    {
        public GameObject m_ManagerGameObject;

        void OnEnable()
        {
            if (m_ManagerGameObject == null)
            {
                m_ManagerGameObject = new GameObject("AdaptivePerformanceManager");
                Holder.instance = m_ManagerGameObject.AddComponent<AdaptivePerformanceManager>();
                DontDestroyOnLoad(m_ManagerGameObject);
            }
        }
    }

    public static class AdaptivePerformanceInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            ScriptableObject.CreateInstance<AdaptivePerformanceManagerSpawner>();
        }
    }
}
