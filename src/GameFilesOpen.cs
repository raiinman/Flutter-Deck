using System;
using System.Diagnostics;
using System.IO;

// The "Files" button of a game (SPEC-builder.md, "The Files button"): shows a folder in the file explorer of Windows.
// Rules of this file (check-builder.ps1 reads it):
//  - It opens a folder that exists, by its full path, and nothing else. Its name has no extension (the build folders
//    are "mml1" and "mml2"), so if a file took the folder's place in the last moment, Windows could not run it.
//  - It copies nothing and sends nothing. The folder of a built game holds code from the player's disc.
public static class GameFilesOpen {
  // "" when the folder was handed to Windows to show; else a sentence for the player.
  public static string Open(string folder){
    try{
      if(String.IsNullOrEmpty(folder)||!Path.IsPathRooted(folder)||folder!=Path.GetFullPath(folder)||Path.GetExtension(folder.TrimEnd('\\')).Length>0||!Directory.Exists(folder))return "The folder was not found.";
      Process.Start(new ProcessStartInfo(folder.TrimEnd('\\')+"\\"){UseShellExecute=true,Verb="open"});
      return "";
    }catch(Exception){return "The folder could not be opened.";}
  }
}
