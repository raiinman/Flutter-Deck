using System;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

// What Windows shows for FlutterDeck.exe (Properties > Details, Task Manager). The version comes from
// Updater.Version so the file, the Updates screen and the release manifest cannot disagree.
[assembly: AssemblyTitle("Flutter Deck")]
[assembly: AssemblyProduct("Flutter Deck")]
[assembly: AssemblyCompany("Dead Signal Interactive")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Dead Signal Interactive. MIT licence.")]
[assembly: AssemblyDescription("Unofficial fan-made launcher for Mega Man Legends. Not affiliated with Capcom.")]
// Per-monitor screen scale (FlutterDeck.manifest) needs the program to say it targets .NET Framework 4.6.2 or later, or WPF keeps its old scale handling.
[assembly: System.Runtime.Versioning.TargetFrameworkAttribute(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]
[assembly: AssemblyVersion(Updater.Version+".0")]
[assembly: AssemblyFileVersion(Updater.Version+".0")]
[assembly: AssemblyInformationalVersion(Updater.Version)]

public static class InstallRecord {
  // After a self-update the installed-apps list would still show the old version. If this copy is the
  // installed one (the entry's folder is this folder), correct the version shown. Touches nothing else,
  // creates nothing, and does nothing for a copy run from an ordinary folder.
  public static void RefreshVersion(string exePath){
    try{
      using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\FlutterDeck",true)){
        if(key==null)return;
        string location=key.GetValue("InstallLocation") as string,folder=Path.GetDirectoryName(exePath);
        if(location==null||!String.Equals(location.TrimEnd('\\'),folder.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))return;
        if((key.GetValue("DisplayVersion") as string)!=Updater.Version)key.SetValue("DisplayVersion",Updater.Version);
      }
    }catch(Exception){}
  }
}
