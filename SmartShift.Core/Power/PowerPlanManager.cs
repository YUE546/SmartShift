using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace SmartShift.Core.Power
{
    public static class PowerPlanManager
    {
        // Windows 内置三大电源计划的 GUID
        public static readonly Guid HighPerformance = new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        public static readonly Guid Balanced = new Guid("381b4222-f694-41f0-9685-ff5bb260df2e");
        public static readonly Guid PowerSaver = new Guid("a1841308-3541-4fab-bc81-f71556f20b4a");

        private const uint ERROR_NO_MORE_ITEMS = 259;
        private static readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);
        private static List<PowerPlan> _cachedPlans;
        private static DateTime _cacheTime;

        public static string LastError { get; private set; }

        /// <summary>枚举系统中所有电源计划（带缓存）</summary>
        public static IReadOnlyList<PowerPlan> GetAllPlans()
        {
            if (_cachedPlans != null && DateTime.Now - _cacheTime < _cacheExpiry)
                return _cachedPlans.AsReadOnly();

            var plans = new List<PowerPlan>();
            Guid activeGuid = GetActivePlanId();

            uint index = 0;
            while (true)
            {
                uint bufferSize = 16; // GUID 固定 16 字节
                IntPtr bufferPtr = Marshal.AllocHGlobal((int)bufferSize);
                try
                {
                    uint result = NativeMethods.PowerEnumerate(
                        IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                        16 /* ACCESS_SCHEME */, index, bufferPtr, ref bufferSize);

                    if (result == ERROR_NO_MORE_ITEMS || result != 0)
                        break;

                    Guid planGuid = (Guid)Marshal.PtrToStructure(bufferPtr, typeof(Guid));
                    string planName = ReadFriendlyName(planGuid);
                    bool isActive = planGuid == activeGuid;

                    plans.Add(new PowerPlan(planGuid, planName, isActive));
                    index++;
                }
                finally
                {
                    Marshal.FreeHGlobal(bufferPtr);
                }
            }

            _cachedPlans = plans;
            _cacheTime = DateTime.Now;
            return plans.AsReadOnly();
        }

        /// <summary>强制清除缓存，下次调用 GetAllPlans 时重新枚举</summary>
        public static void ClearCache()
        {
            _cachedPlans = null;
        }

        /// <summary>获取当前活动的电源计划</summary>
        public static PowerPlan GetActivePlan()
        {
            Guid activeGuid = GetActivePlanId();
            string name = ReadFriendlyName(activeGuid);
            return new PowerPlan(activeGuid, name, true);
        }

        /// <summary>切换到指定电源计划（按 GUID）</summary>
        public static bool SetActivePlan(Guid planId)
        {
            IntPtr guidPtr = Marshal.AllocHGlobal(Marshal.SizeOf(planId));
            try
            {
                Marshal.StructureToPtr(planId, guidPtr, false);
                uint result = NativeMethods.PowerSetActiveScheme(IntPtr.Zero, guidPtr);
                if (result == 0)
                {
                    LastError = null;
                    ClearCache();
                    return true;
                }
                else
                {
                    LastError = $"Win32 错误码: {result}";
                    return false;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(guidPtr);
            }
        }

        /// <summary>切换到指定电源计划（按名称模糊匹配）</summary>
        public static bool SetActivePlanByName(string planName)
        {
            if (string.IsNullOrWhiteSpace(planName))
            {
                LastError = "计划名称不能为空";
                return false;
            }

            var plans = GetAllPlans();
            foreach (var plan in plans)
            {
                if (plan.Name.IndexOf(planName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return SetActivePlan(plan.Id);
                }
            }

            LastError = $"未找到名称包含 \"{planName}\" 的电源计划";
            return false;
        }

        private static Guid GetActivePlanId()
        {
            uint result = NativeMethods.PowerGetActiveScheme(IntPtr.Zero, out IntPtr activePolicyGuid);
            if (result != 0)
            {
                throw new InvalidOperationException($"获取活动电源计划失败，错误码: {result}");
            }

            try
            {
                return (Guid)Marshal.PtrToStructure(activePolicyGuid, typeof(Guid));
            }
            finally
            {
                NativeMethods.LocalFree(activePolicyGuid);
            }
        }

        private static string ReadFriendlyName(Guid schemeGuid)
        {
            IntPtr schemePtr = Marshal.AllocHGlobal(Marshal.SizeOf(schemeGuid));
            try
            {
                Marshal.StructureToPtr(schemeGuid, schemePtr, false);

                uint bufferSize = 0;
                // 第一次调用：获取所需缓冲区大小（Buffer 传 IntPtr.Zero）
                uint result = NativeMethods.PowerReadFriendlyName(
                    IntPtr.Zero, schemePtr, IntPtr.Zero, IntPtr.Zero,
                    IntPtr.Zero, ref bufferSize);

                if (bufferSize == 0)
                    return schemeGuid.ToString();

                IntPtr nameBufferPtr = Marshal.AllocHGlobal((int)bufferSize);
                try
                {
                    result = NativeMethods.PowerReadFriendlyName(
                        IntPtr.Zero, schemePtr, IntPtr.Zero, IntPtr.Zero,
                        nameBufferPtr, ref bufferSize);

                    if (result != 0 || bufferSize == 0)
                        return schemeGuid.ToString();

                    // 读取 Unicode 字符串
                    string name = Marshal.PtrToStringUni(nameBufferPtr);
                    return string.IsNullOrEmpty(name) ? schemeGuid.ToString() : name;
                }
                finally
                {
                    Marshal.FreeHGlobal(nameBufferPtr);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(schemePtr);
            }
        }

        internal static class NativeMethods
        {
            [DllImport("powrprof.dll", EntryPoint = "PowerEnumerate")]
            public static extern uint PowerEnumerate(
                IntPtr RootPowerKey,
                IntPtr SchemeGuid,
                IntPtr SubGroupOfPowerSettingsGuid,
                uint AccessFlags,
                uint Index,
                IntPtr Buffer,
                ref uint BufferSize);

            [DllImport("powrprof.dll", EntryPoint = "PowerReadFriendlyName")]
            public static extern uint PowerReadFriendlyName(
                IntPtr RootPowerKey,
                IntPtr SchemeGuid,
                IntPtr SubGroupOfPowerSettingsGuid,
                IntPtr PowerSettingGuid,
                IntPtr Buffer,
                ref uint BufferSize);

            [DllImport("powrprof.dll", EntryPoint = "PowerGetActiveScheme")]
            public static extern uint PowerGetActiveScheme(
                IntPtr UserRootPowerKey,
                out IntPtr ActivePolicyGuid);

            [DllImport("powrprof.dll", EntryPoint = "PowerSetActiveScheme")]
            public static extern uint PowerSetActiveScheme(
                IntPtr UserRootPowerKey,
                IntPtr SchemeGuid);

            [DllImport("kernel32.dll", EntryPoint = "LocalFree")]
            public static extern IntPtr LocalFree(IntPtr hMem);
        }
    }
}
