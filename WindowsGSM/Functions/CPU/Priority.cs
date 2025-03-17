using System.Diagnostics;

namespace WindowsGSM.Functions.CPU
{
    static class Priority
    {
        public static int GetPriorityInteger(string priority)
        {
            return int.TryParse(priority, out int result) ? result : 2;
        }

        public static string GetPriorityByInteger(int priority)
        {
            return priority switch {
                0 => "最低",
                1 => "較低",
                3 => "較高",
                4 => "高",
                5 => "即時",
                _ => "一般",
            };
        }

        public static Process SetProcessWithPriority(Process p, int priority)
        {
            p.PriorityClass = priority switch {
                0 => ProcessPriorityClass.Idle,
                1 => ProcessPriorityClass.BelowNormal,
                3 => ProcessPriorityClass.AboveNormal,
                4 => ProcessPriorityClass.High,
                5 => ProcessPriorityClass.RealTime,
                _ => ProcessPriorityClass.Normal,
            };
            return p;
        }
    }
}
