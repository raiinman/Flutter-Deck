using System;
using System.IO;
public static class DeckPathsPureTest {
 public static int Main(){
  string original=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FlutterDeck");
  if(DeckPaths.Root!=original||DeckPaths.IsPortable)throw new Exception("Default root changed");
  if(DeckPaths.RestartArguments!="")throw new Exception("The usual data folder needs no restart option");
  foreach(string bad in new[]{"relative","C:relative","\\\\server\\share",""}){
   bool refused=false;try{DeckPaths.Configure(new[]{"--data-dir",bad});}catch(ArgumentException){refused=true;}
   if(!refused||DeckPaths.Root!=original)throw new Exception("Invalid path accepted or changed default");
  }
  string isolated=Path.Combine(Path.GetTempPath(),"flutter-deck-path-test-"+Guid.NewGuid().ToString("N"));
  DeckPaths.Configure(new[]{"--data-dir",isolated});
  if(DeckPaths.Root!=isolated||!DeckPaths.IsPortable||Directory.Exists(isolated))throw new Exception("Configure mutated filesystem");
  string[] settings={LauncherPreferences.PathName,GameRuntime.SettingsPath,GameRuntime.DataFolder,LocalSetup.ConfigPath,OverlaySettings.PathName,UiPreferences.PathName,Updater.PrefsPath};
  foreach(string value in settings)if(!value.StartsWith(isolated+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Setting escaped root: "+value);
  // A launcher that restarts itself after an update keeps its data folder.
  if(DeckPaths.RestartArguments!="--data-dir \""+isolated+"\"")throw new Exception("Restart option lost the data folder: "+DeckPaths.RestartArguments);
  foreach(string odd in new[]{"D:\\my data\\flutter deck","D:\\","D:\\ends with slash\\"}){
   DeckPaths.Configure(new[]{"--data-dir",odd});string said=DeckPaths.RestartArguments,full=Path.GetFullPath(odd);
   int tail=0;while(tail<full.Length&&full[full.Length-1-tail]=='\\')tail++;
   if(said!="--data-dir \""+full+new string('\\',tail)+"\""||DeckPaths.Root!=full)throw new Exception("Restart option is wrong for "+odd+": "+said);
  }
  foreach(string bad in new[]{"--evil","--data-dir C:\\x","C:\\other.exe",null}){
   bool refused=false;try{Updater.Relaunch(Path.Combine(isolated,"FlutterDeck.exe"),bad);}catch(ArgumentException){refused=true;}catch(Exception){}
   if(!refused)throw new Exception("A restart option that is not the data folder option was not refused");
  }
  Console.WriteLine("PASS: default root, invalid paths, non-mutating configuration, seven portable settings paths, and the restart option that keeps the data folder");return 0;
 }
}
