// Limpieza de disco, programas al iniciar y lecturas del HUD.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Hanafuda
{
    public class CleanTarget { public string Name, Desc; public List<string> Paths = new List<string>(); public bool On = true, Recycle; public long Size; }

    public static class Cleaner
    {
        public static bool SafePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) return false;
            string full;
            try { full = Path.GetFullPath(path).TrimEnd('\\'); } catch { return false; }
            if (full.Length <= 3) return false;
            var blocked = new[]
            {
                Environment.GetEnvironmentVariable("WINDIR"), Environment.GetEnvironmentVariable("LOCALAPPDATA"), Environment.GetEnvironmentVariable("APPDATA"),
                Environment.GetEnvironmentVariable("ProgramData"), Environment.GetEnvironmentVariable("USERPROFILE"),
                Path.Combine(Environment.GetEnvironmentVariable("SystemDrive") + "\\", "Users"), Environment.SystemDirectory
            };
            return !blocked.Where(b => !string.IsNullOrEmpty(b)).Any(b => string.Equals(b.TrimEnd('\\'), full, StringComparison.OrdinalIgnoreCase));
        }

        public static long Size(string path)
        {
            long total = 0;
            try
            {
                foreach (var i in new DirectoryInfo(path).EnumerateFileSystemInfos())
                {
                    if ((i.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                    var f = i as FileInfo;
                    total += f != null ? f.Length : Size(i.FullName);
                }
            }
            catch { }
            return total;
        }

        // Borra el contenido. Los enlaces se quitan sin entrar en su destino.
        public static long Clear(string path)
        {
            long freed = 0;
            FileSystemInfo[] items;
            try { items = new DirectoryInfo(path).GetFileSystemInfos(); } catch { return 0; }
            foreach (var i in items)
            {
                try
                {
                    if ((i.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        if (i is DirectoryInfo) Directory.Delete(i.FullName, false); else i.Delete();
                        continue;
                    }
                    var f = i as FileInfo;
                    if (f != null) { long len = f.Length; f.Attributes = FileAttributes.Normal; f.Delete(); freed += len; }
                    else { freed += Clear(i.FullName); i.Delete(); }
                }
                catch { }
            }
            return freed;
        }

        static IEnumerable<string> Chromium(string userData)
        {
            if (!Directory.Exists(userData)) yield break;
            yield return Path.Combine(userData, "ShaderCache");
            yield return Path.Combine(userData, "GrShaderCache");
            foreach (var d in Directory.GetDirectories(userData))
            {
                var n = Path.GetFileName(d);
                if (n == "Default" || n.StartsWith("Profile ", StringComparison.Ordinal))
                    foreach (var c in new[] { "Cache", "Code Cache", "GPUCache" }) yield return Path.Combine(d, c);
            }
        }

        public static List<CleanTarget> Targets()
        {
            string la = Environment.GetEnvironmentVariable("LOCALAPPDATA"), win = Environment.GetEnvironmentVariable("WINDIR"), pd = Environment.GetEnvironmentVariable("ProgramData");
            var wer = new List<string>();
            foreach (var b in new[] { pd + "\\Microsoft\\Windows\\WER", la + "\\Microsoft\\Windows\\WER" })
                foreach (var s in new[] { "ReportArchive", "ReportQueue", "Temp" }) wer.Add(b + "\\" + s);
            var firefox = new List<string>();
            var ff = la + "\\Mozilla\\Firefox\\Profiles";
            if (Directory.Exists(ff)) foreach (var p in Directory.GetDirectories(ff)) { firefox.Add(Path.Combine(p, "cache2")); firefox.Add(Path.Combine(p, "shader-cache")); }
            const string cacheDesc = "Solo caché. No toca contraseñas, cookies ni historial.";
            var list = new List<CleanTarget>
            {
                new CleanTarget { Name = "Temporales del usuario", Desc = "Archivos que los programas dejaron en %TEMP%.", Paths = { Path.GetTempPath() } },
                new CleanTarget { Name = "Temporales de Windows", Desc = "Contenido de C:\\Windows\\Temp.", Paths = { win + "\\Temp" } },
                new CleanTarget { Name = "Volcados de errores", Desc = "Minidumps de pantallazos azules.", Paths = { win + "\\Minidump" } },
                new CleanTarget { Name = "Informes de errores", Desc = "Reportes de fallos ya generados.", Paths = wer },
                new CleanTarget { Name = "Caché de Chrome", Desc = cacheDesc, Paths = Chromium(la + "\\Google\\Chrome\\User Data").ToList() },
                new CleanTarget { Name = "Caché de Edge", Desc = cacheDesc, Paths = Chromium(la + "\\Microsoft\\Edge\\User Data").ToList() },
                new CleanTarget { Name = "Caché de Brave", Desc = cacheDesc, Paths = Chromium(la + "\\BraveSoftware\\Brave-Browser\\User Data").ToList() },
                new CleanTarget { Name = "Caché de Firefox", Desc = cacheDesc, Paths = firefox },
                new CleanTarget { Name = "Papelera de reciclaje", Desc = "Borra definitivamente lo que hay en la Papelera.", On = false, Recycle = true }
            };
            return list;
        }

        public static void Measure(CleanTarget t)
        {
            t.Size = t.Paths.Where(Directory.Exists).Sum(p => Size(p));
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHEmptyRecycleBin(IntPtr hwnd, string root, uint flags);

        public static long Run(CleanTarget t)
        {
            if (t.Recycle)
            {
                if (Engine.Dry) { Engine.Log("[simulación] vaciar Papelera"); return 0; }
                SHEmptyRecycleBin(IntPtr.Zero, null, 0x7);
                return 0;
            }
            long freed = 0;
            foreach (var p in t.Paths)
            {
                if (!Directory.Exists(p) || !SafePath(p)) continue;
                if (Engine.Dry) { Engine.Log("[simulación] limpiar " + p); continue; }
                freed += Clear(p);
            }
            return freed;
        }

        public static string Format(long b)
        {
            if (b >= 1L << 30) return (b / 1073741824.0).ToString("0.0") + " GB";
            if (b >= 1L << 20) return (b / 1048576) + " MB";
            if (b >= 1L << 10) return (b / 1024) + " KB";
            return b + " B";
        }
    }

    public class StartupItem { public string Name, Scope, Command, Approved; public bool Enabled; }

    public static class Startup
    {
        public static List<StartupItem> Items()
        {
            const string cv = "Microsoft\\Windows\\CurrentVersion";
            var list = new List<StartupItem>();
            var sources = new[]
            {
                new[] { "HKCU", "Software\\" + cv + "\\Run", "HKCU\\Software\\" + cv + "\\Explorer\\StartupApproved\\Run", "usuario" },
                new[] { "HKLM", "SOFTWARE\\" + cv + "\\Run", "HKLM\\SOFTWARE\\" + cv + "\\Explorer\\StartupApproved\\Run", "equipo" },
                new[] { "HKLM", "SOFTWARE\\WOW6432Node\\" + cv + "\\Run", "HKLM\\SOFTWARE\\" + cv + "\\Explorer\\StartupApproved\\Run32", "equipo" }
            };
            foreach (var s in sources)
            {
                var hive = s[0] == "HKCU" ? RegistryHive.CurrentUser : RegistryHive.LocalMachine;
                using (var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var k = root.OpenSubKey(s[1]))
                {
                    if (k == null) continue;
                    foreach (var n in k.GetValueNames().Where(x => x.Length > 0))
                        list.Add(new StartupItem { Name = n, Scope = s[3], Command = Convert.ToString(k.GetValue(n)), Approved = s[2], Enabled = !IsOff(s[2], n) });
                }
            }
            var folders = new[]
            {
                new[] { Environment.GetFolderPath(Environment.SpecialFolder.Startup), "HKCU\\Software\\" + cv + "\\Explorer\\StartupApproved\\StartupFolder" },
                new[] { Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "HKLM\\SOFTWARE\\" + cv + "\\Explorer\\StartupApproved\\StartupFolder" }
            };
            foreach (var f in folders)
            {
                if (!Directory.Exists(f[0])) continue;
                foreach (var file in Directory.GetFiles(f[0]).Where(x => !x.EndsWith("desktop.ini", StringComparison.OrdinalIgnoreCase)))
                {
                    var n = Path.GetFileName(file);
                    list.Add(new StartupItem { Name = n, Scope = "carpeta", Command = file, Approved = f[1], Enabled = !IsOff(f[1], n) });
                }
            }
            return list;
        }

        static bool IsOff(string approved, string name)
        {
            var s = Engine.ReadReg(approved, name);
            if (!s.Exists || s.Kind != "Binary") return false;
            var bytes = Convert.FromBase64String(s.Value);
            return bytes.Length > 0 && (bytes[0] & 1) == 1;
        }

        public static void Set(StartupItem item, bool enabled)
        {
            var data = new byte[12];
            if (enabled) data[0] = 2;
            else { data[0] = 3; Array.Copy(BitConverter.GetBytes(DateTime.Now.ToFileTime()), 0, data, 4, 8); }
            Engine.WriteReg(item.Approved, item.Name, "Binary", Convert.ToBase64String(data));
            if (!Engine.Dry) item.Enabled = enabled;
        }
    }

    public static class Metrics
    {
        [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
        [DllImport("kernel32.dll")] static extern ulong GetTickCount64();
        static long lastIdle, lastTotal;

        public static double Cpu()
        {
            long idle, kernel, user;
            if (!GetSystemTimes(out idle, out kernel, out user)) return 0;
            long total = kernel + user;
            long di = idle - lastIdle, dt = total - lastTotal;
            lastIdle = idle; lastTotal = total;
            if (dt <= 0) return 0;
            return Math.Max(0, Math.Min(100, 100.0 * (dt - di) / dt));
        }

        public static void Ram(out double usedGb, out double totalGb, out double pct)
        {
            var m = new Engine.MemoryStatus();
            Engine.GlobalMemoryStatusEx(m);
            totalGb = m.TotalPhys / 1073741824.0;
            usedGb = (m.TotalPhys - m.AvailPhys) / 1073741824.0;
            pct = m.Load;
        }

        public static TimeSpan Uptime() { return TimeSpan.FromMilliseconds(GetTickCount64()); }

        public static string Gpu()
        {
            var names = new List<string>();
            using (var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (var cls = root.OpenSubKey("SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}"))
            {
                if (cls != null)
                    foreach (var sub in cls.GetSubKeyNames().Where(x => x.Length == 4))
                        using (var k = cls.OpenSubKey(sub))
                        {
                            var d = k == null ? null : k.GetValue("DriverDesc") as string;
                            if (!string.IsNullOrEmpty(d) && !names.Contains(d) && d.IndexOf("Basic", StringComparison.OrdinalIgnoreCase) < 0) names.Add(d);
                        }
            }
            return names.Count > 0 ? string.Join(" + ", names) : "GPU no detectada";
        }

        public static string WindowsName()
        {
            var build = Engine.ReadReg("HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "CurrentBuildNumber");
            var disp = Engine.ReadReg("HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "DisplayVersion");
            var edition = Engine.ReadReg("HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "EditionID");
            int b;
            int.TryParse(build.Exists ? build.Value : "0", out b);
            return (b >= 22000 ? "Windows 11 " : "Windows 10 ") + (edition.Exists ? edition.Value : "") + (disp.Exists ? " · " + disp.Value : "") + " · build " + b;
        }
    }
}
