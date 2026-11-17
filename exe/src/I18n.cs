// Idioma de la interfaz (es / en). Se guarda en HKCU\Software\SmoothTwk\Lang.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.Win32;

namespace SmoothTwk
{
    public static class L
    {
        const string Key = "Software\\SmoothTwk";
        public static string Lang = "es";
        public static bool En { get { return Lang == "en"; } }

        public static string T(string es, string en) { return En ? en : es; }

        public static void Init()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(Key))
                {
                    var v = k == null ? null : k.GetValue("Lang") as string;
                    if (v == "es" || v == "en") { Lang = v; return; }
                }
            }
            catch { }
            Lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? "es" : "en";
        }

        public static void Save(string lang)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(Key)) k.SetValue("Lang", lang);
        }

        // Aplica las traducciones embebidas (<idioma>.json y <idioma>-*.json) sobre el catálogo en español.
        public static void ApplyToCatalog()
        {
            var names = Assembly.GetExecutingAssembly().GetManifestResourceNames()
                .Where(n => n == Lang + ".json" || (n.StartsWith(Lang + "-") && n.EndsWith(".json")));
            foreach (var res in names)
            {
                var root = Json.Serializer.DeserializeObject(Catalog.Resource(res)) as Dictionary<string, object>;
                foreach (var t in Catalog.Tweaks)
                {
                    var d = Entry(root, "tweaks", t.Id);
                    if (d == null) continue;
                    t.Name = Pick(d, "name", t.Name); t.Desc = Pick(d, "desc", t.Desc); t.Group = Pick(d, "group", t.Group);
                    t.Help = Pick(d, "help", t.Help); t.Warn = Has(d, "warn") ? Json.S(d, "warn") : t.Warn;
                }
                foreach (var a in Catalog.Actions)
                {
                    var d = Entry(root, "actions", a.Id);
                    if (d == null) continue;
                    a.Name = Pick(d, "name", a.Name); a.Desc = Pick(d, "desc", a.Desc); a.Group = Pick(d, "group", a.Group);
                    a.Help = Pick(d, "help", a.Help); a.Button = Pick(d, "button", a.Button); a.Warn = Has(d, "warn") ? Json.S(d, "warn") : a.Warn;
                }
                foreach (var g in Catalog.Games)
                {
                    var d = Entry(root, "games", g.Id);
                    if (d == null) continue;
                    g.Tagline = Pick(d, "tagline", g.Tagline);
                    var cautions = Json.A(d, "cautions").Select(x => Convert.ToString(x)).ToList();
                    if (cautions.Count == g.Cautions.Count) g.Cautions = cautions;
                    var guide = Json.D(d, "guide").ToList();
                    if (guide.Count == g.Guide.Count)
                        for (int i = 0; i < guide.Count; i++)
                        {
                            g.Guide[i].Group = Pick(guide[i], "group", g.Guide[i].Group); g.Guide[i].Name = Pick(guide[i], "name", g.Guide[i].Name);
                            g.Guide[i].Value = Pick(guide[i], "value", g.Guide[i].Value); g.Guide[i].Why = Pick(guide[i], "why", g.Guide[i].Why);
                        }
                }
            }
        }

        static Dictionary<string, object> Entry(Dictionary<string, object> root, string kind, string id)
        {
            object section, entry;
            if (root == null || !root.TryGetValue(kind, out section)) return null;
            var map = section as Dictionary<string, object>;
            return map != null && map.TryGetValue(id, out entry) ? entry as Dictionary<string, object> : null;
        }

        static bool Has(Dictionary<string, object> d, string k) { return d.ContainsKey(k) && d[k] != null; }
        static string Pick(Dictionary<string, object> d, string k, string fallback) { var v = Json.S(d, k); return string.IsNullOrEmpty(v) ? fallback : v; }
    }
}
