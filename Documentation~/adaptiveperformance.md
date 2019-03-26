**_Adaptive Performance Guide_**

# About Adaptive Performance

Use the Adaptive Performance package to get feedback about the thermal state of your mobile device and react appropriately. As an example, use the API provided to create applications that react to the thermal trend and events of the device. This ensures constant frame rates over a longer period of time while avoiding thermal throttling, even before throttling happens.

# Installing Adaptive Performance

To install this package, follow the instructions in the [Package Manager documentation](https://docs.unity3d.com/Packages/com.unity.package-manager-ui@latest/index.html). 

# Using Adaptive Performance

Once the Adaptive Performance package is added to your Unity project a GameObject that implements `IAdaptivePerformance` is automatically created during runtime.
You can access the instance via `UnityEngine.Mobile.AdaptivePerformance.Holder.instance`.

Please note that Adaptive Performance is currently only supported on Samsung Galaxy S10 devices.
You can check if your device is supported with the `IAdaptivePerformance.active` call.
To get detailed information during runtime, enable debug logging with the `UnityEngine.Mobile.AdaptivePerformance.StartupSettings.logging` flag:

```
static class AdaptivePerformanceConfig
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Setup()
	{
		UnityEngine.Mobile.AdaptivePerformance.StartupSettings.logging = true;
    }
}
```

## Frame time

The Adaptive Performance API always tracks the average GPU and overall frame times through the properties `IAdaptivePerformance.averageGpuFrameTime` and `IAdaptivePerformance.averageFrameTime`.
Based on this values the API also determines the current performance bottleneck (`IAdaptivePerformance.performanceBottleneck`) so thay you can adjust the the game content at runtime in a more targeted way.
For example in a GPU bound application lowering the rendering resolution often improves the framerate significantly, while the same change may not make a big different in a purely CPU bound application.

## Device thermal state feedback

The Adaptive Performance API gives you access to the current thermal warning level of the device (`IAdaptivePerformance.warningLevel`) and a more detailed temperature level (`IAdaptivePerformance.temperatureLevel`).
The application can use those values as feedback to make modifications and avoid getting throttled by the operating system.

The following example shows the implementation of Unity component that adjusts the global LOD bias based on Adaptive Performance feedback:

```
using UnityEngine;  
using UnityEngine.Mobile.AdaptivePerformance;  
  
public class AdaptiveLOD : MonoBehaviour  
{  
    private IAdaptivePerformance ap = null;  
  
    void Start() {  
        ap = Holder.instance;  
  		if (!ap.active)  
            return;  
  
        QualitySettings.lodBias = 1.0f;  
        ap.ThermalEvent += OnThermalEvent;  
    }  
  
    void OnThermalEvent(object obj, ThermalEventArgs ev) {  
        switch (ev.warningLevel) {  
            case PerformanceWarningLevel.NoWarning:  
                QualitySettings.lodBias = 1;  
                break;  
            case PerformanceWarningLevel.ThrottlingImminent:
				if (ev.temperatureLevel > 0.8f)
					QualitySettings.lodBias = 0.75f;
				else
					QualitySettings.lodBias = 1.0f;
                break;  
            case PerformanceWarningLevel.Throttling:  
                QualitySettings.lodBias = 0.5f;  
                break;  
        }  
    }  
} 
```

## Configuring CPU and GPU performance levels

The CPU and GPU of a mobile device make up for a very large part of the power utilization of a mobile device, especially when running a game.
Typically, the operating system decides which clock speeds are used for the CPU and GPU.

CPU cores and GPUs are less efficient when run at their maximum clock speed. Running at high clock speeds overheats the mobile device easily and the operating system throttles the frequency of the CPU and GPU to cool down the device.
This can be avoided by limiting the maximum allowed clock speeds by setting the properties `IAdaptivePerformance.cpuLevel` and `IAdaptivePerformance.gpuLevel`.

The application can configure those properties based the thermal feedback and frame time data provided by the Adaptive Performance API and the application's special knowledge about the current performance requirements:
- did the application reach the target frame rate in the previous frames?
- is the application in an in-game scene or in a menu?
- are device temperatures rising?
- is the device close to thermal throttling?
- is the device GPU or CPU bound?

Please note that changing GPU and GPU levels only has an effect as long as the device is not in thermal throttling state (`IAdaptivePerformance.warningLevel` equals `PerformanceWarningLevel.Throttling`).

For following example show how to configure performance levels based on the current type of scene:

```
public void EnterMenu()
{   
    if (!ap.active)  
        return;   
  
    // Set low CPU and GPU level in menu  
    ap.cpuLevel = 0;  
    ap.gpuLevel = 0;
    // Set low target FPS  
    Application.targetFrameRate = 15;  
}  
  
public void ExitMenu()
{   
    // Set higher CPU and GPU level when going back into the game  
    ap.cpuLevel = ap.maxCpuPerformanceLevel;  
    ap.gpuLevel = ap.maxGpuPerformanceLevel;  
} 
```

# Technical details
## Requirements

This version of Adaptive Performance is compatible with the following versions of the Unity Editor:

* 2018.3 and later (2019.1 recommended)

Adaptive Performance is supported on Galaxy S10 devices with Samsung GameSDK 1.6.

## Document revision history
This section includes the revision history of the document. The revision history tracks when a document is created, edited, and updated. If you create or update a document, you must add a new row describing the revision.  The Documentation Team also uses this table to track when a document is edited and its editing level. An example is provided:
 
|Date|Reason|
|---|---|
|Mar 14, 2019|Document created. Work in progress for initial release.|
