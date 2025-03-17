using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.IO;
using System.Threading.Tasks;

namespace WindowsGSM.Functions
{
    public class JavaHelper
    {
        private static string JreInstallFileName = $"jdk-22.0.2_windows-x64_bin.exe";
        private static string JavaAbsoluteInstallPath = Path.Combine(GetProgramFilesAbsolutePath(), "Java");
        private static string JreAbsoluteInstallPath = Path.Combine(JavaAbsoluteInstallPath, "jdk-22");
        private static string JreDownloadLink = "https://download.oracle.com/java/22/archive/jdk-22.0.2_windows-x64_bin.exe";

        public struct JREDownloadTaskResult : IEquatable<JREDownloadTaskResult>
        {
            public bool installed;
            public string error;

            public readonly bool Equals(JREDownloadTaskResult other)
            {
                return installed == other.installed && error == other.error;
            }

            public override readonly bool Equals(object obj) {
                return obj is JREDownloadTaskResult result && Equals(result);
            }

            public override int GetHashCode() {
                throw new NotImplementedException();
            }
            public static bool operator ==(JREDownloadTaskResult left, JREDownloadTaskResult right) {
                return left.Equals(right);
            }

            public static bool operator !=(JREDownloadTaskResult left, JREDownloadTaskResult right) {
                return !(left == right);
            }
        }
        public static string FindJavaExecutableAbsolutePath()
        {
            return FindNewestJavaExecutableAbsolutePath();
        }

        public static async Task<JREDownloadTaskResult> DownloadJREToServer(string serverID)
        {
            string serverFilesPath = Functions.ServerPath.GetServersServerFiles(serverID);
            
            //Download jre-8u231-windows-i586-iftw.exe from https://www.java.com/en/download/manual.jsp
            string jrePath = Path.Combine(serverFilesPath, JreInstallFileName);
            JREDownloadTaskResult result;
            result.installed = true;
            result.error = string.Empty;

            try {
                //Run jre-8u231-windows-i586-iftw.exe to install Java
                Stream stream = await App.httpClient.GetStreamAsync(JreDownloadLink);
                using FileStream fileStream = File.Create(jrePath);
                await stream.CopyToAsync(fileStream);
                //using WebClient webClient = new();
                //await webClient.DownloadFileTaskAsync(JreDownloadLink, jrePath);
                string installPath = Functions.ServerPath.GetServersServerFiles(serverID);
                ProcessStartInfo psi = new(jrePath) {
                    WorkingDirectory = installPath,
                    Arguments = $"INSTALL_SILENT=Enable INSTALLDIR=\"{JreAbsoluteInstallPath}\""
                };
                Process p = new() {
                    StartInfo = psi,
                    EnableRaisingEvents = true
                };
                p.Start();

                //wait until the java.exe can be found in the newly installed jre folder
                while (FindJavaExecutableAbsolutePathInJavaRuntimeDirectory(JreAbsoluteInstallPath).Length == 0) {
                    await Task.Delay(100);
                }
            }
            catch
            {
                result.error = String.Concat("Could not install JRE '", JreInstallFileName, "'");
                result.installed = false;
                return result;
            }

            return result;
        }

        public static bool IsJREInstalled()
        {
            return FindJavaExecutableAbsolutePath().Length > 0;
        }

        private static string GetProgramFilesAbsolutePath()
        {
            string programFilesAbsolutePath;
            if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
            {
                //since this a 32bit application a workaround has to be added here since Environment.SpecialFolder.ProgramFiles == Environment.SpecialFolder.CommonProgramFilesX86 in 32bit processes on a 64bit system
                string programFilesX86AbsolutePath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                programFilesAbsolutePath = programFilesX86AbsolutePath.Replace(" (x86)", "");
            }
            else
            {
                programFilesAbsolutePath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            }

            return programFilesAbsolutePath;
        }

        private static string FindJavaExecutableAbsolutePathInJavaRuntimeDirectory(string javaRuntimeAbsolutePath)
        {
            //first check in jre_xxxx/
            string javaExecutableAbsolutePath = Path.Combine(javaRuntimeAbsolutePath, "java.exe");
            if (File.Exists(javaExecutableAbsolutePath))
            {
                //put path in quotes in case there are whitespaces in the path
                return String.Concat("\"", javaExecutableAbsolutePath, "\"");
            }

            //then in jre_xxxx/bin/
            javaExecutableAbsolutePath = Path.Combine(javaRuntimeAbsolutePath, "bin", "java.exe");
            if (File.Exists(javaExecutableAbsolutePath))
            {
                //put path in quotes in case there are whitespaces in the path
                return String.Concat("\"", javaExecutableAbsolutePath, "\"");
            }

            return string.Empty;
        }

        private static string QueryJavaVersion(string javaExecutablePath)
        {
            Process p;
            try
            {
                ProcessStartInfo psi = new("cmd.exe") {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = string.Join(" ", "/c", javaExecutablePath, "-version")
                };
                p = Process.Start(psi);
                p.WaitForExit();
            }
            catch
            {
                return string.Empty;
            }

            // some java version write to stderr on default, apparently even for non error messages
            string output = p.StandardOutput.ReadToEnd();
            if (output.Length == 0)
            {
                output = p.StandardError.ReadToEnd();
            }

            // version string should be 'java version "x.x.x_xxx"'
            int javaVersionStart = output.IndexOf('\"');
            if (javaVersionStart == -1)
            {
                return string.Empty;
            }

            //skip first "
            javaVersionStart += 1;

            int javaVersionEnd = output.IndexOf('\"', javaVersionStart);
            if (javaVersionEnd == -1)
            {
                return string.Empty;
            }

            string javaVersion = output[javaVersionStart..javaVersionEnd];
            return javaVersion;
        }

        private class JavaExecutable(string javaExecutableAbsolutePath, string javaVersionString) : IComparable<JavaExecutable>
        {
            public readonly string javaExecutableAbsolutePath = javaExecutableAbsolutePath;
            public readonly string javaVersionString = javaVersionString;

            public int CompareTo(JavaExecutable other)
            {
                return String.Compare(javaVersionString, other.javaVersionString, true, System.Globalization.CultureInfo.InvariantCulture);
            }

            public override bool Equals(object obj)
            {
                JavaExecutable other = obj as JavaExecutable;
                return other is not null && CompareTo(other) == 0;
            }

            public override int GetHashCode()
            {
                return javaVersionString.GetHashCode();
            }

            public static bool operator == (JavaExecutable left, JavaExecutable right)
            {
                return left.javaVersionString == right.javaVersionString;
            }

            public static bool operator != (JavaExecutable left, JavaExecutable right)
            {
                return left.javaVersionString != right.javaVersionString;
            }

            public static bool operator < (JavaExecutable left, JavaExecutable right)
            {
                return left.CompareTo(right) < 0;
            }

            public static bool operator > (JavaExecutable left, JavaExecutable right)
            {
                return left.CompareTo(right) > 0;
            }

            public static bool operator <= (JavaExecutable left, JavaExecutable right)
            {
                return left.CompareTo(right) <= 0;

            }

            public static bool operator >=(JavaExecutable left, JavaExecutable right)
            {
                return left.CompareTo(right) >= 0;

            }
        };

        private static string FindJavaExecutableAbsolutePath(string javaDirectoryAbsolutePath)
        {
            if (!Directory.Exists(javaDirectoryAbsolutePath))
            {
                return string.Empty;
            }

            List<string> javaRuntimeDirectories = [.. Directory.EnumerateDirectories(javaDirectoryAbsolutePath)];
            if (javaRuntimeDirectories.Count == 0)
            {
                return string.Empty;
            }

            List<JavaExecutable> javaExecutables = [];
            foreach (string javaRuntimePath in javaRuntimeDirectories)
            {
                string javaExecutableAbsolutePath = FindJavaExecutableAbsolutePathInJavaRuntimeDirectory(javaRuntimePath);
                if (javaExecutableAbsolutePath.Length > 0)
                {
                    JavaExecutable javaExecutable = new(javaExecutableAbsolutePath, QueryJavaVersion(javaExecutableAbsolutePath));
                    if (javaExecutable.javaVersionString.Length == 0)
                    {
                        continue;
                    }

                    javaExecutables.Add(javaExecutable);
                }
            }

            if (javaExecutables.Count == 0)
            {
                return string.Empty;
            }

            // sort by version and reverse result so that the most recent version is the first entry in the array
            javaExecutables.Sort();
            javaExecutables.Reverse();

            return javaExecutables[0].javaExecutableAbsolutePath;
        }

        private static string FindNewestJavaExecutableAbsolutePath()
        {
            string javaDirectoryAbsolutePath = Environment.GetEnvironmentVariable("JAVA_HOME");
            javaDirectoryAbsolutePath ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Java");
            string javaRuntimeAbsolutePath = FindJavaExecutableAbsolutePath(javaDirectoryAbsolutePath);

            if (javaRuntimeAbsolutePath.Length > 0)
            {
                return javaRuntimeAbsolutePath;
            }

            if (!Environment.Is64BitOperatingSystem)
            {
                return string.Empty;
            }

            //call GetProgramFilesAbsolutePath in case this is a x86 process running on an x64 os
            javaDirectoryAbsolutePath = Path.Combine(GetProgramFilesAbsolutePath(), "Java");
            return FindJavaExecutableAbsolutePath(javaDirectoryAbsolutePath);
        }
    }
}
