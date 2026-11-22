// Pure tests for PresetCore.cs. Reads only the repository's presets folder. No window, no game, no Prism.
using System;
using System.Collections.Generic;
using System.IO;
using FlutterDeck.Presets;

static class PresetPureTest
{
    static int failures;
    static int count;

    static void Check(bool ok, string what)
    {
        count++;
        if (!ok) { failures++; Console.WriteLine("FAIL: " + what); }
    }

    sealed class FakeBackend : IPictureBackend
    {
        public bool CanShow = true;
        public bool CanApply = true;
        public bool NullReason;
        public int Applied;
        public string BackendName { get { return "fake"; } }
        public bool Supports(Preset p, out string reason) { reason = CanShow ? "" : "not supported here"; return CanShow; }
        public bool Apply(Preset p, out string reason) { reason = CanApply ? "" : (NullReason ? null : "backend refused"); if (CanApply) Applied++; return CanApply; }
    }

    static Preset P(string id, string text)
    {
        List<string> problems = new List<string>();
        return PresetFiles.Parse(id, text, problems);
    }

    static bool Rejects(string id, string text)
    {
        List<string> problems = new List<string>();
        Preset p = PresetFiles.Parse(id, text, problems);
        return p == null && problems.Count == 1;
    }

    static int Main(string[] args)
    {
        // Parsing
        Preset ok = P("clean", "name=Clean\nfilter=bicubic-cas\nsharpness=20\n");
        Check(ok != null && ok.Name == "Clean" && ok.Filter == "bicubic-cas" && ok.Sharpness == 20 && ok.Variant == "", "valid preset parses");
        Check(Rejects("x", "name=X\nfilter=xbr\nsharpness=30"), "sharpness on a filter that ignores it is rejected");
        Check(Rejects("x", "name=X\nfilter=bicubic\nsharpness=1"), "plain bicubic ignores sharpness");
        Check(P("a", "# note\r\n\r\nname = A \r\nfilter = NIS\r\n") != null, "comments, blank lines, CRLF, spaces and case are accepted");
        Preset fsr = P("f", "name=F\nfilter=fsr1\n");
        Check(fsr != null && fsr.Variant == "original" && fsr.Sharpness == 0, "variant and sharpness default");
        Check(Rejects("x", "filter=cas"), "missing name rejected");
        Check(Rejects("x", "name=X"), "missing filter rejected");
        Check(Rejects("x", "name=X\nfilter=lanczos"), "unknown filter rejected");
        Check(Rejects("x", "name=X\nfilter=cas\nsharpness=101"), "sharpness above 100 rejected");
        Check(Rejects("x", "name=X\nfilter=cas\nsharpness=-1"), "negative sharpness rejected");
        Check(Rejects("x", "name=X\nfilter=cas\nsharpness=5.5"), "fractional sharpness rejected");
        Check(Rejects("x", "name=X\nfilter=cas\nvariant=m"), "variant on a filter without variants rejected");
        Check(Rejects("x", "name=X\nfilter=anime4k\nvariant=huge"), "unknown variant rejected");
        Check(Rejects("x", "name=X\nfilter=cas\nglow=3"), "unknown key rejected");
        Check(Rejects("x", "name=X\nname=Y\nfilter=cas"), "duplicate key rejected");
        Check(Rejects("x", "name=X\nfilter"), "line without equals rejected");
        Check(Rejects("x", "name=" + new string('n', 33) + "\nfilter=cas"), "long name rejected");
        Check(Rejects("x", "name=X\u0007\nfilter=cas"), "control character in name rejected");
        Check(Rejects("x", new string('#', 5000)), "oversize text rejected");
        Check(Rejects("..\\evil", "name=X\nfilter=cas"), "path characters in id rejected");
        Check(Rejects("Upper", "name=X\nfilter=cas"), "upper-case id rejected");
        Check(Rejects("-x", "name=X\nfilter=cas"), "leading hyphen in id rejected");
        Check(Rejects("x", null), "null text rejected");

        // Service
        FakeBackend fake = new FakeBackend();
        List<Preset> list = new List<Preset>();
        list.Add(P("one", "name=One\nfilter=integer"));
        list.Add(P("two", "name=Two Words\nfilter=xbr"));
        PresetService svc = new PresetService(list, fake);
        Check(svc.List().Count == 2, "service lists presets");
        Check(svc.ActiveId == "", "no active preset at start");
        string reason;
        int raised = 0;
        string raisedId = null;
        svc.ActiveChanged += delegate(string id) { raised++; raisedId = id; };
        Check(svc.Apply("TWO WORDS", out reason) && svc.ActiveId == "two" && reason == "", "apply by name, any case");
        Check(raised == 1 && raisedId == "two", "event raised once with the id");
        Check(svc.Apply("one", out reason) && svc.ActiveId == "one", "apply by id");
        Check(!svc.Apply("nope", out reason) && svc.ActiveId == "one" && reason.Length > 0, "unknown name leaves active preset alone");
        fake.CanShow = false;
        Check(!svc.Apply("two", out reason) && svc.ActiveId == "one" && reason == "not supported here", "unsupported preset leaves active alone and gives the reason");
        fake.CanShow = true; fake.CanApply = false;
        int before = raised;
        Check(!svc.Apply("two", out reason) && svc.ActiveId == "one" && raised == before && reason == "backend refused", "backend failure leaves active alone and raises no event");
        Check(svc.Find(null) == null && svc.Find("  ONE ") != null, "find handles null and spaces");
        Check(P("one", "name=One\nfilter=integer") != null && list.Count == 2, "caller list unchanged");
        bool threw = false;
        try { new PresetService(null, fake); } catch (ArgumentNullException) { threw = true; }
        Check(threw, "null list refused");

        // Defects found by the independent check
        Check(Rejects("x", "name=​\nfilter=cas"), "invisible name rejected");
        Check(Rejects("x", "name=‮evil\nfilter=cas"), "text-direction override in name rejected");
        Check(Rejects("x", "name=X\ndescription=a\u0000b\nfilter=cas"), "control character in description rejected");
        Check(Rejects("x", "name=" + new string('d', 5) + "\ndescription=" + new string('d', 81) + "\nfilter=cas"), "long description rejected");
        Check(P("x", "﻿name=X\nfilter=cas") != null, "byte order mark at the start is accepted");
        Check(!PresetFiles.IsValidId("con") && !PresetFiles.IsValidId("nul") && !PresetFiles.IsValidId("com1") && PresetFiles.IsValidId("console"), "reserved Windows names refused as ids");
        Check(PresetFiles.Parse("x", "name=X", null) == null, "null problems list does not crash");

        List<Preset> clash = new List<Preset>();
        clash.Add(P("a", "name=B\nfilter=cas"));
        clash.Add(P("b", "name=Other\nfilter=cas"));
        PresetService svc2 = new PresetService(clash, new FakeBackend());
        Check(svc2.Apply("b", out reason) && svc2.ActiveId == "b", "an id wins over a name that equals it");

        FakeBackend nullReason = new FakeBackend();
        nullReason.CanApply = false;
        nullReason.NullReason = true;
        PresetService svc3 = new PresetService(list, nullReason);
        Check(!svc3.Apply("one", out reason) && !string.IsNullOrEmpty(reason), "a backend that gives no reason still yields a sentence");

        FakeBackend fake4 = new FakeBackend();
        PresetService svc4 = new PresetService(list, fake4);
        int secondHeard = 0;
        svc4.ActiveChanged += delegate(string id) { throw new InvalidOperationException("listener broke"); };
        svc4.ActiveChanged += delegate(string id) { secondHeard++; };
        Check(svc4.Apply("one", out reason) && svc4.ActiveId == "one" && secondHeard == 1, "a throwing listener does not hide success or stop later listeners");

        PresetService svc5 = new PresetService(list, new FakeBackend());
        string innerReason = null;
        bool innerResult = true;
        svc5.ActiveChanged += delegate(string id) { innerResult = svc5.Apply("two", out innerReason); };
        Check(svc5.Apply("one", out reason) && !innerResult && svc5.ActiveId == "one" && innerReason.Length > 0, "apply from inside the event is refused");
        string again;
        Check(svc5.Apply("two", out again), "apply works again after the event ends");

        // Folder loading with temporary files
        string tmp = Path.Combine(Path.GetTempPath(), "fd-preset-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            File.WriteAllText(Path.Combine(tmp, "a.preset"), "name=B\nfilter=cas\n");
            File.WriteAllText(Path.Combine(tmp, "b.preset"), "name=Other\nfilter=cas\n");
            File.WriteAllText(Path.Combine(tmp, "c.preset"), "name=Fine\nfilter=nis\n");
            PresetLoadResult r1 = PresetFiles.LoadFolder(tmp);
            Check(r1.Presets.Count == 2 && r1.Problems.Count == 1 && r1.Presets[0].Id == "b" && r1.Presets[1].Id == "c", "name equal to another id is refused at load, others load");

            File.WriteAllBytes(Path.Combine(tmp, "d.preset"), new byte[] { 0x6E, 0x61, 0x6D, 0x65, 0x3D, 0xFF, 0xFE, 0x0A });
            File.WriteAllText(Path.Combine(tmp, "e.preset"), "name=Fine\nfilter=nis\n");
            File.WriteAllText(Path.Combine(tmp, "f.preset"), new string('#', 5000));
            File.WriteAllText(Path.Combine(tmp, "g.preset"), "name=G\nfilter=cas\nsharpness=200\n");
            PresetLoadResult r2 = PresetFiles.LoadFolder(tmp);
            Check(r2.Presets.Count == 2 && r2.Problems.Count == 5, "invalid UTF-8, repeated name, oversize file, bad value and the id clash are each reported; good files still load, got " + r2.Presets.Count + " presets and " + r2.Problems.Count + " problems");

            using (FileStream locked = new FileStream(Path.Combine(tmp, "c.preset"), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                PresetLoadResult r3 = PresetFiles.LoadFolder(tmp);
                bool reportedLock = false;
                for (int i = 0; i < r3.Problems.Count; i++) if (r3.Problems[i].IndexOf("could not be read") >= 0) reportedLock = true;
                Check(reportedLock && r3.Presets.Count >= 1, "a locked file is reported and the rest still load");
            }
        }
        finally { try { Directory.Delete(tmp, true); } catch (IOException) { } }

        string tmp2 = Path.Combine(Path.GetTempPath(), "fd-preset-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp2);
        try
        {
            for (int i = 0; i < 70; i++)
                File.WriteAllText(Path.Combine(tmp2, "p" + i.ToString("D2") + ".preset"), "name=Look " + i + "\nfilter=bilinear\n");
            PresetLoadResult r4 = PresetFiles.LoadFolder(tmp2);
            Check(r4.Presets.Count == PresetFiles.MaxPresets && r4.Problems.Count == 1, "only 64 presets load and the cap is reported");
        }
        finally { try { Directory.Delete(tmp2, true); } catch (IOException) { } }

        Check(P("f", "name=F\nfilter=fsr1\nsharpness=30") != null, "fsr1 accepts sharpness (Prism 0.9.0)");

        // Prism profile names
        Preset pn = new Preset(); pn.Filter = "anime4k"; pn.Variant = "vl";
        Check(PrismNames.Filter(pn) == "Anime4K VL", "anime4k vl maps to 'Anime4K VL'");
        pn.Filter = "bicubic-cas"; pn.Variant = ""; pn.Sharpness = 20;
        Check(PrismNames.Filter(pn) == "Bicubic CAS" && PrismNames.Sharpness(pn) == 0.2, "bicubic-cas maps to 'Bicubic CAS', sharpness 0.2");
        pn.Filter = "fsr1"; pn.Variant = "tiled";
        Check(PrismNames.Filter(pn) == "FSR 1" && PrismNames.FsrVariant(pn) == "Tiled", "fsr1 tiled maps");
        pn.Filter = "xbr"; Check(PrismNames.Filter(pn) == "xBR" && PrismNames.FsrVariant(pn) == null, "xbr maps; no FSR variant");
        foreach (string f in PresetFiles.Filters)
        {
            Preset q = new Preset(); q.Filter = f; q.Variant = PresetFiles.VariantsOf(f).Length > 0 ? PresetFiles.VariantsOf(f)[0] : "";
            Check(PrismNames.Filter(q) != null, "every known filter maps to a Prism name: " + f);
        }

        // No backend installed
        PresetService none1 = new PresetService(list, new UnavailableBackend());
        Check(!none1.Apply("one", out reason) && reason == "DSI Prism is not installed." && none1.ActiveId == "" && none1.List().Count == 2, "no backend: list works, apply gives the sentence, nothing becomes active");
        PresetService none2 = new PresetService(list, new UnavailableBackend(null));
        Check(!none2.Apply("one", out reason) && reason == "DSI Prism is not installed.", "no backend with a null reason falls back to the default sentence");
        PresetService none3 = new PresetService(list, new UnavailableBackend("Prism is closed."));
        Check(!none3.Apply("one", out reason) && reason == "Prism is closed.", "no backend with its own sentence");

        // Shipped presets
        string folder = args.Length > 0 ? args[0] : "presets";
        PresetLoadResult shipped = PresetFiles.LoadFolder(folder);
        Check(shipped.Problems.Count == 0, "shipped presets load with no problems: " + string.Join("; ", shipped.Problems.ToArray()));
        Check(shipped.Presets.Count == 8, "eight shipped presets, found " + shipped.Presets.Count);
        PresetLoadResult none = PresetFiles.LoadFolder(Path.Combine(folder, "does-not-exist"));
        Check(none.Presets.Count == 0 && none.Problems.Count == 1, "missing folder reported, no crash");

        if (failures > 0) { Console.WriteLine("FAILED " + failures + " of " + count); return 1; }
        Console.WriteLine("PASS: " + count + " preset checks");
        return 0;
    }
}
