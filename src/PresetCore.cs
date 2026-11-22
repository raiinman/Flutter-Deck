// Picture presets for Flutter Deck. C# 5 only. No file or process side effects except reading preset files.
// A preset is a named list of picture settings. The launcher owns the files. A backend (DSI Prism, later)
// applies a preset to the picture. This file builds no filter and no effect.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace FlutterDeck.Presets
{
    public sealed class Preset
    {
        public string Id;          // file name without extension: a-z, 0-9, hyphen
        public string Name;        // shown to the player
        public string Description; // one short line
        public string Filter;      // see PresetFiles.Filters
        public string Variant;     // "" when the filter has no variants
        public int Sharpness;      // 0 to 100
    }

    public sealed class PresetLoadResult
    {
        public List<Preset> Presets = new List<Preset>();
        public List<string> Problems = new List<string>();
    }

    // The thing that really changes the picture. DSI Prism will provide one. Tests provide a fake.
    public interface IPictureBackend
    {
        string BackendName { get; }
        // True when this backend can show the preset. When false, reason says why in plain words.
        bool Supports(Preset preset, out string reason);
        // Apply while the game runs. True only when the picture really changed.
        bool Apply(Preset preset, out string reason);
    }

    // Used when no picture backend is present (for example DSI Prism is not installed).
    // The overlay keeps one code path: it asks, and gets a plain sentence back.
    public sealed class UnavailableBackend : IPictureBackend
    {
        readonly string why;

        public UnavailableBackend() : this("DSI Prism is not installed.") { }

        public UnavailableBackend(string why)
        {
            this.why = string.IsNullOrEmpty(why) ? "DSI Prism is not installed." : why;
        }

        public string BackendName { get { return "none"; } }
        public bool Supports(Preset preset, out string reason) { reason = why; return false; }
        public bool Apply(Preset preset, out string reason) { reason = why; return false; }
    }

    // Maps a preset to the case-sensitive strings DSI Prism stores in a profile
    // (Filter, FsrVariant, Sharpness). Filter names confirmed by Astra 2026-11-17 from GpuBridge.FilterId.
    public static class PrismNames
    {
        public static string Filter(Preset p)
        {
            if (p == null) return null;
            switch (p.Filter)
            {
                case "prism-learn-balanced": return "Prism Learn Balanced";
                case "prism-learn-performance": return "Prism Learn Performance";
                case "nearest": return "Nearest";
                case "bilinear": return "Bilinear";
                case "integer": return "Integer";
                case "bicubic": return "Bicubic";
                case "bicubic-cas": return "Bicubic CAS";
                case "fsr1": return "FSR 1";
                case "nis": return "NIS";
                case "cas": return "CAS";
                case "sgsr": return "SGSR";
                case "xbr": return "xBR";
                case "anime4k": return "Anime4K " + (p.Variant ?? "").ToUpperInvariant();
                default: return null;
            }
        }

        public static string FsrVariant(Preset p)
        {
            if (p == null || p.Filter != "fsr1") return null;
            return p.Variant == "tiled" ? "Tiled" : "Original";
        }

        // Prism's Sharpness is a float from 0 to 1.
        public static double Sharpness(Preset p)
        {
            return p == null ? 0 : p.Sharpness / 100.0;
        }
    }

    public static class PresetFiles
    {
        public const int MaxFileBytes = 4096;
        public const int MaxPresets = 64;
        public const int MaxNameLength = 32;
        public const int MaxDescriptionLength = 80;

        // Scaling filters that DSI Prism lists in its README. "anime4k" is the neural scaler.
        public static readonly string[] Filters = new string[] { "nearest", "bilinear", "integer", "bicubic", "bicubic-cas", "fsr1", "nis", "cas", "sgsr", "xbr", "anime4k", "prism-learn-balanced", "prism-learn-performance" };

        // Filters that read the sharpness value in DSI Prism (Prism 0.9.0 import accepts nonzero sharpness for these; Anime4K, xBR, plain bicubic, nearest, bilinear and integer do not).
        public static readonly string[] SharpnessFilters = new string[] { "bicubic-cas", "fsr1", "nis", "cas", "sgsr" };

        public static string[] VariantsOf(string filter)
        {
            if (filter == "fsr1") return new string[] { "original", "tiled" };
            if (filter == "anime4k") return new string[] { "s", "m", "l", "vl", "ul" };
            return new string[0];
        }

        public static bool IsValidId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 32) return false;
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-';
                if (!ok) return false;
            }
            if (id[0] == '-' || id[id.Length - 1] == '-') return false;
            // Windows cannot create a file with these names.
            string[] reserved = new string[] { "con", "prn", "aux", "nul", "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9", "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9" };
            return Array.IndexOf(reserved, id) < 0;
        }

        // Parse one preset from text. On any problem, add a line to problems and return null.
        public static Preset Parse(string id, string text, List<string> problems)
        {
            if (problems == null) problems = new List<string>();
            string where = "preset '" + id + "': ";
            if (!IsValidId(id)) { problems.Add(where + "file name must use a-z, 0-9 and hyphen, 1 to 32 characters"); return null; }
            if (text == null) { problems.Add(where + "no text"); return null; }
            text = text.TrimStart('\uFEFF');
            if (Encoding.UTF8.GetByteCount(text) > MaxFileBytes) { problems.Add(where + "file is larger than " + MaxFileBytes + " bytes"); return null; }

            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) { problems.Add(where + "line " + (i + 1) + " is not key=value"); return null; }
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string value = line.Substring(eq + 1).Trim();
                if (values.ContainsKey(key)) { problems.Add(where + "key '" + key + "' appears twice"); return null; }
                values[key] = value;
            }

            foreach (string key in values.Keys)
            {
                if (key != "name" && key != "description" && key != "filter" && key != "variant" && key != "sharpness")
                { problems.Add(where + "unknown key '" + key + "'"); return null; }
            }

            Preset p = new Preset();
            p.Id = id;

            string name;
            if (!values.TryGetValue("name", out name) || name.Length == 0) { problems.Add(where + "name is missing"); return null; }
            if (name.Length > MaxNameLength) { problems.Add(where + "name is longer than " + MaxNameLength + " characters"); return null; }
            if (HasHiddenCharacter(name)) { problems.Add(where + "name has a control, invisible or text-direction character"); return null; }
            if (name.IndexOf('\uFFFD') >= 0) { problems.Add(where + "name has a character that is not valid text"); return null; }
            p.Name = name;

            string description;
            values.TryGetValue("description", out description);
            description = description ?? "";
            if (description.Length > MaxDescriptionLength) { problems.Add(where + "description is longer than " + MaxDescriptionLength + " characters"); return null; }
            if (HasHiddenCharacter(description)) { problems.Add(where + "description has a control, invisible or text-direction character"); return null; }
            if (description.IndexOf('\uFFFD') >= 0) { problems.Add(where + "description has a character that is not valid text"); return null; }
            p.Description = description;

            string filter;
            if (!values.TryGetValue("filter", out filter)) { problems.Add(where + "filter is missing"); return null; }
            filter = filter.ToLowerInvariant();
            if (Array.IndexOf(Filters, filter) < 0) { problems.Add(where + "unknown filter '" + filter + "'"); return null; }
            p.Filter = filter;

            string variant;
            values.TryGetValue("variant", out variant);
            variant = (variant ?? "").ToLowerInvariant();
            string[] allowed = VariantsOf(filter);
            if (variant.Length == 0) variant = allowed.Length > 0 ? allowed[0] : "";
            else if (Array.IndexOf(allowed, variant) < 0)
            { problems.Add(where + "variant '" + variant + "' does not fit filter '" + filter + "'"); return null; }
            p.Variant = variant;

            string sharp;
            int sharpness = 0;
            if (values.TryGetValue("sharpness", out sharp))
            {
                if (!int.TryParse(sharp, NumberStyles.None, CultureInfo.InvariantCulture, out sharpness) || sharpness < 0 || sharpness > 100)
                { problems.Add(where + "sharpness must be a whole number from 0 to 100"); return null; }
            }
            if (sharpness > 0 && Array.IndexOf(SharpnessFilters, filter) < 0)
            { problems.Add(where + "filter '" + filter + "' does not use sharpness; leave it at 0"); return null; }
            p.Sharpness = sharpness;
            return p;
        }

        // Control (Cc), format such as zero-width and direction marks (Cf), and line or paragraph separators.
        static bool HasHiddenCharacter(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                UnicodeCategory c = char.GetUnicodeCategory(s[i]);
                if (c == UnicodeCategory.Control || c == UnicodeCategory.Format || c == UnicodeCategory.LineSeparator || c == UnicodeCategory.ParagraphSeparator) return true;
            }
            return false;
        }

        // Read every *.preset file in a folder. A bad file is skipped and reported; the rest still load.
        public static PresetLoadResult LoadFolder(string folder)
        {
            PresetLoadResult result = new PresetLoadResult();
            if (!Directory.Exists(folder)) { result.Problems.Add("preset folder not found"); return result; }
            string[] files = Directory.GetFiles(folder, "*.preset");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            UTF8Encoding strict = new UTF8Encoding(false, true);
            List<Preset> loaded = new List<Preset>();
            for (int i = 0; i < files.Length; i++)
            {
                if (loaded.Count >= MaxPresets) { result.Problems.Add("more than " + MaxPresets + " presets; the rest are ignored"); break; }
                string id = Path.GetFileNameWithoutExtension(files[i]);
                string text;
                try
                {
                    FileInfo info = new FileInfo(files[i]);
                    if (info.Length > MaxFileBytes) { result.Problems.Add("preset '" + id + "': file is larger than " + MaxFileBytes + " bytes"); continue; }
                    text = File.ReadAllText(files[i], strict);
                }
                catch (IOException) { result.Problems.Add("preset '" + id + "': file could not be read"); continue; }
                catch (UnauthorizedAccessException) { result.Problems.Add("preset '" + id + "': file could not be read"); continue; }
                catch (ArgumentException) { result.Problems.Add("preset '" + id + "': file is not valid UTF-8 text"); continue; }
                Preset p = Parse(id, text, result.Problems);
                if (p == null) continue;
                if (!seen.Add(p.Name.ToLowerInvariant())) { result.Problems.Add("preset '" + id + "': another preset already uses the name '" + p.Name + "'"); continue; }
                loaded.Add(p);
            }
            // A name must not equal another preset's id, or "apply by id or name" would be ambiguous.
            for (int i = 0; i < loaded.Count; i++)
            {
                bool clash = false;
                for (int j = 0; j < loaded.Count; j++)
                    if (j != i && loaded[j].Id == loaded[i].Name.ToLowerInvariant()) clash = true;
                if (clash) result.Problems.Add("preset '" + loaded[i].Id + "': its name matches the id of another preset");
                else result.Presets.Add(loaded[i]);
            }
            return result;
        }
    }

    // The three things the overlay asks for: list, apply by name, report the active one.
    public sealed class PresetService
    {
        readonly List<Preset> presets;
        readonly IPictureBackend backend;
        string activeId;
        bool applying;

        public PresetService(IList<Preset> presets, IPictureBackend backend)
        {
            if (presets == null) throw new ArgumentNullException("presets");
            if (backend == null) throw new ArgumentNullException("backend");
            this.presets = new List<Preset>(presets);
            this.backend = backend;
        }

        // Raised after a preset really applied. The argument is the preset id.
        public event Action<string> ActiveChanged;

        public IList<Preset> List() { return presets.AsReadOnly(); }

        // Id of the active preset, or "" when none was applied in this run.
        public string ActiveId { get { return activeId ?? ""; } }

        public Preset Find(string idOrName)
        {
            if (idOrName == null) return null;
            string key = idOrName.Trim().ToLowerInvariant();
            for (int i = 0; i < presets.Count; i++)
                if (presets[i].Id == key) return presets[i];
            for (int i = 0; i < presets.Count; i++)
                if (presets[i].Name.ToLowerInvariant() == key) return presets[i];
            return null;
        }

        // Apply by id or name. The active preset changes only when the backend reports success.
        public bool Apply(string idOrName, out string reason)
        {
            if (applying) { reason = "A picture look is already being applied."; return false; }
            Preset p = Find(idOrName);
            if (p == null) { reason = "No picture look is called '" + idOrName + "'."; return false; }
            applying = true;
            try
            {
                string why;
                if (!backend.Supports(p, out why)) { reason = Plain(why, "This picture look is not available here."); return false; }
                if (!backend.Apply(p, out why)) { reason = Plain(why, "The picture look could not be applied."); return false; }
                activeId = p.Id;
                reason = "";
                Action<string> handler = ActiveChanged;
                if (handler != null)
                {
                    // One failing listener must not stop the others or hide a change that really happened.
                    Delegate[] all = handler.GetInvocationList();
                    for (int i = 0; i < all.Length; i++)
                    {
                        try { ((Action<string>)all[i])(p.Id); }
                        catch (Exception) { }
                    }
                }
                return true;
            }
            finally { applying = false; }
        }

        static string Plain(string reason, string fallback)
        {
            return string.IsNullOrEmpty(reason) ? fallback : reason;
        }
    }
}
