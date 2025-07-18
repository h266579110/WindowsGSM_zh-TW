using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace WindowsGSM_Plugin_Development
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.BackgroundColor = ConsoleColor.DarkBlue;
            string title = "Welcome to WindowsGSM Plugin Development Environment";
            Console.WriteLine($"\t{string.Concat(Enumerable.Repeat("-", title.Length))}\t\t");
            Console.WriteLine($"\t{title}\t\t");
            Console.WriteLine($"\t{string.Concat(Enumerable.Repeat("-", title.Length))}\t\t");
            Console.ResetColor();

            string currentDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            string wgsmRoot = Path.GetFullPath(Path.Combine(currentDir, "..", "..", ".."));
            string wgsm = Path.Combine(wgsmRoot, "WindowsGSM");
            string wgsnBin = Directory.CreateDirectory(Path.Combine(wgsm, "bin")).FullName; // Create \bin directory
#if DEBUG
            string target = "Debug";
#else
            string target = "Release";
#endif
            string wgsmTarget = Directory.CreateDirectory(Path.Combine(wgsnBin, target)).FullName; // Create \Debug or \Release directory
            string wgsmPlugins = Directory.CreateDirectory(Path.Combine(wgsmTarget, "plugins")).FullName; // Create \plugins directory
            string localPlugins = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "..", "..", "Plugins"));

            Console.Write("\nSearching your new plugins in ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"({localPlugins})\n");
            Console.ResetColor();

            List<string> pluginsList = [];
            foreach (string pluginFile in Directory.GetFiles(localPlugins, "*.cs", SearchOption.TopDirectoryOnly).ToList())
            {
                pluginsList.Add(pluginFile);
                Console.Write("Found => ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"{Path.GetFileName(pluginFile)}\n");
                Console.ResetColor();
            }

            Console.Write("\nReady to install your new plugins to ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"({wgsmPlugins})\n");
            Console.ResetColor();

            foreach (string pluginFile in pluginsList)
            {
                string pluginFolder = Directory.CreateDirectory(Path.Combine(wgsmPlugins, Path.GetFileName(pluginFile))).FullName;
                string plugin = Path.Combine(pluginFolder, Path.GetFileName(pluginFile));
                Console.Write("Install => ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"{Path.GetFileName(pluginFile)}");
                Console.ResetColor();
                Console.Write(" => ");

                if (DeleteOldPlugin(plugin))
                {
                    File.Copy(pluginFile, Path.Combine(pluginFolder, Path.GetFileName(pluginFile)));
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("Success\n");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("Fail\n");
                }

                Console.ResetColor();
            }

            Console.Write("\nPlugins installation done, please change the Startup Project to ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("WindowsGSM");
            Console.ResetColor();
            Console.Write(" for testing your new plugins!\n");

            Console.WriteLine("\nPress Any Key To Exit...");
            Console.ReadKey();

            /*
            Console.WriteLine("\nAuto close in 5 seconds...");
            for (var i = 0; i < 5; i++)
            {
                Thread.Sleep(1000);
            }*/
        }

        private static bool DeleteOldPlugin(string oldPluginFile)
        {
            if (!File.Exists(oldPluginFile))
            {
                return true;
            }

            try
            {
                File.Delete(oldPluginFile);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }
    }
}
