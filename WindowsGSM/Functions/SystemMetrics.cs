using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;

namespace WindowsGSM.Functions
{
    class SystemMetrics
    {
        public string CPUType { get; private set; }
        public int CPUCoreCount { get; private set; }
        public string RAMType { get; private set; }
        public double RAMTotalSize { get; private set; }
        public string DiskName { get; private set; }
        public string DiskType { get; private set; }
        public long DiskTotalSize { get; private set; }

        public void GetCPUStaticInfo()
        {
            try
            {
                ManagementObjectCollection mbo = new ManagementObjectSearcher("SELECT Name, NumberOfCores FROM Win32_Processor").Get();
                CPUType = mbo.Cast<ManagementBaseObject>().Select(c => c["Name"].ToString()).FirstOrDefault();
                CPUCoreCount = mbo.Cast<ManagementBaseObject>().Sum(x => int.Parse(x["NumberOfCores"].ToString()));
            }
            catch
            {
                CPUType = "Fail to get CPU Type";
                CPUCoreCount = -1;
            }
        }

        public void GetRAMStaticInfo()
        {
            try
            {
                const string RAM_TOTAL_MEMORY = "TotalVisibleMemorySize";
                RAMTotalSize = new ManagementObjectSearcher($"Select {RAM_TOTAL_MEMORY} from Win32_OperatingSystem").Get().Cast<ManagementObject>().Select(m => double.Parse(m[RAM_TOTAL_MEMORY].ToString())).FirstOrDefault();
                RAMType = GetMemoryType();
            }
            catch
            {
                RAMTotalSize = -1;
                RAMType = "Fail to get RAM Type";
            }
        }

        public void GetDiskStaticInfo(string disk = null)
        {
            disk ??= Path.GetPathRoot(Process.GetCurrentProcess().MainModule.FileName);
            DiskName = disk.TrimEnd('\\');
            DiskType = DriveInfo.GetDrives().Where(x => (x.Name == disk) && x.IsReady).Select(x => x.DriveFormat).FirstOrDefault();
            DiskTotalSize = DriveInfo.GetDrives().Where(x => (x.Name == disk) && x.IsReady).Select(x => x.TotalSize).FirstOrDefault();
        }

        public static double GetCPUUsage()
        {
            try
            {
                const string CPU_USAGE = "PercentProcessorTime";
                return double.Parse(new ManagementObjectSearcher($"SELECT {CPU_USAGE} FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'").Get().Cast<ManagementObject>().First().Properties[CPU_USAGE].Value.ToString());
            }
            catch
            {
                return 0;
            }
        }

        public double GetRAMUsage()
        {
            try
            {
                const string RAM_FREE_MEMORY = "FreePhysicalMemory";
                double freeMemory = new ManagementObjectSearcher($"Select {RAM_FREE_MEMORY} from Win32_OperatingSystem").Get().Cast<ManagementObject>().Select(m => double.Parse(m[RAM_FREE_MEMORY].ToString())).FirstOrDefault();
                return (RAMTotalSize == 0) ? 0 : (1 - (freeMemory / RAMTotalSize)) * 100;
            }
            catch
            {
                return 0;
            }
        }

        public double GetDiskUsage(string disk = null)
        {
            disk ??= Path.GetPathRoot(Process.GetCurrentProcess().MainModule.FileName);
            double freeSpace = DriveInfo.GetDrives().Where(x => (x.Name == disk) && x.IsReady).Select(x => x.AvailableFreeSpace).FirstOrDefault();
            return (DiskTotalSize == 0) ? 0 : (1 - (freeSpace / DiskTotalSize)) * 100;
        }

        public static string GetMemoryRatioString(double percent, double totalMemory)
        {
            int count = 0;
            while (totalMemory > 1024.0)
            {
                totalMemory /= 1024.0;
                count++;
            }

            return $"{string.Format("{0:0.00}", totalMemory * percent / 100)}/{string.Format("{0:0.00}", totalMemory)} {(count == 1 ? "MB" : count == 2 ? "GB" : "TB")} ";
        }

        public static string GetDiskRatioString(double percent, double totalDisk)
        {
            int count = 0;
            while (totalDisk > 1024.0)
            {
                totalDisk /= 1024.0;
                count++;
            }

            return $"{string.Format("{0:0.00}", totalDisk * percent / 100)}/{string.Format("{0:0.00}", totalDisk)} {(count == 1 ? "KB" : count == 2 ? "MB" : count == 3 ? "GB" : "TB")} ";
        }

        private static string GetMemoryType()
        {
            const string RAM_MEMORY_TYPE = "MemoryType";
            ManagementObjectCollection mbo = new ManagementObjectSearcher($"SELECT {RAM_MEMORY_TYPE} FROM Win32_PhysicalMemory").Get();
            return mbo.Cast<ManagementBaseObject>().Select(c => int.Parse(c[RAM_MEMORY_TYPE].ToString())).FirstOrDefault() switch {
                1 => "Other",
                2 => "DRAM",
                3 => "Synchronous DRAM",
                4 => "Cache DRAM",
                5 => "EDO",
                6 => "EDRAM",
                7 => "VRAM",
                8 => "SRAM",
                9 => "RAM",
                10 => "ROM",
                11 => "Flash",
                12 => "EEPROM",
                13 => "FEPROM",
                14 => "EPROM",
                15 => "CDRAM",
                16 => "3DRAM",
                17 => "SDRAM",
                18 => "SGRAM",
                19 => "RDRAM",
                20 => "DDR",
                21 => "DDR2",
                22 => "DDR2 FB-DIMM",
                23 => "Undefined 23",
                24 => "DDR3",
                25 => "Undefined 25",
                _ => "Unknown",
            };
        }
    }
}
