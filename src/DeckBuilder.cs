using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

// "Build my game from my disc" for the three games (SPEC-builder.md; builder kit contracts BUILD-1 and BUILD-2). This is the same window class as Program.cs, in its own file.
// Rules of this file (check-builder.ps1 reads it):
//  - The built program holds code from the player's disc. This file never copies, uploads or shares it. It only names
//    it as the game program in the launcher's own settings, after the marker file proved it (BuildCore.BuiltProblem).
//  - A build starts only after the player pressed "Build the game" and said yes.
//  - Before every start of a game, Program.cs calls BuildGate(), and before it asks a game program what it is, it calls
//    BuildReadiness(): a program that a build made runs only while its marker proves it, only as the game that the
//    marker names, and never while a build runs.
//  - One build at a time. The game of a build is fixed when it starts; choosing another game later changes nothing of it.
//  - The session log gets fixed words only. No path and no line of the build's output goes into it.
public partial class Deck {
  GameBuild buildRun;                 // from the yes to "Build the game" until that build has ended
  int buildGame=1,buildPick=1;        // the game of the running or last build; the game the window was opened for
  BuildProgress buildProgress=new BuildProgress();
  readonly BuildTools[] buildTools=new BuildTools[BuildCore.Games.Length+1];readonly DateTime[] buildToolsRead=new DateTime[BuildCore.Games.Length+1];   // by game number
  readonly List<string> buildLines=new List<string>();readonly object buildSync=new object();
  string buildLast="",buildNote="";bool buildNoteGood,buildCloseHooked;int buildTicket;
  Window buildWindow;StackPanel buildPanel;TextBlock buildStepLine,buildLastLine;ProgressBar buildBar;DispatcherTimer buildTimer;
  const int BuildLinesKept=600;

  string BuildFolderOf(BuildGame g){return BuildCore.OutputFolder(DeckPaths.Root,g);}
  // The game the window shows: the game of the running build, else the game it was opened for.
  BuildGame BuildShown {get{return BuildCore.Game(buildRun!=null?buildGame:buildPick)??BuildCore.Games[0];}}
  BuildTools BuildToolsNow(BuildGame g,bool fresh=false){
    if(fresh||buildTools[g.Id]==null||(DateTime.UtcNow-buildToolsRead[g.Id]).TotalSeconds>3){buildTools[g.Id]=BuildCore.Find(Path.GetDirectoryName(ExePath),DeckPaths.Root,g);buildToolsRead[g.Id]=DateTime.UtcNow;}
    return buildTools[g.Id];
  }
  // The button is on the card of a game that can be built, and only when the kit of that game came with this Flutter Deck.
  bool BuildOffered(LibraryItem item){if(item==null)return false;var g=BuildCore.Game(item.BuiltIn);return g!=null&&BuildToolsNow(g).Offered;}

  // Called by PlayGame before every start. Throws a sentence for the player when the program must not start.
  void BuildGate(){
    if(buildRun!=null||BuildCore.AnyBuildRunning(DeckPaths.Root))throw new InvalidOperationException("A game is being built. Wait until the build has finished, or cancel it. Nothing was started.");
    string stop=BuildCore.StartProblem(runtime.Program,BuildCore.Game(game),DeckPaths.Root,null,GameBuild.RealPath,GameBuild.FileId);
    if(stop.Length>0)throw new InvalidOperationException(stop+" Nothing was started.");
  }
  // Called by UpdatePlay before GameRuntime.Readiness, which runs the game program for a moment to ask what it is.
  // A program that a build made is not run even for that until its marker proves it. null lets the check go on.
  RuntimeCheck BuildReadiness(){
    if(buildRun!=null||BuildCore.AnyBuildRunning(DeckPaths.Root))return new RuntimeCheck{Problem="A game is being built. Wait until the build has finished, or cancel it."};
    string stop=BuildCore.StartProblem(runtime.Program,BuildCore.Game(game),DeckPaths.Root,GameRuntime.CachedHash,GameBuild.RealPath,GameBuild.FileId);
    return stop.Length>0?new RuntimeCheck{Problem=stop}:null;
  }

  // The "Files" button of a game: shows the folder of the game that Flutter Deck built, in the file explorer of
  // Windows. A game that was not built here has no such folder: the button says so and opens nothing else.
  void OpenGameFiles(int gameId){
    var g=BuildCore.Game(gameId);string name=GameRuntime.Game(gameId).Name;
    string folder=BuildCore.FilesFolder(DeckPaths.Root,g);
    if(folder.Length==0){
      MessageBox.Show(this,"Flutter Deck has not built "+name+" on this PC, so there is no folder of game files to open."+(g!=null&&BuildToolsNow(g).Offered?"\n\nTo build it from your disc image, use \"Build my game...\" on the game's card in the Library.":"")+"\n\nA game program that you chose yourself is not kept by Flutter Deck. Nothing was changed.","Flutter Deck - Game files",MessageBoxButton.OK,MessageBoxImage.Information);
      return;
    }
    string said=GameFilesOpen.Open(folder);
    if(said.Length>0)MessageBox.Show(this,said+" Nothing was changed.","Flutter Deck - Game files",MessageBoxButton.OK,MessageBoxImage.Information);
    else sessionLog.Add("game-files-opened");
  }

  void BuildSay(string text,bool good){buildNote=text;buildNoteGood=good;RenderBuild();}
  void BuildStart(){
    if(buildRun!=null)return;
    var g=BuildCore.Game(buildPick);if(g==null)return;   /* the game of this build, fixed from here on */
    var tools=BuildToolsNow(g,true);
    if(tools.Problem.Length>0||!tools.Offered){RenderBuild();return;}
    if(GameRunning){BuildSay("Close the game first. A game cannot be built while a game runs.",false);return;}
    if(BuildCore.AnyBuildRunning(DeckPaths.Root)){BuildSay("Another build is running. Wait until it has finished.",false);return;}
    string cue=localSetup.Game[g.Id-1]??"";
    if(cue.Length==0||!File.Exists(cue)){BuildSay("Choose your disc image of "+g.Name+" first, on the setup and readiness screen. Then build the game.",false);return;}
    string cueProblem=GameRuntime.CheckCue(cue);
    if(cueProblem.Length>0){BuildSay(cueProblem,false);return;}
    string sync=BuildCore.SyncProblem(BuildFolderOf(g),BuildCore.SyncRoots(),Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    if(sync.Length>0){BuildSay(sync,false);return;}
    try{var drive=new DriveInfo(Path.GetPathRoot(Path.GetFullPath(BuildFolderOf(g))));if(drive.AvailableFreeSpace<g.SpaceNeeded){BuildSay("There is not enough free space for the build. It needs about "+(g.SpaceNeeded/(1024*1024*1024.0)).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" GB on drive "+drive.Name+".",false);return;}}catch(Exception){}
    if(MessageBox.Show(buildWindow??(Window)this,"Build "+g.Name+" from your disc image?\n\n"+Path.GetFileName(cue)+"\n\nThis takes "+g.Time+" and "+g.Space+" on this PC. Nothing is downloaded and nothing is sent.\n\nThe built game holds code from your disc. It is for you only and stays on this PC.","Flutter Deck - Build my game",MessageBoxButton.OKCancel,MessageBoxImage.Question)!=MessageBoxResult.OK)return;
    if(buildRun!=null||GameRunning||BuildCore.AnyBuildRunning(DeckPaths.Root))return;   /* asked again: the dialog may have been open for a long time */
    var progress=new BuildProgress();int ticket=++buildTicket;buildGame=g.Id;
    lock(buildSync){buildLines.Clear();buildLast="";buildProgress=progress;}
    buildNote="";
    try{
      buildRun=GameBuild.Start(tools,cue,DeckPaths.Root,g,
        delegate(string line){lock(buildSync){if(ticket!=buildTicket)return;bool ours=BuildCore.Apply(progress,line);if(!ours&&line.Trim().Length>0)buildLast=line.Trim();buildLines.Add(line);if(buildLines.Count>BuildLinesKept)buildLines.RemoveAt(0);}},
        delegate(int code){try{Dispatcher.BeginInvoke(new Action(delegate{BuildEnded(ticket,code,g);}));}catch(Exception){}});
    }catch(InvalidOperationException e){buildRun=null;BuildSay(e.Message,false);return;}
    sessionLog.Add("game-build-started");
    if(!buildCloseHooked){buildCloseHooked=true;Closed+=delegate{EndBuild();};}   // closing the launcher ends a running build
    RenderBuild();Render();
  }
  void BuildEnded(int ticket,int code,BuildGame g){
    if(ticket!=buildTicket||buildRun==null)return;
    var run=buildRun;buildRun=null;buildPick=g.Id;   /* the window shows the game whose result it reports */
    BuildProgress p;lock(buildSync){p=buildProgress;}
    if(run.Cancelled&&code!=0){sessionLog.Add("game-build-cancelled");BuildSay("The build was cancelled. A game that was built before, if there is one, was not changed."+(p.StopReason.Length>0?"\n\nThe build said: "+p.StopReason:""),false);}   /* a build that had already finished says its own result; one that stopped by itself at that moment also shows its reason */
    else if(code==0){
      string exe,hash;string problem=BuildCore.BuiltProblem(BuildFolderOf(g),g,out exe,out hash);
      if(problem.Length==0&&(g.MarkerNamesGame?p.Game!=g.Code:p.Game.Length>0&&p.Game!=g.Code))problem="The builder kit did not say that it built "+g.Name+".";   /* the first kit prints no game line */
      if(problem.Length>0){sessionLog.Add("game-build-stopped");BuildSay("The build ended, but its result could not be proven. "+problem,false);}
      else{
        sessionLog.Add("game-build-ready");
        string used=BuildUse(g,exe,hash);
        BuildSay(used.Length>0?"The game is built, but Flutter Deck could not take it as the game program. "+used:"The game is ready. Flutter Deck now starts this program for "+g.Name+"."+(p.TrackDiffers?" Your disc image is not byte for byte the reference copy. The game's code is correct. If you later see wrong pictures or hear wrong sound, the disc image is damaged.":""),used.Length==0);
      }
    }else{sessionLog.Add("game-build-stopped");BuildSay(BuildCore.ExitText(code,g)+(p.StopReason.Length>0?"\n\nThe build said: "+p.StopReason:""),false);}
    Render();
  }
  // Names the built program as the game program of the game it was built as. "" when done, else a sentence.
  string BuildUse(BuildGame g,string exe,string hash){
    if(runtimeLoadFailed)return "The saved settings need attention first. Open Help. Nothing was changed.";   /* never write over a settings file that could not be read */
    if(RunningGameIs(g.Id))return "Close "+g.Name+" first.";
    string proven=BuildCore.StartProblem(exe,g,DeckPaths.Root,null,GameBuild.RealPath,GameBuild.FileId);   /* the marker of this game must prove it at this moment too */
    if(proven.Length>0)return proven;
    string problem=GameRuntime.CheckProgram(exe,GameRuntime.Game(g.Id).ProgramHint);
    if(problem.Length>0)return problem;
    var slot=runtimes[g.Id-1];
    string before=slot.Program,beforeHash=slot.AcceptedHash;
    try{slot.Program=exe;slot.AcceptedHash=hash;GameRuntime.ForgetHashes();GameRuntime.Save(GameRuntime.SettingsPath,slot,g.Id);playNote="";playFailed=false;sessionLog.Add("game-program-set");return "";}
    catch(Exception){slot.Program=before;slot.AcceptedHash=beforeHash;return "The setting could not be saved.";}
  }
  bool RunningGameIs(int id){return GameRunning&&sessionGame==id;}
  void BuildCancel(){
    var run=buildRun;if(run==null)return;
    buildNote="The build is being ended.";buildNoteGood=false;RenderBuild();
    new Thread(delegate(){run.Cancel();}){IsBackground=true}.Start();
  }
  // The launcher closes: a running build is ended with every process under it.
  void EndBuild(){var run=buildRun;if(run==null)return;buildTicket++;buildRun=null;run.Cancel();}

  // ---- the window ----
  void ShowBuildWindow(int gameId){
    if(BuildCore.Game(gameId)==null)return;
    if(buildPick!=gameId){buildPick=gameId;buildNote="";lock(buildSync){if(buildRun==null&&buildGame!=gameId)buildLines.Clear();}}
    if(buildWindow!=null){RenderBuild();buildWindow.Activate();return;}
    buildPanel=new StackPanel{Margin=new Thickness(20,16,20,18)};
    buildWindow=new Window{Owner=this,Title="Flutter Deck - Build my game",Width=680,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Ink("#0E2233"),ShowInTaskbar=false,
      Content=new ScrollViewer{Content=buildPanel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxHeight=720}};
    Id(buildWindow,"build-window");
    buildWindow.Closed+=delegate{buildWindow=null;buildPanel=null;buildStepLine=null;buildLastLine=null;buildBar=null;if(buildTimer!=null){buildTimer.Stop();buildTimer=null;}};   // a running build goes on
    RenderBuild();buildWindow.Show();
  }
  TextBlock BuildText(string text,double size=14,string color="#F5EAD0"){var t=Label(text,size,color);t.Margin=new Thickness(0,0,0,8);return t;}
  void BuildTick(){
    if(buildStepLine==null||buildBar==null||buildLastLine==null)return;
    string step,last;double part;
    lock(buildSync){step=BuildCore.StepText(buildProgress);part=BuildCore.Fraction(buildProgress);last=buildLast;}
    buildStepLine.Text=step;buildBar.Value=part;buildLastLine.Text=last.Length>160?last.Substring(0,160):last;
  }
  void RenderBuild(){
    if(buildPanel==null)return;
    var p=buildPanel;p.Children.Clear();buildStepLine=null;buildLastLine=null;buildBar=null;
    if(buildTimer!=null){buildTimer.Stop();buildTimer=null;}
    var g=BuildShown;
    var title=BuildText("Build "+g.Name+" from your disc",20);title.FontWeight=FontWeights.Bold;Id(title,"build-title");p.Children.Add(title);
    var tools=BuildToolsNow(g);
    if(!tools.Offered||tools.Problem.Length>0){
      var broken=BuildText(tools.Offered?tools.Problem:"The build tools did not come with this Flutter Deck.",14,"#FFD98A");Id(broken,"build-tools-problem");p.Children.Add(broken);
      return;
    }
    if(buildRun!=null){
      buildStepLine=BuildText("",15,"#8BEFFF");Id(buildStepLine,"build-step");p.Children.Add(buildStepLine);
      buildBar=new ProgressBar{Minimum=0,Maximum=1,Height=16,Margin=new Thickness(0,0,0,8),Foreground=Ink("#8BEFFF"),Background=Ink("#0B1C2A"),BorderBrush=Ink("#5985A0")};
      Id(buildBar,"build-progress");System.Windows.Automation.AutomationProperties.SetName(buildBar,"Build progress");p.Children.Add(buildBar);
      buildLastLine=BuildText("",12,"#AEC8DB");buildLastLine.TextTrimming=TextTrimming.CharacterEllipsis;buildLastLine.TextWrapping=TextWrapping.NoWrap;Id(buildLastLine,"build-last");p.Children.Add(buildLastLine);
      p.Children.Add(BuildText("This takes "+g.Time+". You can close this window: the build goes on. No game can be started until the build has finished. If you close Flutter Deck, the build is ended.",13,"#AEC8DB"));
      if(!buildRun.Tied){var loose=BuildText("Windows did not let Flutter Deck tie this build to itself. If Flutter Deck is ended by force, the build goes on by itself until it is done.",13,"#FFD98A");Id(loose,"build-not-tied");p.Children.Add(loose);}
      var busy=new WrapPanel{Margin=new Thickness(-2,0,0,8)};
      busy.Children.Add(LibButton("Cancel the build",BuildCancel,"build-cancel","Cancel the build"));
      p.Children.Add(busy);
      BuildTick();
      buildTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(300)};buildTimer.Tick+=delegate{BuildTick();};buildTimer.Start();
    }else{
      p.Children.Add(BuildText("Flutter Deck makes the game program on this PC from your own disc image of "+g.Name+" (USA version). It takes "+g.Time+" and "+g.Space+". Nothing is downloaded and nothing is sent."));
      p.Children.Add(BuildText("The built game holds code from your disc. It is for you only and stays on this PC. Do not pass it on.",13,"#AEC8DB"));
      string cue=localSetup.Game[g.Id-1]??"";
      var disc=BuildText(cue.Length>0?"Disc image: "+Path.GetFileName(cue):"Disc image: not chosen yet. Choose it on the setup and readiness screen.",14,cue.Length>0?"#F5EAD0":"#FFD98A");Id(disc,"build-disc");p.Children.Add(disc);
      string exe,hash;string problem=BuildCore.BuiltProblem(BuildFolderOf(g),g,out exe,out hash);
      bool built=problem.Length==0,inUse=built&&String.Equals(runtimes[g.Id-1].Program,exe,StringComparison.OrdinalIgnoreCase);
      var marker=built?BuildCore.ReadMarker(BuildFolderOf(g)):null;
      bool older=marker!=null&&marker.KitVersion<tools.KitVersion;
      string state=built?"A built game is on this PC"+(inUse?", and Flutter Deck starts it for "+g.Name+".":". Flutter Deck does not use it now.")+(marker!=null&&marker.KitVersion<g.OldestKit?" It was built by an older builder. Build it again: the new builder is safer with disc images that are not your own.":older?" A newer builder came with this Flutter Deck: build again to get its changes.":""):problem;
      var stateLine=BuildText(state,14,built?"#9BE8A8":"#AEC8DB");Id(stateLine,"build-state");p.Children.Add(stateLine);
      if(built)p.Children.Add(BuildText("The built game is kept in "+BuildFolderOf(g)+". Removing Flutter Deck's settings does not remove it.",12,"#AEC8DB"));
      var row=new WrapPanel{Margin=new Thickness(-2,0,0,8)};
      row.Children.Add(LibButton(built?"Build again":"Build the game",BuildStart,"build-start",built?"Build the game again":"Build the game"));
      if(built&&!inUse)row.Children.Add(LibButton("Use the built game",delegate{string said=BuildUse(g,exe,hash);BuildSay(said.Length>0?said:"Flutter Deck now starts the built game for "+g.Name+".",said.Length==0);Render();},"build-use","Use the built game"));
      bool kept;lock(buildSync){kept=buildLines.Count>0&&buildGame==g.Id;}
      if(kept)row.Children.Add(LibButton("Copy the details",delegate{string all;lock(buildSync){all=String.Join("\r\n",buildLines.ToArray());}try{Clipboard.SetText(all);BuildSay("The details of the last build are copied.",true);}catch(Exception){BuildSay("The details could not be copied.",false);}},"build-copy","Copy the details of the last build"));
      p.Children.Add(row);
    }
    if(buildNote.Length>0){var note=BuildText(buildNote,14,buildNoteGood?"#9BE8A8":"#FFD98A");Id(note,"build-note");p.Children.Add(note);}
  }
}
