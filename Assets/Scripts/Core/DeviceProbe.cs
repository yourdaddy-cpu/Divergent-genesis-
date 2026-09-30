using System;
using UnityEngine;

namespace DivergentGenesis.Core
{
    /// <summary>
    /// Picks a starting graphics tier from what the device actually is, rather
    /// than making the player guess on first launch.
    ///
    /// The floor we design for is 4 GB RAM and a Dimensity 6500-class SoC, so the
    /// conservative answer is "Low" unless there is clear evidence of more. It is
    /// always a starting point - the settings panel overrides it and the choice
    /// is remembered in PlayerPrefs.
    /// </summary>
    public static class DeviceProbe
    {
        private const int Unset = -1;

        private static int _cachedTier = Unset;
        private static int _cachedMemoryGb = Unset;
        private static int _cachedProcessorCores = Unset;
        private static string _cachedGpu;
        private static int _cachedBatteryLevel = Unset;

        /// <summary>Recommended starting tier. Computed once, then cached.</summary>
        public static GraphicsTier DetectTier()
        {
            if (_cachedTier == Unset) _cachedTier = (int)ComputeTier();
            return (GraphicsTier)_cachedTier;
        }

        private static GraphicsTier ComputeTier()
        {
            int memory = MemoryGb;
            int cores = ProcessorCores;
            string gpu = (GpuName ?? string.Empty).ToLowerInvariant();
            bool vulkan = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan;

            int score = 0;
            score += memory >= 12 ? 3 : memory >= 8 ? 2 : memory >= 6 ? 1 : 0;
            score += cores >= 8 ? 3 : cores >= 6 ? 2 : cores >= 4 ? 1 : 0;

            // A flagship GPU string is a much stronger signal than core count on
            // big.LITTLE chips, where the "big" cores are the same either way.
            if (gpu.Contains("adreno 7") || gpu.Contains("adreno 6") ||
                gpu.Contains("mali-g7") || gpu.Contains("mali-g6") ||
                gpu.Contains("xclipse") || gpu.Contains("immortalis") ||
                gpu.Contains("powervr sgx") || gpu.Contains("apple"))
                score += 2;
            else if (gpu.Contains("adreno 5") || gpu.Contains("mali-g5") ||
                     gpu.Contains("adreno 4") || gpu.Contains("mali-g4") ||
                     gpu.Contains("mali-t") || gpu.Contains("powervr"))
                score += 1;
            else if (gpu.Contains("swiftshader") || gpu.Contains("llvmpipe") || gpu.Contains("mesa"))
                return GraphicsTier.Potato;      // running in a software rasteriser

            if (vulkan) score += 1;

            if (score >= 7) return GraphicsTier.Ultra;
            if (score >= 5) return GraphicsTier.High;
            if (score >= 3) return GraphicsTier.Medium;
            if (score >= 1) return GraphicsTier.Low;
            return GraphicsTier.Potato;
        }

        // ------------------------------------------------------------- evidence
        public static int MemoryGb
        {
            get
            {
                if (_cachedMemoryGb != Unset) return _cachedMemoryGb;

                int mb = 0;
                try
                {
                    // Reported in MB on both Android and desktop. Phones lie about
                    // this constantly (a "4 GB" device often reports 3.6 GB, and
                    // some report the total rather than the app-visible amount),
                    // so floor it rather than rounding.
                    mb = SystemInfo.systemMemorySize;
                }
                catch (Exception)
                {
                    mb = 0;
                }

                _cachedMemoryGb = mb <= 0 ? 4 : Math.Max(1, mb / 1024);
                return _cachedMemoryGb;
            }
        }

        public static int ProcessorCores
        {
            get { return _cachedProcessorCores != Unset ? _cachedProcessorCores : (_cachedProcessorCores = Mathf.Clamp(SystemInfo.processorCount, 1, 32)); }
        }

        public static string GpuName
        {
            get { return _cachedGpu ?? (_cachedGpu = SafeGpuName()); }
        }

        private static string SafeGpuName()
        {
            try { return SystemInfo.graphicsDeviceName; }
            catch (Exception) { return string.Empty; }
        }

        /// <summary>0-100, or -1 when the platform cannot report it.</summary>
        public static int BatteryLevel
        {
            get
            {
                if (_cachedBatteryLevel != Unset) return _cachedBatteryLevel;
                float f = SystemInfo.batteryLevel;
                _cachedBatteryLevel = f < 0f ? -1 : Mathf.Clamp(Mathf.RoundToInt(f * 100f), 0, 100);
                return _cachedBatteryLevel;
            }
        }

        public static string Summary
        {
            get
            {
                return MemoryGb + " GB RAM, " + ProcessorCores + " cores, " + GpuName +
                       ", " + SystemInfo.graphicsDeviceType +
                       ", tier " + DetectTier() + " (" + QualityProfile.Create(DetectTier()).Name + ")";
            }
        }

        /// <summary>Forgets the cached probe. Used when the player changes tier.</summary>
        public static void ResetCache()
        {
            _cachedTier = Unset;
        }
    }
}
