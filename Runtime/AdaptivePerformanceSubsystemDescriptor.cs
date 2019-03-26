using System;
using UnityEngine.Scripting;

#if UNITY_2019_2_OR_NEWER
using UnityEngine;
#else
using UnityEngine.Experimental;
#endif

[assembly: AlwaysLinkAssembly]
namespace UnityEngine.Mobile.AdaptivePerformance
{
    [Preserve]
    public sealed class AdaptivePerformanceSubsystemDescriptor : SubsystemDescriptor<AdaptivePerformanceSubsystem>
    {
        public struct Cinfo
        {
            public string id { get; set; }
            public Type subsystemImplementationType { get; set; }
        }

        private AdaptivePerformanceSubsystemDescriptor(Cinfo cinfo)
        {
            id = cinfo.id;
            subsystemImplementationType = cinfo.subsystemImplementationType;
        }

        public static AdaptivePerformanceSubsystemDescriptor RegisterDescriptor(Cinfo cinfo)
        {
            var desc = new AdaptivePerformanceSubsystemDescriptor(cinfo);
            if (SubsystemRegistration.CreateDescriptor(desc))
                return desc;
            else
                return null;
        }
    }
}
