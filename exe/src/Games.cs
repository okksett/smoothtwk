// Biblioteca de juegos: detecta donde esta instalado cada juego, sus ejecutables y su icono.
// Los iconos se sacan del juego instalado en la PC; no se incluye ningun logo dentro del exe.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Hanafuda
{
    public class GameDef
    {
        public string Id, Name, Short;
        public string[] SteamIds = new string[0], SteamDirs = new string[0], EpicNames = new string[0], UninstallNames = new string[0], DefaultRoots = new string[0];
        public string[] RegistryRoots = new string[0];   // "HKLM\\...|ValueName" que guardan la carpeta del juego
        public string[] Exes = new string[0];            // rutas relativas a la carpeta del juego; * permitido en el nombre
        public string[] IconExes = new string[0];        // rutas relativas preferidas para el icono
    }

    public class GameInfo
    {
        public GameDef Def;
        public string Root = "";
        public List<string> Exes = new List<string>();
        public string IconPath = "";
        public bool Installed { get { return Exes.Count > 0; } }
    }

    public static class GameLibrary
    {
        static string Env(string name) { return Environment.GetEnvironmentVariable(name) ?? ""; }

        public static readonly List<GameDef> Defs = new List<GameDef>
        {
            new GameDef { Id = "fivem", Name = "FiveM", Short = "5M",
                DefaultRoots = new[] { Path.Combine(Env("LOCALAPPDATA"), "FiveM") },
                Exes = new[] { "FiveM.exe", "FiveM.app\\*GTAProcess.exe", "FiveM.app\\data\\cache\\subprocess\\*GTAProcess.exe" }, IconExes = new[] { "FiveM.exe" } },
            new GameDef { Id = "r6", Name = "Rainbow Six Siege", Short = "R6",
                SteamIds = new[] { "359550" }, SteamDirs = new[] { "Tom Clancy's Rainbow Six Siege" }, UninstallNames = new[] { "Rainbow Six Siege" },
                RegistryRoots = new[] { "HKLM\\SOFTWARE\\WOW6432Node\\Ubisoft\\Launcher\\Installs\\635|InstallDir" },
                DefaultRoots = new[] { "C:\\Program Files (x86)\\Ubisoft\\Ubisoft Game Launcher\\games\\Tom Clancy's Rainbow Six Siege" },
                Exes = new[] { "RainbowSix.exe", "RainbowSix_BE.exe" }, IconExes = new[] { "RainbowSix.exe" } },
            new GameDef { Id = "valorant", Name = "VALORANT", Short = "VAL",
                UninstallNames = new[] { "VALORANT" }, DefaultRoots = new[] { "C:\\Riot Games\\VALORANT\\live" },
                Exes = new[] { "VALORANT.exe", "ShooterGame\\Binaries\\Win64\\VALORANT-Win64-Shipping.exe" }, IconExes = new[] { "VALORANT.exe" } },
            new GameDef { Id = "fortnite", Name = "Fortnite", Short = "FN",
                EpicNames = new[] { "Fortnite" }, DefaultRoots = new[] { "C:\\Program Files\\Epic Games\\Fortnite" },
                Exes = new[] { "FortniteGame\\Binaries\\Win64\\FortniteClient-Win64-Shipping.exe", "FortniteGame\\Binaries\\Win64\\FortniteClient-Win64-Shipping_EAC_EOS.exe",
                    "FortniteGame\\Binaries\\Win64\\FortniteClient-Win64-Shipping_BE.exe", "FortniteGame\\Binaries\\Win64\\FortniteLauncher.exe" },
                IconExes = new[] { "FortniteGame\\Binaries\\Win64\\FortniteLauncher.exe", "FortniteGame\\Binaries\\Win64\\FortniteClient-Win64-Shipping.exe" } },
            new GameDef { Id = "warzone", Name = "Call of Duty: Warzone", Short = "WZ",
                SteamIds = new[] { "1938090", "1962663" }, SteamDirs = new[] { "Call of Duty HQ", "Call of Duty" }, UninstallNames = new[] { "Call of Duty" },
                DefaultRoots = new[] { "C:\\Program Files (x86)\\Call of Duty", "C:\\XboxGames\\Call of Duty\\Content" },
                Exes = new[] { "*cod*.exe", "_retail_\\*cod*.exe" }, IconExes = new[] { "cod.exe", "_retail_\\cod.exe" } },
            new GameDef { Id = "minecraft", Name = "Minecraft", Short = "MC",
                UninstallNames = new[] { "Minecraft Launcher" },
                DefaultRoots = new[] { "C:\\XboxGames\\Minecraft Launcher\\Content", "C:\\Program Files (x86)\\Minecraft Launcher", Path.Combine(Env("APPDATA"), ".minecraft"),
                    Path.Combine(Env("LOCALAPPDATA"), "Packages\\Microsoft.4297127D64EC6_8wekyb3d8bbwe\\LocalCache\\Local"), "C:\\XboxGames\\Minecraft for Windows\\Content" },
                Exes = new[] { "Minecraft.exe", "MinecraftLauncher.exe", "Minecraft.Windows.exe", "runtime\\*\\windows-x64\\*\\bin\\javaw.exe", "runtime\\*\\bin\\javaw.exe" },
                IconExes = new[] { "Minecraft.exe", "MinecraftLauncher.exe", "Minecraft.Windows.exe" } }
        };

        static readonly Dictionary<string, GameInfo> cache = new Dictionary<string, GameInfo>();
        static readonly object gate = new object();

        public static GameDef Def(string id) { return Defs.FirstOrDefault(d => d.Id == id); }

        public static GameInfo Find(string id, bool refresh = false)
        {
            lock (gate)
            {
                GameInfo info;
                if (!refresh && cache.TryGetValue(id, out info)) return info;
                info = Detect(Def(id));
                cache[id] = info;
                return info;
            }
        }

        public static void Refresh() { lock (gate) cache.Clear(); }

        public static GameInfo Detect(GameDef def)
        {
            var info = new GameInfo { Def = def };
            if (def == null) return info;
            foreach (var root in Roots(def).Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var found = new List<string>();
                foreach (var rel in def.Exes) found.AddRange(Expand(root, rel));
                if (found.Count == 0) continue;
                info.Root = root;
                foreach (var f in found) if (!info.Exes.Contains(f, StringComparer.OrdinalIgnoreCase)) info.Exes.Add(f);
                if (info.IconPath.Length == 0)
                    foreach (var rel in def.IconExes)
                    {
                        var icon = Expand(root, rel).FirstOrDefault();
                        if (icon != null) { info.IconPath = icon; break; }
                    }
                if (info.IconPath.Length == 0) info.IconPath = info.Exes[0];
            }
            return info;
        }

        // Expande una ruta relativa con comodines (* en carpetas o en el nombre) sin recorrer todo el disco.
        static IEnumerable<string> Expand(string root, string rel)
        {
            var parts = rel.Split('\\');
            var current = new List<string> { root };
            for (int i = 0; i < parts.Length; i++)
            {
                bool last = i == parts.Length - 1;
                var next = new List<string>();
                foreach (var dir in current)
                {
                    try
                    {
                        if (parts[i].Contains("*"))
                            next.AddRange(last ? Directory.GetFiles(dir, parts[i]) : Directory.GetDirectories(dir, parts[i]));
                        else
                        {
                            var p = Path.Combine(dir, parts[i]);
                            if (last ? File.Exists(p) : Directory.Exists(p)) next.Add(p);
                        }
                    }
                    catch { }
                }
                current = next.Take(40).ToList();
                if (current.Count == 0) break;
            }
            return current;
        }

        static IEnumerable<string> Roots(GameDef def)
        {
            foreach (var r in def.DefaultRoots) yield return r;
            foreach (var spec in def.RegistryRoots)
            {
                var p = spec.Split('|');
                var s = Engine.ReadReg(p[0], p[1]);
                if (s.Exists) yield return s.Value.TrimEnd('\\', '/').Replace('/', '\\');
            }
            if (def.SteamDirs.Length > 0)
                foreach (var lib in SteamLibraries())
                    foreach (var d in def.SteamDirs) yield return Path.Combine(lib, "steamapps", "common", d);
            if (def.EpicNames.Length > 0)
                foreach (var e in EpicInstalls())
                    if (def.EpicNames.Any(n => string.Equals(n, e.Key, StringComparison.OrdinalIgnoreCase))) yield return e.Value;
            if (def.UninstallNames.Length > 0)
                foreach (var u in UninstallEntries())
                    if (def.UninstallNames.Any(n => u.Key.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)) yield return u.Value;
        }

        static List<string> SteamLibraries()
        {
            var libs = new List<string>();
            var steam = Engine.ReadReg("HKCU\\Software\\Valve\\Steam", "SteamPath");
            var root = steam.Exists ? steam.Value.Replace('/', '\\') : "C:\\Program Files (x86)\\Steam";
            libs.Add(root);
            try
            {
                var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdf))
                    foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                        libs.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
            }
            catch { }
            return libs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        static List<KeyValuePair<string, string>> EpicInstalls()
        {
            var list = new List<KeyValuePair<string, string>>();
            var dir = Path.Combine(Env("ProgramData"), "Epic\\EpicGamesLauncher\\Data\\Manifests");
            try
            {
                if (Directory.Exists(dir))
                    foreach (var f in Directory.GetFiles(dir, "*.item"))
                    {
                        var d = Json.Serializer.DeserializeObject(File.ReadAllText(f)) as Dictionary<string, object>;
                        var name = Json.S(d, "DisplayName");
                        var loc = Json.S(d, "InstallLocation");
                        if (name != null && loc != null) list.Add(new KeyValuePair<string, string>(name, loc));
                    }
            }
            catch { }
            return list;
        }

        static List<KeyValuePair<string, string>> UninstallEntries()
        {
            var list = new List<KeyValuePair<string, string>>();
            var sources = new[]
            {
                new { Hive = RegistryHive.LocalMachine, Path = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall" },
                new { Hive = RegistryHive.LocalMachine, Path = "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall" },
                new { Hive = RegistryHive.CurrentUser, Path = "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall" }
            };
            foreach (var s in sources)
            {
                try
                {
                    using (var root = RegistryKey.OpenBaseKey(s.Hive, RegistryView.Registry64))
                    using (var k = root.OpenSubKey(s.Path))
                    {
                        if (k == null) continue;
                        foreach (var sub in k.GetSubKeyNames())
                            using (var e = k.OpenSubKey(sub))
                            {
                                if (e == null) continue;
                                var name = e.GetValue("DisplayName") as string;
                                if (string.IsNullOrEmpty(name)) continue;
                                var loc = e.GetValue("InstallLocation") as string;
                                if (string.IsNullOrEmpty(loc))
                                {
                                    var icon = (e.GetValue("DisplayIcon") as string ?? "").Split(',')[0].Trim('"');
                                    if (icon.Length > 0) { try { loc = Path.GetDirectoryName(icon); } catch { } }
                                }
                                if (!string.IsNullOrEmpty(loc)) list.Add(new KeyValuePair<string, string>(name, loc.Trim('"').TrimEnd('\\')));
                            }
                    }
                }
                catch { }
            }
            return list;
        }

        // ------------------------------------------------------------ iconos
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHDefExtractIcon(string iconFile, int index, uint flags, out IntPtr large, out IntPtr small, uint size);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);
        static readonly Dictionary<string, Bitmap> icons = new Dictionary<string, Bitmap>();

        public static Bitmap Icon(GameInfo info, int size)
        {
            if (info == null || info.IconPath.Length == 0) return null;
            var key = info.IconPath + "|" + size;
            lock (icons)
            {
                Bitmap bmp;
                if (icons.TryGetValue(key, out bmp)) return bmp;
                bmp = null;
                try
                {
                    IntPtr large, small;
                    if (SHDefExtractIcon(info.IconPath, 0, 0, out large, out small, (uint)(size | (16 << 16))) == 0 && large != IntPtr.Zero)
                    {
                        using (var ic = System.Drawing.Icon.FromHandle(large)) bmp = ic.ToBitmap();
                        DestroyIcon(large);
                        if (small != IntPtr.Zero) DestroyIcon(small);
                    }
                }
                catch { }
                if (bmp == null)
                {
                    try { using (var ic = System.Drawing.Icon.ExtractAssociatedIcon(info.IconPath)) bmp = ic.ToBitmap(); } catch { }
                }
                icons[key] = bmp;
                return bmp;
            }
        }

        // Valores de registro por ejecutable para los ajustes "game-gpu" y "game-fso".
        public static List<RegEntry> ExeEntries(string special, string game)
        {
            var list = new List<RegEntry>();
            foreach (var exe in Find(game).Exes)
            {
                if (special == "game-gpu")
                {
                    const string path = "HKCU\\Software\\Microsoft\\DirectX\\UserGpuPreferences";
                    var cur = Engine.ReadReg(path, exe);
                    var tokens = (cur.Exists ? cur.Value : "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(t => !t.StartsWith("GpuPreference=", StringComparison.OrdinalIgnoreCase)).ToList();
                    tokens.Insert(0, "GpuPreference=2");
                    list.Add(new RegEntry { Path = path, Name = exe, Kind = "String", Value = string.Join(";", tokens) + ";" });
                }
                else if (special == "game-fso")
                {
                    const string path = "HKCU\\Software\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers";
                    var cur = Engine.ReadReg(path, exe);
                    var flags = (cur.Exists ? cur.Value : "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Where(f => f != "~").ToList();
                    if (!flags.Contains("DISABLEDXMAXIMIZEDWINDOWEDMODE", StringComparer.OrdinalIgnoreCase)) flags.Add("DISABLEDXMAXIMIZEDWINDOWEDMODE");
                    list.Add(new RegEntry { Path = path, Name = exe, Kind = "String", Value = "~ " + string.Join(" ", flags) });
                }
            }
            return list;
        }
    }
}
