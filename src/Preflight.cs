using System;
using System.IO;
public sealed class PreflightResult { public string Title,State,Detail,Action; }
public static class Preflight {
 public const string LauncherRevision="Home + local preflight / revision 1";
 public static PreflightResult Check(string path,bool bios){var r=new PreflightResult{Title=bios?"BIOS reference":"Game reference",Action="Open Setup and select the intended file."};
  if(String.IsNullOrWhiteSpace(path)){r.State="Not selected";r.Detail="No saved reference for this target.";return r;}
  try{if(!File.Exists(path)){r.State="Missing or inaccessible";r.Detail="The saved file cannot be found or accessed. It may have moved; no search or repair was performed.";return r;}
   string problem=bios?LocalSetup.CheckBios(path):LocalSetup.CheckGame(path);if(problem.Length>0){r.State="Needs attention";r.Detail=problem;return r;}
   r.State="Metadata check passed";r.Detail=bios?"512 KiB candidate shape only. BIOS identity, region and compatibility are not established.":"Supported nonempty reference; CUE track metadata checked when applicable. Game title, region, revision and compatibility are not established.";r.Action="No reference repair indicated. A verified native runtime is still required to play.";return r;
  }catch{r.State="Could not check";r.Detail="File metadata could not be read. Check local access and reopen Setup; no file was changed.";return r;}
 }
}
