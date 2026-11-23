using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

// Window-level test of GameOverlay.cs. A stand-in "game" window of the game's window class is parked off screen;
// the cursor and the foreground state come from a fake probe. NO real mouse or keyboard input is used, nothing is
// read from or written to the launcher's settings folder, and the launcher itself is not started. Buttons are driven
// through in-process automation peers, not through a UI Automation client.
public static class OverlayHostTest {
  static int run,failed;
  static void Check(bool ok,string what){run++;if(!ok){failed++;Console.WriteLine("FAIL: "+what);}}
  static void Info(string text){Console.WriteLine("INFO: "+text);}

  delegate IntPtr WndProc(IntPtr h,uint msg,IntPtr w,IntPtr l);
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct WNDCLASS {public uint style;public WndProc proc;public int cbClsExtra,cbWndExtra;public IntPtr hInstance,hIcon,hCursor,hbrBackground;[MarshalAs(UnmanagedType.LPWStr)] public string menu;[MarshalAs(UnmanagedType.LPWStr)] public string name;}
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern ushort RegisterClassW(ref WNDCLASS wc);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr CreateWindowExW(int ex,string cls,string title,uint style,int x,int y,int w,int h,IntPtr parent,IntPtr menu,IntPtr inst,IntPtr param);
  [DllImport("user32.dll")] static extern IntPtr DefWindowProcW(IntPtr h,uint m,IntPtr w,IntPtr l);
  [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr h);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out OverlayNative.RECT r);
  [DllImport("user32.dll")] static extern IntPtr SendMessageW(IntPtr h,uint m,IntPtr w,IntPtr l);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  static WndProc keep;

  sealed class TestProbe : IGameProbe {
    readonly Win32Probe real;
    public int X,Y;public bool Foreground=true;public PxRect[] Screens=new PxRect[0];public List<IntPtr> Fronted=new List<IntPtr>();
    public TestProbe(int pid){real=new Win32Probe(pid);}
    public IntPtr FindGame(){return real.FindGame();}
    public bool ClientRect(IntPtr g,out PxRect r){return real.ClientRect(g,out r);}
    public bool GameIsForeground(IntPtr g){return Foreground;}
    public bool Cursor(out int x,out int y){x=X;y=Y;return true;}
    public PxRect[] Monitors(){return Screens;}
    public void BringToFront(IntPtr hwnd){Fronted.Add(hwnd);} // recorded only: the test never moves the real focus
  }
  static void Pump(){var f=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(delegate{f.Continue=false;}));Dispatcher.PushFrame(f);}
  static T FindById<T>(object root,string id) where T:FrameworkElement{
    var el=root as FrameworkElement;
    if(el!=null&&el is T&&AutomationProperties.GetAutomationId(el)==id)return (T)el;
    var d=root as DependencyObject;if(d==null)return null;
    foreach(object child in LogicalTreeHelper.GetChildren(d)){var found=FindById<T>(child,id);if(found!=null)return found;}
    return null;
  }
  static string NameOf(FrameworkElement e){return UIElementAutomationPeer.CreatePeerForElement(e).GetName();}
  static void Invoke(FrameworkElement e){var peer=UIElementAutomationPeer.CreatePeerForElement(e);((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();Pump();}
  static string Text(FrameworkElement root,string id){var tb=FindById<TextBlock>(root,id+"-text");if(tb!=null)return tb.Text;var b=FindById<Button>(root,id);var t=b==null?null:b.Content as TextBlock;return t==null?"":t.Text;}
  static bool Ticked(FrameworkElement root,string id){var t=FindById<TextBlock>(root,id+"-tick");return t!=null&&t.Visibility==Visibility.Visible;}
  static void Capture(FrameworkElement el,string path){
    el.UpdateLayout();int w=(int)Math.Ceiling(el.ActualWidth)+40,h=(int)Math.Ceiling(el.ActualHeight)+20;
    var dv=new DrawingVisual();using(var dc=dv.RenderOpen()){dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x2E,0x5C,0x3E)),null,new Rect(0,0,w,h));dc.DrawRectangle(new VisualBrush(el),null,new Rect(20,10,el.ActualWidth,el.ActualHeight));}
    var bmp=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32);bmp.Render(dv);
    var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(bmp));using(var f=File.Create(path))enc.Save(f);
  }
  static FrameworkElement rootRL(GameOverlay g){return g.Content;}
  static OverlayNative.RECT RectOf(IntPtr h){OverlayNative.RECT r;GetWindowRect(h,out r);return r;}

  [STAThread] public static int Main(string[] args){
    string scratch=args.Length>0?args[0]:Path.Combine(Path.GetTempPath(),"overlay-host-test");
    if(Directory.Exists(scratch))Directory.Delete(scratch,true);Directory.CreateDirectory(scratch);
    new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
    try{Everything(scratch);}catch(Exception e){failed++;Console.WriteLine("FAIL: unexpected exception "+e);}
    Console.WriteLine((failed==0?"PASS":"FAILED")+": "+run+" overlay window checks, "+failed+" failed");
    return failed==0?0:1;
  }

  static void Everything(string scratch){
    // the stand-in game: the game's window class, a title with the game's prefix, parked far off screen
    keep=delegate(IntPtr h,uint m,IntPtr w,IntPtr l){return DefWindowProcW(h,m,w,l);};
    var wc=new WNDCLASS{proc=keep,name=Win32Probe.GameClass};
    Check(RegisterClassW(ref wc)!=0,"the stand-in game window class registers");
    const int GX=-20000,GY=-20000,GW=1280,GH=720;
    IntPtr gameWindow=CreateWindowExW(0x08000080,Win32Probe.GameClass,Win32Probe.TitlePrefix+" (test stand-in)",0x90000000u,GX,GY,GW,GH,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
    Check(gameWindow!=IntPtr.Zero&&IsWindowVisible(gameWindow),"the stand-in game window exists");
    int pid=Process.GetCurrentProcess().Id;
    var game=new PxRect(GX,GY,GX+GW,GY+GH);
    var probe=new TestProbe(pid){X=GX+GW/2,Y=GY+300,Screens=new[]{new PxRect(0,0,1920,1080)}};
    Check(new Win32Probe(pid).FindGame()==gameWindow,"the probe finds the game window by process id and class");
    Check(new Win32Probe(pid+1).FindGame()==IntPtr.Zero,"a different process id finds nothing");
    PxRect seen;Check(new Win32Probe(pid).ClientRect(gameWindow,out seen)&&seen.SameAs(game),"the probe reads the game picture rectangle ("+seen+")");

    string settingsPath=Path.Combine(scratch,"overlay.txt");
    int stops=0,music=0,nexts=0,restarts=0;bool playing=false,muted=false;string launcherAt="";double vol=0.4,uiScale=1.0;IList<int> gameKeys=new List<int>();
    var host=new OverlayHost{StopGame=delegate{stops++;},ToggleMusic=delegate{music++;playing=!playing;},NextTrack=delegate{nexts++;},MusicPlaying=delegate{return playing;},RestartGame=delegate{restarts++;},ShowLauncher=delegate(string s){launcherAt=s;},MusicVolume=delegate{return vol;},SetMusicVolume=delegate(double v){vol=v;},MusicMuted=delegate{return muted;},ToggleMuteMusic=delegate{muted=!muted;},UiScale=delegate{return uiScale;},StepUiScale=delegate(int d){uiScale=Math.Round(uiScale+0.1*d,1);},GameKeyCodes=delegate{return gameKeys;}};
    // a stand-in for the launcher's key draft: the overlay must edit THIS, through callbacks, and write no file itself
    KeyMap draft=KeyMap.Defaults();bool dirty=false;int saves=0;
    host.KeyDraft=delegate{return draft;};
    host.EditKey=delegate(string b,string k){var others=draft.SetSingle(b,k);dirty=true;string n=KeyMap.ButtonLabel(b)+" is now "+KeyMap.KeyLabel(k)+".";foreach(string x in others)n+=" "+KeyMap.ButtonLabel(x)+" had that key.";return n;};
    host.KeysDirty=delegate{return dirty;};
    host.SaveKeys=delegate{saves++;dirty=false;return "Keys saved.";};
    host.DiscardKeys=delegate{draft=KeyMap.Defaults();dirty=false;return "Changes discarded.";};
    host.DefaultKeys=delegate{draft=KeyMap.Defaults();dirty=true;return "Default keys shown.";};
    host.KeyWarning=delegate{return "";};
    // a stand-in for the launcher's game settings: the overlay may change them only through these callbacks
    var gset=new List<GameSetting>{
      new GameSetting{Id="window-size",Label="Window size",Choices=new[]{"480p","720p","1080p"},Value=1},
      new GameSetting{Id="intro-movie",Label="Opening movie",Value=1},
      new GameSetting{Id="smooth-movement",Label="Smooth movement",Value=1,Enabled=false,Hint="Always on with third-person controls"},
      // seven more, so the panel is as tall as the real one (ten settings)
      new GameSetting{Id="sharp-picture",Label="Sharper picture",Value=1},new GameSetting{Id="vibration",Label="Controller vibration",Value=1},
      new GameSetting{Id="vibration-strength",Label="Vibration strength",Choices=new[]{"0% (off)","25%","50%","75%","100%"},Value=4},
      new GameSetting{Id="third-person",Label="Third-person controls",Value=1},new GameSetting{Id="camera-speed",Label="Camera speed",Choices=new[]{"50%","75%","100%","150%","200%"},Value=2},
      new GameSetting{Id="invert-look",Label="Invert camera up and down",Value=0},new GameSetting{Id="lock-on",Label="Target lock-on",Value=1}};
    int gsetChanges=0;string gsetFail=null;int startSize=1,startMovie=1;
    host.GameSettings=delegate{return gset;};
    host.SetGameSetting=delegate(string id,int v){gsetChanges++;if(gsetFail!=null)return gsetFail;foreach(GameSetting g in gset)if(g.Id==id)g.Value=v;return "";};
    host.GameSettingsPending=delegate{return gset[0].Value!=startSize||gset[1].Value!=startMovie;};
    var o=GameOverlay.Attach(pid,host,new OverlaySettings{KeyboardMenu=false},settingsPath,OverlayFeatures.None,probe,false); // keyboard control is switched on in its own section below
    Check(o.WindowHandle!=IntPtr.Zero&&!o.Visible&&o.Mode==OverlayMode.Hidden&&!IsWindowVisible(o.WindowHandle),"attached: the overlay window exists and is hidden");
    int ex=OverlayNative.GetWindowLong(o.WindowHandle,OverlayNative.GWL_EXSTYLE);
    Check((ex&OverlayNative.WS_EX_NOACTIVATE)!=0&&(ex&OverlayNative.WS_EX_TOOLWINDOW)!=0&&o.NoActivate,"the window is no-activate and a tool window (no taskbar button)");
    Check(SendMessageW(o.WindowHandle,0x0021,IntPtr.Zero,IntPtr.Zero)==new IntPtr(3),"a click on the overlay answers MA_NOACTIVATE");
    if(o.HotkeyRegistered){Check(o.HotkeyProblem.Length==0,"the default Ctrl+Alt+O registered with Windows");}
    else Info("Ctrl+Alt+O could not be registered here (another program holds it): "+o.HotkeyProblem);
    if(o.HotkeyRegistered)Check(!OverlayNative.RegisterHotKey(IntPtr.Zero,0x7001,0x4003u,0x4Fu),"while the overlay holds its hotkey Windows refuses the same combination to others");
    OverlayNative.UnregisterHotKey(IntPtr.Zero,0x7001);

    // hover: outside the zone, then in it
    probe.X=GX+GW/2;probe.Y=GY+300;o.Step(1000);o.Step(1500);Check(!o.Visible,"cursor mid-picture: no bar");
    long frontBefore=(long)OverlayNative.GetForegroundWindow();
    probe.Y=GY+2;o.Step(2000);Check(o.Visible,"cursor at the top edge: the bar appears on the first tick, with no dwell");
    o.Step(2200);Check(o.Visible&&o.Mode==OverlayMode.Bar&&!o.Pinned,"cursor rests at the top edge: the bar appears");
    Pump();
    Check(IsWindowVisible(o.WindowHandle),"the overlay window is visible");
    var ui=o.UiRect;var r=RectOf(o.WindowHandle);
    Check(ui.Width>100&&ui.Height>30,"the bar has a sensible size ("+ui.Width+" by "+ui.Height+" px)");
    Check(Math.Abs((ui.Left+ui.Right)/2-(game.Left+game.Right)/2)<=2,"the bar is centred on the game picture");
    Check(ui.Top>=game.Top&&ui.Top<=game.Top+40&&ui.Bottom<game.Bottom,"the bar sits at the top of the game picture");
    Check(Math.Abs(r.Left-ui.Left)<=1&&Math.Abs(r.Top-ui.Top)<=1&&Math.Abs((r.Right-r.Left)-ui.Width)<=2,"the window really is where the bar rectangle says");
    ex=OverlayNative.GetWindowLong(o.WindowHandle,OverlayNative.GWL_EXSTYLE);
    Check((ex&OverlayNative.WS_EX_NOACTIVATE)!=0&&(ex&8)!=0,"shown: still no-activate, and topmost");
    long front1=(long)OverlayNative.GetForegroundWindow();
    Info("foreground before 0x"+frontBefore.ToString("X")+" after 0x"+front1.ToString("X")+"; overlay 0x"+((long)o.WindowHandle).ToString("X")+" stand-in game 0x"+((long)gameWindow).ToString("X"));
    Check(front1!=(long)o.WindowHandle&&front1!=(long)gameWindow,"showing the bar did not make the overlay (or the stand-in game) the foreground window (observed; this test process was not necessarily in front, so this alone proves little)");
    if(front1!=frontBefore)Info("the foreground window changed during the test to another window (0x"+front1.ToString("X")+", before 0x"+frontBefore.ToString("X")+"); not the overlay, so most likely the owner's own activity");
    var root=o.Content;
    Check(NameOf(FindById<Button>(root,"overlay-stop"))=="Stop session"&&NameOf(FindById<Button>(root,"overlay-menu"))=="Deck menu"&&NameOf(FindById<Button>(root,"overlay-pause"))=="Pause"&&NameOf(FindById<Button>(root,"overlay-restart"))=="Restart session"&&NameOf(FindById<Button>(root,"overlay-fullscreen"))=="Fullscreen","the five buttons carry names for screen readers and automation");
    Check(FindById<Button>(root,"overlay-music")==null,"there is no music button on the pill: Pause, Restart, Stop and Fullscreen are for the game");
    Check(!FindById<Button>(root,"overlay-pause").IsEnabled&&!FindById<Button>(root,"overlay-fullscreen").IsEnabled&&FindById<Button>(root,"overlay-restart").IsEnabled&&FindById<Button>(root,"overlay-stop").IsEnabled&&FindById<Button>(root,"overlay-menu").IsEnabled,"pause and full screen are drawn disabled (the game has no live control); restart (the launcher's own), stop and menu work");
    Capture(root,Path.Combine(scratch,"overlay-bar.png"));

    // leaving
    probe.Y=GY+500;o.Step(3300);Check(o.Visible,"cursor leaves: still shown at 1.1 s (the old 0.9 s rule would already have hidden the bar)");
    o.Step(3500);Pump();Check(!o.Visible&&!IsWindowVisible(o.WindowHandle),"cursor leaves: the bar hides 1.2 s after the cursor last touched the band");

    // stop needs two clicks
    probe.Y=GY+2;o.Step(4000);o.Step(4200);Check(o.Visible,"bar shown again");
    var stop=FindById<Button>(root,"overlay-stop");
    Invoke(stop);Check(stops==0&&NameOf(stop)=="Click again to stop the session","first click on Stop only asks for confirmation");
    Capture(root,Path.Combine(scratch,"overlay-stop-armed.png"));
    o.Step(4300);Check(o.Visible&&stops==0,"the confirmation waits");
    o.Step(4200+3100);Check(NameOf(stop)=="Stop session"&&stops==0,"an unanswered confirmation expires");
    probe.Y=GY+2;o.Step(7400);Check(o.Visible,"bar still shown");
    Invoke(stop);Invoke(stop);Check(stops==1&&!o.Visible,"two clicks end the game once and close the bar");
    // restart: two clicks, the launcher closes and starts the game again
    probe.Y=GY+300;o.Step(7500);probe.Y=GY+2;o.Step(7600);o.Step(7700);Check(o.Visible,"bar shown for the restart check");
    var restart=FindById<Button>(root,"overlay-restart");
    Invoke(restart);Check(restarts==0&&NameOf(restart)=="Click again to restart the session","first click on Restart only asks for confirmation");
    o.Step(7710);Check(FindById<Border>(root,"overlay-confirm").Visibility==Visibility.Visible&&FindById<TextBlock>(root,"overlay-confirm-text").Text=="Restart session? Click again.","a caption under the pill asks for the second click");
    Capture(root,Path.Combine(scratch,"overlay-restart-armed.png"));
    o.Step(7700+3100);Check(NameOf(restart)=="Restart session"&&restarts==0,"an unanswered restart confirmation expires");
    probe.Y=GY+2;o.Step(11000);Check(o.Visible,"bar still shown");
    Invoke(restart);Invoke(restart);Check(restarts==1&&!o.Visible,"two clicks restart once and close the bar");
    probe.Y=GY+300;o.Step(11500);
    // menu: the first menu, then a submenu to its right
    probe.Y=GY+300;o.Step(11900); // closed on purpose: the cursor must leave the band before the bar comes back
    probe.Y=GY+2;o.Step(12000);o.Step(12200);Check(o.Visible,"bar shown for the menu");
    var menu=FindById<Button>(root,"overlay-menu");int barHeight=o.UiRect.Height,barWidth=o.UiRect.Width;
    Invoke(menu);o.Step(12300);Check(o.Mode==OverlayMode.Menu&&o.UiRect.Height>barHeight&&o.OpenSubmenuName==null,"the menu opens below the pill with no submenu yet");
    Check(Math.Abs(o.UiRect.Left+150-(game.Left+game.Right)/2)<=3,"the pill stays centred on the game picture while the menu hangs to its right");
    o.OpenSubmenu("Audio");Check(o.OpenSubmenuName=="Audio"&&o.UiRect.Width>barWidth+150,"pointing at Audio opens its submenu to the right of the menu");
    Check(FindById<Button>(root,"aud-play")==null&&FindById<Button>(root,"aud-mute")==null&&FindById<FrameworkElement>(root,"aud-volume")==null&&FindById<Button>(root,"overlay-next-track")==null,"the overlay has no launcher music controls");
    o.OpenSubmenu("View");Check(o.OpenSubmenuName=="View","View opens in place of Audio");
    var savesRow=FindById<Button>(root,"menu-Saves");savesRow.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=Mouse.MouseEnterEvent,Source=savesRow});
    Check(o.OpenSubmenuName=="Saves","pointing at a menu name (the hover path, not the test call) opens its submenu");
    Check(Math.Abs(o.UiRect.Left+150-(game.Left+game.Right)/2)<=3,"the pill has not moved while submenus open and change");
    o.OpenSubmenu("View");
    Check(Text(root,"view-fullscreen")=="Fullscreen"&&!FindById<Button>(root,"view-fullscreen").IsEnabled&&FindById<Button>(root,"view-fit")!=null&&!FindById<Button>(root,"view-fit").IsEnabled,"the kit's View rows are there, disabled until supported");
    Check(!FindById<Button>(root,"view-screenshot").IsEnabled&&!FindById<Button>(root,"view-preset").IsEnabled&&!FindById<Button>(root,"aud-game-volume").IsEnabled&&!FindById<Button>(root,"aud-game-mute").IsEnabled&&!FindById<Button>(root,"ses-reload-keys").IsEnabled&&!FindById<Button>(root,"ses-status").IsEnabled,"game controls we could offer later (screenshot, picture preset, game volume and mute, reload keys, status) are listed but disabled");
    // the launcher-side controls
    o.OpenSubmenu("View");Check(FindById<TextBlock>(root,"view-scale-value").Text=="100%","the UI scale row shows the launcher's text size");
    Invoke(FindById<Button>(root,"view-scale-plus"));Check(Math.Abs(uiScale-1.1)<0.001&&FindById<TextBlock>(root,"view-scale-value").Text=="110%","UI scale plus steps the launcher's text size up");
    Invoke(FindById<Button>(root,"view-scale-minus"));Invoke(FindById<Button>(root,"view-scale-minus"));Check(Math.Abs(uiScale-0.9)<0.001,"UI scale minus steps it down");
    Invoke(FindById<Button>(root,"view-scale-plus"));
    o.OpenSubmenu("Library");Invoke(FindById<Button>(root,"lib-return"));Check(launcherAt=="Home","Return to launcher asks the launcher to come to the front");
    o.OpenSubmenu("Help");Invoke(FindById<Button>(root,"help-setup"));Check(launcherAt=="Setup","Setup guide opens the launcher's setup screen");
    Invoke(FindById<Button>(root,"help-about"));Check(launcherAt=="Help","About opens the launcher's help screen");
    o.OpenSubmenu("Session");Check(FindById<Button>(root,"ses-restart").IsEnabled&&!FindById<Button>(root,"ses-resume").IsEnabled,"Session > Restart works; Resume stays disabled (the game cannot pause)");
    // game settings: a panel under Session; the launcher keeps the values and Restart applies them
    Check(FindById<Button>(root,"ses-settings").IsEnabled&&Text(root,"ses-settings")=="Game settings\u2026","Session > Game settings is on offer when the launcher gives settings");
    Invoke(FindById<Button>(root,"ses-settings"));o.Step(12310);Check(o.OpenSubmenuName=="Game settings","Game settings opens its panel");
    Check(FindById<TextBlock>(root,"gset-window-size-value").Text=="720p"&&Text(root,"gset-window-size")=="Window size","a choice row shows its label and its current entry");
    Check(Ticked(root,"gset-intro-movie")&&!Ticked(root,"gset-window-size"),"a switch that is on shows a tick");
    Check(!FindById<Button>(root,"gset-smooth-movement").IsEnabled&&NameOf(FindById<Button>(root,"gset-smooth-movement")).Contains("third-person"),"a setting the launcher marks unavailable is greyed out and says why");
    Check(!FindById<Button>(root,"gset-restart").IsEnabled&&FindById<TextBlock>(root,"gset-status").Text.StartsWith("Changes start"),"nothing changed yet: Restart to apply is off");
    Check(Math.Abs(FindById<Button>(root,"gset-window-size").ActualHeight-26)<0.6&&FindById<Button>(root,"gset-back")==null,"settings rows are compact (26 px, they are "+FindById<Button>(root,"gset-window-size").ActualHeight+") and there is no Back row");
    Check(FindById<FrameworkElement>(root,"gset-restart").TranslatePoint(new Point(0,0),FindById<FrameworkElement>(root,"gset-window-size")).Y<0,"Restart to apply is the first row, above the settings");
    Check(FindById<FrameworkElement>(root,"gset-status").TranslatePoint(new Point(0,0),FindById<FrameworkElement>(root,"gset-window-size")).Y>0,"status line is under the settings");
    Check(o.UiRect.Height<=453,"the whole overlay with ten settings open is at most 453 px high, so with the pill's 27 px margin it fits a 480 px picture (it is "+o.UiRect.Height+")");
    Invoke(FindById<Button>(root,"gset-intro-movie"));
    Check(gset[1].Value==0&&gsetChanges==1&&!Ticked(root,"gset-intro-movie"),"a click on a switch flips it through the launcher, not in the overlay");
    Check(FindById<Button>(root,"gset-restart").IsEnabled&&FindById<TextBlock>(root,"gset-status").Text.StartsWith("Saved"),"a change turns Restart to apply on and says it is saved");
    Invoke(FindById<Button>(root,"gset-window-size"));Check(gset[0].Value==2&&FindById<TextBlock>(root,"gset-window-size-value").Text=="1080p","a click on a choice goes to the next entry");
    Invoke(FindById<Button>(root,"gset-window-size"));Check(gset[0].Value==0&&FindById<TextBlock>(root,"gset-window-size-value").Text=="480p","and wraps round after the last");
    Check(!FindById<Button>(root,"gset-smooth-movement").IsEnabled&&gsetChanges==3&&gset[2].Value==1,"a greyed out setting cannot be clicked, and the launcher was asked only for the three real changes");
    gsetFail="Could not keep that.";Invoke(FindById<Button>(root,"gset-intro-movie"));
    Check(FindById<TextBlock>(root,"gset-status").Text=="Could not keep that."&&gset[1].Value==0,"when the launcher refuses a change, its reason is shown");
    gsetFail=null;Invoke(FindById<Button>(root,"gset-intro-movie"));Check(FindById<TextBlock>(root,"gset-status").Text.StartsWith("Saved")&&gset[1].Value==1,"the next good change clears the reason");
    Capture(root,Path.Combine(scratch,"overlay-game-settings.png"));
    int r0=restarts;Invoke(FindById<Button>(root,"gset-restart"));
    Check(restarts==r0&&Text(root,"gset-restart")=="Click again to restart\u2026","Restart to apply asks for a second click, like the pill's Restart");
    o.Step(12310+3100);Check(restarts==r0&&Text(root,"gset-restart")=="Restart to apply changes\u2026","an unanswered confirmation expires");
    o.OpenSubmenu("View");Check(o.OpenSubmenuName=="View","pointing at another menu name leaves the panel");o.OpenSubmenu("Session");
    // the overlay holds no copy: what the launcher says is what the panel shows
    gset[0].Value=1;gset[1].Value=1;o.OpenSubmenu("Game settings");o.Step(12320);
    Check(!FindById<Button>(root,"gset-restart").IsEnabled&&FindById<TextBlock>(root,"gset-status").Text.StartsWith("Changes start"),"back at the starting values: Restart to apply is off again");
    gset[0].Value=2;o.Step(12330);Check(FindById<TextBlock>(root,"gset-window-size-value").Text=="1080p"&&FindById<Button>(root,"gset-restart").IsEnabled,"a value the launcher changed shows on the next redraw, and Restart to apply follows the launcher's answer");
    gset[0].Value=1;o.OpenSubmenu("Session");
    o.OpenSubmenu("View");
    Capture(root,Path.Combine(scratch,"overlay-menu.png"));
    Check(Text(root,"overlay-hotkey")=="Overlay key: Ctrl+Alt+O  (change)"&&!Ticked(root,"overlay-fullscreen-only"),"menu rows show the hotkey and the hover setting");
    Check(!FindById<Button>(root,"overlay-fullscreen-only").IsEnabled&&Text(root,"overlay-fullscreen-only")=="Quick bar only in full screen","Quick bar only in full screen is greyed out (not working right now)");
    Invoke(menu);o.Step(12400);Check(o.Mode==OverlayMode.Bar&&o.OpenSubmenuName==null,"the menu button closes the menu and its submenu");
    // a saved full-screen-only line is ignored while that option is greyed out
    probe.X=GX+GW/2;probe.Y=GY+2;
    SendMessageW(o.HotkeyHandle,0x0312,new IntPtr(0x4F56),IntPtr.Zero);Check(!o.Visible,"the hotkey closes the overlay");
    o.Settings.FullScreenOnly=true;
    probe.Y=GY+300;o.Step(20000);probe.Y=GY+2;o.Step(20100);o.Step(20300);Check(o.Visible,"full-screen-only is ignored while greyed out: a windowed game still gets the hover bar");
    o.Settings.FullScreenOnly=false;
    probe.Screens=new[]{new PxRect(0,0,1920,1080)};
    SendMessageW(o.HotkeyHandle,0x0312,new IntPtr(0x4F56),IntPtr.Zero);Check(!o.Visible,"the hotkey closes it again");
    // quick bar off: hovering does nothing, the hotkey still works
    o.Settings.FullScreenOnly=false;o.Settings.HoverBar=false;
    probe.Y=GY+300;o.Step(40000);probe.Y=GY+2;o.Step(40100);o.Step(40300);Check(!o.Visible,"quick bar off: hovering in the band does not open the bar");
    SendMessageW(o.HotkeyHandle,0x0312,new IntPtr(0x4F56),IntPtr.Zero);Check(o.Visible&&o.Pinned,"quick bar off: the hotkey still opens the bar");
    SendMessageW(o.HotkeyHandle,0x0312,new IntPtr(0x4F56),IntPtr.Zero);Check(!o.Visible,"quick bar off: the hotkey closes it");
    o.Settings.HoverBar=true;
    probe.Y=GY+300;o.Step(40500);probe.Y=GY+2;o.Step(40600);o.Step(40800);Check(o.Visible,"quick bar on again: hovering opens the bar");
    Invoke(FindById<Button>(root,"overlay-menu"));o.Step(40900);o.OpenSubmenu("View");
    Check(Ticked(root,"overlay-hover"),"the Quick bar row shows a tick while it is on");
    Invoke(FindById<Button>(root,"overlay-hover"));
    Check(!o.Settings.HoverBar&&!Ticked(root,"overlay-hover")&&File.ReadAllText(settingsPath).Contains("quick-bar = no"),"the Quick bar row turns it off and saves the choice in the scratch settings file");
    Invoke(FindById<Button>(root,"overlay-hover"));Check(o.Settings.HoverBar&&File.ReadAllText(settingsPath).Contains("quick-bar = yes"),"and back on");
    SendMessageW(o.HotkeyHandle,0x0312,new IntPtr(0x4F56),IntPtr.Zero);Check(!o.Visible,"the hotkey closes the open menu");
    // Restart to apply, from the settings panel: two clicks, then the launcher restarts the game
    probe.Y=GY+300;o.Step(41000);probe.Y=GY+2;o.Step(41100);o.Step(41300);Check(o.Visible,"bar shown for the settings restart");
    Invoke(FindById<Button>(root,"overlay-menu"));o.Step(41400);o.OpenSubmenu("Session");Invoke(FindById<Button>(root,"ses-settings"));
    startSize=0;gset[0].Value=1;o.Step(41450);
    int r1=restarts;Invoke(FindById<Button>(root,"gset-restart"));Invoke(FindById<Button>(root,"gset-restart"));
    Check(restarts==r1+1&&!o.Visible,"two clicks on Restart to apply restart once and close the overlay");
    startSize=1;

    // hotkey message (the message Windows sends; sent here by the test, not by a key press)
    IntPtr hk=o.HotkeyHandle;Check(hk!=IntPtr.Zero,"the hotkey window exists");
    probe.Y=GY+600;
    SendMessageW(hk,0x0312,new IntPtr(0x4F56),IntPtr.Zero);Check(o.Visible&&o.Pinned,"hotkey message opens a pinned bar");
    o.Step(500000);Check(o.Visible,"a pinned bar stays while the mouse is elsewhere");
    SendMessageW(hk,0x0312,new IntPtr(0x4F56),IntPtr.Zero);Check(!o.Visible,"hotkey message closes it");
    probe.Foreground=false;SendMessageW(hk,0x0312,new IntPtr(0x4F56),IntPtr.Zero);Check(!o.Visible,"hotkey message while another program is in front does nothing");
    SendMessageW(hk,0x0312,new IntPtr(0x1234),IntPtr.Zero);Check(!o.Visible,"another message id is ignored");
    probe.Foreground=true;SendMessageW(hk,0x0312,new IntPtr(0x4F56),IntPtr.Zero);Check(o.Visible,"hotkey opens it again");
    probe.Foreground=false;o.Step(600000);Check(!o.Visible,"switching to another program closes even a pinned bar");
    probe.Foreground=true;

    // choosing another overlay key
    SendMessageW(hk,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(700000);
    Invoke(FindById<Button>(root,"overlay-menu"));o.Step(700010);Check(o.Mode==OverlayMode.Menu,"menu open for the key change");
    o.OpenSubmenu("Help");Invoke(FindById<Button>(root,"overlay-hotkey"));
    Check(o.Mode==OverlayMode.Keys&&!o.NoActivate&&!o.HotkeyRegistered,"key change: the overlay may now take the keyboard and the old hotkey is released");
    Check(o.Note.StartsWith("Press the new overlay key"),"key change: it asks for a key");
    o.OpenSubmenu("View");Invoke(FindById<Button>(root,"overlay-hover"));Check(o.Settings.HoverBar&&o.Note.Contains("stays on"),"the quick bar cannot be turned off while the overlay key is not registered (nothing else could open the overlay)");
    Check(probe.Fronted.Count==1&&probe.Fronted[0]==o.WindowHandle,"key change: the overlay asked to be brought to the front, and only then");
    Check(o.HandleKey(Key.LeftCtrl,ModifierKeys.Control,false)&&o.Mode==OverlayMode.Keys,"a modifier alone is ignored");
    Check(o.HandleKey(Key.F11,ModifierKeys.None,false)&&o.Mode==OverlayMode.Keys&&o.Note.Contains("F11"),"F11 is refused with the reason");
    Check(o.HandleKey(Key.O,ModifierKeys.None,false)&&o.Mode==OverlayMode.Keys,"a bare letter is refused");
    Check(o.HandleKey(Key.Tab,ModifierKeys.Control|ModifierKeys.Alt,false)&&o.Mode==OverlayMode.Keys,"Tab is refused");
    Check(o.HandleKey(Key.P,ModifierKeys.Control|ModifierKeys.Windows,false)&&o.Mode==OverlayMode.Keys&&o.Note.Contains("Windows key"),"the Windows key is refused");
    gameKeys=new List<int>{0xA2};
    Check(o.HandleKey(Key.P,ModifierKeys.Control|ModifierKeys.Alt,false)&&o.Mode==OverlayMode.Keys&&o.Note.Contains("Ctrl is one of the game"),"a modifier the game maps is refused");
    gameKeys=new List<int>();
    Check(o.HandleKey(Key.P,ModifierKeys.Control|ModifierKeys.Alt,true)&&o.Mode==OverlayMode.Keys,"a repeated key is ignored");
    Check(o.HandleKey(Key.Escape,ModifierKeys.None,false)&&o.Mode==OverlayMode.Menu&&o.NoActivate&&o.Settings.Vk==0x4F,"Esc cancels and the overlay stops taking the keyboard");
    Check(probe.Fronted.Count==2&&probe.Fronted[1]==gameWindow,"Esc: the foreground is handed back to the game window");
    Check(o.HotkeyRegistered||o.HotkeyProblem.Length>0,"Esc: the old hotkey is registered again (or its problem is reported)");
    Invoke(FindById<Button>(root,"overlay-hotkey"));Check(o.Mode==OverlayMode.Keys,"key change entered again");
    // a combination another program already holds
    bool tookQ=OverlayNative.RegisterHotKey(IntPtr.Zero,0x7002,0x4003u,0x51u);
    if(tookQ){
      Check(o.HandleKey(Key.Q,ModifierKeys.Control|ModifierKeys.Alt,false)&&o.Mode==OverlayMode.Keys&&o.Note.Contains("already used")&&o.Settings.Vk==0x4F,"a combination held by another program is refused and the old choice stays");
      OverlayNative.UnregisterHotKey(IntPtr.Zero,0x7002);
    }else Info("Ctrl+Alt+Q was not free here; the 'held by another program' case was not exercised");
    Check(o.HandleKey(Key.P,ModifierKeys.Control|ModifierKeys.Alt,false)&&o.Mode==OverlayMode.Menu&&o.Settings.Vk==0x50&&o.Settings.Mods==3,"a good combination is accepted and keyboard mode ends");
    Check(o.NoActivate&&o.HotkeyRegistered,"after the change the overlay no-activate again and the new hotkey is registered");
    Check(File.ReadAllText(settingsPath).Contains("hotkey = Ctrl+Alt+P"),"the new key is saved in the scratch settings file");
    Check(Text(root,"overlay-hotkey")=="Overlay key: Ctrl+Alt+P  (change)","the menu shows the new key");
    Check(Math.Abs(FindById<Button>(root,"menu-Library").ActualHeight-39)<0.6&&Math.Abs(FindById<Button>(root,"menu-Help").ActualHeight-39)<0.6,"menu rows keep the kit's 39 px height after a key change (it was "+FindById<Button>(root,"menu-Library").ActualHeight+")");
    Capture(root,Path.Combine(scratch,"overlay-menu-after-key.png"));
    // keyboard wait: the overlay could not become the active window
    Invoke(FindById<Button>(root,"overlay-hotkey"));
    Check(o.Mode==OverlayMode.Keys,"keyboard mode entered a third time");
    o.Step(800000);
    Check(o.Mode==OverlayMode.Menu&&o.Note.Contains("could not take the keyboard")&&o.NoActivate,"when the overlay never becomes the active window, keyboard mode ends by itself with a note and the game keeps the keyboard");

    // the key editor
    o.OpenSubmenu("Help");Invoke(FindById<Button>(root,"overlay-keys"));
    Check(o.Mode==OverlayMode.Keys&&!o.NoActivate&&!o.HotkeyRegistered,"editor: the overlay takes the keyboard and releases the hotkey");
    bool allRows=true;foreach(string b in KeyMap.Buttons)allRows&=FindById<Button>(root,"overlay-key-"+b)!=null;
    Check(allRows&&Text(root,"overlay-key-CROSS")=="Cross:  X"&&Text(root,"overlay-key-START")=="Start:  Enter","editor: fourteen rows show the launcher's current keys");
    Check(FindById<Button>(root,"overlay-keys-save")!=null&&!FindById<Button>(root,"overlay-keys-save").IsEnabled&&!FindById<Button>(root,"overlay-keys-discard").IsEnabled,"editor: Save and Discard are off while nothing changed");
    Capture(root,Path.Combine(scratch,"overlay-editor.png"));
    Check(o.HandleKey(Key.K,ModifierKeys.None,false)&&draft.Describe("CROSS")=="X","editor: a key does nothing until a row is clicked");
    var cross=FindById<Button>(root,"overlay-key-CROSS");
    Invoke(cross);Check(Text(root,"overlay-key-CROSS")=="Cross:  press a key...","editor: clicking a row waits for a key");
    Check(o.HandleKey(Key.F11,ModifierKeys.None,false)&&FindById<TextBlock>(root,"overlay-keys-status").Text.Contains("cannot be used")&&draft.Describe("CROSS")=="X"&&Text(root,"overlay-key-CROSS")=="Cross:  press a key...","editor: F11 is refused, the wait continues");
    Check(o.HandleKey(Key.K,ModifierKeys.None,true)&&draft.Describe("CROSS")=="X","editor: a key repeat is ignored");
    Check(o.HandleKey(Key.K,ModifierKeys.None,false)&&draft.Describe("CROSS")=="K"&&Text(root,"overlay-key-CROSS")=="Cross:  K","editor: the pressed key goes into the launcher's draft");
    Check(FindById<Button>(root,"overlay-keys-save").IsEnabled&&FindById<Button>(root,"overlay-keys-discard").IsEnabled,"editor: Save and Discard turn on after a change");
    Invoke(cross);o.HandleKey(Key.Z,ModifierKeys.None,false);
    Check(draft.Describe("CROSS")=="Z"&&draft.Describe("SQUARE")=="K","editor: a key already on another button swaps (the launcher's rule, through its callback)");
    Invoke(FindById<Button>(root,"overlay-key-SQUARE"));
    Check(o.HandleKey(Key.Escape,ModifierKeys.None,false)&&o.Mode==OverlayMode.Keys&&Text(root,"overlay-key-SQUARE")=="Square:  K","editor: Esc cancels a wait and keeps the editor open");
    Invoke(FindById<Button>(root,"overlay-keys-save"));
    Check(saves==1&&!FindById<Button>(root,"overlay-keys-save").IsEnabled,"editor: Save calls the launcher's save once");
    Invoke(FindById<Button>(root,"overlay-keys-defaults"));
    Check(draft.Describe("CROSS")=="X"&&FindById<Button>(root,"overlay-keys-discard").IsEnabled,"editor: Default keys goes through the launcher");
    Invoke(FindById<Button>(root,"overlay-keys-discard"));
    Check(!FindById<Button>(root,"overlay-keys-discard").IsEnabled,"editor: Discard goes through the launcher");
    Check(o.HandleKey(Key.Escape,ModifierKeys.None,false)&&o.Mode==OverlayMode.Menu&&o.NoActivate,"editor: Esc with no wait closes it and the game keeps the keyboard");
    Check(probe.Fronted[probe.Fronted.Count-1]==gameWindow,"editor: the foreground is handed back to the game window");
    Check(o.HotkeyRegistered||o.HotkeyProblem.Length>0,"editor: the overlay hotkey is registered again (or its problem reported)");
    o.OpenSubmenu("Help");Invoke(FindById<Button>(root,"overlay-keys"));Check(o.Mode==OverlayMode.Keys,"editor opened again");
    Invoke(FindById<Button>(root,"overlay-key-CIRCLE"));o.HandleKey(Key.J,ModifierKeys.None,false);
    Invoke(FindById<Button>(root,"overlay-keys-back"));Check(o.Mode==OverlayMode.Menu&&o.NoActivate,"editor: Back closes it");
    Check(o.Note.Contains("Unsaved key changes are still waiting"),"editor: leaving with unsaved edits says so");
    o.OpenSubmenu("Help");Invoke(FindById<Button>(root,"overlay-keys"));o.Step(900000);
    Check(o.Mode==OverlayMode.Menu&&o.Note.Contains("could not take the keyboard")&&!o.Note.Contains("nothing changed")&&o.Note.Contains("Unsaved"),"editor: it gives up by itself when the overlay never becomes the active window, without claiming nothing changed while edits are unsaved");

    // keyboard control of the menus (opened with the overlay key only)
    {
      var rootN=o.Content;IntPtr hkN=o.HotkeyHandle;
      if(o.Visible)SendMessageW(hkN,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(910000);Check(!o.Visible,"keyboard section: overlay closed first");
      probe.Foreground=true;probe.Y=GY+300;o.Step(910050);
      o.Settings.KeyboardMenu=true;probe.Fronted.Clear();o.TestForeground=null;
      probe.Y=GY+2;o.Step(910100);o.Step(910300);Check(o.Visible&&!o.NavActive&&o.NoActivate,"hover opens the bar without taking the keyboard");
      probe.Y=GY+300;o.Step(910400);o.Step(912000);Check(!o.Visible,"the hover bar closes again");
      SendMessageW(hkN,0x0312,new IntPtr(0x4F56),IntPtr.Zero);
      Check(o.Visible&&o.Pinned&&o.NavActive&&!o.NoActivate,"the overlay key opens the overlay and keyboard control starts (the window may now take the keyboard)");
      o.Step(912010);Check(probe.Fronted.Contains(o.WindowHandle),"the overlay asks Windows to bring its window to the front");
      o.TestForeground=true;o.Step(912020);
      Check(o.NavFocusId=="overlay-restart","the highlight starts on the first button that works (restart; pause is greyed out)");
      Check(o.HandleKey(Key.Right,ModifierKeys.None,false)&&o.NavFocusId=="overlay-stop","Right moves to Stop");
      o.HandleKey(Key.Right,ModifierKeys.None,false);Check(o.NavFocusId=="overlay-menu","Right skips the greyed-out fullscreen button");
      o.HandleKey(Key.Right,ModifierKeys.None,false);Check(o.NavFocusId=="overlay-restart","Right wraps round to the first button");
      o.HandleKey(Key.Left,ModifierKeys.None,false);Check(o.NavFocusId=="overlay-menu","Left wraps back");
      o.HandleKey(Key.Left,ModifierKeys.None,true);Check(o.NavFocusId=="overlay-stop","a held arrow key repeats");
      int stops0=stops;o.HandleKey(Key.Enter,ModifierKeys.None,true);Check(o.Mode==OverlayMode.Bar&&stops==stops0&&o.NavFocusId=="overlay-stop","a held Enter does not repeat");
      var ring=FindById<Border>(rootN,"overlay-nav-ring");var stopBtn=FindById<Button>(rootN,"overlay-stop");
      o.Step(912030);
      Point rp=new Point(Canvas.GetLeft(ring),Canvas.GetTop(ring)),bp=stopBtn.TranslatePoint(new Point(0,0),(UIElement)rootN);
      Check(ring.Visibility==Visibility.Visible&&Math.Abs(rp.X-bp.X)<0.6&&Math.Abs(rp.Y-bp.Y)<0.6&&Math.Abs(ring.Width-stopBtn.ActualWidth)<0.6,"the ring sits on the highlighted button");
      o.HandleKey(Key.Right,ModifierKeys.None,false);Check(o.NavFocusId=="overlay-menu","back on the menu button");
      o.HandleKey(Key.Enter,ModifierKeys.None,false);Check(o.Mode==OverlayMode.Menu&&o.NavFocusId=="menu-Library"&&o.OpenSubmenuName=="Library","Enter on the menu button opens the menu on its first name");
      o.HandleKey(Key.Down,ModifierKeys.None,false);Check(o.NavFocusId=="menu-Session"&&o.OpenSubmenuName=="Session","Down moves to Session and shows its rows");
      o.HandleKey(Key.Right,ModifierKeys.None,false);Check(o.NavFocusId=="ses-restart","Right goes into the rows, onto the first one that works");
      o.HandleKey(Key.Down,ModifierKeys.None,false);Check(o.NavFocusId=="ses-stop","Down moves to the next row");
      o.HandleKey(Key.Down,ModifierKeys.None,false);Check(o.NavFocusId=="ses-settings","Down skips the greyed-out rows");
      gset[0].Value=startSize;gset[1].Value=startMovie;
      o.HandleKey(Key.Enter,ModifierKeys.None,false);Check(o.OpenSubmenuName=="Game settings"&&o.NavFocusId=="gset-window-size","Enter on Game settings opens the panel and goes into it, past the greyed-out Restart row");
      int c0=gsetChanges;o.HandleKey(Key.Space,ModifierKeys.None,false);Check(gsetChanges==c0+1&&gset[0].Value==2,"Space changes the setting through the launcher's callback");
      o.Step(912500);Capture(rootN,Path.Combine(scratch,"overlay-keyboard.png"));
      o.HandleKey(Key.Up,ModifierKeys.None,false);Check(o.NavFocusId=="gset-restart","once a change is waiting, Up reaches the Restart row");
      o.HandleKey(Key.Down,ModifierKeys.None,false);o.HandleKey(Key.Down,ModifierKeys.None,false);Check(o.NavFocusId=="gset-intro-movie","the greyed-out smooth movement row is skipped");
      o.HandleKey(Key.Left,ModifierKeys.None,false);Check(o.OpenSubmenuName=="Session"&&o.NavFocusId=="menu-Session","Left goes back out of the panel to the Session name");
      o.HandleKey(Key.Escape,ModifierKeys.None,false);Check(o.Mode==OverlayMode.Bar&&o.NavFocusId=="overlay-menu"&&o.NavActive,"Esc closes the menu and returns to the pill");
      probe.Fronted.Clear();o.HandleKey(Key.Escape,ModifierKeys.None,false);
      Check(!o.Visible&&o.Mode==OverlayMode.Hidden&&!o.NavActive&&o.NoActivate&&probe.Fronted.Count>0&&probe.Fronted[probe.Fronted.Count-1]==gameWindow,"Esc on the pill closes the overlay, the window stops taking the keyboard and the game window gets the front back");
      Check(ring.Visibility!=Visibility.Visible,"the ring is gone with the overlay");
      // the overlay key closes it too, and gives the front back
      o.Step(913000);SendMessageW(hkN,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(913010);o.Step(913020);
      Check(o.Visible&&o.NavActive,"opened again with the overlay key");
      probe.Fronted.Clear();SendMessageW(hkN,0x0312,new IntPtr(0x4F56),IntPtr.Zero);
      Check(!o.Visible&&!o.NavActive&&o.NoActivate&&probe.Fronted.Contains(gameWindow),"the overlay key closes it and the game gets the front back");
      // the player switches to another program: the overlay closes and does not take the front back
      o.Step(914000);SendMessageW(hkN,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(914010);
      probe.Fronted.Clear();o.TestForeground=false;probe.Foreground=false;o.Step(916000);
      Check(!o.Visible&&!o.NavActive&&o.NoActivate&&!probe.Fronted.Contains(gameWindow),"switching to another program closes it and the game window is not pulled back to the front");
      probe.Foreground=true;
      // the overlay cannot get the keyboard: no note, the mouse menu still works
      string noteBefore=o.Note;o.Step(920000);o.TestForeground=false;SendMessageW(hkN,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(920010);
      Check(o.Visible&&o.NavActive,"it waits while it asks for the keyboard");
      o.Step(920010+1700);Check(o.Visible&&!o.NavActive&&o.NoActivate&&o.Note==noteBefore,"if the overlay never gets the keyboard it quietly goes back to mouse use, with no new note");
      Invoke(FindById<Button>(rootN,"overlay-menu"));o.Step(922000);Check(o.Mode==OverlayMode.Menu,"the mouse menu still works then");
      SendMessageW(hkN,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(922100);
      // the switch in the Help menu
      o.TestForeground=true;SendMessageW(hkN,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(918000);Invoke(FindById<Button>(rootN,"overlay-menu"));o.Step(918010);
      o.OpenSubmenu("Help");Check(Ticked(rootN,"overlay-keyboard"),"the Help menu row shows keyboard control as on");
      probe.Fronted.Clear();Invoke(FindById<Button>(rootN,"overlay-keyboard"));
      Check(!o.Settings.KeyboardMenu&&!o.NavActive&&o.NoActivate&&!Ticked(rootN,"overlay-keyboard")&&File.ReadAllText(settingsPath).Contains("keyboard-menu = no")&&probe.Fronted.Contains(gameWindow),"turning it off ends keyboard control, gives the game the front back and saves the choice");
      SendMessageW(hkN,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(918100);
      SendMessageW(hkN,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(918200);
      Check(o.Visible&&o.Pinned&&!o.NavActive&&o.NoActivate,"with it off the overlay key opens the overlay and leaves the keyboard with the game");
      Check(!o.HandleKey(Key.Down,ModifierKeys.None,false),"with it off arrow keys are not read");
      SendMessageW(hkN,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(918300);
      // more focus paths (checker findings 1 to 3 and 7)
      o.Settings.KeyboardMenu=true;o.TestForeground=true;probe.Foreground=true;
      o.Step(940000);SendMessageW(hkN,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(940010);
      probe.Foreground=false; // the game is no longer in front: only the overlay's own claim keeps it open
      o.Step(944000);o.Step(944100);Check(o.Visible&&o.NavActive,"while the overlay holds the front (and the game does not) it stays open, even long after the 1.5 s wait");
      probe.Foreground=true;SendMessageW(hkN,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(946000);
      SendMessageW(hkN,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(946010); // opened again; the Alt+Tab below happens inside the 1.5 s wait
      o.TestForeground=false;probe.Fronted.Clear();SendMessageW(hkN,0x0312,new IntPtr(0x4F56),IntPtr.Zero);
      Check(!o.Visible&&!o.NavActive&&!probe.Fronted.Contains(gameWindow),"if the overlay lost the front first (Alt+Tab) and then closes, the game window is not pulled back to the front");
      o.TestForeground=true;o.Step(945000);SendMessageW(hkN,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(945010);
      o.HandleKey(Key.Down,ModifierKeys.None,false);o.HandleKey(Key.Escape,ModifierKeys.None,false);
      Check(o.Mode==OverlayMode.Bar&&o.Visible&&o.NavActive,"Esc closes only the menu when the menu was opened by the mouse while the highlight was on the pill");
      Invoke(FindById<Button>(rootN,"overlay-menu"));o.Step(945100);
      o.HandleKey(Key.Escape,ModifierKeys.None,false);Check(o.Mode==OverlayMode.Bar&&o.Visible,"Esc after a mouse-opened menu closes the menu, not the overlay");
      o.HandleKey(Key.Down,ModifierKeys.None,false);
      for(int i=0;i<5;i++)o.HandleKey(Key.Down,ModifierKeys.None,false);
      o.HandleKey(Key.Right,ModifierKeys.None,false);Check(o.OpenSubmenuName=="Help"&&o.NavFocusId=="overlay-keys","keyboard: Help menu rows");
      for(int i=0;i<3;i++)o.HandleKey(Key.Down,ModifierKeys.None,false);
      Check(o.NavFocusId=="overlay-hotkey","keyboard: down to the Overlay key row");
      o.HandleKey(Key.Enter,ModifierKeys.None,false);
      Check(o.Mode==OverlayMode.Keys&&!o.NavActive&&!o.NoActivate,"Enter on Overlay key hands over from keyboard control to the key-choosing mode");
      o.HandleKey(Key.Escape,ModifierKeys.None,false);
      Check(o.Mode==OverlayMode.Menu&&!o.NavActive&&o.NoActivate,"Esc ends key choosing, and keyboard control does not come back by itself");
      SendMessageW(hkN,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(945500);
      // the window loses the active state by itself (the player clicks the game): keyboard control ends, the overlay stays, the game is not pulled back
      o.Step(947000);SendMessageW(hkN,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(947010);
      Check(o.Visible&&o.NavActive,"opened with the overlay key for the lost-focus check");
      probe.Fronted.Clear();o.SimulateLostActive();
      Check(o.Visible&&!o.NavActive&&o.NoActivate&&!probe.Fronted.Contains(gameWindow)&&FindById<Border>(rootN,"overlay-nav-ring").Visibility!=Visibility.Visible,"the window losing the active state ends keyboard control, keeps the overlay open, hides the ring and does not pull the game back");
      Check(!o.HandleKey(Key.Down,ModifierKeys.None,false),"after that the arrow keys are not read");
      SendMessageW(hkN,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(947100);
      // the mouse and the keys share one highlight; a note does not outlive its menu
      o.Settings.KeyboardMenu=true;o.TestForeground=true;probe.Foreground=true;
      o.Step(948000);SendMessageW(hkN,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(948010);
      o.NavPointAt("overlay-stop");Check(o.NavFocusId=="overlay-stop","pointing at a pill button moves the highlight there");
      o.HandleKey(Key.Right,ModifierKeys.None,false);Check(o.NavFocusId=="overlay-menu","the keys continue from where the mouse pointed");
      Invoke(FindById<Button>(rootN,"overlay-menu"));o.Step(948100);
      o.NavPointAt("menu-View");Check(o.NavFocusId=="menu-View","pointing at a menu name moves the highlight there");
      o.OpenSubmenu("View");o.NavPointAt("overlay-hover");Check(o.NavFocusId=="overlay-hover","pointing at a row inside the open submenu moves the highlight there");
      o.OpenSubmenu("Help");o.NavPointAt("help-about");o.HandleKey(Key.Up,ModifierKeys.None,false);Check(o.NavFocusId=="help-setup","Up continues from the row the mouse pointed at");
      o.OpenSubmenu("Help");Invoke(FindById<Button>(rootN,"overlay-hotkey"));o.HandleKey(Key.Escape,ModifierKeys.None,false);
      Check(o.Note.Length>0,"a key-choosing note is shown while its menu is open");
      Invoke(FindById<Button>(rootN,"overlay-menu"));o.Step(948200);
      Check(o.Mode==OverlayMode.Bar&&o.Note.Length==0,"closing the menu clears the note, so it does not come back next time");
      SendMessageW(hkN,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(948300);
      o.Settings.KeyboardMenu=false;o.TestForeground=null;probe.Foreground=true;
    }

    // screen scale above 100 percent (a fake scale: the real window keeps this machine's scale, so only what the overlay reports is checked)
    {
      var rootS=o.Content;IntPtr hkS=o.HotkeyHandle;
      if(o.Visible)SendMessageW(hkS,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(930000);
      probe.Foreground=true;probe.Y=GY+300;o.TestScale=1.0;o.Step(930010);
      probe.Y=GY+100;o.Step(930100);o.Step(930300);Check(!o.Visible,"scale 100%: a cursor 100 pixels down is outside the 90 pixel hover band");
      probe.Y=GY+300;o.Step(930400);o.TestScale=1.5;o.Step(930500);
      probe.Y=GY+100;o.Step(930600);o.Step(930800);Check(o.Visible&&o.Mode==OverlayMode.Bar,"scale 150%: the hover band is 135 pixels, so the same cursor position opens the bar");
      Check(o.Fit==1&&Math.Abs(o.UiRect.Width-450)<=1&&Math.Abs(o.UiRect.Height-96)<=1,"scale 150%: the 300 by 64 pill is reported as 450 by 96 pixels ("+o.UiRect+")");
      Check(o.UiRect.Left==GX+(GW-450)/2&&o.UiRect.Top==GY+(int)Math.Round(27*1.5),"scale 150%: the pill is centred at the top, 27 dip down ("+o.UiRect+")");
      probe.Y=GY+300;o.Step(931000);o.Step(932300);Check(!o.Visible,"hover bar closed again");
      // a small picture at a high scale: the whole overlay shrinks so the tallest menu still fits under the top of the picture
      o.TestScale=2.0;SendMessageW(hkS,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(933000);
      Check(o.Visible&&o.Fit==1,"scale 200%: the pill alone fits a 720 pixel picture (360 dip), so nothing shrinks");
      Invoke(FindById<Button>(rootS,"overlay-menu"));o.Step(933100);o.OpenSubmenu("Session");Invoke(FindById<Button>(rootS,"ses-settings"));o.Step(933200);o.Step(933250);
      Check(o.OpenSubmenuName=="Game settings"&&o.Fit<1&&o.Fit>=0.6,"scale 200%: the settings panel is taller than the 360 dip picture, so the overlay shrinks (factor "+o.Fit+")");
      Check(o.UiRect.Bottom<=GY+GH&&o.UiRect.Right<=GX+GW,"the shrunk overlay with the settings panel open stays inside the picture ("+o.UiRect+")");
      double fitKept=o.Fit;o.OpenSubmenu("Library");o.Step(933260);Check(o.Fit==fitKept,"once shrunk it keeps its size while it stays open");
      SendMessageW(hkS,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(933300);
      o.TestScale=1.0;SendMessageW(hkS,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(933400);
      o.Step(933450);Check(o.Fit==1&&Math.Abs(o.UiRect.Width-300)<=1,"closed and opened again at 100%: the overlay is its normal size ("+o.UiRect+")");
      SendMessageW(hkS,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(933500);o.TestScale=null;
    }

    // back to the game from the launcher with the overlay key (the About, Setup guide and Return to launcher rows put the launcher in front and the overlay away)
    {
      IntPtr hkL=o.HotkeyHandle;bool launcherFront=true;host.LauncherForeground=delegate{return launcherFront;};
      o.Settings.KeyboardMenu=false;o.TestForeground=null;probe.Foreground=true;
      if(o.Visible)SendMessageW(hkL,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(960000);
      probe.Foreground=false;probe.Fronted.Clear();
      SendMessageW(hkL,0x0312,new IntPtr(0x4F56),IntPtr.Zero);
      Check(o.Visible&&o.Pinned&&probe.Fronted.Contains(gameWindow),"launcher in front: the overlay key brings the game window to the front and opens the overlay");
      o.Step(960100);Check(o.Visible,"it stays open while Windows brings the game forward (the first moment the game is still not in front)");
      probe.Foreground=true;o.Step(962000);Check(o.Visible&&o.Mode==OverlayMode.Bar,"and it stays once the game is in front");
      SendMessageW(hkL,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(962100);
      launcherFront=false;probe.Foreground=false;probe.Fronted.Clear();
      SendMessageW(hkL,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(962200);
      Check(!o.Visible&&!probe.Fronted.Contains(gameWindow),"another program in front (not the launcher): the key still does nothing and the game is not pulled forward");
      launcherFront=true;probe.Foreground=true;host.LauncherForeground=null;
    }

    // the developer's report: Library > Return to launcher with the mouse, then back in the game: does the bar come back by hovering?
    {
      IntPtr hkR=o.HotkeyHandle;bool launcherFront=false;host.LauncherForeground=delegate{return launcherFront;};
      var oldShow=host.ShowLauncher;host.ShowLauncher=delegate(string where){launcherAt=where;launcherFront=true;probe.Foreground=false;};
      o.Settings.KeyboardMenu=true;o.TestForeground=null;probe.Foreground=true;
      if(o.Visible)SendMessageW(hkR,0x0312,new IntPtr(0x4F56),IntPtr.Zero);
      probe.Y=GY+300;o.Step(970000);o.Step(970100);
      probe.Y=GY+2;o.Step(970200);o.Step(970400);Check(o.Visible&&!o.NavActive,"hover opens the bar (return-to-launcher story)");
      Invoke(FindById<Button>(rootRL(o),"overlay-menu"));o.Step(970500);o.OpenSubmenu("Library");
      Invoke(FindById<Button>(rootRL(o),"lib-return"));o.Step(970600);o.Step(970700);
      Check(launcherAt=="Home"&&!o.Visible,"Return to launcher: the launcher is in front and the overlay is hidden");
      launcherFront=false;probe.Foreground=true;probe.Y=GY+300;o.Step(972000);o.Step(972100);
      probe.Y=GY+2;o.Step(972200);o.Step(972400);
      Check(o.Visible&&o.Mode==OverlayMode.Bar,"back in the game, hovering at the top brings the bar back");
      probe.Y=GY+300;o.Step(974000);o.Step(975000);
      host.ShowLauncher=oldShow;host.LauncherForeground=null;
    }

    // the game goes away while keyboard control is on
    o.Settings.KeyboardMenu=true;o.TestForeground=true;probe.Foreground=true;
    o.Step(950000);SendMessageW(o.HotkeyHandle,0x0312,new IntPtr(0x4F56),IntPtr.Zero);o.Step(950010);
    Check(o.Visible&&o.NavActive,"keyboard control is on when the game ends");
    probe.Fronted.Clear();DestroyWindow(gameWindow);
    o.Step(950500);o.Step(951500);
    Check(!o.Visible&&o.Mode==OverlayMode.Hidden&&!o.NavActive&&o.NoActivate&&probe.Fronted.Count==0,"the game ends during keyboard control: the overlay hides, keyboard control ends, the window is no-activate again and nothing is brought to the front");
    o.TestForeground=null;
    o.Dispose();Pump();Check(!IsWindowVisible(o.WindowHandle),"disposed: the overlay window is gone");
    Check(OverlayNative.RegisterHotKey(IntPtr.Zero,0x7003,0x4003u,0x50u),"disposed: Ctrl+Alt+P is free again (the overlay released its hotkey)");
    OverlayNative.UnregisterHotKey(IntPtr.Zero,0x7003);
    o.Step(1);Check(!o.HandleKey(Key.A,ModifierKeys.None,false),"after Dispose keys are ignored");
    // a hotkey saved earlier is checked against the game's key map at attach
    gameKeys=new List<int>{0xA2};
    var blocked=GameOverlay.Attach(pid,host,new OverlaySettings(),Path.Combine(scratch,"never.txt"),OverlayFeatures.None,probe,false);
    Check(!blocked.HotkeyRegistered&&blocked.HotkeyProblem.Contains("Ctrl is one of the game"),"attach: Ctrl+Alt+O is not registered when the game's key map uses Ctrl");
    blocked.Dispose();
    gameKeys=new List<int>();
    // the game ends while the player is choosing a key: nothing may stay registered
    var late=GameOverlay.Attach(pid,host,new OverlaySettings{Mods=3,Vk=0x4B},Path.Combine(scratch,"never2.txt"),OverlayFeatures.None,probe,false);
    bool lateOk=late.HotkeyRegistered;
    if(lateOk){
      var gw2=CreateWindowExW(0x08000080,Win32Probe.GameClass,Win32Probe.TitlePrefix+" (second)",0x90000000u,GX,GY,GW,GH,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
      SendMessageW(late.HotkeyHandle,0x0312,new IntPtr(0x4F56),IntPtr.Zero);
      Invoke(FindById<Button>(late.Content,"overlay-menu"));late.Step(1300);
      Invoke(FindById<Button>(late.Content,"overlay-hotkey"));
      Check(late.Mode==OverlayMode.Keys,"late: keyboard mode entered");
      late.Dispose();Pump();
      Check(OverlayNative.RegisterHotKey(IntPtr.Zero,0x7004,0x4003u,0x4Bu),"disposed while choosing a key: Ctrl+Alt+K is free again (no leaked hotkey)");
      OverlayNative.UnregisterHotKey(IntPtr.Zero,0x7004);
      DestroyWindow(gw2);
    }else Info("Ctrl+Alt+K was not free here; the dispose-in-keyboard-mode case was not exercised");

    // live control of the game (proposed channel, 2026-11-22): the overlay only hands command lines to the launcher
    {
      var gw3=CreateWindowExW(0x08000080,Win32Probe.GameClass,Win32Probe.TitlePrefix+" (live control)",0x90000000u,GX,GY,GW,GH,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
      var sent=new List<string>();var lh=new OverlayHost();lh.GameCommand=delegate(string c){sent.Add(c);};lh.GameVolume=delegate{return 0.8;};lh.GameMuted=delegate{return false;};GameLiveState gs=new GameLiveState{Volume=80};lh.GameState=delegate{return gs;};
      var lc=GameOverlay.Attach(pid,lh,new OverlaySettings{KeyboardMenu=false},Path.Combine(scratch,"live.txt"),new OverlayFeatures(new[]{OverlayLayout.LiveControl}),probe,false);
      var rootLc=lc.Content;long tt=980000;probe.Foreground=true;
      Action ensure=delegate{
        if(!lc.Visible){probe.Y=GY+300;lc.Step(tt+=100);probe.Y=GY+2;lc.Step(tt+=100);lc.Step(tt+=300);}
        if(lc.Mode==OverlayMode.Bar){Invoke(FindById<Button>(rootLc,"overlay-menu"));lc.Step(tt+=100);}
      };
      ensure();Check(lc.Visible,"live control: the bar opens");
      var pb=FindById<Button>(rootLc,"overlay-pause");var fb=FindById<Button>(rootLc,"overlay-fullscreen");
      Check(pb.IsEnabled&&fb.IsEnabled&&NameOf(pb)=="Pause","live control: Pause and Fullscreen are enabled when the game lists the feature");
      var pauseGlyph=(System.Windows.Shapes.Path)pb.Tag;var glyphPause=pauseGlyph.Data;
      Invoke(pb);Check(sent.Count==1&&sent[0]=="pause on"&&NameOf(pb)=="Resume"&&(string)pb.ToolTip=="Resume"&&!Object.ReferenceEquals(pauseGlyph.Data,glyphPause),"live control: Pause sends 'pause on' and the button becomes Resume (name, tooltip and play icon)");
      Invoke(pb);Check(sent.Count==2&&sent[1]=="pause off"&&NameOf(pb)=="Pause"&&(string)pb.ToolTip=="Pause"&&Object.ReferenceEquals(pauseGlyph.Data,glyphPause),"live control: the second click sends 'pause off' and the pause icon returns");
      Invoke(pb);ensure();lc.OpenSubmenu("Session");
      Check(FindById<Button>(rootLc,"ses-resume").IsEnabled,"live control: Session > Resume game is enabled");
      Invoke(FindById<Button>(rootLc,"ses-resume"));Check(sent[sent.Count-1]=="pause off"&&NameOf(pb)=="Pause","live control: Resume game sends 'pause off' and the button shows Pause again");
      int before=sent.Count;ensure();lc.OpenSubmenu("Session");Invoke(FindById<Button>(rootLc,"ses-resume"));Check(sent.Count==before,"live control: Resume game while not paused sends nothing");
      ensure();Invoke(FindById<Button>(rootLc,"overlay-fullscreen"));Check(sent[sent.Count-1]=="fullscreen toggle","live control: the Fullscreen button sends 'fullscreen toggle'");
      ensure();lc.OpenSubmenu("View");Check(FindById<Button>(rootLc,"view-fullscreen").IsEnabled&&FindById<Button>(rootLc,"view-screenshot").IsEnabled,"live control: View > Fullscreen and Take screenshot are enabled");
      Invoke(FindById<Button>(rootLc,"view-screenshot"));Check(sent[sent.Count-1]=="screenshot","live control: Take screenshot sends 'screenshot'");
      ensure();lc.OpenSubmenu("View");Invoke(FindById<Button>(rootLc,"view-fullscreen"));Check(sent[sent.Count-1]=="fullscreen toggle","live control: View > Fullscreen sends 'fullscreen toggle'");
      ensure();lc.OpenSubmenu("Audio");
      Check(FindById<Button>(rootLc,"aud-game-mute").IsEnabled&&FindById<FrameworkElement>(rootLc,"aud-game-volume-bar")!=null,"live control: Audio has a working mute row and a volume bar");
      Check(FindById<TextBlock>(rootLc,"aud-game-volume-value").Text=="80%","live control: the volume bar starts at the game's own 80 percent");
      Invoke(FindById<Button>(rootLc,"aud-game-mute"));lc.Step(tt+=50);Check(sent[sent.Count-1]=="mute on"&&Ticked(rootLc,"aud-game-mute"),"live control: Mute sends 'mute on' and is ticked");
      Invoke(FindById<Button>(rootLc,"aud-game-mute"));lc.Step(tt+=50);Check(sent[sent.Count-1]=="mute off"&&!Ticked(rootLc,"aud-game-mute"),"live control: the second click sends 'mute off' and clears the tick");
      before=sent.Count;lc.SetBar("aud-game-volume",0.8);Check(sent.Count==before,"live control: the same volume sends nothing");
      lc.SetBar("aud-game-volume",0.5);Check(sent[sent.Count-1]=="volume 50"&&sent.Count==before+1,"live control: the volume bar sends 'volume 50'");
      lc.SetBar("aud-game-volume",0.504);lc.SetBar("aud-game-volume",0.5);Check(sent.Count==before+1,"live control: one line per whole percent, not one per mouse move");
      lc.SetBar("aud-game-volume",7);Check(sent[sent.Count-1]=="volume 100","live control: a value over 1 is held at 100");
      ensure();lc.OpenSubmenu("View");
      Check(FindById<Button>(rootLc,"view-speed").IsEnabled&&!Ticked(rootLc,"view-speed"),"live control: View > Unlimited speed is enabled and starts unticked");
      Invoke(FindById<Button>(rootLc,"view-speed"));lc.Step(tt+=50);Check(sent[sent.Count-1]=="speed unlimited"&&Ticked(rootLc,"view-speed"),"live control: Unlimited speed sends 'speed unlimited' and is ticked");
      Invoke(FindById<Button>(rootLc,"view-speed"));lc.Step(tt+=50);Check(sent[sent.Count-1]=="speed normal"&&!Ticked(rootLc,"view-speed"),"live control: the second click sends 'speed normal' and clears the tick");
      // what the game says about itself (its state file): the overlay follows it, and ignores it for a moment after its own command
      ensure();tt+=3000;gs.Paused=true;gs.Muted=true;gs.Volume=35;lc.Step(tt);
      Check(NameOf(pb)=="Resume"&&(string)pb.ToolTip=="Resume","state: the game paused itself, so the button shows Resume");
      Check(Ticked(rootLc,"aud-game-mute")&&FindById<TextBlock>(rootLc,"aud-game-volume-value").Text=="35%","state: the game's own mute and volume are shown");
      tt+=3000;lc.Step(tt);gs.Paused=false;lc.SetBar("aud-game-volume",0.6);gs.Volume=35;tt+=600;lc.Step(tt);
      Check(FindById<TextBlock>(rootLc,"aud-game-volume-value").Text=="60%"&&NameOf(pb)=="Resume","state: right after the overlay's own command the old state is ignored, so a volume drag is not undone");
      tt+=2000;lc.Step(tt);
      Check(FindById<TextBlock>(rootLc,"aud-game-volume-value").Text=="35%"&&NameOf(pb)=="Pause","state: a moment later the game's own state wins again (Esc in the game ended the pause)");
      gs.Unlimited=true;tt+=3000;lc.Step(tt);Check(Ticked(rootLc,"view-speed"),"state: the game runs at unlimited speed, so the row is ticked");gs.Unlimited=false;tt+=3000;lc.Step(tt);Check(!Ticked(rootLc,"view-speed"),"state: back to normal speed clears the tick");
      gs.LastShot=@"C:\runs\game_shots\shot_f000100.bmp";tt+=600;lc.Step(tt);
      if(lc.HotkeyRegistered)Check(lc.Note=="Screenshot saved: shot_f000100.bmp","state: a new screenshot is named in the menu note");
      lc.Step(tt+=600);Check(lc.Note=="Screenshot saved: shot_f000100.bmp"||!lc.HotkeyRegistered,"state: the same screenshot is not announced twice");
      lh.GameState=delegate{return null;};tt+=1200;lc.Step(tt);Check(NameOf(pb)=="Pause","state: no state file leaves the overlay as it was");
      lc.Dispose();Pump();
      // without the feature, or with no way to send, the controls stay disabled and send nothing
      sent.Clear();
      var ln=GameOverlay.Attach(pid,lh,new OverlaySettings{KeyboardMenu=false},Path.Combine(scratch,"live2.txt"),OverlayFeatures.None,probe,false);
      Check(!FindById<Button>(ln.Content,"overlay-pause").IsEnabled&&!FindById<Button>(ln.Content,"overlay-fullscreen").IsEnabled&&sent.Count==0,"live control: a game that does not list the feature gets disabled Pause and Fullscreen");
      ln.SetBar("aud-game-volume",0.3);Check(sent.Count==0,"live control: a game that does not list the feature gets no volume line, even if the bar is driven");
      ln.Dispose();Pump();
      // a game started muted, and odd start volumes
      var mh=new OverlayHost();mh.GameCommand=delegate(string c){sent.Add(c);};mh.GameMuted=delegate{return true;};mh.GameVolume=delegate{return 1.5;};
      var lm=GameOverlay.Attach(pid,mh,new OverlaySettings{KeyboardMenu=false},Path.Combine(scratch,"live4.txt"),new OverlayFeatures(new[]{OverlayLayout.LiveControl}),probe,false);
      Check(Ticked(lm.Content,"aud-game-mute")&&FindById<TextBlock>(lm.Content,"aud-game-volume-value").Text=="100%","live control: a game started muted shows the tick, and a start volume over 1 shows 100%");
      Invoke(FindById<Button>(lm.Content,"aud-game-mute"));Check(sent.Count==1&&sent[0]=="mute off","live control: the first click on a game started muted sends 'mute off'");
      lm.Dispose();Pump();sent.Clear();
      mh.GameVolume=delegate{return Double.NaN;};
      var lv=GameOverlay.Attach(pid,mh,new OverlaySettings{KeyboardMenu=false},Path.Combine(scratch,"live5.txt"),new OverlayFeatures(new[]{OverlayLayout.LiveControl}),probe,false);
      Check(FindById<TextBlock>(lv.Content,"aud-game-volume-value").Text=="100%","live control: a start volume that is not a number shows 100%, not nonsense");
      lv.Dispose();Pump();
      var lh3=new OverlayHost();var ld=GameOverlay.Attach(pid,lh3,new OverlaySettings{KeyboardMenu=false},Path.Combine(scratch,"live3.txt"),new OverlayFeatures(new[]{OverlayLayout.LiveControl}),probe,false);
      Check(!FindById<Button>(ld.Content,"overlay-pause").IsEnabled,"live control: the feature without a way to send leaves Pause disabled");
      ld.Dispose();Pump();DestroyWindow(gw3);
    }
  }
}
