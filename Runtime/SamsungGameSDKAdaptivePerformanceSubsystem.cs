#if UNITY_ANDROID

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Scripting;

namespace UnityEngine.Mobile.AdaptivePerformance
{
    internal static class GameSDKLog
    {
        public static void Debug(string str)
        {
            APLog.Debug($"[Samsung GameSDK] {str}");
        }
    }

    internal class JNITaskScheduler : TaskScheduler, IDisposable
    {
        private Thread m_Thread;
        private BlockingCollection<Task> m_Queue = new BlockingCollection<Task>();
        private bool m_Disposed = false;

        public JNITaskScheduler()
        {
            m_Thread = new Thread(new ThreadStart(ThreadProc));
            m_Thread.Name = "SamsungGameSDK";
            m_Thread.Start();
        }

        private void ThreadProc()
        {
            AndroidJNI.AttachCurrentThread();

            foreach (var task in m_Queue.GetConsumingEnumerable())
            {
                TryExecuteTask(task);
            }

            AndroidJNI.DetachCurrentThread();
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return m_Queue.ToArray();
        }

        protected override void QueueTask(Task task)
        {
            if (task != null)
                m_Queue.Add(task);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return false;
        }

        private void Dispose(bool disposing)
        {
            if (m_Disposed)
                return;

            if (disposing)
            {
                m_Queue.CompleteAdding();
                m_Queue.Dispose();
            }
            m_Disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }


    internal class AsyncValue<T>
    {
        private TaskScheduler scheduler = null;
        private Task<T> task = null;
        private Func<T> updateFunc = null;
        private float updateTimeDeltaSeconds;
        private float updateTimestamp;

        public AsyncValue(TaskScheduler scheduler, T value, float updateTimeDeltaSeconds, Func<T> updateFunc)
        {
            this.scheduler = scheduler;
            this.updateTimeDeltaSeconds = updateTimeDeltaSeconds;
            this.updateFunc = updateFunc;
            this.value = value;
        }

        public bool Update(float timestamp)
        {
            bool changed = false;
            if (task != null && task.IsCompleted)
            {
                var newValue = task.Result;
                changed = !value.Equals(newValue);
                if (changed)
                    changeTimestamp = timestamp;

                value = newValue;
                task = null;
                updateTimestamp = timestamp;
            }

            if (task == null)
            {
                if (timestamp - updateTimestamp > updateTimeDeltaSeconds)
                {
                    task = Task.Factory.StartNew<T>(updateFunc, CancellationToken.None, TaskCreationOptions.None, scheduler);
                }
            }

            return changed;
        }

        public void SyncUpdate(float timestamp)
        {
            task = null;
            var oldValue = value;
            updateTimestamp = timestamp;
            value = updateFunc();

            if (!value.Equals(oldValue))
                changeTimestamp = timestamp;
        }

        public T value { get; private set; }
        public float changeTimestamp { get; private set; }
    }

    [Preserve]
    public class SamsungGameSDKAdaptivePerformanceSubsystem : AdaptivePerformanceSubsystem
    {
        private const string sceneName = "UnityScene";
        private NativeApi m_Api = null;

        private JNITaskScheduler m_Scheduler;

        override public event EventHandler<PerformanceWarningEventArgs> PerformanceWarning;
        override public event EventHandler<TemperatureChangeEventArgs> TemperatureChange;
        override public event EventHandler PerformanceLevelDisabled;

		private AsyncValue<int> m_MainTemperature = null;
        private float m_ThermalTrend = 0.0f;

        private AsyncValue<int> m_SkinTemp = null;
        private AsyncValue<int> m_PSTLevel = null;

        private AsyncValue<double> m_GPUTime = null;

        private Version m_Version = null;

        private int m_MinTempLevel = 0;
        private int m_MaxTempLevel = 7;

        public SamsungGameSDKAdaptivePerformanceSubsystem()
        {
            m_Api = new NativeApi(OnSustainedPerformanceWarning, OnSustainedPerformanceTimeout);
            m_Scheduler = new JNITaskScheduler();
            m_SkinTemp = new AsyncValue<int>(m_Scheduler, -1, 2.7f, () => m_Api.GetSkinTempLevel());
            m_PSTLevel = new AsyncValue<int>(m_Scheduler, -1, 3.3f, () => m_Api.GetPSTLevel());
            m_GPUTime = new AsyncValue<double>(m_Scheduler, -1.0, 0.0f, () => m_Api.GetGpuFrameTime());

            maxGpuPerformanceLevel = 3;
            maxCpuPerformanceLevel = 3;

            m_MainTemperature = m_SkinTemp;
        }

        private void OnSustainedPerformanceWarning(PerformanceWarningLevel warningLevel)
        {
            var args = new PerformanceWarningEventArgs();
            args.warningLevel = warningLevel;

            PerformanceWarning?.Invoke(this, args);
        }

        private void OnSustainedPerformanceTimeout()
        {
            PerformanceLevelDisabled?.Invoke(this, EventArgs.Empty);
        } 

       private void ImmediateUpdateTemperature()
        {
			var timestamp = Time.time;
         	m_PSTLevel?.SyncUpdate(timestamp);
            m_SkinTemp?.SyncUpdate(timestamp);

        }
        override public void Start()
        {
            if (m_Api.Initialize())
            {
                if (Version.TryParse(m_Api.GetVersion(), out m_Version))
                {
                    if (m_Version >= new Version(1, 6))
                    {
                        m_MaxTempLevel = 7;
                        m_MinTempLevel = 0;
                        initialized = true;
                        m_MainTemperature = m_SkinTemp;
                    }
                    else if (m_Version >= new Version(1, 5))
                    {
                        m_MaxTempLevel = 6;
                        m_MinTempLevel = 0;
                        initialized = true;
                        m_MainTemperature = m_PSTLevel;
                        m_SkinTemp = null;
                    }
                    else
                    {
                        m_Api.Terminate();
                        initialized = false;
                    }
                }
            }

            if (initialized)
            {
                ImmediateUpdateTemperature();

                var args = new TemperatureChangeEventArgs();
                args.temperatureLevel = GetTemperatureLevel();
                args.temperatureTrend = 0.0f;
                TemperatureChange?.Invoke(this, args);
            }
        }

        override public void Stop()
        {
            
        }

        override public void Destroy()
        {
            if (initialized)
            {
                m_Api.Terminate();
                initialized = false;
            }

            m_Scheduler.Dispose();
        }

        override public string PrintStats()
        {
           return $"SkinTemp={m_SkinTemp?.value} PSTLevel={m_PSTLevel?.value}";
        }

        private bool UpdateTemperature(float timestamp)
        {
            float trend = 0.0f;
            int oldTemperatureLevel = m_MainTemperature.value;
            float oldTemperatureTimestamp = m_MainTemperature.changeTimestamp;
            bool change = false;

            if (m_MainTemperature.Update(timestamp))
            {
                int newTemperatureLevel = m_MainTemperature.value;
                float newTemperatureTimestamp = m_MainTemperature.changeTimestamp;

                int temperatureLevelDiff = (newTemperatureLevel - oldTemperatureLevel);

                if (temperatureLevelDiff < 0)
                {
                    if (temperatureLevelDiff < -1)
                    {
                        // temp level decreased by more than 1 level -> rapid decrease
                        trend = -1.0f;
                    }
                    else
                    {
                        trend = -0.5f;
                    }
                }
                else
                {
                    if (temperatureLevelDiff > 1)
                    {
                        // temp level increased by more than 1 level -> rapid increase
                        trend = 1.0f;
                    }
                    else if (trend > 0.0f && newTemperatureTimestamp - oldTemperatureTimestamp < 1.0f * 60.0f)
                    {
                        // multiple increases within 1 minute -> rapid increase
                        trend = 0.8f;
                    }
                    else
                    {
                        trend = 0.5f;
                    }
                }

                change = true;
            }
            else
            {
                if (trend != 0.0f)
                {
                    if (timestamp - oldTemperatureTimestamp > 5.0f * 60.0f)
                    {
                        // no change within 5 minutes -> constant temperature
                        trend = 0.0f;
                        change = true;
                    }
                }
            }

            m_ThermalTrend = trend;

             if (m_MainTemperature == m_SkinTemp)
                 m_PSTLevel?.Update(timestamp);
             else
                 m_SkinTemp?.Update(timestamp);

            return change;
        }

        override public void Update()
        {
            // GameSDK API is very slow (~4ms per call), so update those numbers once per frame from another thread

            float timeSinceStartup = Time.time;

            m_GPUTime.Update(timeSinceStartup);

            if (UpdateTemperature(timeSinceStartup))
                EmitTemperatureChangeEvent();
        }

        private void EmitTemperatureChangeEvent()
        {
            var args = new TemperatureChangeEventArgs();
            args.temperatureLevel = GetTemperatureLevel();
            args.temperatureTrend = m_ThermalTrend;
            TemperatureChange?.Invoke(this, args);
        }

        override public Version GetVersion()
        {
            return m_Version;
        }

        private static float NormalizeTemperatureLevel(int currentTempLevel, int minValue, int maxValue)
        {
            float tempLevel = -1.0f;
            if (currentTempLevel >= minValue && currentTempLevel <= maxValue)
            {
                tempLevel = (float)currentTempLevel / (float)maxValue;
                tempLevel = Math.Min(Math.Max(tempLevel, Constants.minTemperatureLevel), maxValue);
            }
            return tempLevel;
        }

        private float NormalizeTemperatureLevel(int currentTempLevel)
        {
            return NormalizeTemperatureLevel(currentTempLevel, m_MinTempLevel, m_MaxTempLevel);
        }

        private static float NormalizeJTLevel(int currentTempLevel)
        {
            return NormalizeTemperatureLevel(currentTempLevel, NativeApi.minJTLevel, NativeApi.maxJTLevel);
        }

        private float GetTemperatureLevel()
        {
            return NormalizeTemperatureLevel(m_MainTemperature.value);
        }

        override public float GetGpuFrameTime()
        {
            var frameTimeMs = m_GPUTime.value;
            if (frameTimeMs >= 0.0)
            {
                return (float) (frameTimeMs / 1000.0);
            }
            return -1.0f;
        }

        override public bool SetPerformanceLevel(int cpuLevel, int gpuLevel)
        {
            bool success = m_Api.SetLevelWithScene(sceneName, cpuLevel, gpuLevel);
            return success;
        }

        public override void ApplicationPause()
        {
            if (initialized)
                m_Api.UnregisterListener();
        }

        public override void ApplicationResume()
        {
            if (initialized)
                m_Api.RegisterListener();
            PerformanceLevelDisabled?.Invoke(this, EventArgs.Empty);

            m_ThermalTrend = 0.0f;
            ImmediateUpdateTemperature();
            EmitTemperatureChangeEvent();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterDescriptor()
        {
            if (NativeApi.IsAvailable())
            {
                AdaptivePerformanceSubsystemDescriptor.RegisterDescriptor(new AdaptivePerformanceSubsystemDescriptor.Cinfo
                {
                    id = "SamsungGameSDK",
                    subsystemImplementationType = typeof(SamsungGameSDKAdaptivePerformanceSubsystem)
                });
            }
        }

        internal class NativeApi : AndroidJavaProxy
        {
            static private AndroidJavaObject s_GameSDK = null;
            static private IntPtr s_GameSDKRawObjectID;
            static private IntPtr s_GetCpuJTLevelID;
            static private IntPtr s_GetGpuJTLevelID;
            static private IntPtr s_GetGpuFrameTimeID;
            static private IntPtr s_GetPSTLevelID;
            static private IntPtr s_GetSkinTempLevelID;

            static private bool s_isAvailable = false;

            private Action<PerformanceWarningLevel> SustainedPerformanceWarningEvent;
            private Action SustainedPerformanceTimeoutEvent;

            public const int minJTLevel = 0;
            public const int maxJTLevel = 6;

            public NativeApi(Action<PerformanceWarningLevel> sustainedPerformanceWarning, Action sustainedPerformanceTimeout)
                : base("com.samsung.android.gamesdk.GameSDKManager$Listener")
            {
                SustainedPerformanceWarningEvent = sustainedPerformanceWarning;
                SustainedPerformanceTimeoutEvent = sustainedPerformanceTimeout;
                StaticInit();              
            }

            [Preserve]
            void onHighTempWarning(int warningLevel)
            {
                GameSDKLog.Debug($"Listener: onHighTempWarning(warningLevel={warningLevel})");
                if (warningLevel == 0)
                    SustainedPerformanceWarningEvent(PerformanceWarningLevel.NoWarning);
                else if (warningLevel == 1)
                    SustainedPerformanceWarningEvent(PerformanceWarningLevel.ThrottlingImminent);
                else if (warningLevel == 2)
                    SustainedPerformanceWarningEvent(PerformanceWarningLevel.Throttling);
            }

            [Preserve]
            void onReleasedByTimeout()
            {
                GameSDKLog.Debug("Listener: onReleasedByTimeout()");
                SustainedPerformanceTimeoutEvent();
            }


            static IntPtr GetJavaMethodID(IntPtr classId, string name, string sig)
            {
                AndroidJNI.ExceptionClear();
                var mid = AndroidJNI.GetMethodID(classId, name, sig);

                IntPtr ex = AndroidJNI.ExceptionOccurred();
                if (ex != (IntPtr)0)
                {
                    AndroidJNI.ExceptionDescribe();
                    AndroidJNI.ExceptionClear();
                    return (IntPtr)0;
                }
                else
                {
                    return mid;
                }
            }

            static private void StaticInit()
            {
                if (s_GameSDK == null)
                {
                    Int64 startTime = DateTime.Now.Ticks;
                    try
                    {
                        s_GameSDK = new AndroidJavaObject("com.samsung.android.gamesdk.GameSDKManager");
                        if (s_GameSDK != null)
                            s_isAvailable = s_GameSDK.CallStatic<bool>("isAvailable");
                    }
                    catch (Exception)
                    {
                        s_isAvailable = false;
                        s_GameSDK = null;
                    }

                    float duration = (float)(DateTime.Now.Ticks - startTime) / 10000.0f;  // ms

                    // GameSDKLog.Debug($"GameSDK static initialization took {duration}ms. isAvailable={s_isAvailable}");

                    if (s_isAvailable)
                    {
                        s_GameSDKRawObjectID = s_GameSDK.GetRawObject();
                        var classID = s_GameSDK.GetRawClass();

                        s_GetPSTLevelID = GetJavaMethodID(classID, "getTempLevel", "()I");
                        s_GetCpuJTLevelID = GetJavaMethodID(classID, "getCpuJTLevel", "()I");
                        s_GetGpuJTLevelID = GetJavaMethodID(classID, "getGpuJTLevel", "()I");
                        s_GetGpuFrameTimeID = GetJavaMethodID(classID, "getGpuFrameTime", "()D");
                        s_GetSkinTempLevelID = GetJavaMethodID(classID, "getSkinTempLevel", "()I");
                    }
                }
            }

            static public bool IsAvailable()
            {
                StaticInit();
                return s_isAvailable;
            }

            public bool RegisterListener()
            {
                bool success = false;
                try
                {
                    success = s_GameSDK.Call<bool>("setListener", this);
                }
                catch (Exception)
                {
                    success = false;
                   
                }

                if (!success)
                    GameSDKLog.Debug("failed to register listener");

                return success;
            }

            public void UnregisterListener()
            {
                bool success = true;
                try
                {
                    GameSDKLog.Debug("setListener(null)");
                    success = s_GameSDK.Call<bool>("setListener", (Object)null);
                }
                catch (Exception)
                {
                    success = false;
                }

                if (!success)
                    GameSDKLog.Debug("setListener(null) failed!");
            }

            public bool Initialize()
            {
                bool isInitialized = false;
                try
                {
                    isInitialized = s_GameSDK.Call<bool>("initialize");
                    if (isInitialized)
                    {
                        isInitialized = RegisterListener();
                    }
                    else
                    {
                        GameSDKLog.Debug("GameSDK.initialize() failed!");
                    }
                }
                catch (Exception)
                {
                    GameSDKLog.Debug("[Exception] GameSDK.initialize() failed!");
                }

                return isInitialized;
            }

            public void Terminate()
            {
                UnregisterListener();

                bool success = true;

                try
                {
                    var packageName = Application.identifier;
                    GameSDKLog.Debug($"GameSDK.finalize({packageName})");
                    success = s_GameSDK.Call<bool>("finalize", packageName);
                }
                catch (Exception)
                {
                    success = false;
                }

                if (!success)
                    GameSDKLog.Debug("GameSDK.finalize() failed!");
            }

            public string GetVersion()
            {
                string sdkVersion = "";
                try
                {
                    sdkVersion = s_GameSDK.Call<string>("getVersion");
                }
                catch (Exception)
                {
                    GameSDKLog.Debug("[Exception] GameSDK.getVersion() failed!");
                }
                return sdkVersion;
            }

            public int GetPSTLevel()
            {
                int currentTempLevel = -1;
                try
                {
                    currentTempLevel = AndroidJNI.CallIntMethod(s_GameSDKRawObjectID, s_GetPSTLevelID, null);
                    if (AndroidJNI.ExceptionOccurred() != IntPtr.Zero)
                    {
                        AndroidJNI.ExceptionDescribe();
                        AndroidJNI.ExceptionClear();
                    }
                }
                catch (Exception)
                {
                    GameSDKLog.Debug("[Exception] GameSDK.getPSTLevel() failed!");
                }
                return currentTempLevel;
            }

            public int GetSkinTempLevel()
            {
                int currentTempLevel = -1;
                try
                {
                    currentTempLevel = AndroidJNI.CallIntMethod(s_GameSDKRawObjectID, s_GetSkinTempLevelID, null);
                    if (AndroidJNI.ExceptionOccurred() != IntPtr.Zero)
                    {
                        AndroidJNI.ExceptionDescribe();
                        AndroidJNI.ExceptionClear();
                    }
                }
                catch (Exception)
                {
                    GameSDKLog.Debug("[Exception] GameSDK.getSkinTempLevel() failed!");
                }
                return currentTempLevel;
            }

            public int GetCpuJTLevel()
            {
                int currentCpuTempLevel = -1;
                try
                {
                    currentCpuTempLevel = AndroidJNI.CallIntMethod(s_GameSDKRawObjectID, s_GetCpuJTLevelID, null);
                    if (AndroidJNI.ExceptionOccurred() != IntPtr.Zero)
                    {
                        AndroidJNI.ExceptionDescribe();
                        AndroidJNI.ExceptionClear();
                    }
                }
                catch (Exception)
                {
                    GameSDKLog.Debug("[Exception] GameSDK.getCpuJTLevel() failed!");
                }

                return currentCpuTempLevel;
            }
            public int GetGpuJTLevel()
            {
                int currentGpuTempLevel = -1;
                try
                {
                    currentGpuTempLevel = AndroidJNI.CallIntMethod(s_GameSDKRawObjectID, s_GetGpuJTLevelID, null);
                    if (AndroidJNI.ExceptionOccurred() != IntPtr.Zero)
                    {
                        AndroidJNI.ExceptionDescribe();
                        AndroidJNI.ExceptionClear();
                    }
                }
                catch (Exception)
                {
                    GameSDKLog.Debug("[Exception] GameSDK.getGpuJTLevel() failed!");
                }
                return currentGpuTempLevel;
            }
            public double GetGpuFrameTime()
            {
                double gpuFrameTime = -1.0;
                try
                {
                    gpuFrameTime = AndroidJNI.CallDoubleMethod(s_GameSDKRawObjectID, s_GetGpuFrameTimeID, null);
                    if (AndroidJNI.ExceptionOccurred() != IntPtr.Zero)
                    {
                        AndroidJNI.ExceptionDescribe();
                        AndroidJNI.ExceptionClear();
                    }
                }
                catch (Exception)
                {
                    GameSDKLog.Debug("[Exception] GameSDK.getGpuFrameTime() failed!");
                }

                return gpuFrameTime;
            }

            public bool SetLevelWithScene(string scene, int cpu, int gpu)
            {
                bool success = false;
                try
                {
                    success = s_GameSDK.Call<bool>("setLevelWithScene", scene, cpu, gpu);
                    GameSDKLog.Debug($"setLevelWithScene({scene}, {cpu}, {gpu}) -> {success}");
                }
                catch (Exception)
                {
                    GameSDKLog.Debug($"[Exception] GameSDK.setLevelWithScene({scene}, {cpu}, {gpu}) failed!");
                }
                return success;
            }
        }

    }
}

#endif 
