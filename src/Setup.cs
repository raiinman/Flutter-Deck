using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

[assembly: AssemblyTitle("Flutter Deck Setup")]
[assembly: AssemblyProduct("Flutter Deck")]
[assembly: AssemblyCompany(Installer.Publisher)]
[assembly: AssemblyCopyright("Copyright (c) 2026 Dead Signal Interactive. MIT licence.")]
[assembly: AssemblyDescription("Installs Flutter Deck for the current Windows user.")]
[assembly: AssemblyVersion(Updater.Version+".0")]
[assembly: AssemblyFileVersion(Updater.Version+".0")]
[assembly: AssemblyInformationalVersion(Updater.Version)]

// Setup and Uninstall for Flutter Deck, one source.
//  - Setup carries the build tools (developer, 2026-11-20): the official Python and Zig zips and one zip per builder kit, plus
//    their notices, as resources (see build-setup.ps1). They are unpacked into the folder "builder" beside the launcher.
//  - The uninstaller is the same source compiled without those tools (resource "Uninstaller"), so the copy in the
//    install folder, and the copy each uninstall leaves in the temp folder, stay small.
//  - Run as downloaded: installs for the current user only (no administrator prompt), into a fixed folder.
//  - Run as "Uninstall Flutter Deck.exe" from the install folder: copies itself to the temp folder and
//    reruns from there, so it can remove the install folder it started in.
// There is no silent mode and no switch that redirects where it installs.
// File work is in InstallCore.cs; this file holds the window, the shortcuts and the registry entry.
public static class SetupShell {
  static object Shell(){return Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));}
  static object Call(object target,string method,params object[] args){return target.GetType().InvokeMember(method,BindingFlags.InvokeMethod,null,target,args);}
  static void Set(object target,string property,object value){target.GetType().InvokeMember(property,BindingFlags.SetProperty,null,target,new[]{value});}
  static object Get(object target,string property){return target.GetType().InvokeMember(property,BindingFlags.GetProperty,null,target,null);}
  public static string StartMenuShortcut {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),Installer.AppName+".lnk");}}
  public static string DesktopShortcut {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Installer.AppName+".lnk");}}
  // Returns false, changing nothing, when a shortcut of that name already points somewhere else (the user's own).
  public static bool CreateShortcut(string linkPath,string target){
    object shell=Shell(),link=Call(shell,"CreateShortcut",linkPath);
    if(File.Exists(linkPath)){string points=Get(link,"TargetPath") as string;if(!String.IsNullOrEmpty(points)&&!String.Equals(points,target,StringComparison.OrdinalIgnoreCase))return false;}
    Set(link,"TargetPath",target);Set(link,"WorkingDirectory",Path.GetDirectoryName(target));Set(link,"IconLocation",target+",0");Set(link,"Description","Flutter Deck launcher");Call(link,"Save");return true;
  }
  // Removes a shortcut only if it points at this install; a shortcut of the same name the user made for something else is left.
  public static void RemoveShortcut(string linkPath,string target){
    try{if(!File.Exists(linkPath))return;object link=Call(Shell(),"CreateShortcut",linkPath);string points=Get(link,"TargetPath") as string;if(String.Equals(points,target,StringComparison.OrdinalIgnoreCase))File.Delete(linkPath);}catch(Exception){}
  }
  public static void WriteUninstallEntry(string folder,string version,int sizeKb){
    using(var key=Registry.CurrentUser.CreateSubKey(Installer.UninstallKey)){
      string exe=Path.Combine(folder,Installer.ExeName),uninstaller=Path.Combine(folder,Installer.UninstallerName);
      key.SetValue("DisplayName",Installer.AppName);key.SetValue("DisplayVersion",version);key.SetValue("Publisher",Installer.Publisher);
      key.SetValue("DisplayIcon",exe+",0");key.SetValue("InstallLocation",folder);key.SetValue("UninstallString","\""+uninstaller+"\"");
      key.SetValue("NoModify",1,RegistryValueKind.DWord);key.SetValue("NoRepair",1,RegistryValueKind.DWord);key.SetValue("EstimatedSize",sizeKb,RegistryValueKind.DWord);
    }
  }
  // Removes the installed-apps entry only if it belongs to the folder being uninstalled.
  public static void RemoveUninstallEntry(string folder){
    try{using(var key=Registry.CurrentUser.OpenSubKey(Installer.UninstallKey)){if(key==null)return;string location=key.GetValue("InstallLocation") as string;if(!String.Equals(location,folder,StringComparison.OrdinalIgnoreCase))return;}Registry.CurrentUser.DeleteSubKey(Installer.UninstallKey,false);}catch(Exception){}
  }
  public static string InstalledVersion(string folder){
    try{string exe=Path.Combine(folder,Installer.ExeName);if(!File.Exists(exe))return "";var info=FileVersionInfo.GetVersionInfo(exe);return info.ProductVersion??"";}catch(Exception){return "";}
  }
}

public static class SetupPayload {
  // Resource name, folder inside "builder", and whether the zip's one top folder is left out (the Zig zip has one).
  static readonly string[,] Zips={{"ToolKit1","kit","n"},{"ToolKit2","kit2","n"},{"ToolKit3","kit3","n"},{"ToolPython","python","n"},{"ToolZig","zig","y"}};
  static readonly string[,] Files={{"ToolNotices","NOTICES.txt"},{"ToolZigNotices",@"zig\THIRD-PARTY-NOTICES.txt"},{"ToolPythonNotices",@"python\THIRD-PARTY-NOTICES.txt"}};
  static Func<Stream> Opener(string name){return delegate{return Assembly.GetExecutingAssembly().GetManifestResourceStream(name);};}
  // Null when this program holds no build tools (the uninstaller).
  public static BuilderPayload Builder(){
    var a=Assembly.GetExecutingAssembly();if(a.GetManifestResourceInfo("ToolZig")==null)return null;
    var p=new BuilderPayload();
    for(int i=0;i<Zips.GetLength(0);i++)p.Parts.Add(new BuilderPart{Folder=Zips[i,1],StripTopFolder=Zips[i,2]=="y",Open=Opener(Zips[i,0])});
    for(int i=0;i<Files.GetLength(0);i++)p.Files.Add(new BuilderFile{Path=Files[i,1],Open=Opener(Files[i,0])});
    return p;
  }
}

public class SetupWindow : Window {
  static SolidColorBrush Ink(string c){return (SolidColorBrush)new BrushConverter().ConvertFromString(c);}
  readonly StackPanel body=new StackPanel{Margin=new Thickness(26,22,26,22)};
  readonly bool uninstall;readonly string folder;bool busy;
  readonly BuilderPayload builder=SetupPayload.Builder();long builderBytes=-1;
  static string Mb(long bytes){return ((bytes+1048575)/1048576).ToString("N0")+" MB";}
  TextBlock Text(string value,double size,string color){return new TextBlock{Text=value,FontSize=size,Foreground=Ink(color),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10),FontFamily=new FontFamily("Segoe UI")};}
  Button Action(string label,bool primary,Action click){
    var b=new Button{Content=label,MinWidth=130,MinHeight=38,Margin=new Thickness(8,0,0,0),FontSize=16,Padding=new Thickness(14,4,14,4),Foreground=Ink("#FFF1D8"),Background=Ink(primary?"#1F6A86":"#163B54"),BorderBrush=Ink(primary?"#5ECDEB":"#5985A0"),BorderThickness=new Thickness(1),Cursor=System.Windows.Input.Cursors.Hand,IsDefault=primary};
    b.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate TargetType='Button' xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'><Border x:Name='B' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='5' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='B' Property='BorderBrush' Value='#F5EAD0'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='B' Property='BorderBrush' Value='#F5EAD0'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='B' Property='Opacity' Value='0.5'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
    b.Click+=delegate{click();};return b;
  }
  CheckBox Option(string label,bool on){return new CheckBox{Content=label,IsChecked=on,Foreground=Ink("#F5EAD0"),FontSize=16,Margin=new Thickness(0,4,0,8),VerticalContentAlignment=VerticalAlignment.Center};}
  StackPanel Buttons(params Button[] buttons){var row=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,14,0,0)};foreach(var b in buttons)row.Children.Add(b);return row;}
  void Show(params UIElement[] items){body.Children.Clear();foreach(var item in items)body.Children.Add(item);}

  public SetupWindow(bool uninstall,string folder){
    this.uninstall=uninstall;this.folder=folder;
    Title=uninstall?"Uninstall Flutter Deck":"Flutter Deck Setup";Width=560;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=Ink("#102638");Content=body;
    try{using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("Icon")){if(s!=null)Icon=BitmapFrame.Create(s,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);}}catch(Exception){}
    // While files are being written or removed the window cannot be closed: stopping half-way is what the rollback is for, not the close button.
    Closing+=delegate(object s,System.ComponentModel.CancelEventArgs e){if(busy)e.Cancel=true;};
    if(uninstall)UninstallPage();else InstallPage();
  }
  // A page with a title, a line and a progress bar, for the long parts.
  ProgressBar Working(string title,string line,TextBlock status){
    var bar=new ProgressBar{Height=18,Minimum=0,Maximum=1000,Margin=new Thickness(0,6,0,10),Foreground=Ink("#5ECDEB"),Background=Ink("#163B54")};
    Show(Text(title,24,"#F5EAD0"),Text(line,15,"#AEC8DB"),bar,status);return bar;
  }

  void InstallPage(){
    string installed=SetupShell.InstalledVersion(folder);bool existing=File.Exists(Path.Combine(folder,Installer.ExeName));
    var desktop=Option("Add a shortcut to the desktop",!existing||File.Exists(SetupShell.DesktopShortcut));var start=Option("Start Flutter Deck when Setup finishes",true);
    string state=!existing?"":installed.Length==0?"An earlier Flutter Deck is installed and will be replaced. Your settings are kept.":Installer.CompareVersions(installed,Updater.Version)>0?"A NEWER Flutter Deck ("+installed+") is installed. Continuing replaces it with this older one. Your settings are kept.":"Flutter Deck "+installed+" is installed and will be replaced. Your settings are kept.";
    var install=Action(existing?"Replace":"Install",true,delegate{DoInstall(desktop.IsChecked==true,start.IsChecked==true);});
    string tools="";
    if(builder!=null){try{builderBytes=Installer.BuilderBytes(builder);tools="Includes the build tools that turn your own game discs into games: the builder kits for the three games, Python and Zig (about "+Mb(builderBytes)+" once installed). Installing takes a minute or two.";}catch(Exception){builderBytes=-1;tools="The build tools in this Setup file could not be read. Download Setup again.";install.IsEnabled=false;}}
    Show(Text("Flutter Deck "+Updater.Version,26,"#F5EAD0"),
      Text("A free, open-source launcher for Mega Man Legends. Unofficial fan project, not affiliated with Capcom. No game or BIOS files are included.",15,"#AEC8DB"),
      Text("Installs for your Windows account only. No administrator permission is needed.",15,"#F5EAD0"),
      tools.Length>0?Text(tools,15,"#F5EAD0"):new TextBlock(),
      Text("Folder: "+folder,13,"#8BEFFF"),
      state.Length>0?Text(state,15,"#FFD98A"):new TextBlock(),
      desktop,start,Buttons(Action("Cancel",false,delegate{Close();}),install));
  }
  void DoInstall(bool desktop,bool start){
    try{
      if(!Installer.CanInstallInto(folder)){Problem("Setup will not install into this folder because it already holds other files:\n"+folder);return;}
      if(Installer.LauncherInUse(folder)){Problem("Flutter Deck is running. Close it, then choose Install again. Setup does not close it for you.");return;}
      var assembly=Assembly.GetExecutingAssembly();
      // The uninstaller placed in the install folder is the small build without the tools, when this Setup carries one.
      var status=Text("Getting ready...",15,"#F5EAD0");var bar=Working("Installing Flutter Deck","Please wait. Setup cannot be closed until it has finished.",status);
      busy=true;Exception error=null;
      Action<long,long> progress=delegate(long done,long total){Dispatcher.BeginInvoke(new Action(delegate{bar.Value=total<=0?0:1000.0*done/total;status.Text="Installing the build tools: "+Mb(done)+" of "+Mb(total);}));};
      var worker=new System.Threading.Thread(delegate(){
        string temp=null;
        try{
          string uninstallerSource=assembly.Location;
          using(var small=assembly.GetManifestResourceStream("Uninstaller")){if(small!=null){temp=Path.Combine(Path.GetTempPath(),"FlutterDeck-Uninstaller-"+Guid.NewGuid().ToString("N")+".exe");using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))small.CopyTo(f);uninstallerSource=temp;}}
          Installer.Install(folder,delegate(int i){return assembly.GetManifestResourceStream("P"+i);},uninstallerSource,builder,progress);
        }catch(Exception e){error=e;}
        finally{if(temp!=null){try{File.Delete(temp);}catch(Exception){}}}
        Dispatcher.BeginInvoke(new Action(delegate{busy=false;if(error!=null)InstallFailed(error);else Finish(desktop,start);}));
      });
      worker.IsBackground=false;worker.Start();
    }
    catch(Exception e){busy=false;InstallFailed(e);}
  }
  void InstallFailed(Exception e){
    InstallPage();
    if(e is NotEnoughSpaceException){var n=(NotEnoughSpaceException)e;Problem("There is not enough free space for Flutter Deck and its build tools. It needs about "+Mb(n.Needed)+" free on drive "+Path.GetPathRoot(folder)+" and "+Mb(n.Free)+" is free. Nothing was changed. Free some space and try again.");}
    else if(e is InvalidDataException||e is FileNotFoundException)Problem("This Setup file is damaged: its build tools could not be read. Nothing was changed. Download Setup again.");
    else if(e is UnauthorizedAccessException)Problem("Windows did not allow Setup to write to:\n"+folder+"\nSetup put back what was installed before.");
    else if(e is IOException)Problem("Setup could not write the files. Flutter Deck or a game build may be running, another program may be holding one of its files, or the disk may be full. Setup put back what was installed before. Close Flutter Deck and try again.");
    else if(e is InvalidOperationException)Problem("Setup will not install into this folder:\n"+folder);
    else Problem("Setup did not finish. It put back what was installed before.");
  }
  void Finish(bool desktop,bool start){
    try{
      string exe=Path.Combine(folder,Installer.ExeName);var notes=new List<string>();
      try{if(!SetupShell.CreateShortcut(SetupShell.StartMenuShortcut,exe))notes.Add("A different Start Menu shortcut named Flutter Deck already exists and was left as it is.");}catch(Exception){notes.Add("The Start Menu shortcut could not be created.");}
      if(desktop){try{if(!SetupShell.CreateShortcut(SetupShell.DesktopShortcut,exe))notes.Add("A different desktop shortcut named Flutter Deck already exists and was left as it is.");}catch(Exception){notes.Add("The desktop shortcut could not be created.");}}
      try{long bytes=0;foreach(string name in Installer.Payload)bytes+=new FileInfo(Path.Combine(folder,name)).Length;if(builderBytes>0)bytes+=builderBytes;SetupShell.WriteUninstallEntry(folder,Updater.Version,(int)(bytes/1024));}catch(Exception){notes.Add("Flutter Deck could not be added to Windows' installed-apps list. Use \"Uninstall Flutter Deck\" in its folder to remove it.");}
      if(start){try{Process.Start(new ProcessStartInfo(exe){UseShellExecute=false,WorkingDirectory=folder}).Dispose();}catch(Exception){notes.Add("Flutter Deck could not be started automatically. Open it from the Start Menu.");}}
      var done=new List<UIElement>{Text("Flutter Deck is installed",24,"#F5EAD0"),Text("Open it from the Start Menu. To remove it later, use Windows Settings > Apps > Installed apps.",15,"#AEC8DB")};
      foreach(string note in notes)done.Add(Text(note,15,"#FFD98A"));
      done.Add(Buttons(Action("Close",true,delegate{Close();})));Show(done.ToArray());
    }
    catch(Exception){Problem("Flutter Deck is installed, but Setup could not finish the shortcuts. Open it from its folder:\n"+folder);}
  }
  void Problem(string message){MessageBox.Show(this,message,Title,MessageBoxButton.OK,MessageBoxImage.Warning);}

  void UninstallPage(){
    var settings=Option("Also remove my Flutter Deck settings (window size, text size, game and BIOS locations, update choice)",false);
    Show(Text("Remove Flutter Deck?",24,"#F5EAD0"),
      Text("This removes the launcher, its build tools, its shortcuts and its entry in Windows' installed-apps list. Games you built and their saves stay in your Flutter Deck data folder.",15,"#AEC8DB"),
      Text("Your game, BIOS, save and music files are never touched.",15,"#F5EAD0"),
      Text("Folder: "+folder,13,"#8BEFFF"),
      settings,Buttons(Action("Cancel",true,delegate{Close();}),Action("Uninstall",false,delegate{DoUninstall(settings.IsChecked==true);})));
  }
  void DoUninstall(bool removeSettings){
    try{
      if(!Installer.IsInstallFolder(folder)){Problem("Flutter Deck was not found in:\n"+folder+"\nNothing was removed.");return;}
      if(Installer.LauncherInUse(folder)){Problem("Flutter Deck is running. Close it, then choose Uninstall again.");return;}
      var status=Text("Removing files...",15,"#F5EAD0");var bar=Working("Removing Flutter Deck","Please wait. This window cannot be closed until it has finished.",status);bar.IsIndeterminate=true;
      busy=true;List<string> result=null;Exception error=null;
      var worker=new System.Threading.Thread(delegate(){
        // The copy of this program in the install folder may need a moment to finish closing after handing over.
        try{for(int attempt=0;attempt<6;attempt++){result=Installer.Uninstall(folder);if(result.Count==0||!Installer.IsInstallFolder(folder))break;System.Threading.Thread.Sleep(300);}}catch(Exception e){error=e;}
        Dispatcher.BeginInvoke(new Action(delegate{busy=false;if(error!=null)UninstallFailed(error);else Removed(result,removeSettings);}));
      });
      worker.IsBackground=false;worker.Start();
    }
    catch(Exception e){busy=false;UninstallFailed(e);}
  }
  void UninstallFailed(Exception e){
    UninstallPage();
    if(e is InvalidOperationException)Problem("Uninstall will not remove this folder because it, or a folder inside it, is a link to another location:\n"+folder+"\nNothing was removed. Delete the folder yourself if you no longer need it.");
    else Problem("Uninstall did not finish. Close Flutter Deck and try again.");
  }
  void Removed(List<string> failed,bool removeSettings){
    try{
      string exe=Path.Combine(folder,Installer.ExeName);
      if(failed!=null&&failed.Count>0){
        // Incomplete: shortcuts, the installed-apps entry and settings are left so Uninstall can be run again.
        Show(Text("Flutter Deck was only partly removed",24,"#FFD98A"),
          Text("These files could not be removed, probably because another program (or a game build) is using them: "+String.Join(", ",(failed.Count>12?failed.GetRange(0,12):failed).ToArray())+(failed.Count>12?" and "+(failed.Count-12)+" more":""),15,"#F5EAD0"),
          Text("Close anything using them and run Uninstall again. Your shortcuts and settings were left as they are.",15,"#AEC8DB"),
          Buttons(Action("Close",true,delegate{Close();})));
        return;
      }
      SetupShell.RemoveShortcut(SetupShell.StartMenuShortcut,exe);SetupShell.RemoveShortcut(SetupShell.DesktopShortcut,exe);SetupShell.RemoveUninstallEntry(folder);
      bool settingsGone=false;if(removeSettings){try{Installer.RemoveSettings(Installer.SettingsFolder);}catch(Exception){}settingsGone=!Installer.SettingsRemain(Installer.SettingsFolder);}
      var done=new List<UIElement>{Text("Flutter Deck was removed",24,"#F5EAD0")};
      if(Directory.Exists(folder))done.Add(Text("The folder was kept because it still holds other files:\n"+folder,15,"#AEC8DB"));
      done.Add(Text(!removeSettings?"Your Flutter Deck settings were kept, so a later install picks up where you left off.":settingsGone?"Your Flutter Deck settings were removed.":"Some of your Flutter Deck settings could not be removed and are still in:\n"+Installer.SettingsFolder,15,"#AEC8DB"));
      done.Add(Buttons(Action("Close",true,delegate{Close();})));Show(done.ToArray());
    }
    catch(Exception){Problem("Uninstall did not finish. Close Flutter Deck and try again.");}
  }

  [STAThread]public static int Main(string[] args){
    try{
      string self=Assembly.GetExecutingAssembly().Location,here=Path.GetDirectoryName(self);
      // Started as the installed uninstaller: rerun from the temp folder so the install folder can be removed.
      if(String.Equals(Path.GetFileName(self),Installer.UninstallerName,StringComparison.OrdinalIgnoreCase)){
        string copy=Path.Combine(Path.GetTempPath(),"FlutterDeck-Uninstall-"+Guid.NewGuid().ToString("N")+".exe");File.Copy(self,copy,false);
        Process.Start(new ProcessStartInfo(copy,"/uninstall \""+here+"\""){UseShellExecute=false}).Dispose();return 0;
      }
      if(args.Length==2&&args[0]=="/uninstall"){
        if(!Installer.IsInstallFolder(args[1])){MessageBox.Show("Flutter Deck was not found in the folder to uninstall. Nothing was removed.","Uninstall Flutter Deck");return 1;}
        new Application().Run(new SetupWindow(true,Path.GetFullPath(args[1]).TrimEnd('\\')));return 0;
      }
      if(args.Length>0){MessageBox.Show("Flutter Deck Setup takes no options. Run it by itself to install.","Flutter Deck Setup");return 1;}
      new Application().Run(new SetupWindow(false,Installer.DefaultFolder));return 0;
    }
    catch(Exception){MessageBox.Show("Flutter Deck Setup could not start. Nothing was changed.","Flutter Deck Setup");return 1;}
  }
}
