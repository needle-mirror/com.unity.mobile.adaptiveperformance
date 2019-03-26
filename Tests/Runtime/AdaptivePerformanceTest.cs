using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;

using AdaptivePerformance = UnityEngine.Mobile.AdaptivePerformance;

public static class AdaptivePerformanceTestSetup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        AdaptivePerformance.StartupSettings.enable = true;
        AdaptivePerformance.StartupSettings.logging = false;
        AdaptivePerformance.StartupSettings.preferredSubsystem = AdaptivePerformance.TestAdaptivePerformanceSubsystem.Initialize();
    }
}

class AdaptivePerformanceTests
{

    [UnityTest]
    public IEnumerator Applies_Cpu_Level()
    {
        var subsystem = AdaptivePerformance.StartupSettings.preferredSubsystem as AdaptivePerformance.TestAdaptivePerformanceSubsystem;
        var ap = AdaptivePerformance.Holder.instance;

        subsystem.acceptsPerformanceLevel = true;

        var level = ap.maxCpuPerformanceLevel;

        ap.cpuLevel = level;

        yield return null;

        Assert.AreEqual(level, ap.cpuLevel);
        Assert.AreEqual(level, ap.currentCpuLevel);
    }

    [UnityTest]
    public IEnumerator Applies_Gpu_Level()
    {
        var subsystem = AdaptivePerformance.StartupSettings.preferredSubsystem as AdaptivePerformance.TestAdaptivePerformanceSubsystem;
        var ap = AdaptivePerformance.Holder.instance;

        subsystem.acceptsPerformanceLevel = true;

        var level = ap.maxGpuPerformanceLevel;

        ap.gpuLevel = level;

        yield return null;

        Assert.AreEqual(level, ap.gpuLevel);
        Assert.AreEqual(level, ap.currentGpuLevel);
    }

    [UnityTest]
    public IEnumerator Unknown_GpuLevel_In_Throttling_State()
    {
        var subsystem = AdaptivePerformance.StartupSettings.preferredSubsystem as AdaptivePerformance.TestAdaptivePerformanceSubsystem;
        var ap = AdaptivePerformance.Holder.instance;

        subsystem.acceptsPerformanceLevel = false;
        subsystem.EmitPerformanceWarning(new AdaptivePerformance.PerformanceWarningEventArgs { warningLevel = AdaptivePerformance.PerformanceWarningLevel.Throttling });

        var level = ap.maxGpuPerformanceLevel;

        ap.gpuLevel = level;

        yield return null;

        Assert.AreEqual(AdaptivePerformance.Constants.unknownPerformceLevel, ap.currentGpuLevel);
    }

    [UnityTest]
    public IEnumerator Unknown_CpuLevel_In_Throttling_State()
    {
        var subsystem = AdaptivePerformance.StartupSettings.preferredSubsystem as AdaptivePerformance.TestAdaptivePerformanceSubsystem;
        var ap = AdaptivePerformance.Holder.instance;

        subsystem.acceptsPerformanceLevel = false;
        subsystem.EmitPerformanceWarning(new AdaptivePerformance.PerformanceWarningEventArgs { warningLevel = AdaptivePerformance.PerformanceWarningLevel.Throttling });

        var level = ap.maxCpuPerformanceLevel;

        ap.cpuLevel = level;

        yield return null;

        Assert.AreEqual(AdaptivePerformance.Constants.unknownPerformceLevel, ap.currentCpuLevel);
    }

    [UnityTest]
    public IEnumerator Ignores_Invalid_Cpu_Level()
    {
        var subsystem = AdaptivePerformance.StartupSettings.preferredSubsystem as AdaptivePerformance.TestAdaptivePerformanceSubsystem;
        var ap = AdaptivePerformance.Holder.instance;

        subsystem.acceptsPerformanceLevel = true;
        subsystem.EmitPerformanceWarning(new AdaptivePerformance.PerformanceWarningEventArgs { warningLevel = AdaptivePerformance.PerformanceWarningLevel.NoWarning });

        ap.cpuLevel = 100;

        yield return null;

        Assert.AreEqual(AdaptivePerformance.Constants.unknownPerformceLevel, ap.currentCpuLevel);
    }

    [UnityTest]
    public IEnumerator Ignores_Invalid_Gpu_Level()
    {
        var subsystem = AdaptivePerformance.StartupSettings.preferredSubsystem as AdaptivePerformance.TestAdaptivePerformanceSubsystem;
        var ap = AdaptivePerformance.Holder.instance;

        subsystem.acceptsPerformanceLevel = true;
        subsystem.EmitPerformanceWarning(new AdaptivePerformance.PerformanceWarningEventArgs { warningLevel = AdaptivePerformance.PerformanceWarningLevel.NoWarning });

        ap.gpuLevel = -2;

        yield return null;

        Assert.AreEqual(AdaptivePerformance.Constants.unknownPerformceLevel, ap.currentGpuLevel);
    }

    [UnityTest]
    public IEnumerator TemperatureChangeEvent_Values_Are_Applied()
    {
        var subsystem = AdaptivePerformance.StartupSettings.preferredSubsystem as AdaptivePerformance.TestAdaptivePerformanceSubsystem;
        var ap = AdaptivePerformance.Holder.instance;

        subsystem.EmitTemperatureChange(new AdaptivePerformance.TemperatureChangeEventArgs { temperatureLevel = 1.0f, temperatureTrend = 1.0f });

        yield return null;

        Assert.AreEqual(1.0f, ap.temperatureLevel);
        Assert.AreEqual(1.0f, ap.temperatureTrend);
    }

    [UnityTest]
    public IEnumerator WarningLevel_Is_Applied()
    {
        var subsystem = AdaptivePerformance.StartupSettings.preferredSubsystem as AdaptivePerformance.TestAdaptivePerformanceSubsystem;
        var ap = AdaptivePerformance.Holder.instance;

        subsystem.EmitPerformanceWarning(new AdaptivePerformance.PerformanceWarningEventArgs { warningLevel = AdaptivePerformance.PerformanceWarningLevel.ThrottlingImminent });

        yield return null;

        Assert.AreEqual(AdaptivePerformance.PerformanceWarningLevel.ThrottlingImminent, ap.warningLevel);

        subsystem.EmitPerformanceWarning(new AdaptivePerformance.PerformanceWarningEventArgs { warningLevel = AdaptivePerformance.PerformanceWarningLevel.Throttling });

        yield return null;

        Assert.AreEqual(AdaptivePerformance.PerformanceWarningLevel.Throttling, ap.warningLevel);

        subsystem.EmitPerformanceWarning(new AdaptivePerformance.PerformanceWarningEventArgs { warningLevel = AdaptivePerformance.PerformanceWarningLevel.NoWarning });

        yield return null;

        Assert.AreEqual(AdaptivePerformance.PerformanceWarningLevel.NoWarning, ap.warningLevel);
    }
}
