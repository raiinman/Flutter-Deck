using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

// The in-game overlay: a small bar the launcher draws over the running game window. It opens when the mouse rests
// at the top of the game picture and from a global hotkey. It never takes the keyboard from the game, except while
// the player is choosing a key. OverlayCore.cs holds the decisions; SPEC-overlay.md says what is and is not verified.

static class OverlayNative {
  public const int GWL_EXSTYLE=-20,WS_EX_TOOLWINDOW=0x80,WS_EX_NOACTIVATE=0x08000000;
  public const uint SWP_NOSIZE=1,SWP_NOMOVE=2,SWP_NOACTIVATE=0x10;
  public static readonly IntPtr HWND_TOPMOST=new IntPtr(-1);
  public delegate bool EnumProc(IntPtr hwnd,IntPtr lparam);
  [StructLayout(LayoutKind.Sequential)] public struct RECT {public int Left,Top,Right,Bottom;}
  [StructLayout(LayoutKind.Sequential)] public struct POINT {public int X,Y;}
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc proc,IntPtr lparam);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd,StringBuilder text,int max);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd,StringBuilder text,int max);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
  [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hwnd,out RECT rect);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hwnd,ref POINT point);
  [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT point);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
  [DllImport("user32.dll",EntryPoint="GetWindowLongW")] public static extern int GetWindowLong(IntPtr hwnd,int index);
  [DllImport("user32.dll",EntryPoint="SetWindowLongW")] public static extern int SetWindowLong(IntPtr hwnd,int index,int value);
  [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hwnd,int id,uint modifiers,uint vk);
  [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd,int id);
}

// Where the game is and what the player is doing, behind an interface so tests can supply the cursor.
public interface IGameProbe {
  IntPtr FindGame();                                  // IntPtr.Zero when the game has no window
  bool ClientRect(IntPtr game,out PxRect rect);       // the game picture in screen pixels; false when hidden or minimised
  bool GameIsForeground(IntPtr game);
  bool Cursor(out int x,out int y);
  PxRect[] Monitors();
  void BringToFront(IntPtr hwnd);                     // make this window the foreground window (the keyboard goes with it)
}

// The real thing. The game window is found by the game's own process id first; its window class
// (RuntimeGpuPresent) or title start (rock_neo_rt live) only decide which of that process's windows. These are the
// game side's facts of 2026-11-16, not contract promises.
public sealed class Win32Probe : IGameProbe {
  public const string GameClass="RuntimeGpuPresent",TitlePrefix="rock_neo_rt live",TitlePrefix2="mml2_rt live",TitlePrefix3="tron_rt live"; // the second prefix is Mega Man Legends 2 (contract L2-1), the third Tron Bonne (contract T-2)
  readonly int pid;
  public Win32Probe(int processId){pid=processId;}
  public IntPtr FindGame(){
    IntPtr found=IntPtr.Zero;
    OverlayNative.EnumWindows(delegate(IntPtr h,IntPtr l){
      uint owner;OverlayNative.GetWindowThreadProcessId(h,out owner);
      if((int)owner!=pid||!OverlayNative.IsWindowVisible(h))return true;
      var cls=new StringBuilder(80);OverlayNative.GetClassName(h,cls,80);
      var title=new StringBuilder(120);OverlayNative.GetWindowText(h,title,120);
      if(cls.ToString()==GameClass||title.ToString().StartsWith(TitlePrefix,StringComparison.Ordinal)||title.ToString().StartsWith(TitlePrefix2,StringComparison.Ordinal)||title.ToString().StartsWith(TitlePrefix3,StringComparison.Ordinal)){found=h;return false;}
      return true;
    },IntPtr.Zero);
    return found;
  }
  public bool ClientRect(IntPtr game,out PxRect rect){
    rect=new PxRect();
    OverlayNative.RECT r;if(!OverlayNative.IsWindow(game)||OverlayNative.IsIconic(game)||!OverlayNative.GetClientRect(game,out r))return false;
    var p=new OverlayNative.POINT{X=0,Y=0};if(!OverlayNative.ClientToScreen(game,ref p))return false;
    rect=new PxRect(p.X,p.Y,p.X+(r.Right-r.Left),p.Y+(r.Bottom-r.Top));return !rect.IsEmpty;
  }
  public bool GameIsForeground(IntPtr game){
    IntPtr front=OverlayNative.GetForegroundWindow();if(front==IntPtr.Zero)return false;
    uint owner;OverlayNative.GetWindowThreadProcessId(front,out owner);return (int)owner==pid;
  }
  public void BringToFront(IntPtr hwnd){OverlayNative.SetForegroundWindow(hwnd);}
  public bool Cursor(out int x,out int y){OverlayNative.POINT p;if(OverlayNative.GetCursorPos(out p)){x=p.X;y=p.Y;return true;}x=0;y=0;return false;}
  public PxRect[] Monitors(){
    var screens=System.Windows.Forms.Screen.AllScreens;var list=new PxRect[screens.Length];
    for(int i=0;i<screens.Length;i++){var b=screens[i].Bounds;list[i]=new PxRect(b.Left,b.Top,b.Right,b.Bottom);}
    return list;
  }
}

// What the launcher does when the player uses the overlay. Any member may be null: that control then does nothing.
// What the game says it is doing (its .state file), handed over by the launcher. The overlay only shows it; it never reads the file itself.
public sealed class GameLiveState { public bool Paused,Muted,Fullscreen,Unlimited;public int Volume=100;public string LastShot=""; }

public sealed class OverlayHost {
  public Action<string> ApplyPicture;
  public Func<string> PictureStatus,PictureSelected;
  public Func<bool> PictureBusy,PicturePresentationForeground,LauncherForeground;
  public Action StopGame;                 // ask the game to close, as if its window were closed
  public Action ToggleMusic,NextTrack;    // the launcher's own music
  public Func<bool> MusicPlaying;
  public Func<IList<int>> GameKeyCodes;   // virtual-key codes the game's key map uses, so the hotkey avoids them
  // The key editor. The overlay keeps NO draft and writes NO file: it edits the launcher's own draft through these
  // callbacks, and the launcher saves from its one save path (SPEC-keys.md). Leave KeyDraft null to hide the editor.
  public Func<KeyMap> KeyDraft;                 // the launcher's current draft (loaded if needed)
  public Func<string,string,string> EditKey;    // button, key name -> status line (swaps with another button if needed)
  public Func<bool> KeysDirty;
  public Func<string> SaveKeys,DiscardKeys,DefaultKeys; // each returns a status line
  public Func<string> KeyWarning;               // text about a damaged saved map, or empty
  // Controls the launcher can do on its own (2026-11-17). A null callback leaves that control disabled.
  public Action RestartGame;                    // close the game and start it again (the launcher does both)
  public Action<string> ShowLauncher;           // bring the launcher to the front on "Home", "Setup" or "Help"
  public Func<double> MusicVolume;public Action<double> SetMusicVolume; // 0 to 1: the launcher's own music
  public Func<bool> MusicMuted;public Action ToggleMuteMusic;
  public Func<double> UiScale;public Action<int> StepUiScale;           // the launcher's own text size, steps of 10 percent
  // Game settings (2026-11-18): the play settings the game program offers, changed while it runs and used by the next start.
  // The overlay keeps no copy: the launcher owns the values and the file. Leave GameSettings or SetGameSetting null to hide the panel.
  public Func<IList<GameSetting>> GameSettings;  // the settings on offer now, with their current values
  public Func<string,int,string> SetGameSetting; // setting id, new value -> "" when it is kept, or a line saying why not
  public Func<bool> GameSettingsPending;         // true when the next start would differ from the running game
  // Live control (proposed 2026-11-22): the overlay never talks to the game. It hands one command line to the launcher, which writes the game's control file.
  // Lines: "pause on", "pause off", "fullscreen toggle", "screenshot", "volume 0-100", "mute on", "mute off". Leave GameCommand null and the controls stay disabled.
  public Action<string> GameCommand;
  public Func<double> GameVolume;                // the game's volume at its start, 0 to 1 (optional)
  public Func<bool> GameMuted;                   // true when the game was started muted (optional)
  public Func<GameLiveState> GameState;          // what the game reports now, or null when it has not (optional; the overlay asks about twice a second while it is shown)
}

static class OverlayArt {
  static readonly Dictionary<string,ImageSource> cache=new Dictionary<string,ImageSource>();
  // The developer's control art, embedded in the program as S-<name> (SPEC-controls.md). Null when this build lacks it.
  public static ImageSource Sprite(string name){
    ImageSource found;if(cache.TryGetValue(name,out found))return found;
    try{using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("S-"+name)){if(s!=null){var img=new BitmapImage();img.BeginInit();img.CacheOption=BitmapCacheOption.OnLoad;img.StreamSource=s;img.EndInit();img.Freeze();found=img;}}}catch(Exception){found=null;}
    cache[name]=found;return found;
  }
}

public sealed class GameOverlay : IDisposable {
  const int HotkeyId=0x4F56,WM_HOTKEY=0x0312,WM_MOUSEACTIVATE=0x0021,MA_NOACTIVATE=3,StopArmMs=3000,KeyboardWaitMs=1500;
  readonly IGameProbe probe;readonly OverlayHost host;readonly OverlaySettings settings;readonly string settingsPath;readonly OverlayFeatures features;
  readonly OverlayState state=new OverlayState();
  readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
  readonly Dictionary<string,TextBlock> rowText=new Dictionary<string,TextBlock>();
  Window win;IntPtr winHandle=IntPtr.Zero;HwndSource hotkeySource;DispatcherTimer timer;
  Border pill,menuBox,noteBox;Button stopButton,menuButton,restartButton;TextBlock noteText;long restartArmedUntil;
  Border confirmBox;TextBlock confirmText; // the caption under the pill while Stop or Restart waits for its second click
  readonly Dictionary<string,System.Windows.Shapes.Rectangle> barFill=new Dictionary<string,System.Windows.Shapes.Rectangle>();
  readonly Dictionary<string,TextBlock> barText=new Dictionary<string,TextBlock>();
  const double BarW=110;
  // Owner, 2026-11-17: "Quick bar only in full screen" is not working right now, so the row is greyed out and a saved
  // full-screen-only line in overlay.txt is ignored. Set this to true to bring the option back.
  static readonly bool FullScreenOnlyWorks=false;
  // The pill and its menus use the developer's kit numbers: pill 300 by 64, menu 174 wide, submenu 286 wide, rows 39 high every 41.
  const double PillW=300,PillH=64,MenuLeft=237,MenuTop=74,MenuW=174,SubLeft=421,SubW=286,RowH=39,RowPitch=41;
  Canvas stage;System.Windows.Shapes.Rectangle menuTab;string openSub;double availDip=100000; // room to the right of the pill's left edge, in device-independent pixels
 
  readonly Dictionary<string,Border> subBoxes=new Dictionary<string,Border>();
  readonly Dictionary<string,Button> mainRows=new Dictionary<string,Button>();
  readonly Dictionary<string,TextBlock> tickText=new Dictionary<string,TextBlock>();
  IntPtr game=IntPtr.Zero;long lastFind=-100000,lastNow,stopArmedUntil,keysStarted,monitorsAt=-100000;
  PxRect lastUi,lastGame;PxRect[] monitors=new PxRect[0];
  bool shown,disposed,registered,keysActive,noActivate,hotkeyPending,musicShown;
  double scale=1,pillWidthDip=160;int errors;
  bool paused,gameMuted,unlimited;double gameVol=1;int gameVolSent=-1;Button pauseButton;long lastSendAt=-100000,liveStateAt=-100000;string lastShot=null; // live control: what the overlay last asked of the game (the game has no state file yet)
  string hotkeyProblem="",keysNote="";
  Border editorBox;Button editorSave,editorDiscard;TextBlock editorStatus,editorWarning;bool editorOpen;string capturing;
  readonly Dictionary<string,TextBlock> keyText=new Dictionary<string,TextBlock>();
  readonly Dictionary<string,Button> gsetRows=new Dictionary<string,Button>();Button gsetRestart;string gsetNote="";bool gsetPending;IList<GameSetting> gsetList=new List<GameSetting>();
  const string GsetPanel="Game settings";const double SettingRowH=26;
  // keyboard control of the menus (only after the overlay key opened the overlay)
  bool navActive,navBring;int navLevel,navPill,navMain,navSub;long navStarted;Border navRing;readonly List<Button> pillList=new List<Button>();
  double fit=1;public double? TestScale; // fit: how much the overlay is shrunk to fit a small game picture (only ever smaller while it stays open)
  public double Fit {get{return fit;}}
  public bool? TestForeground; // tests only: say whether the overlay window is in front; null asks Windows

  // Trace for diagnosing the overlay on a real game. It writes only state changes and errors, never file contents, to a small file:
  // the path in the environment variable FLUTTERDECK_OVERLAY_TRACE if set, else fd-overlay-trace.txt in the temp folder.
  // The file is started afresh at each launch and stops growing at 300 000 bytes.
  static readonly string tracePath=TracePathChoice();
  static bool traceStarted; // the first write of a launch replaces the old file
  static string TracePathChoice(){
    try{
      string p=Environment.GetEnvironmentVariable("FLUTTERDECK_OVERLAY_TRACE");
      if(String.IsNullOrEmpty(p))p=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"fd-overlay-trace.txt");
      return p;
    }catch(Exception){return null;}
  }
  static void Trace(string text){
    if(String.IsNullOrEmpty(tracePath))return;
    try{var info=new System.IO.FileInfo(tracePath);if(traceStarted&&info.Exists&&info.Length>300000)return;using(var w=new System.IO.StreamWriter(tracePath,traceStarted)){traceStarted=true;w.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff")+" "+text);}}catch(Exception){}
  }
  OverlayMode traceMode=OverlayMode.Hidden;IntPtr traceGame=IntPtr.Zero;PxRect traceRect;bool traceFg=true;
  public static GameOverlay Attach(int gamePid,OverlayHost host,OverlaySettings settings,string settingsPath,OverlayFeatures features,IGameProbe probe,bool autoRun){
    var o=new GameOverlay(host,settings,settingsPath,features,probe??new Win32Probe(gamePid));
    o.Build();
    Trace("attached to game process "+gamePid+"; hotkey "+o.settings.HotkeyText+" registered="+o.registered+" "+o.hotkeyProblem+"; full-screen-only="+o.settings.FullScreenOnly);
    if(autoRun){o.timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(40)};o.timer.Tick+=delegate{o.Step(o.clock.ElapsedMilliseconds);};o.timer.Start();}
    return o;
  }
  GameOverlay(OverlayHost host,OverlaySettings settings,string settingsPath,OverlayFeatures features,IGameProbe probe){
    this.host=host??new OverlayHost();this.settings=settings??new OverlaySettings();this.settingsPath=settingsPath;this.features=features??OverlayFeatures.None;this.probe=probe;
  }

  // ---- facts for tests and for the launcher ----
  bool launcherPicker;
  public void OpenPicturePicker(){
    if(disposed||host.ApplyPicture==null)return;
    if(game==IntPtr.Zero)game=probe.FindGame();
    PxRect rect;if(game==IntPtr.Zero||!probe.ClientRect(game,out rect)||rect.IsEmpty)return;
    lastGame=rect;launcherPicker=true;state.Mode=OverlayMode.Menu;state.Pinned=true;openSub="Picture preset";Apply(rect,lastNow);
  }
  // Backend progress can arrive while no observation tick redraws this menu.
  // Refresh its text and automation values on the UI thread immediately.
  public void RefreshPictureStatus(){if(!disposed)Refresh();}
  public IntPtr GameWindow {get{return game;}}
  public OverlayMode Mode {get{return state.Mode;}}
  public bool Visible {get{return shown;}}
  public bool Pinned {get{return state.Pinned;}}
  public string HotkeyProblem {get{return hotkeyProblem;}}
  public string Note {get{return keysActive?keysNote:hotkeyProblem.Length>0?hotkeyProblem:keysNote;}}
  public bool HotkeyRegistered {get{return registered;}}
  public bool NoActivate {get{return noActivate;}}
  public IntPtr WindowHandle {get{return winHandle;}}
  public IntPtr HotkeyHandle {get{return hotkeySource==null?IntPtr.Zero:hotkeySource.Handle;}}
  public PxRect UiRect {get{return lastUi;}}
  public FrameworkElement Content {get{return win==null?null:win.Content as FrameworkElement;}}
  public OverlaySettings Settings {get{return settings;}}

  // ---- building the window ----
  // Colours from the developer's pill kit (Flutter-Deck-Top-Pill-Menu-Kit): navy panel, cream ink, cyan-blue edge, red seam, gold studs.
  static readonly Brush Panel=Frozen(new SolidColorBrush(Color.FromArgb(0xF2,0x15,0x2D,0x40)));
  static readonly Brush Cream=Frozen(new SolidColorBrush(Color.FromRgb(0xFF,0xEB,0xC6)));
  static readonly Brush Edge=Frozen(new SolidColorBrush(Color.FromRgb(0x52,0x7C,0x8E)));
  static readonly Brush Chevron=Frozen(new SolidColorBrush(Color.FromRgb(0xA9,0xC7,0xD6)));
  static readonly Brush Cyan=Frozen(new SolidColorBrush(Color.FromRgb(0x5D,0xE5,0xFA)));
  static readonly FontFamily UiFont=new FontFamily("Segoe UI");
  static Brush MenuFill(){var g=new LinearGradientBrush(Color.FromRgb(0x1B,0x35,0x49),Color.FromRgb(0x10,0x23,0x33),new Point(0,0),new Point(1,1));g.Freeze();return g;}
  static readonly Brush HoverFill=Frozen(new SolidColorBrush(Color.FromRgb(0x24,0x51,0x6A)));
  static readonly Brush HoverWash=Frozen(new SolidColorBrush(Color.FromArgb(0x99,0x24,0x51,0x6A))); // see-through: the wash draws over a menu row's text
  static readonly Brush HoverEdge=Frozen(new SolidColorBrush(Color.FromRgb(0x4D,0x93,0xA6)));
  static readonly Brush PressFill=Frozen(new SolidColorBrush(Color.FromRgb(0x0B,0x1C,0x29)));
  static readonly Brush ArmFill=Frozen(new SolidColorBrush(Color.FromRgb(0x4A,0x25,0x24)));
  static readonly Brush Seam=Frozen(new SolidColorBrush(Color.FromRgb(0xD4,0x57,0x41)));
  static readonly Brush Gold=Frozen(new SolidColorBrush(Color.FromRgb(0xD5,0xAD,0x62)));
  static Brush PillFill(){var g=new LinearGradientBrush(Color.FromRgb(0x20,0x3B,0x51),Color.FromRgb(0x10,0x23,0x33),new Point(0,0),new Point(0,1));g.Freeze();return g;}
  static readonly Brush Amber=Frozen(new SolidColorBrush(Color.FromRgb(0xF2,0xB8,0x6B)));
  static Brush Frozen(SolidColorBrush b){b.Freeze();return b;}

  void Build(){
    win=new Window{WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=null,ShowInTaskbar=false,ShowActivated=false,Topmost=true,UseLayoutRounding=true,SnapsToDevicePixels=true,ResizeMode=ResizeMode.NoResize,SizeToContent=SizeToContent.WidthAndHeight,WindowStartupLocation=WindowStartupLocation.Manual,Left=-32000,Top=-32000,Title="Flutter Deck overlay"};
    AutomationProperties.SetAutomationId(win,"FlutterDeckOverlay");AutomationProperties.SetName(win,"Flutter Deck overlay");
    stage=new Canvas{Background=null,Width=PillW,Height=PillH};
    // The five controls of the kit, always shown; the ones the game cannot do yet are drawn disabled.
    var row=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    bool first=true;
    foreach(string id in OverlayLayout.Pill(features)){
      Button b;
      if(id=="stop"){b=StateIcon("overlay-stop","Stop session","stop",OnStop);stopButton=b;}
      else if(id=="menu"){b=StateIcon("overlay-menu","Deck menu","menu",OnMenu);menuButton=b;}
      else{
        string title=id=="pause"?"Pause":id=="restart"?"Restart session":"Fullscreen";
        // Pause and fullscreen: no action until the game names a control (OverlayLayout.GameControls). Restart is the launcher's own.
        Action click=id=="restart"&&host.RestartGame!=null?(Action)OnRestart:id=="pause"&&LiveOn("pause")?(Action)OnPause:id=="fullscreen"&&LiveOn("fullscreen")?(Action)OnFullscreen:null;
        b=StateIcon("overlay-"+id,title,id,click);
        if(click==null)b.ToolTip=title+" (not available yet)";
        else if(id=="restart")restartButton=b;
        else if(id=="pause")pauseButton=b;
      }
      if(!first)b.Margin=new Thickness(4,0,0,0);
      first=false;row.Children.Add(b);pillList.Add(b);
    }
    // The pill: 64 high, rounded, navy gradient, a red seam along the top and a gold stud at each end (the kit's pill-shell, drawn in code).
    var shell=new Grid();shell.Children.Add(row);
    shell.Children.Add(new System.Windows.Shapes.Rectangle{Width=252,Height=3,Fill=Seam,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(23,0,0,0),IsHitTestVisible=false});
    shell.Children.Add(new System.Windows.Shapes.Ellipse{Width=4,Height=4,Fill=Gold,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(12,30,0,0),IsHitTestVisible=false});
    shell.Children.Add(new System.Windows.Shapes.Ellipse{Width=4,Height=4,Fill=Gold,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,30,12,0),IsHitTestVisible=false});
    pill=new Border{Width=PillW,Height=PillH,Background=PillFill(),BorderBrush=Edge,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(22),Child=shell};
    stage.Children.Add(pill);Canvas.SetLeft(pill,0);Canvas.SetTop(pill,0);
    // The menus: six names; each opens a submenu of rows (OverlayMenus). A row works only when it has an action here.
    var actions=MenuActions();
    var mainStack=new StackPanel{Orientation=Orientation.Vertical};
    foreach(string name in OverlayMenus.Names){
      mainStack.Children.Add(MainRow(name));
      var rows=new StackPanel{Orientation=Orientation.Vertical};
      foreach(MenuItemSpec spec in OverlayMenus.Items(name)){
        if(spec.Id=="aud-game-volume"&&LiveOn("volume"))rows.Children.Add(VolumeRow(spec));
        else if(spec.Id=="aud-volume"&&host.MusicVolume!=null&&host.SetMusicVolume!=null)rows.Children.Add(VolumeRow(spec));
        else if(spec.Id=="view-scale"&&host.UiScale!=null&&host.StepUiScale!=null)rows.Children.Add(ScaleRow(spec));
        else rows.Children.Add(ItemRow(spec,actions));
      }
      var sub=MenuPanel(SubW,rows);sub.Visibility=Visibility.Collapsed;subBoxes[name]=sub;stage.Children.Add(sub);
    }
    if(host.ApplyPicture!=null){
      var pictureRows=new StackPanel();
      var pictureActions=new Dictionary<string,Action>();
      pictureActions["picture-back"]=delegate{OpenSub("View");};
      pictureActions["picture-balanced"]=delegate{host.ApplyPicture("balanced");Refresh();};
      pictureActions["picture-performance"]=delegate{host.ApplyPicture("performance");Refresh();};
      pictureRows.Children.Add(ItemRow(new MenuItemSpec("picture-back","Back to View",null,false,true),pictureActions));
      pictureRows.Children.Add(ItemRow(new MenuItemSpec("picture-balanced","Learn Balanced 2x",null,false,true),pictureActions));
      pictureRows.Children.Add(ItemRow(new MenuItemSpec("picture-performance","Learn Performance 2x",null,false,true),pictureActions));
      var pictureStatus=new TextBlock{Foreground=Amber,FontFamily=UiFont,FontSize=13,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(11,8,11,8)};
      AutomationProperties.SetAutomationId(pictureStatus,"picture-status");rowText["picture-status"]=pictureStatus;pictureRows.Children.Add(pictureStatus);
      var picturePanel=MenuPanel(SubW,pictureRows);picturePanel.Visibility=Visibility.Collapsed;subBoxes["Picture preset"]=picturePanel;stage.Children.Add(picturePanel);
    }
    if(host.GameSettings!=null&&host.SetGameSetting!=null){
      // Game settings: one row per setting the game program offers, then a line of status and the Restart row. Built from the list the launcher gives now.
      var gRows=new StackPanel();var gActs=new Dictionary<string,Action>();
      if(host.RestartGame!=null)gActs["gset-restart"]=OnRestart;
      // Short on purpose: no Back row (pointing at another menu name switches panels), the Restart row first so it is never
      // the one that hangs below a small game picture, and compact setting rows.
      gsetRestart=ItemRow(new MenuItemSpec("gset-restart","Restart to apply changes\u2026",null,false,true),gActs);gRows.Children.Add(gsetRestart);
      IList<GameSetting> offered=null;try{offered=host.GameSettings();}catch(Exception){}
      if(offered!=null)foreach(GameSetting gs in offered)gRows.Children.Add(SettingRow(gs));
      var gStatus=new TextBlock{Foreground=Amber,FontFamily=UiFont,FontSize=13,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(11,8,11,8)};
      AutomationProperties.SetAutomationId(gStatus,"gset-status");rowText["gset-status"]=gStatus;gRows.Children.Add(gStatus);
      var gPanel=MenuPanel(SubW,gRows);gPanel.Visibility=Visibility.Collapsed;subBoxes[GsetPanel]=gPanel;stage.Children.Add(gPanel);
    }
    menuBox=MenuPanel(MenuW,mainStack);menuBox.Visibility=Visibility.Collapsed;stage.Children.Add(menuBox);
    menuTab=new System.Windows.Shapes.Rectangle{Width=26,Height=3,Fill=Seam,RadiusX=1.5,RadiusY=1.5,Visibility=Visibility.Collapsed,IsHitTestVisible=false};stage.Children.Add(menuTab);
    noteText=new TextBlock{Foreground=Amber,FontFamily=UiFont,FontSize=13,TextWrapping=TextWrapping.Wrap};
    AutomationProperties.SetAutomationId(noteText,"overlay-note");
    noteBox=new Border{Width=SubW,Background=Panel,BorderBrush=Edge,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Padding=new Thickness(10,6,10,6),Child=noteText,Visibility=Visibility.Collapsed};
    stage.Children.Add(noteBox);
    confirmText=new TextBlock{Foreground=Cream,FontFamily=UiFont,FontSize=13};
    confirmBox=new Border{Background=Panel,BorderBrush=Seam,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Padding=new Thickness(12,6,12,6),Child=confirmText,Visibility=Visibility.Collapsed,IsHitTestVisible=false};
    AutomationProperties.SetAutomationId(confirmBox,"overlay-confirm");AutomationProperties.SetAutomationId(confirmText,"overlay-confirm-text");stage.Children.Add(confirmBox);
    BuildEditor();
    navRing=new Border{BorderBrush=Cyan,BorderThickness=new Thickness(2),CornerRadius=new CornerRadius(8),Background=null,IsHitTestVisible=false,Visibility=Visibility.Collapsed};
    AutomationProperties.SetAutomationId(navRing,"overlay-nav-ring");stage.Children.Add(navRing);
    win.Content=stage;
    win.PreviewMouseMove+=delegate(object sender,MouseEventArgs e){try{if(navActive&&!keysActive){var d=e.OriginalSource as DependencyObject;while(d!=null&&!(d is Button))d=d is Visual||d is System.Windows.Media.Media3D.Visual3D?VisualTreeHelper.GetParent(d):LogicalTreeHelper.GetParent(d);NavSelect(d as Button);}}catch(Exception){}};
    win.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){try{e.Handled=HandleKey(e.Key==Key.System?e.SystemKey:e.Key==Key.ImeProcessed?e.ImeProcessedKey:e.Key==Key.DeadCharProcessed?e.DeadCharProcessedKey:e.Key,Keyboard.Modifiers,e.IsRepeat);}catch(Exception ex){Trace("ERROR in a key handler: "+ex);e.Handled=true;}}; // a failing host callback must never reach the launcher's dispatcher
    win.Deactivated+=delegate{OnLostActive();};
    var helper=new WindowInteropHelper(win);winHandle=helper.EnsureHandle();
    var source=HwndSource.FromHwnd(winHandle);if(source!=null)source.AddHook(WindowHook);
    SetNoActivate(true);
    var p=new HwndSourceParameters("FlutterDeckOverlayKeys"){Width=0,Height=0,PositionX=0,PositionY=0,ParentWindow=new IntPtr(-3),WindowStyle=0}; // message-only: it only receives the hotkey
    hotkeySource=new HwndSource(p);hotkeySource.AddHook(HotkeyHook);
    RegisterHotkey();
    try{if(host.GameVolume!=null){double v0=host.GameVolume();gameVol=Double.IsNaN(v0)?1:Math.Max(0,Math.Min(1,v0));}if(host.GameMuted!=null)gameMuted=host.GameMuted();}catch(Exception){}
    gameVolSent=(int)Math.Round(gameVol*100);
    Refresh();
    pillWidthDip=PillW;
  }
  // What each menu row does. A row with no entry here is drawn disabled (the kit: unsupported actions stay unavailable).
  Dictionary<string,Action> MenuActions(){
    var a=new Dictionary<string,Action>();
    a["ses-stop"]=OnStop;
    if(host.ApplyPicture!=null)a["view-preset"]=delegate{OpenSub("Picture preset");};
    if(host.GameSettings!=null&&host.SetGameSetting!=null)a["ses-settings"]=delegate{OpenSub(GsetPanel);};
    if(host.KeyDraft!=null&&host.EditKey!=null&&host.SaveKeys!=null)a["overlay-keys"]=BeginEditor;
    a["overlay-hotkey"]=BeginKeys;
    if(FullScreenOnlyWorks)a["overlay-fullscreen-only"]=ToggleFullScreenOnly; // greyed out until it works
    a["overlay-hover"]=ToggleHover;
    a["overlay-keyboard"]=ToggleKeyboardMenu;
    if(host.RestartGame!=null)a["ses-restart"]=OnRestart;
    if(LiveOn("pause"))a["ses-resume"]=OnResume;
    if(LiveOn("fullscreen"))a["view-fullscreen"]=OnFullscreen;
    if(LiveOn("screenshot"))a["view-screenshot"]=delegate{Send("screenshot");};
    if(LiveOn("speed"))a["view-speed"]=OnSpeed;
    if(LiveOn("volume"))a["aud-game-mute"]=OnGameMute;
    if(host.ShowLauncher!=null){a["lib-return"]=delegate{host.ShowLauncher("Home");};a["help-setup"]=delegate{host.ShowLauncher("Setup");};a["help-about"]=delegate{host.ShowLauncher("Help");};}
    return a;
  }
  Border MenuPanel(double width,UIElement rows){
    return new Border{Width=width,Background=MenuFill(),BorderBrush=Edge,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(13),Padding=new Thickness(7),Child=rows};
  }
  // A row of the first menu: the name and a chevron. Pointing at it or clicking it opens its submenu.
  Button MainRow(string name){
    var grid=new Grid{Margin=new Thickness(11,0,11,0)};
    grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
    grid.Children.Add(new TextBlock{Text=name,Foreground=Cream,FontFamily=UiFont,FontSize=15,VerticalAlignment=VerticalAlignment.Center});
    var chev=new TextBlock{Text="\u203A",Foreground=Chevron,FontFamily=UiFont,FontSize=18,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(chev,1);grid.Children.Add(chev);
    var b=new Button{Content=grid,Height=RowH,Margin=new Thickness(0,1,0,1),Focusable=false,Cursor=Cursors.Hand,Template=RowTemplate()};
    AutomationProperties.SetAutomationId(b,"menu-"+name);AutomationProperties.SetName(b,name);
    b.Click+=delegate{try{OpenSub(name);}catch(Exception){}};
    b.MouseEnter+=delegate{try{OpenSub(name);}catch(Exception){}};
    mainRows[name]=b;return b;
  }
  // A row of a submenu: a tick column, the label, and a shortcut or an arrow on the right.
  Button ItemRow(MenuItemSpec spec,Dictionary<string,Action> actions){
    Action act;actions.TryGetValue(spec.Id,out act);
    var grid=new Grid{Margin=new Thickness(11,0,11,0)};
    grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(25)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
    var tick=new TextBlock{Text="\u2713",Foreground=Cyan,FontFamily=UiFont,FontSize=14,VerticalAlignment=VerticalAlignment.Center,Visibility=Visibility.Hidden};
    AutomationProperties.SetAutomationId(tick,spec.Id+"-tick");tickText[spec.Id]=tick;grid.Children.Add(tick);
    var label=new TextBlock{Text=spec.Label,Foreground=Cream,FontFamily=UiFont,FontSize=14,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};
    AutomationProperties.SetAutomationId(label,spec.Id+"-text");Grid.SetColumn(label,1);grid.Children.Add(label);rowText[spec.Id]=label;
    if(spec.Arrow||spec.Shortcut!=null){var right=new TextBlock{Text=spec.Arrow?"\u203A":spec.Shortcut,Foreground=Chevron,FontFamily=UiFont,FontSize=spec.Arrow?18:12,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(12,0,0,0)};Grid.SetColumn(right,2);grid.Children.Add(right);}
    var b=new Button{Content=grid,Height=RowH,Margin=new Thickness(0,1,0,1),Focusable=false,Cursor=Cursors.Hand,Template=RowTemplate(),IsEnabled=act!=null};
    AutomationProperties.SetAutomationId(b,spec.Id);AutomationProperties.SetName(b,spec.Label);
    if(act==null)b.ToolTip="Not available yet";
    b.Click+=delegate{try{if(act!=null)act();}catch(Exception){}};
    return b;
  }
  // A game setting row: a tick column (a switch shows a tick when on), the label, and for a choice its current entry on the right.
  // One click changes it: a switch flips, a choice goes to the next entry. The launcher keeps the value.
  Button SettingRow(GameSetting gs){
    string id="gset-"+gs.Id;
    var grid=new Grid{Margin=new Thickness(11,0,11,0)};
    grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(25)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
    var tick=new TextBlock{Text="\u2713",Foreground=Cyan,FontFamily=UiFont,FontSize=14,VerticalAlignment=VerticalAlignment.Center,Visibility=Visibility.Hidden};
    AutomationProperties.SetAutomationId(tick,id+"-tick");tickText[id]=tick;grid.Children.Add(tick);
    var label=new TextBlock{Text=gs.Label,Foreground=Cream,FontFamily=UiFont,FontSize=14,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};
    AutomationProperties.SetAutomationId(label,id+"-text");Grid.SetColumn(label,1);grid.Children.Add(label);rowText[id]=label;
    var value=new TextBlock{Text=gs.ValueText(),Foreground=Chevron,FontFamily=UiFont,FontSize=13,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(10,0,0,0)};
    AutomationProperties.SetAutomationId(value,id+"-value");rowText[id+"-value"]=value;Grid.SetColumn(value,2);grid.Children.Add(value);
    var b=new Button{Content=grid,Height=SettingRowH,Margin=new Thickness(0,1,0,1),Focusable=false,Cursor=Cursors.Hand,Template=RowTemplate()};
    AutomationProperties.SetAutomationId(b,id);AutomationProperties.SetName(b,gs.Label);
    ToolTipService.SetShowOnDisabled(b,true); // a greyed-out row still shows why
    string sid=gs.Id;b.Click+=delegate{try{ChangeSetting(sid);}catch(Exception){}};
    gsetRows[gs.Id]=b;return b;
  }
  void ChangeSetting(string id){
    try{
      GameSetting current=null;foreach(GameSetting g in gsetList)if(g.Id==id)current=g;
      if(current==null||!current.Enabled)return;
      string problem=host.SetGameSetting(id,current.Next());
      gsetNote=problem??"";
    }catch(Exception){gsetNote="That setting could not be changed.";}
    Refresh();
  }
  // Brings the rows of the Game settings panel in line with the launcher's values. Only while that panel is open.
  void RefreshGameSettings(){
    IList<GameSetting> list=null;try{list=host.GameSettings();}catch(Exception){}
    if(list==null)list=new List<GameSetting>();
    gsetList=list;
    foreach(GameSetting gs in list){
      Button b;if(!gsetRows.TryGetValue(gs.Id,out b))continue;
      string id="gset-"+gs.Id;
      b.IsEnabled=gs.Enabled;b.ToolTip=gs.Hint.Length>0?gs.Hint:null;
      SetTick(id,gs.Ticked);
      TextBlock v;if(rowText.TryGetValue(id+"-value",out v))v.Text=gs.ValueText();
      AutomationProperties.SetName(b,gs.Label+(gs.IsSwitch?(gs.Value!=0?": on":": off"):": "+gs.ValueText())+(gs.Enabled||gs.Hint.Length==0?"":" ("+gs.Hint+")"));
    }
    gsetPending=false;try{gsetPending=host.GameSettingsPending!=null&&host.GameSettingsPending();}catch(Exception){}
    if(gsetRestart!=null)gsetRestart.IsEnabled=gsetPending&&host.RestartGame!=null;
    string status=gsetNote.Length>0?gsetNote:list.Count==0?"This game program has no settings to change here.":gsetPending?"Saved. Restart to apply.":"Changes start at the next restart.";
    rowText["gset-status"].Text=status;
  }
  public bool GameSettingsPending {get{return gsetPending;}}
  // The kit's menu row: transparent, with the lighter blue fill and edge on hover; dimmed when disabled.
  static ControlTemplate RowTemplate(){
    var t=new ControlTemplate(typeof(Button));
    var plate=new FrameworkElementFactory(typeof(Border));plate.Name="plate";
    plate.SetValue(Border.BackgroundProperty,Brushes.Transparent);plate.SetValue(Border.BorderBrushProperty,Brushes.Transparent);
    plate.SetValue(Border.BorderThicknessProperty,new Thickness(1));plate.SetValue(Border.CornerRadiusProperty,new CornerRadius(7));
    var content=new FrameworkElementFactory(typeof(ContentPresenter));
    content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Stretch);content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
    plate.AppendChild(content);t.VisualTree=plate;
    var over=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};
    over.Setters.Add(new Setter(Border.BackgroundProperty,HoverFill,"plate"));over.Setters.Add(new Setter(Border.BorderBrushProperty,HoverEdge,"plate"));t.Triggers.Add(over);
    var off=new Trigger{Property=UIElement.IsEnabledProperty,Value=false};
    off.Setters.Add(new Setter(UIElement.OpacityProperty,0.35));off.Setters.Add(new Setter(Border.BackgroundProperty,Brushes.Transparent,"plate"));off.Setters.Add(new Setter(Border.BorderBrushProperty,Brushes.Transparent,"plate"));t.Triggers.Add(off);
    return t;
  }
  // The "selected" look (open menu, open button): the hover fill and edge held in place.
  static void SetSelected(Button b,bool on){
    if(b==null)return;
    b.ApplyTemplate();var plate=b.Template.FindName("plate",b) as Border;if(plate==null)return;
    if(on){plate.Background=HoverFill;plate.BorderBrush=HoverEdge;}else{plate.ClearValue(Border.BackgroundProperty);plate.ClearValue(Border.BorderBrushProperty);}
  }
  // "Music volume": the label, a bar you click or drag, and the percentage. The launcher owns the value.
  FrameworkElement VolumeRow(MenuItemSpec spec){
    var grid=new Grid{Margin=new Thickness(11,0,11,0)};
    grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(25)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
    grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(40)});
    var label=new TextBlock{Text=spec.Label,Foreground=Cream,FontFamily=UiFont,FontSize=14,VerticalAlignment=VerticalAlignment.Center};
    AutomationProperties.SetAutomationId(label,spec.Id+"-text");Grid.SetColumn(label,1);grid.Children.Add(label);rowText[spec.Id]=label;
    var track=new Grid{Width=BarW,Height=22,Background=Brushes.Transparent,Cursor=Cursors.Hand,VerticalAlignment=VerticalAlignment.Center};
    track.Children.Add(new Border{Height=6,CornerRadius=new CornerRadius(3),Background=PressFill,BorderBrush=Edge,BorderThickness=new Thickness(1),VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false});
    var fill=new System.Windows.Shapes.Rectangle{Height=6,RadiusX=3,RadiusY=3,Fill=Cyan,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false,Width=0};
    track.Children.Add(fill);barFill[spec.Id]=fill;
    string id=spec.Id;
    track.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){track.CaptureMouse();SetBar(id,e.GetPosition(track).X/BarW);e.Handled=true;};
    track.MouseMove+=delegate(object s,MouseEventArgs e){if(track.IsMouseCaptured)SetBar(id,e.GetPosition(track).X/BarW);};
    track.MouseLeftButtonUp+=delegate{track.ReleaseMouseCapture();};
    AutomationProperties.SetAutomationId(track,spec.Id+"-bar");AutomationProperties.SetName(track,spec.Label);Grid.SetColumn(track,2);grid.Children.Add(track);
    var pct=new TextBlock{Foreground=Chevron,FontFamily=UiFont,FontSize=12,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Right,Text="0%"};
    AutomationProperties.SetAutomationId(pct,spec.Id+"-value");barText[spec.Id]=pct;Grid.SetColumn(pct,3);grid.Children.Add(pct);
    var row=new Border{Height=RowH,Margin=new Thickness(0,1,0,1),Child=grid};
    AutomationProperties.SetAutomationId(row,spec.Id);AutomationProperties.SetName(row,spec.Label);
    return row;
  }
  // "UI scale": the label, a minus button, the percentage and a plus button. The launcher owns the value.
  FrameworkElement ScaleRow(MenuItemSpec spec){
    var grid=new Grid{Margin=new Thickness(11,0,11,0)};
    grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(25)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
    grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(46)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
    var label=new TextBlock{Text="Launcher text size",Foreground=Cream,FontFamily=UiFont,FontSize=14,VerticalAlignment=VerticalAlignment.Center};
    AutomationProperties.SetAutomationId(label,spec.Id+"-text");Grid.SetColumn(label,1);grid.Children.Add(label);rowText[spec.Id]=label;
    var minus=SmallButton(spec.Id+"-minus","Smaller launcher text","\u2212",delegate{host.StepUiScale(-1);Refresh();});Grid.SetColumn(minus,2);grid.Children.Add(minus);
    var pct=new TextBlock{Foreground=Cream,FontFamily=UiFont,FontSize=13,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,Text="100%"};
    AutomationProperties.SetAutomationId(pct,spec.Id+"-value");barText[spec.Id]=pct;Grid.SetColumn(pct,3);grid.Children.Add(pct);
    var plus=SmallButton(spec.Id+"-plus","Larger launcher text","+",delegate{host.StepUiScale(1);Refresh();});Grid.SetColumn(plus,4);grid.Children.Add(plus);
    var row=new Border{Height=RowH,Margin=new Thickness(0,1,0,1),Child=grid};
    AutomationProperties.SetAutomationId(row,spec.Id);AutomationProperties.SetName(row,spec.Label);
    return row;
  }
  Button SmallButton(string id,string name,string text,Action click){
    var b=new Button{Width=30,Height=28,Focusable=false,Cursor=Cursors.Hand,Template=RowTemplate(),ToolTip=name,Content=new TextBlock{Text=text,Foreground=Cream,FontFamily=UiFont,FontSize=16,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
    AutomationProperties.SetAutomationId(b,id);AutomationProperties.SetName(b,name);
    b.Click+=delegate{try{click();}catch(Exception){}};
    return b;
  }
  // Called by the volume bar (and by tests): value is 0 to 1.
  public void SetBar(string id,double value){
    value=Math.Max(0,Math.Min(1,Double.IsNaN(value)?0:value));
    try{if(id=="aud-volume"&&host.SetMusicVolume!=null)host.SetMusicVolume(value);}catch(Exception){}
    if(id=="aud-game-volume"&&LiveOn("volume")){gameVol=value;int pct=(int)Math.Round(value*100);if(pct!=gameVolSent){gameVolSent=pct;Send("volume "+pct);}} // one line per whole percent, not one per mouse move
    RefreshBars();
  }
  void RefreshBars(){
    try{
      System.Windows.Shapes.Rectangle f;TextBlock t;
      if(host.MusicVolume!=null&&barFill.TryGetValue("aud-volume",out f)){double v=Math.Max(0,Math.Min(1,host.MusicVolume()));f.Width=BarW*v;if(barText.TryGetValue("aud-volume",out t))t.Text=(int)Math.Round(v*100)+"%";}
      if(barFill.TryGetValue("aud-game-volume",out f)){f.Width=BarW*gameVol;if(barText.TryGetValue("aud-game-volume",out t))t.Text=(int)Math.Round(gameVol*100)+"%";}
      if(host.UiScale!=null&&barText.TryGetValue("view-scale",out t))t.Text=(int)Math.Round(host.UiScale()*100)+"%";
    }catch(Exception){}
  }
  // ---- live control of the game (proposed channel; the launcher writes the control file, see OverlayHost.GameCommand) ----
  bool LiveOn(string control){return host.GameCommand!=null&&OverlayLayout.Offered(control,features);}
  void Send(string line){lastSendAt=lastNow;try{if(host.GameCommand!=null)host.GameCommand(line);}catch(Exception){}}
  void SetPaused(bool on){
    if(paused==on)return;
    paused=on;Send(on?"pause on":"pause off");ShowPauseLook();
  }
  void ShowPauseLook(){
    if(pauseButton!=null){var path=pauseButton.Tag as System.Windows.Shapes.Path;if(path!=null)SetGlyph(path,paused?"play":"pause");pauseButton.ToolTip=paused?"Resume":"Pause";AutomationProperties.SetName(pauseButton,paused?"Resume":"Pause");}
  }
  // The game's own account of itself. For 1.5 seconds after the overlay sent a command the old state is ignored (the game has not answered yet), so a click or a drag is not undone.
  void PollGameState(long now){
    GameLiveState st=null;try{st=host.GameState();}catch(Exception){}
    if(st==null)return;
    if(now-lastSendAt>1500){
      if(paused!=st.Paused){paused=st.Paused;ShowPauseLook();}
      if(gameMuted!=st.Muted){gameMuted=st.Muted;SetTick("aud-game-mute",gameMuted);}
      if(unlimited!=st.Unlimited){unlimited=st.Unlimited;SetTick("view-speed",unlimited);}
      int v=Math.Max(0,Math.Min(100,st.Volume));
      if(v!=gameVolSent){gameVol=v/100.0;gameVolSent=v;RefreshBars();}
    }
    string shot=st.LastShot??"";
    if(lastShot==null)lastShot=shot;
    else if(shot!=lastShot){lastShot=shot;if(shot.Length>0){keysNote="Screenshot saved: "+System.IO.Path.GetFileName(shot);Refresh();}}
  }
  void OnPause(){SetPaused(!paused);}
  void OnResume(){if(paused)SetPaused(false);}
  void OnFullscreen(){Send("fullscreen toggle");}
  void OnSpeed(){unlimited=!unlimited;Send(unlimited?"speed unlimited":"speed normal");SetTick("view-speed",unlimited);}
  void OnGameMute(){gameMuted=!gameMuted;Send(gameMuted?"mute on":"mute off");SetTick("aud-game-mute",gameMuted);}
  void OnRestart(){
    long now=lastNow;
    if(now<restartArmedUntil){restartArmedUntil=0;SetRestartArmed(false);if(host.RestartGame!=null)host.RestartGame();state.Close();Apply(lastGame,now);return;}
    restartArmedUntil=now+StopArmMs;SetRestartArmed(true);
  }
  void SetRestartArmed(bool armed){
    if(restartButton!=null){
      SetPlate(restartButton,armed?ArmFill:null,armed?Seam:null);
      string name=armed?"Click again to restart the session":"Restart session";
      AutomationProperties.SetName(restartButton,name);restartButton.ToolTip=name;
    }
    TextBlock rr;if(rowText.TryGetValue("ses-restart",out rr))rr.Text=armed?"Click again to restart\u2026":"Restart session\u2026";
    if(rowText.TryGetValue("gset-restart",out rr))rr.Text=armed?"Click again to restart\u2026":"Restart to apply changes\u2026";
    Refresh();
  }
  void ToggleHover(){
    // Without a working hotkey nothing else could open the overlay, so the quick bar cannot be turned off then.
    if(settings.HoverBar&&!registered){keysNote="The quick bar stays on: the overlay key is not working, so nothing else could open the overlay.";Refresh();return;}
    settings.HoverBar=!settings.HoverBar;
    try{settings.Save(settingsPath);keysNote="";}catch(Exception){keysNote="That choice could not be saved, so it applies until the launcher closes.";}
    Refresh();
  }
  void ToggleKeyboardMenu(){
    settings.KeyboardMenu=!settings.KeyboardMenu;
    try{settings.Save(settingsPath);keysNote="";}catch(Exception){keysNote="That choice could not be saved, so it applies until the launcher closes.";}
    if(!settings.KeyboardMenu)EndNav(true);
    Refresh();
  }
  void SetTick(string id,bool on){TextBlock t;if(tickText.TryGetValue(id,out t))t.Visibility=on?Visibility.Visible:Visibility.Hidden;}
  void OpenSub(string name){
    if(disposed)return;
    if(!(state.Mode==OverlayMode.Menu||(state.Mode==OverlayMode.Keys&&!editorOpen)))return;
    if(openSub==name)return;
    openSub=name;Apply(lastGame,lastNow);
  }
  // For tests and callers: open one of the six submenus (only while the menu is open).
  public void OpenSubmenu(string name){OpenSub(name);}
  public string OpenSubmenuName {get{return openSub;}}
  static ControlTemplate PlateTemplate(CornerRadius corner,bool leftAligned){
    var t=new ControlTemplate(typeof(Button));
    var grid=new FrameworkElementFactory(typeof(Grid));
    var content=new FrameworkElementFactory(typeof(ContentPresenter));
    content.SetValue(FrameworkElement.HorizontalAlignmentProperty,leftAligned?HorizontalAlignment.Left:HorizontalAlignment.Center);
    content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
    grid.AppendChild(content);
    var wash=new FrameworkElementFactory(typeof(Border));wash.Name="wash";
    wash.SetValue(Border.BackgroundProperty,Brushes.Transparent);wash.SetValue(Border.CornerRadiusProperty,corner);wash.SetValue(UIElement.IsHitTestVisibleProperty,false);
    grid.AppendChild(wash);t.VisualTree=grid;
    var over=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};over.Setters.Add(new Setter(Border.BackgroundProperty,HoverWash,"wash"));t.Triggers.Add(over);
    var off=new Trigger{Property=UIElement.IsEnabledProperty,Value=false};off.Setters.Add(new Setter(UIElement.OpacityProperty,0.4));t.Triggers.Add(off);
    var down=new Trigger{Property=Button.IsPressedProperty,Value=true};down.Setters.Add(new Setter(Border.BackgroundProperty,new SolidColorBrush(Color.FromArgb(0x55,0,0,0)),"wash"));t.Triggers.Add(down);
    return t;
  }
  // The pill's icons are drawn from path data in the kit's 24 by 24 grid, so there are no picture files and nothing is
  // scaled from a bitmap. Stop and play are solid; pause and menu are strokes.
  static Geometry Geo(string d){var g=Geometry.Parse(d);g.Freeze();return g;}
  static readonly Geometry GeoStop=Geo("M8 6H16A2 2 0 0 1 18 8V16A2 2 0 0 1 16 18H8A2 2 0 0 1 6 16V8A2 2 0 0 1 8 6Z");
  static readonly Geometry GeoPlay=Geo("M8 5L19 12L8 19Z");
  static readonly Geometry GeoPause=Geo("M8 6V18M16 6V18");
  static readonly Geometry GeoMenu=Geo("M5 6H19M5 12H19M5 18H19");
  static readonly Geometry GeoRestart=Geo("M19 9A8 8 0 1 0 19 16M19 4V9H14");
  static readonly Geometry GeoFullscreen=Geo("M9 4H4V9M15 4H20V9M20 15V20H15M4 15V20H9");
  static void SetGlyph(System.Windows.Shapes.Path p,string name){
    bool solid=name=="stop"||name=="play";
    p.Data=name=="stop"?GeoStop:name=="play"?GeoPlay:name=="pause"?GeoPause:name=="restart"?GeoRestart:name=="fullscreen"?GeoFullscreen:GeoMenu;
    p.Fill=solid?Cream:null;p.Stroke=solid?null:Cream;
  }
  // A 48 by 44 button with the kit's hover, pressed and disabled looks. The glyph path is kept in Tag.
  Button StateIcon(string id,string name,string glyph,Action click){
    var path=new System.Windows.Shapes.Path{StrokeThickness=2,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round};
    SetGlyph(path,glyph);
    var box=new Viewbox{Width=22,Height=22,Stretch=Stretch.Uniform,IsHitTestVisible=false};
    var canvas=new Canvas{Width=24,Height=24};canvas.Children.Add(path);box.Child=canvas;
    var b=new Button{Width=48,Height=44,Margin=new Thickness(0),Padding=new Thickness(0),Cursor=Cursors.Hand,Focusable=false,Template=PillButtonTemplate(),ToolTip=name,Content=box,Tag=path,IsEnabled=click!=null};
    AutomationProperties.SetName(b,name);AutomationProperties.SetAutomationId(b,id);
    b.Click+=delegate{try{if(click!=null)click();}catch(Exception){}};
    return b;
  }
  static ControlTemplate PillButtonTemplate(){
    var t=new ControlTemplate(typeof(Button));
    var plate=new FrameworkElementFactory(typeof(Border));plate.Name="plate";
    plate.SetValue(Border.BackgroundProperty,Brushes.Transparent);plate.SetValue(Border.BorderBrushProperty,Brushes.Transparent);
    plate.SetValue(Border.BorderThicknessProperty,new Thickness(1));plate.SetValue(Border.CornerRadiusProperty,new CornerRadius(13));
    var content=new FrameworkElementFactory(typeof(ContentPresenter));
    content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
    plate.AppendChild(content);t.VisualTree=plate;
    var over=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};
    over.Setters.Add(new Setter(Border.BackgroundProperty,HoverFill,"plate"));over.Setters.Add(new Setter(Border.BorderBrushProperty,HoverEdge,"plate"));t.Triggers.Add(over);
    var down=new Trigger{Property=Button.IsPressedProperty,Value=true};
    down.Setters.Add(new Setter(Border.BackgroundProperty,PressFill,"plate"));down.Setters.Add(new Setter(UIElement.RenderTransformProperty,new TranslateTransform(0,1),"plate"));t.Triggers.Add(down);
    var off=new Trigger{Property=UIElement.IsEnabledProperty,Value=false};off.Setters.Add(new Setter(UIElement.OpacityProperty,0.35));t.Triggers.Add(off);
    return t;
  }
  Button IconButton(string id,string name,string sprite,double width,double height,Action click){
    var b=new Button{Width=width,Height=height,Margin=new Thickness(3),Padding=new Thickness(0),Cursor=Cursors.Hand,Focusable=false,Template=PlateTemplate(new CornerRadius(8),false),ToolTip=name};
    var art=sprite==null?null:OverlayArt.Sprite(sprite);
    if(sprite!=null)b.Content=art!=null?(object)new Image{Source=art,Stretch=Stretch.Uniform}:new TextBlock{Text=name,Foreground=Cream,FontSize=11,TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center};
    AutomationProperties.SetName(b,name);AutomationProperties.SetAutomationId(b,id);
    b.Click+=delegate{try{click();}catch(Exception){}};
    return b;
  }
  Button MenuRow(string id,string text,Action click){
    var label=new TextBlock{Text=text,Foreground=Cream,FontSize=14,Margin=new Thickness(10,6,10,6),TextWrapping=TextWrapping.Wrap};
    var b=new Button{Content=label,Focusable=false,Cursor=Cursors.Hand,HorizontalContentAlignment=HorizontalAlignment.Left,Padding=new Thickness(0),Template=PlateTemplate(new CornerRadius(6),true),HorizontalAlignment=HorizontalAlignment.Stretch};
    AutomationProperties.SetAutomationId(b,id);b.Click+=delegate{try{click();}catch(Exception){}};
    rowText[id]=label;return b;
  }
  void SetNoActivate(bool on){
    if(winHandle==IntPtr.Zero)return;
    int ex=OverlayNative.GetWindowLong(winHandle,OverlayNative.GWL_EXSTYLE);
    ex=on?(ex|OverlayNative.WS_EX_NOACTIVATE|OverlayNative.WS_EX_TOOLWINDOW):((ex&~OverlayNative.WS_EX_NOACTIVATE)|OverlayNative.WS_EX_TOOLWINDOW);
    OverlayNative.SetWindowLong(winHandle,OverlayNative.GWL_EXSTYLE,ex);noActivate=on;
  }
  // A click on the overlay must not make it the foreground window: the game reads keys only while it is.
  IntPtr WindowHook(IntPtr hwnd,int msg,IntPtr wparam,IntPtr lparam,ref bool handled){
    if(msg==WM_MOUSEACTIVATE&&noActivate){handled=true;return new IntPtr(MA_NOACTIVATE);}
    return IntPtr.Zero;
  }
  IntPtr HotkeyHook(IntPtr hwnd,int msg,IntPtr wparam,IntPtr lparam,ref bool handled){
    if(msg==WM_HOTKEY&&wparam.ToInt64()==HotkeyId){handled=true;hotkeyPending=true;if(!disposed)Step(lastNow);}
    return IntPtr.Zero;
  }

  // ---- the hotkey ----
  bool RegisterHotkey(){
    UnregisterHotkey();hotkeyProblem="";
    if(disposed||hotkeySource==null)return false;
    // The saved key is checked against the game's key map every time it is registered: the player may have mapped it since.
    string rule=HotkeyRules.Check(settings.Mods,settings.Vk,GameKeys());
    if(rule.Length>0){hotkeyProblem=settings.HotkeyText+" cannot be the overlay key now: "+rule;return false;}
    // HotkeyRules.Alt, Ctrl and Shift have the same values as Windows' MOD_ALT, MOD_CONTROL and MOD_SHIFT; 0x4000 is MOD_NOREPEAT.
    registered=OverlayNative.RegisterHotKey(hotkeySource.Handle,HotkeyId,(uint)settings.Mods|0x4000u,(uint)settings.Vk);
    if(!registered)hotkeyProblem=settings.HotkeyText+" is already used by another program, so the overlay key does not work. Choose another in the menu.";
    return registered;
  }
  void UnregisterHotkey(){if(registered&&hotkeySource!=null)OverlayNative.UnregisterHotKey(hotkeySource.Handle,HotkeyId);registered=false;}
  IList<int> GameKeys(){try{return host.GameKeyCodes==null?null:host.GameKeyCodes();}catch(Exception){return null;}}

  // ---- the controls ----
  void OnStop(){
    long now=lastNow;
    if(now<stopArmedUntil){stopArmedUntil=0;SetStopArmed(false);if(host.StopGame!=null)host.StopGame();state.Close();Apply(lastGame,now);return;}
    stopArmedUntil=now+StopArmMs;SetStopArmed(true);
  }
  void SetStopArmed(bool armed){
    if(stopButton==null)return;
    SetPlate(stopButton,armed?ArmFill:null,armed?Seam:null); // the button keeps its icon and gets a red plate; the words go in the caption under the pill
    string name=armed?"Click again to stop the session":"Stop session";
    AutomationProperties.SetName(stopButton,name);stopButton.ToolTip=name;
    TextBlock sr;if(rowText.TryGetValue("ses-stop",out sr))sr.Text=armed?"Click again to stop\u2026":"Stop session\u2026";
    Refresh();
  }
  // Holds a fill and edge on a pill button's plate (null clears them), like SetSelected but in any colour.
  static void SetPlate(Button b,Brush fill,Brush edge){
    if(b==null)return;
    b.ApplyTemplate();var plate=b.Template.FindName("plate",b) as Border;if(plate==null)return;
    if(fill!=null){plate.Background=fill;plate.BorderBrush=edge;}else{plate.ClearValue(Border.BackgroundProperty);plate.ClearValue(Border.BorderBrushProperty);}
  }
  void OnMusic(){if(host.ToggleMusic!=null)host.ToggleMusic();UpdateMusicArt(true);}
  void OnMenu(){long now=lastNow;if(state.Mode==OverlayMode.Menu)state.CloseMenu(now);else state.OpenMenu(now);Apply(lastGame,now);}
  void ToggleFullScreenOnly(){
    settings.FullScreenOnly=!settings.FullScreenOnly;
    try{settings.Save(settingsPath);keysNote="";}catch(Exception){keysNote="That choice could not be saved, so it applies until the launcher closes.";}
    Refresh();
  }
  void UpdateMusicArt(bool force){
    bool playing;try{playing=host.MusicPlaying!=null&&host.MusicPlaying();}catch(Exception){playing=false;}
    if(!force&&playing==musicShown)return;
    musicShown=playing;SetTick("aud-play",playing);
  }

  // The mouse points at a button while keyboard control is on: the highlight follows it, so the keys continue from there.
  void NavSelect(Button b){
    if(b==null||!navActive||!b.IsEnabled)return;
    int i=pillList.IndexOf(b);
    if(i>=0){navLevel=0;navPill=i;return;}
    if(state.Mode!=OverlayMode.Menu)return;
    for(int k=0;k<OverlayMenus.Names.Length;k++)if(mainRows[OverlayMenus.Names[k]]==b){navLevel=1;navMain=k;return;}
    var s=SubButtons();i=s.IndexOf(b);
    if(i>=0){navLevel=2;navSub=i;}
  }
  public void NavPointAt(string automationId){ // tests only: what the mouse move handler does when the pointer is over this button
    var stack=new Stack<DependencyObject>();stack.Push(Content);
    while(stack.Count>0){var d=stack.Pop();var b=d as Button;if(b!=null&&AutomationProperties.GetAutomationId(b)==automationId){NavSelect(b);if(!disposed)Apply(lastGame,lastNow);return;}foreach(object c in LogicalTreeHelper.GetChildren(d)){var dc=c as DependencyObject;if(dc!=null)stack.Push(dc);}}
  }
  // The overlay window stopped being the active window (the player clicked the game or another program).
  void OnLostActive(){
    if(!disposed&&navActive&&!keysActive){EndNav(false);Refresh();}
    if(!disposed&&keysActive){keysNote=editorOpen?"The overlay lost the keyboard.":"The overlay lost the keyboard, so nothing changed.";EndKeys(false);Refresh();}
  }
  public void SimulateLostActive(){OnLostActive();} // tests only: the real window never becomes active in them

  // ---- keyboard control of the menus (SPEC-overlay.md, "Keyboard control, 2026-11-19") ----
  // Only when the overlay key opened the overlay: the player is at the keyboard then. The hover bar never takes the keyboard.
  // The overlay becomes the front window, so the game gets no keys until the overlay closes. Esc, the overlay key or an action
  // closes it, and the game window gets the front back. This is separate from the key-choosing mode (keysActive).
  public bool NavActive {get{return navActive;}}
  // The automation id of the button the keyboard highlight is on ("" when there is none).
  public string NavFocusId {get{Button b=NavTarget();return b==null?"":AutomationProperties.GetAutomationId(b);}}
  long returnUntil=-1;
  bool LauncherInFront(){try{return host.LauncherForeground!=null&&host.LauncherForeground();}catch(Exception){return false;}}
  bool HoldsForeground(){return TestForeground.HasValue?TestForeground.Value:OverlayNative.GetForegroundWindow()==winHandle;}
  bool[] PillEnabled(){var a=new bool[pillList.Count];for(int i=0;i<a.Length;i++)a[i]=pillList[i].IsEnabled;return a;}
  List<Button> SubButtons(){
    var l=new List<Button>();Border box;
    if(openSub!=null&&subBoxes.TryGetValue(openSub,out box)){var sp=box.Child as StackPanel;if(sp!=null)foreach(UIElement c in sp.Children){var b=c as Button;if(b!=null)l.Add(b);}}
    return l;
  }
  static bool[] EnabledOf(List<Button> l){var a=new bool[l.Count];for(int i=0;i<a.Length;i++)a[i]=l[i].IsEnabled;return a;}
  Button NavTarget(){
    if(!navActive)return null;
    if(navLevel==0)return navPill>=0&&navPill<pillList.Count?pillList[navPill]:null;
    if(navLevel==1)return navMain>=0&&navMain<OverlayMenus.Names.Length?mainRows[OverlayMenus.Names[navMain]]:null;
    var s=SubButtons();return navSub>=0&&navSub<s.Count?s[navSub]:null;
  }
  void BeginNav(){
    if(disposed||navActive||keysActive||!settings.KeyboardMenu)return;
    navActive=true;navBring=true;navLevel=0;navStarted=lastNow;navMain=0;navSub=-1;
    navPill=Math.Max(0,OverlayNav.Next(PillEnabled(),-1,1));
    SetNoActivate(false); // only now may the overlay take the keyboard from the game
  }
  // Ends keyboard control without touching the focus (the key-choosing mode takes over, or the overlay is going away).
  void DropNav(){if(navActive){navActive=false;navBring=false;if(navRing!=null)navRing.Visibility=Visibility.Collapsed;}}
  void EndNav(bool giveBack){
    if(!navActive)return;
    bool holding=HoldsForeground();
    DropNav();
    if(!keysActive)SetNoActivate(true);
    // The front goes back to the game only if the overlay still holds it: after Alt+Tab to another program it must not be taken back.
    if(giveBack&&holding&&game!=IntPtr.Zero){try{probe.BringToFront(game);}catch(Exception){}}
  }
  static void ClickButton(Button b){if(b!=null&&b.IsEnabled){try{b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}catch(Exception){}}}
  int MainIndexOfOpenSub(){
    if(openSub==null)return navMain;
    int i=Array.IndexOf(OverlayMenus.Names,openSub=="Picture preset"?"View":openSub==GsetPanel?"Session":openSub);return i<0?navMain:i;
  }
  void OpenNavMenu(){
    if(state.Mode==OverlayMode.Bar)state.OpenMenu(lastNow);
    if(state.Mode!=OverlayMode.Menu)return;
    navLevel=1;navMain=MainIndexOfOpenSub();OpenSub(OverlayMenus.Names[navMain]);
  }
  void CloseNavMenu(){state.CloseMenu(lastNow);openSub=null;navLevel=0;}
  void EnterNavSub(){
    var s=SubButtons();int f=OverlayNav.Next(EnabledOf(s),-1,1);
    if(f>=0){navLevel=2;navSub=f;}
  }
  void BackToMain(){navLevel=1;navMain=MainIndexOfOpenSub();OpenSub(OverlayMenus.Names[navMain]);}
  bool HandleNavKey(Key key){
    if(key==Key.LeftCtrl||key==Key.RightCtrl||key==Key.LeftAlt||key==Key.RightAlt||key==Key.LeftShift||key==Key.RightShift||key==Key.LWin||key==Key.RWin||key==Key.None||key==Key.System||key==Key.ImeProcessed)return true;
    if(state.Mode!=OverlayMode.Menu&&navLevel>0)navLevel=0; // the mouse closed the menu
    else if(state.Mode==OverlayMode.Menu&&navLevel==0){navLevel=1;navMain=MainIndexOfOpenSub();} // the mouse opened it: keys act on the menu
    if(navLevel==2&&OverlayNav.Next(EnabledOf(SubButtons()),-1,1)<0)navLevel=1;
    switch(key){
      case Key.Escape:
        if(navLevel==2)BackToMain();else if(navLevel==1)CloseNavMenu();else{state.Close();}
        break;
      case Key.Left:
        if(navLevel==2)BackToMain();else if(navLevel==1)CloseNavMenu();else{int n=OverlayNav.Next(PillEnabled(),navPill,-1);if(n>=0)navPill=n;}
        break;
      case Key.Right:
        if(navLevel==0){int n=OverlayNav.Next(PillEnabled(),navPill,1);if(n>=0)navPill=n;}else if(navLevel==1)EnterNavSub();
        break;
      case Key.Down:
        if(navLevel==0)OpenNavMenu();
        else if(navLevel==1){navMain=(navMain+1)%OverlayMenus.Names.Length;OpenSub(OverlayMenus.Names[navMain]);}
        else{var s=SubButtons();int n=OverlayNav.Next(EnabledOf(s),navSub,1);if(n>=0)navSub=n;}
        break;
      case Key.Up:
        if(navLevel==1){navMain=(navMain+OverlayMenus.Names.Length-1)%OverlayMenus.Names.Length;OpenSub(OverlayMenus.Names[navMain]);}
        else if(navLevel==2){var s=SubButtons();int n=OverlayNav.Next(EnabledOf(s),navSub,-1);if(n>=0)navSub=n;}
        break;
      case Key.Enter:case Key.Space:
        if(navLevel==0){
          Button b=NavTarget();
          if(b!=null&&b==menuButton){if(state.Mode==OverlayMode.Menu)CloseNavMenu();else OpenNavMenu();}else ClickButton(b);
        }
        else if(navLevel==1)EnterNavSub();
        else{
          string before=openSub;ClickButton(NavTarget());
          if(openSub!=before&&!disposed&&navActive)EnterNavSub(); // the row opened another panel (Game settings, Picture preset): go into it
        }
        break;
    }
    if(!disposed)Apply(lastGame,lastNow);
    return true;
  }
  void UpdateRing(){
    if(navRing==null)return;
    Button b=NavTarget();
    if(b==null||!b.IsVisible){navRing.Visibility=Visibility.Collapsed;return;}
    try{
      stage.UpdateLayout();
      Point p=b.TransformToAncestor(stage).Transform(new Point(0,0));
      navRing.Width=b.ActualWidth;navRing.Height=b.ActualHeight;Canvas.SetLeft(navRing,p.X);Canvas.SetTop(navRing,p.Y);navRing.Visibility=Visibility.Visible;
    }catch(Exception){navRing.Visibility=Visibility.Collapsed;}
  }

  // ---- the keyboard mode: used to choose the overlay key (the key editor will use the same mode) ----
  void BeginKeys(){
    if(state.Mode!=OverlayMode.Menu)return;
    DropNav();state.EnterKeys();keysActive=true;keysStarted=lastNow;
    UnregisterHotkey();SetNoActivate(false); // only now may the overlay take the keyboard from the game
    keysNote="Press the new overlay key combination now. Esc cancels.";
    Refresh();
    try{probe.BringToFront(winHandle);}catch(Exception){}
  }
  void EndKeys(bool giveBackToGame){
    if(disposed)return;
    bool wasEditor=editorOpen;
    keysActive=false;editorOpen=false;capturing=null;
    if(wasEditor){bool dirty;try{dirty=host.KeysDirty!=null&&host.KeysDirty();}catch(Exception){dirty=false;} if(dirty)keysNote=(keysNote.Length>0?keysNote+" ":"")+"Unsaved key changes are still waiting in the launcher: open Game keys and press Save keys, or Discard.";}SetNoActivate(true);
    if(giveBackToGame&&game!=IntPtr.Zero){try{probe.BringToFront(game);}catch(Exception){}}
    RegisterHotkey();
    state.LeaveKeys(lastNow);
  }
  // Called for every key pressed while the overlay has the keyboard. Public so tests can feed it keys without real input.
  public bool HandleKey(Key key,ModifierKeys mods,bool repeat){
    if(!disposed&&navActive&&!keysActive&&state.Mode!=OverlayMode.Keys){
      if(repeat&&key!=Key.Up&&key!=Key.Down&&key!=Key.Left&&key!=Key.Right)return true;
      return HandleNavKey(key);
    }
    if(disposed||state.Mode!=OverlayMode.Keys||!keysActive)return false;
    if(editorOpen)return HandleEditorKey(key,repeat);
    if(repeat)return true;
    if(key==Key.Escape){keysNote="Overlay key unchanged.";EndKeys(true);Refresh();return true;}
    if(key==Key.LeftCtrl||key==Key.RightCtrl||key==Key.LeftAlt||key==Key.RightAlt||key==Key.LeftShift||key==Key.RightShift||key==Key.LWin||key==Key.RWin||key==Key.None||key==Key.System||key==Key.ImeProcessed)return true;
    int vk=KeyInterop.VirtualKeyFromKey(key);
    int m=((mods&ModifierKeys.Control)!=0?HotkeyRules.Ctrl:0)|((mods&ModifierKeys.Alt)!=0?HotkeyRules.Alt:0)|((mods&ModifierKeys.Shift)!=0?HotkeyRules.Shift:0);
    string problem=(mods&ModifierKeys.Windows)!=0?"Do not use the Windows key.":HotkeyRules.Check(m,vk,GameKeys());
    if(problem.Length>0){keysNote=problem;Refresh();return true;}
    int oldMods=settings.Mods,oldKey=settings.Vk;settings.Mods=m;settings.Vk=vk;
    if(!RegisterHotkey()){
      // Windows refused it (another program holds it). Keep the old choice; it is registered again when this mode ends.
      keysNote=HotkeyRules.Format(m,vk)+" is already used by another program. Choose another.";
      settings.Mods=oldMods;settings.Vk=oldKey;hotkeyProblem="";Refresh();return true;
    }
    try{settings.Save(settingsPath);keysNote="Overlay key is now "+settings.HotkeyText+".";}
    catch(Exception){keysNote="Overlay key is now "+settings.HotkeyText+", but it could not be saved, so it lasts until the launcher closes.";}
    EndKeys(true);Refresh();return true;
  }

  // ---- the key editor (SPEC-keys.md, SPEC-overlay.md): edits the launcher's draft through the host, never a file ----
  void BuildEditor(){
    var panel=new StackPanel{Orientation=Orientation.Vertical,Width=400};
    panel.Children.Add(new TextBlock{Text="Game keys",Foreground=Cream,FontSize=15,FontWeight=FontWeights.Bold,Margin=new Thickness(10,6,10,2)});
    var grid=new System.Windows.Controls.Primitives.UniformGrid{Columns=2};
    foreach(string each in KeyMap.Buttons){
      string button=each;
      var label=new TextBlock{Foreground=Cream,FontSize=14,Margin=new Thickness(8,4,8,4)};
      var b=new Button{Content=label,Focusable=false,Cursor=Cursors.Hand,HorizontalContentAlignment=HorizontalAlignment.Left,Padding=new Thickness(0),Template=PlateTemplate(new CornerRadius(6),true),HorizontalAlignment=HorizontalAlignment.Stretch};
      AutomationProperties.SetAutomationId(b,"overlay-key-"+button);
      b.Click+=delegate{try{capturing=capturing==button?null:button;editorStatus.Text=capturing==null?"No change.":"Press the key for "+KeyMap.ButtonLabel(button)+" now. Esc cancels.";RefreshEditor();}catch(Exception){}};
      keyText[button]=label;grid.Children.Add(b);
    }
    panel.Children.Add(grid);
    editorWarning=new TextBlock{Foreground=Amber,FontSize=13,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(10,4,10,0)};
    editorStatus=new TextBlock{Foreground=Cream,FontSize=13,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(10,4,10,4)};
    AutomationProperties.SetAutomationId(editorStatus,"overlay-keys-status");
    panel.Children.Add(editorWarning);panel.Children.Add(editorStatus);
    var footer=new WrapPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(4,0,4,2)};
    editorSave=MenuRow("overlay-keys-save","Save keys",delegate{editorStatus.Text=Safe(host.SaveKeys);capturing=null;RefreshEditor();});
    editorDiscard=MenuRow("overlay-keys-discard","Discard changes",delegate{editorStatus.Text=Safe(host.DiscardKeys);capturing=null;RefreshEditor();});
    var defaults=MenuRow("overlay-keys-defaults","Default keys",delegate{editorStatus.Text=Safe(host.DefaultKeys);capturing=null;RefreshEditor();});
    var back=MenuRow("overlay-keys-back","Back",CloseEditor);
    foreach(var f in new[]{editorSave,editorDiscard,defaults,back}){f.Margin=new Thickness(0,0,6,0);footer.Children.Add(f);}
    panel.Children.Add(footer);
    editorBox=new Border{Background=Panel,BorderBrush=Edge,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(13),Padding=new Thickness(4),Child=panel,Visibility=Visibility.Collapsed};
    stage.Children.Add(editorBox);
  }
  static string Safe(Func<string> call){try{return call==null?"":call()??"";}catch(Exception){return "That did not work.";}}
  KeyMap Draft(){try{return host.KeyDraft==null?null:host.KeyDraft();}catch(Exception){return null;}}
  void RefreshEditor(){
    var draft=Draft();
    if(draft!=null)foreach(string b in KeyMap.Buttons)keyText[b].Text=KeyMap.ButtonLabel(b)+":  "+(capturing==b?"press a key...":draft.Describe(b));
    bool dirty;try{dirty=host.KeysDirty!=null&&host.KeysDirty();}catch(Exception){dirty=false;}
    editorSave.IsEnabled=draft!=null&&dirty&&draft.Missing().Count==0;
    editorDiscard.IsEnabled=dirty&&host.DiscardKeys!=null;
    string warn=Safe(host.KeyWarning);editorWarning.Text=warn;editorWarning.Visibility=warn.Length>0?Visibility.Visible:Visibility.Collapsed;
  }
  void BeginEditor(){
    if(state.Mode!=OverlayMode.Menu)return;
    DropNav();state.EnterKeys();keysActive=true;editorOpen=true;capturing=null;keysStarted=lastNow;
    UnregisterHotkey();SetNoActivate(false); // the editor needs the keyboard; the game does not act on keys while the overlay is in front
    editorStatus.Text="Click a button, then press the key you want. Changes apply the next time you press Play.";
    Refresh();
    try{probe.BringToFront(winHandle);}catch(Exception){}
  }
  void CloseEditor(){if(editorOpen){keysNote="";EndKeys(true);Refresh();}}
  // Keys while the editor has the keyboard. Nothing happens until a button is clicked; Esc cancels a wait, then closes.
  bool HandleEditorKey(Key key,bool repeat){
    if(repeat)return true;
    if(key==Key.Escape){if(capturing!=null){capturing=null;editorStatus.Text="No change.";RefreshEditor();}else CloseEditor();return true;}
    if(capturing==null||key==Key.None)return true;
    string name=KeyMap.FromVirtualKey(KeyInterop.VirtualKeyFromKey(key));
    if(name==null){editorStatus.Text="That key cannot be used here. The game keeps Tab, F11 and F12 for itself, and a few special keys cannot be read.";return true;}
    string button=capturing;capturing=null;
    try{editorStatus.Text=host.EditKey(button,name)??"";}catch(Exception){editorStatus.Text="That key could not be set.";}
    RefreshEditor();return true;
  }

  // ---- one tick ----
  public void Step(long now){
    if(disposed)return;
    lastNow=now;
    try{StepCore(now);errors=0;}
    catch(Exception e){errors++;Trace("ERROR "+errors+": "+e);if(errors>=5){Trace("disposing after five errors in a row");Dispose();}} // a broken overlay must never disturb the game or the launcher
  }
  void StepCore(long now){
    UpdateScale(); // also while hidden: the hover band's depth depends on the screen scale, and the first show must not use a stale 100 percent
    if(game!=IntPtr.Zero&&!OverlayNative.IsWindow(game))game=IntPtr.Zero;
    if(game==IntPtr.Zero&&now-lastFind>=500){lastFind=now;game=probe.FindGame();}
    var o=new OverlayObservation();PxRect rect=new PxRect();
    if(game!=IntPtr.Zero&&probe.ClientRect(game,out rect)&&!rect.IsEmpty){
      o.GameFound=true;o.Game=rect;lastGame=rect;o.GameForeground=probe.GameIsForeground(game)||(host.PicturePresentationForeground!=null&&host.PicturePresentationForeground());
      o.OverlayForeground=(state.Mode==OverlayMode.Keys&&OverlayNative.GetForegroundWindow()==winHandle)||(navActive&&(HoldsForeground()||now-navStarted<=KeyboardWaitMs))||(launcherPicker&&host.LauncherForeground!=null&&host.LauncherForeground());
      if(now-monitorsAt>=2000){monitorsAt=now;monitors=probe.Monitors();}
      o.FullScreen=OverlayGeometry.IsFullScreen(rect,monitors);
      int x,y;o.CursorKnown=probe.Cursor(out x,out y);o.CursorX=x;o.CursorY=y;
      o.Zone=OverlayGeometry.HotZone(rect,(int)Math.Ceiling(pillWidthDip*scale),scale);
      o.Ui=shown?lastUi:new PxRect();
    }
    state.FullScreenOnly=FullScreenOnlyWorks&&settings.FullScreenOnly;state.HoverEnabled=settings.HoverBar;
    if(hotkeyPending){
      hotkeyPending=false;bool wasHidden=state.Mode==OverlayMode.Hidden;bool gameFront=o.GameForeground;
      // The player is in the launcher (About, Setup guide, Return to launcher closed the overlay): the overlay key brings the game back to the front and opens the overlay.
      // Only from the launcher itself, never from another program, so the key still cannot pull a game forward while the player works elsewhere.
      if(!gameFront&&!state.Visible&&o.GameFound&&game!=IntPtr.Zero&&LauncherInFront()){try{probe.BringToFront(game);}catch(Exception){}returnUntil=now+KeyboardWaitMs;gameFront=true;}
      if(state.Visible||o.GameFound)state.Hotkey(gameFront,now);
      if(wasHidden&&state.Mode==OverlayMode.Bar&&state.Pinned)BeginNav(); // opened with the key: the player is at the keyboard
    }
    if(now<=returnUntil)o.GameForeground=true; // Windows may take a moment to put the game in front after the key
    state.Update(o,now);if(state.Mode==OverlayMode.Hidden)launcherPicker=false;
    if(state.Mode==OverlayMode.Hidden)liveStateAt=-100000;
    else if(host.GameState!=null&&LiveOn("pause")&&now-liveStateAt>=500){liveStateAt=now;PollGameState(now);}
    if(!String.IsNullOrEmpty(tracePath)){
      if(game!=traceGame||!rect.SameAs(traceRect)){traceGame=game;traceRect=rect;Trace("game window 0x"+((long)game).ToString("X")+" picture "+rect+" fullScreen="+o.FullScreen);}
      if(o.GameFound&&o.GameForeground!=traceFg){traceFg=o.GameForeground;Trace("game in front="+traceFg+(traceFg?"":" (foreground is "+ForegroundInfo()+")"));}
      if(state.Mode!=traceMode){Trace("mode "+traceMode+" -> "+state.Mode+" cursor="+o.CursorX+","+o.CursorY+" known="+o.CursorKnown+" zone="+o.Zone+" ui="+o.Ui+" pinned="+state.Pinned);traceMode=state.Mode;}
    }
    if(keysActive&&state.Mode==OverlayMode.Keys&&!win.IsActive&&now-keysStarted>KeyboardWaitMs){keysNote=editorOpen?"The overlay could not take the keyboard.":"The overlay could not take the keyboard, so nothing changed.";EndKeys(false);}
    if(navActive&&state.Mode!=OverlayMode.Keys&&!HoldsForeground()&&now-navStarted>KeyboardWaitMs)EndNav(false); // the overlay could not get the keyboard: the mouse menu still works, no note
    if(stopArmedUntil!=0&&now>=stopArmedUntil){stopArmedUntil=0;SetStopArmed(false);}
    if(restartArmedUntil!=0&&now>=restartArmedUntil){restartArmedUntil=0;SetRestartArmed(false);}
    Apply(rect,now);
    if(navBring&&navActive&&shown){navBring=false;try{probe.BringToFront(winHandle);}catch(Exception){}}
  }
  // Brings the window in line with the state: hidden, or shown at the top centre of the game picture.
  void Apply(PxRect gameRect,long now){
    if(disposed)return;
    if(state.Mode==OverlayMode.Hidden){
      if(keysActive)EndKeys(false);
      if(navActive)EndNav(true);
      if(fit!=1){fit=1;stage.LayoutTransform=Transform.Identity;}
      if(shown){win.Visibility=Visibility.Hidden;shown=false;lastUi=new PxRect();stopArmedUntil=0;SetStopArmed(false);restartArmedUntil=0;SetRestartArmed(false);}
      return;
    }
    if(gameRect.IsEmpty)return;
    UpdateScale();
    // The pill's place is fixed first, from the game picture alone: it never moves when menus open or close.
    // A small game picture (often a small window at a high screen scale) can be shorter than an open menu: then the whole overlay shrinks
    // until it fits (never below 0.6) and stays that size until it closes. The height is the layout just made, in dip at scale 1.
    Refresh(); // lay the current panels out first, so the fit below is not one tick late
    double nowFit=OverlayGeometry.FitFactor(gameRect.Height/scale,stage.Height);
    if(nowFit<fit-0.0001){fit=nowFit;stage.LayoutTransform=new ScaleTransform(fit,fit);}
    double eff=scale*fit;
    PxRect pillAt=OverlayGeometry.PlaceBar(gameRect,(int)Math.Ceiling(PillW*eff),(int)Math.Ceiling(PillH*eff),scale);
    availDip=Math.Max(PillW,(gameRect.Right-pillAt.Left)/eff);
    Refresh();UpdateMusicArt(false);
    if(!shown)win.Visibility=Visibility.Visible;
    shown=true;UpdateScale(); // every time: the game may have moved to a screen with another scale
    // The content's own size, not the window's: the window has not been through a layout pass on its first show.
    var content=Content;content.Measure(new Size(Double.PositiveInfinity,Double.PositiveInfinity));
    int w=(int)Math.Ceiling(content.DesiredSize.Width*scale),h=(int)Math.Ceiling(content.DesiredSize.Height*scale);
    win.UpdateLayout();UpdateRing();
    // The window's top left is the pill's top left; the menus hang right and down from it.
    // Position only: the window sizes itself to its content. TOPMOST again each time keeps it above a topmost game window.
    OverlayNative.SetWindowPos(winHandle,OverlayNative.HWND_TOPMOST,pillAt.Left,pillAt.Top,0,0,OverlayNative.SWP_NOSIZE|OverlayNative.SWP_NOACTIVATE);
    lastUi=new PxRect(pillAt.Left,pillAt.Top,pillAt.Left+w,pillAt.Top+h);
  }
  static string ForegroundInfo(){
    try{IntPtr h=OverlayNative.GetForegroundWindow();uint pid;OverlayNative.GetWindowThreadProcessId(h,out pid);var cls=new StringBuilder(80);OverlayNative.GetClassName(h,cls,80);return "pid "+pid+" class "+cls;}catch(Exception){return "unknown";}
  }
  void UpdateScale(){
    if(TestScale.HasValue){scale=TestScale.Value;return;} // tests only
    var src=HwndSource.FromHwnd(winHandle);
    if(src!=null&&src.CompositionTarget!=null){double s=src.CompositionTarget.TransformToDevice.M11;if(s>0)scale=s;}
  }
  void Refresh(){
    bool menu=state.Mode==OverlayMode.Menu||(state.Mode==OverlayMode.Keys&&!editorOpen);
    bool ed=editorOpen&&state.Mode==OverlayMode.Keys;
    if(!menu)openSub=null;
    if(!menu&&!ed&&keysNote.Length>0&&!keysActive)keysNote=""; // a note belongs to the menu it was shown in; it does not wait for the next time
    if(keysActive&&!editorOpen)openSub="Help"; // the overlay key row lives in Help
    if(editorOpen)RefreshEditor();
    rowText["overlay-hotkey"].Text=keysActive?"Overlay key: press a combination...":"Overlay key: "+settings.HotkeyText+"  (change)";
    SetTick("overlay-fullscreen-only",FullScreenOnlyWorks&&settings.FullScreenOnly);SetTick("overlay-hover",settings.HoverBar);SetTick("overlay-keyboard",settings.KeyboardMenu);SetTick("aud-play",musicShown);
    {bool muted=false;try{muted=host.MusicMuted!=null&&host.MusicMuted();}catch(Exception){}SetTick("aud-mute",muted);}
    SetTick("aud-game-mute",gameMuted);SetTick("view-speed",unlimited);
    RefreshBars();
    if(host.ApplyPicture!=null){
      string selected=host.PictureSelected==null?"":host.PictureSelected();
      SetTick("picture-balanced",selected=="balanced");SetTick("picture-performance",selected=="performance");
      rowText["picture-status"].Text=host.PictureStatus==null?"Choose a picture preset.":host.PictureStatus();
    }
    if(menu&&openSub==GsetPanel&&subBoxes.ContainsKey(GsetPanel))RefreshGameSettings();
    foreach(KeyValuePair<string,Button> kv in mainRows)SetSelected(kv.Value,menu&&(openSub==kv.Key||(kv.Key=="View"&&openSub=="Picture preset")||(kv.Key=="Session"&&openSub==GsetPanel)));
    SetSelected(menuButton,menu);
    LayoutStage(menu,ed);
    AutomationProperties.SetName(menuBox,"Overlay menu");
  }
  // Puts each visible part where the kit puts it and sizes the stage to hold them all. The window follows the stage.
  void LayoutStage(bool menu,bool ed){
    double w=PillW,h=PillH,bottom=PillH;
    double mLeft=OverlayGeometry.MenuLeftFor(availDip,MenuW,SubW,10,MenuLeft,openSub!=null);
    menuBox.Visibility=menu?Visibility.Visible:Visibility.Collapsed;menuTab.Visibility=menuBox.Visibility;
    editorBox.Visibility=ed?Visibility.Visible:Visibility.Collapsed;
    foreach(KeyValuePair<string,Border> kv in subBoxes)kv.Value.Visibility=menu&&openSub==kv.Key?Visibility.Visible:Visibility.Collapsed;
    if(menu){
      Put(menuBox,mLeft,MenuTop,ref w,ref h);Canvas.SetLeft(menuTab,mLeft+18);Canvas.SetTop(menuTab,MenuTop-2);bottom=MenuTop+menuBox.DesiredSize.Height;
      if(openSub!=null){int idx=openSub==GsetPanel?0:Array.IndexOf(OverlayMenus.Names,openSub=="Picture preset"?"View":openSub);Put(subBoxes[openSub],mLeft+MenuW+10,openSub==GsetPanel?MenuTop:MenuTop+8+idx*RowPitch,ref w,ref h);} // the settings panel is tall, so it starts level with the top of the menu
    }
    if(ed){Put(editorBox,0,MenuTop,ref w,ref h);bottom=MenuTop+editorBox.DesiredSize.Height;}
    string note=Note;noteText.Text=note;
    if(note.Length>0&&(menu||ed)){
      // beside the menu, under the pill, where nothing else is drawn; below the menu when there is no room on the left
      bool side=menu&&mLeft>=130;
      noteBox.Width=side?mLeft-10:(menu?MenuW:SubW);noteBox.Visibility=Visibility.Visible;
      Put(noteBox,side||!menu?0:mLeft,side?MenuTop:bottom+8,ref w,ref h);
    }
    else noteBox.Visibility=Visibility.Collapsed;
    bool sa=stopArmedUntil!=0,ra=restartArmedUntil!=0;
    if(sa||ra){
      // under the armed button: the buttons are 48 wide, 4 apart, starting 22 in (pause, restart, stop, fullscreen, menu)
      confirmText.Text=sa?"Stop session? Click again.":"Restart session? Click again.";
      confirmBox.Visibility=Visibility.Visible;confirmBox.Measure(new Size(Double.PositiveInfinity,Double.PositiveInfinity));
      double cx=sa?150:98;
      Put(confirmBox,Math.Max(0,cx-confirmBox.DesiredSize.Width/2),PillH+8,ref w,ref h);
    }else confirmBox.Visibility=Visibility.Collapsed;
    stage.Width=w;stage.Height=h;
  }
  static void Put(FrameworkElement e,double left,double top,ref double w,ref double h){
    e.Measure(new Size(Double.PositiveInfinity,Double.PositiveInfinity));Canvas.SetLeft(e,left);Canvas.SetTop(e,top);
    w=Math.Max(w,left+e.DesiredSize.Width);h=Math.Max(h,top+e.DesiredSize.Height);
  }

  public void Dispose(){
    if(disposed)return;disposed=true;Trace("disposed");
    try{if(timer!=null)timer.Stop();}catch(Exception){}
    try{UnregisterHotkey();}catch(Exception){}
    try{if(hotkeySource!=null)hotkeySource.Dispose();}catch(Exception){}
    try{if(win!=null)win.Close();}catch(Exception){}
  }
}
