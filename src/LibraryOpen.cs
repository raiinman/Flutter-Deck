using System;
using System.Diagnostics;
using System.IO;
// The only place a Library item is opened (SPEC-library.md). It is kept apart from Program.cs, which starts no program itself
// (check-catalog.ps1), in the same way as the game start (GameRuntime.cs) and the update install (Updater.cs).
// Only an added movie is opened: a full local path with a video extension, whose file exists. Windows picks the program.
// This is not a game start: it gives no arguments, and it never receives a program file.
public static class LibraryOpen {
  // "" when Windows accepted the file; otherwise a sentence for the user. Nothing is started when a sentence is returned.
  public static string Open(LibraryItem item){
    if(!LibraryCore.CanOpen(item)||!File.Exists(item.Path))return "The movie file was not found.";
    try{Process.Start(new ProcessStartInfo(item.Path){UseShellExecute=true});return "";}
    catch{return "Windows could not open this file. Check that a program is set for this kind of file.";}
  }
}
