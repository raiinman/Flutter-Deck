using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// Tests of OverlayCore.cs. No window, no real input, nothing outside the scratch folder given as the argument.
public static class OverlayPureTest {
  static int run,failed;
  static void Check(bool ok,string what){run++;if(!ok){failed++;Console.WriteLine("FAIL: "+what);}}
  static OverlayObservation Seen(PxRect game,int x,int y,bool foreground,bool fullScreen){
    var o=new OverlayObservation{GameFound=true,GameForeground=foreground,FullScreen=fullScreen,CursorKnown=true,CursorX=x,CursorY=y,Game=game};
    o.Zone=OverlayGeometry.HotZone(game,200,1.0);return o;
  }
  public static int Main(string[] args){
    string scratch=args.Length>0?args[0]:Path.Combine(Path.GetTempPath(),"overlay-pure-test");
    if(Directory.Exists(scratch))Directory.Delete(scratch,true);Directory.CreateDirectory(scratch);
    Geometry();State();Hotkeys();Settings(scratch);Features();Menus();GameSettingModel();
    Console.WriteLine((failed==0?"PASS":"FAILED")+": "+run+" overlay checks, "+failed+" failed");
    return failed==0?0:1;
  }

  static void Geometry(){
    var game=new PxRect(0,0,1920,1080);
    var zone=OverlayGeometry.HotZone(game,200,1.0);
    Check(zone.Top==0&&zone.Height==90,"zone is the top 90 pixels at 100%");
    Check(OverlayGeometry.FitFactor(720,452)==1&&OverlayGeometry.FitFactor(480,452)==1&&OverlayGeometry.FitFactor(478,452)<1,"fit factor: 1 while the tallest layout plus the 27 pixel margin fits the picture");
    Check(OverlayGeometry.FitFactor(360,452)==0.7&&OverlayGeometry.FitFactor(100,452)==0.6&&OverlayGeometry.FitFactor(0,452)==1&&OverlayGeometry.FitFactor(360,0)==1,"fit factor: steps of 0.05, never below 0.6, 1 when a size is unknown");
    Check(OverlayGeometry.HotZone(game,200,2.0).Height==180,"zone height scales with the screen scale");
    Check(zone.Left+zone.Right==1920,"zone is centred");
    Check(zone.Width==1920&&zone.Left==0,"zone is the whole picture width");
    Check(OverlayGeometry.HotZone(game,500,1.0).Width==1920,"zone does not depend on the bar width");
    Check(OverlayGeometry.HotZone(new PxRect(0,0,300,200),400,1.0).Width==300,"zone never wider than the picture");
    Check(OverlayGeometry.HotZone(new PxRect(),200,1.0).IsEmpty,"no zone without a game picture");
    Check(OverlayGeometry.MenuLeftFor(1000,174,286,10,237,true)==237&&OverlayGeometry.MenuLeftFor(1000,174,286,10,237,false)==237,"a wide picture keeps the menu where the kit puts it");
    Check(OverlayGeometry.MenuLeftFor(640,174,286,10,237,true)==170&&OverlayGeometry.MenuLeftFor(640,174,286,10,237,false)==237,"a narrow picture pulls the menu left only as far as its submenu needs");
    Check(OverlayGeometry.MenuLeftFor(300,174,286,10,237,true)==0,"a very narrow picture never pushes the menu past the pill's left edge");
    var bar=OverlayGeometry.PlaceBar(game,300,50,1.0);
    Check(bar.Left==810&&bar.Right==1110,"bar is centred");
    Check(bar.Top==27&&bar.Height==50,"bar sits 27 pixels below the top edge (the kit)");
    var offset=OverlayGeometry.PlaceBar(new PxRect(100,40,900,640),300,50,1.0);
    Check(offset.Left==350&&offset.Top==67,"bar follows a game picture that is not at the screen origin");
    var big=OverlayGeometry.PlaceBar(new PxRect(0,0,200,100),500,500,1.0);
    Check(big.Left==0&&big.Right==200&&big.Top==0&&big.Bottom==100,"bar is kept inside a small picture");
    var monitors=new List<PxRect>{new PxRect(0,0,1920,1080),new PxRect(1920,0,3840,1080)};
    Check(OverlayGeometry.IsFullScreen(game,monitors),"picture equal to a monitor is full screen");
    Check(OverlayGeometry.IsFullScreen(new PxRect(1920,0,3840,1081),monitors),"one pixel out is still full screen");
    Check(OverlayGeometry.IsFullScreen(new PxRect(1920,0,3840,1080),monitors),"second monitor counts");
    Check(!OverlayGeometry.IsFullScreen(new PxRect(100,100,1000,700),monitors),"a small window is not full screen");
    Check(!OverlayGeometry.IsFullScreen(new PxRect(0,0,1920,1040),monitors),"picture shorter than the monitor is not full screen");
    Check(!OverlayGeometry.IsFullScreen(game,null),"no monitors, no full screen");
  }

  static void State(){
    var game=new PxRect(0,0,1920,1080);
    var s=new OverlayState();
    // hover wakes the bar at once: no dwell
    s.Update(Seen(game,960,2,true,true),1000);Check(s.Mode==OverlayMode.Bar&&!s.Pinned,"shown on the first sight of the cursor in the band");
    Check(OverlayState.DwellMs==0&&OverlayState.LeaveMs==1200,"timing: no dwell, 1.2 s hold");
    // a pass through the band shows it, and it hides 1.2 s after the cursor last touched the band
    s=new OverlayState();
    s.Update(Seen(game,960,2,true,true),0);s.Update(Seen(game,960,300,true,true),60);Check(s.Mode==OverlayMode.Bar,"a pass through the band is enough");
    s.Update(Seen(game,960,300,true,true),1000);Check(s.Mode==OverlayMode.Bar,"still shown before the hold has passed");
    s.Update(Seen(game,960,300,true,true),1260);Check(s.Mode==OverlayMode.Hidden,"hidden after the hold");
    // closed on purpose while the cursor is still in the band: it stays closed until the cursor leaves and comes back
    s=new OverlayState();s.Update(Seen(game,960,40,true,true),0);s.Hotkey(true,10);Check(s.Mode==OverlayMode.Hidden,"the hotkey closes the bar");
    s.Update(Seen(game,960,40,true,true),20);s.Update(Seen(game,960,40,true,true),500);Check(s.Mode==OverlayMode.Hidden,"a bar closed by the hotkey does not reopen while the cursor stays in the band");
    s.Update(Seen(game,960,500,true,true),600);s.Update(Seen(game,960,40,true,true),700);Check(s.Mode==OverlayMode.Bar,"it reopens when the cursor has left the band and come back");
    s=new OverlayState();s.Update(Seen(game,960,40,true,true),0);s.Close();s.Update(Seen(game,960,40,true,true),50);Check(s.Mode==OverlayMode.Hidden,"a bar closed by Stop or Hide does not reopen under the cursor");
    // quick bar off: hovering never opens it, the hotkey still does
    s=new OverlayState{HoverEnabled=false};s.Update(Seen(game,960,40,true,true),0);s.Update(Seen(game,960,40,true,true),500);Check(s.Mode==OverlayMode.Hidden,"quick bar off: hovering in the band does nothing");
    s.Hotkey(true,600);Check(s.Mode==OverlayMode.Bar&&s.Pinned,"quick bar off: the hotkey still opens the bar");
    s.Hotkey(true,700);Check(s.Mode==OverlayMode.Hidden,"quick bar off: the hotkey closes it again");
    // anywhere along the top counts, not only the middle
    s=new OverlayState();s.Update(Seen(game,50,80,true,true),0);Check(s.Mode==OverlayMode.Bar,"the band is the whole width, 90 pixels deep");
    // not in front: no show
    s=new OverlayState();s.Update(Seen(game,960,2,false,true),0);s.Update(Seen(game,960,2,false,true),500);
    Check(s.Mode==OverlayMode.Hidden,"the bar does not wake while another program is in front");
    // below the band
    s=new OverlayState();s.Update(Seen(game,960,200,true,true),0);s.Update(Seen(game,960,200,true,true),500);
    Check(s.Mode==OverlayMode.Hidden,"a cursor below the band does not wake it");
    // cursor unknown
    var unknown=Seen(game,960,2,true,true);unknown.CursorKnown=false;
    s=new OverlayState();s.Update(unknown,0);s.Update(unknown,500);Check(s.Mode==OverlayMode.Hidden,"no cursor, no bar");
    // full screen only
    s=new OverlayState{FullScreenOnly=true};s.Update(Seen(game,960,2,true,false),0);s.Update(Seen(game,960,2,true,false),500);
    Check(s.Mode==OverlayMode.Hidden,"full-screen-only: no hover bar in a window");
    s.Update(Seen(game,960,2,true,true),600);s.Update(Seen(game,960,2,true,true),800);
    Check(s.Mode==OverlayMode.Bar,"full-screen-only: hover bar in full screen");
    s=new OverlayState{FullScreenOnly=true};s.Hotkey(true,0);Check(s.Mode==OverlayMode.Bar&&s.Pinned,"full-screen-only does not stop the hotkey");
    // leaving hides after the leave time, with the bar rectangle keeping it alive
    s=new OverlayState();
    var o=Seen(game,960,2,true,true);s.Update(o,0);s.Update(o,150);
    o=Seen(game,960,40,true,true);o.Ui=new PxRect(810,10,1110,60);
    s.Update(o,200);Check(s.Mode==OverlayMode.Bar,"cursor over the bar keeps it");
    o=Seen(game,960,500,true,true);o.Ui=new PxRect(810,10,1110,60);
    s.Update(o,1000);Check(s.Mode==OverlayMode.Bar,"cursor away: still shown before the leave time");
    s.Update(o,1400);Check(s.Mode==OverlayMode.Hidden,"cursor away: hidden after the leave time");
    // between the zone and the bar does not count as leaving
    s=new OverlayState();o=Seen(game,960,2,true,true);s.Update(o,0);s.Update(o,150);
    o=Seen(game,960,8,true,true);o.Ui=new PxRect(810,27,1110,91);s.Update(o,2000);
    Check(s.Mode==OverlayMode.Bar,"the gap between the zone and the bar keeps it open");
    // a cursor resting in the band below the bar (bar 27 to 91, band 0 to 90) must not make the bar blink on and off
    s=new OverlayState();int changes=0;OverlayMode last=OverlayMode.Hidden;
    for(long t=0;t<=6000;t+=40){o=Seen(game,960,85,true,true);o.Ui=s.Visible?new PxRect(810,27,1110,91):new PxRect();s.Update(o,t);if(s.Mode!=last){changes++;last=s.Mode;}}
    Check(changes==1&&s.Mode==OverlayMode.Bar,"a cursor resting in the band below the bar keeps the bar up (no blinking)");
    // an open menu does not time out
    s=new OverlayState();o=Seen(game,960,2,true,true);s.Update(o,0);s.Update(o,150);s.OpenMenu(150);Check(s.Mode==OverlayMode.Menu,"menu opens from the bar");
    o=Seen(game,960,500,true,true);o.Ui=new PxRect(810,10,1110,300);
    s.Update(o,1500);Check(s.Mode==OverlayMode.Menu,"the open menu outlasts the bar's leave time");
    s.Update(o,600000);Check(s.Mode==OverlayMode.Menu,"the open menu does not time out");
    var away=Seen(game,960,500,false,true);away.Ui=new PxRect(810,10,1110,300);s.Update(away,600010);Check(s.Mode==OverlayMode.Hidden,"the open menu closes when the game loses focus");
    s=new OverlayState();s.OpenMenu(0);Check(s.Mode==OverlayMode.Hidden,"the menu cannot open while hidden");
    s=new OverlayState();s.Hotkey(true,0);s.OpenMenu(0);s.CloseMenu(10);Check(s.Mode==OverlayMode.Bar,"the menu closes back to the bar");
    // hotkey
    s=new OverlayState();s.Hotkey(false,0);Check(s.Mode==OverlayMode.Hidden,"the hotkey does nothing while another program is in front");
    s.Hotkey(true,0);Check(s.Mode==OverlayMode.Bar&&s.Pinned,"the hotkey opens a pinned bar");
    o=Seen(game,960,900,true,true);o.Ui=new PxRect(810,10,1110,60);s.Update(o,60000);Check(s.Mode==OverlayMode.Bar,"a pinned bar stays while the cursor is elsewhere");
    s.Hotkey(true,70000);Check(s.Mode==OverlayMode.Hidden&&!s.Pinned,"the hotkey closes it again");
    s.Hotkey(true,0);o=Seen(game,960,900,false,true);o.Ui=new PxRect(810,10,1110,60);s.Update(o,10);
    Check(s.Mode==OverlayMode.Hidden&&!s.Pinned,"switching to another program closes even a pinned bar");
    s.Hotkey(true,0);s.Hotkey(false,5);Check(s.Mode==OverlayMode.Hidden,"the hotkey closes an open overlay even if the game is not in front");
    // game gone
    s=new OverlayState();s.Hotkey(true,0);s.Update(new OverlayObservation(),10);Check(s.Mode==OverlayMode.Hidden&&!s.Pinned,"no game window, no overlay");
    // keyboard mode
    s=new OverlayState();s.Hotkey(true,0);s.OpenMenu(0);s.EnterKeys();Check(s.Mode==OverlayMode.Keys,"keyboard mode follows the menu");
    o=Seen(game,960,900,false,true);o.Ui=new PxRect(810,10,1110,300);s.Update(o,99999);Check(s.Mode==OverlayMode.Keys,"keyboard mode is ended by the controller, not by the cursor or focus");
    s.Hotkey(true,5);Check(s.Mode==OverlayMode.Keys,"the hotkey does not close keyboard mode");
    s.Update(new OverlayObservation(),6);Check(s.Mode==OverlayMode.Hidden,"keyboard mode still ends when the game is gone");
    s=new OverlayState();s.EnterKeys();Check(s.Mode==OverlayMode.Hidden,"keyboard mode cannot start from hidden");
    s=new OverlayState();s.Hotkey(true,0);s.OpenMenu(0);s.EnterKeys();s.LeaveKeys(40);Check(s.Mode==OverlayMode.Menu&&!s.Pinned,"leaving keyboard mode returns to the menu");
  }

  static void Hotkeys(){
    Check(HotkeyRules.Check(HotkeyRules.DefaultMods,HotkeyRules.DefaultKey,null).Length==0,"the default Ctrl+Alt+O is allowed");
    Check(HotkeyRules.Format(HotkeyRules.DefaultMods,HotkeyRules.DefaultKey)=="Ctrl+Alt+O","the default reads Ctrl+Alt+O");
    var defaults=new List<int>{0x26,0x28,0x25,0x27,0x58,0x5A,0x53,0x44,0x51,0x57,0x41,0x45,0x0D,0xA1}; // the game's default keys
    Check(HotkeyRules.Check(HotkeyRules.DefaultMods,HotkeyRules.DefaultKey,defaults).Length==0,"the default hotkey clashes with none of the default game keys");
    Check(HotkeyRules.Check(0,0x79,defaults).Length==0,"F10 alone is allowed when the game does not use it");
    Check(HotkeyRules.Check(0,0x79,new List<int>{0x79}).Length>0,"F10 alone is refused when the game uses it");
    Check(HotkeyRules.Check(0,0x4F,null).Length>0,"a bare letter is refused");
    Check(HotkeyRules.Check(HotkeyRules.Shift,0x4F,null).Length>0,"Shift and a letter is refused");
    Check(HotkeyRules.Check(HotkeyRules.Ctrl,0x4F,null).Length==0,"Ctrl and a letter is allowed");
    Check(HotkeyRules.Check(HotkeyRules.Alt,0x20,null).Length==0,"Alt and Space is allowed");
    foreach(int bad in new[]{0x09,0x7A,0x7B,0x0D}){
      Check(HotkeyRules.Check(HotkeyRules.Ctrl|HotkeyRules.Alt,bad,null).Length>0,"reserved key 0x"+bad.ToString("X2")+" is refused with modifiers");
      Check(HotkeyRules.Check(0,bad,null).Length>0,"reserved key 0x"+bad.ToString("X2")+" is refused alone");
    }
    Check(HotkeyRules.Check(HotkeyRules.Alt,0x0D,null).Length>0,"Alt+Enter is refused");
    Check(HotkeyRules.Check(HotkeyRules.Alt,0x73,null).Length>0,"Alt+F4 is refused");
    Check(HotkeyRules.Check(HotkeyRules.Ctrl|HotkeyRules.Alt,0x2E,null).Length>0,"Ctrl+Alt+Delete is refused");
    Check(HotkeyRules.Check(HotkeyRules.Ctrl,0x1B,null).Length>0,"Esc is not a usable overlay key");
    Check(HotkeyRules.Check(HotkeyRules.Ctrl,0xBC,null).Length>0,"punctuation is not offered");
    Check(HotkeyRules.Check(8,0x4F,null).Length>0,"an unknown modifier bit is refused");
    Check(HotkeyRules.Check(HotkeyRules.Ctrl|HotkeyRules.Alt,0x4F,new List<int>{0xA2}).Length>0,"Ctrl is refused when the game maps Left Ctrl");
    Check(HotkeyRules.Check(HotkeyRules.Ctrl|HotkeyRules.Alt,0x4F,new List<int>{0xA5}).Length>0,"Alt is refused when the game maps Right Alt");
    Check(HotkeyRules.Check(HotkeyRules.Ctrl|HotkeyRules.Shift,0x4F,new List<int>{0xA0}).Length>0,"Shift is refused when the game maps Left Shift");
    Check(HotkeyRules.Check(HotkeyRules.Ctrl|HotkeyRules.Alt,0x4F,new List<int>{0xA1}).Length==0,"a game Shift key does not matter when the chord has no Shift");
    // names
    Check(HotkeyRules.KeyName(0x41)=="A"&&HotkeyRules.KeyName(0x35)=="5"&&HotkeyRules.KeyName(0x70)=="F1"&&HotkeyRules.KeyName(0x79)=="F10","key names");
    Check(HotkeyRules.KeyName(0x7A)==null&&HotkeyRules.KeyName(0x09)==null&&HotkeyRules.KeyName(0x1B)==null,"F11, Tab and Esc have no usable name");
    foreach(int code in new[]{0x41,0x5A,0x30,0x39,0x70,0x79,0x20,0x2D,0x2E,0x24,0x23,0x21,0x22})Check(HotkeyRules.KeyCode(HotkeyRules.KeyName(code))==code,"name and code agree for 0x"+code.ToString("X2"));
    int m,v;
    Check(HotkeyRules.TryParse("Ctrl+Alt+O",out m,out v)&&m==3&&v==0x4F,"parse Ctrl+Alt+O");
    Check(HotkeyRules.TryParse(" alt + f9 ",out m,out v)&&m==HotkeyRules.Alt&&v==0x78,"parse is forgiving about case and spaces");
    Check(HotkeyRules.TryParse("F10",out m,out v)&&m==0&&v==0x79,"parse a bare F key");
    Check(!HotkeyRules.TryParse("Ctrl+",out m,out v),"parse refuses a missing key");
    Check(!HotkeyRules.TryParse("Win+O",out m,out v),"parse refuses the Windows key");
    Check(!HotkeyRules.TryParse("Ctrl+F11",out m,out v),"parse refuses F11");
    Check(!HotkeyRules.TryParse("",out m,out v)&&!HotkeyRules.TryParse(null,out m,out v),"parse refuses nothing");
    Check(HotkeyRules.Format(HotkeyRules.Ctrl|HotkeyRules.Alt|HotkeyRules.Shift,0x20)=="Ctrl+Alt+Shift+Space","format lists Ctrl, Alt, Shift in order");
  }

  static void Settings(string scratch){
    {
      var t=new OverlaySettings();Check(t.HoverBar&&t.Enabled,"defaults: quick bar on, overlay on");
      t.HoverBar=false;t.Enabled=false;string tp=Path.Combine(scratch,"toggles.txt");t.Save(tp);var u=OverlaySettings.Load(tp);
      Check(!u.HoverBar&&!u.Enabled&&File.ReadAllText(tp).Contains("quick-bar = no")&&File.ReadAllText(tp).Contains("overlay = no"),"the quick bar and overlay switches are saved and read back");
      File.WriteAllText(tp,"quick-bar = maybe\r\noverlay = whatever\r\n");u=OverlaySettings.Load(tp);Check(u.HoverBar&&u.Enabled,"anything but 'no' leaves them on");
    }
    {
      var k=new OverlaySettings();Check(k.KeyboardMenu,"defaults: keyboard control after the overlay key is on");
      string kp=Path.Combine(scratch,"keyboard.txt");k.KeyboardMenu=false;k.Save(kp);
      Check(!OverlaySettings.Load(kp).KeyboardMenu&&File.ReadAllText(kp).Contains("keyboard-menu = no"),"keyboard control off is saved and read back");
      File.WriteAllText(kp,"keyboard-menu = maybe\r\n");Check(OverlaySettings.Load(kp).KeyboardMenu,"anything but 'no' leaves keyboard control on");
      var all=new bool[]{false,true,false,true};
      Check(OverlayNav.Next(all,-1,1)==1&&OverlayNav.Next(all,1,1)==3&&OverlayNav.Next(all,3,1)==1&&OverlayNav.Next(all,1,-1)==3&&OverlayNav.Next(all,3,-1)==1,"the highlight skips greyed-out entries and wraps round in both directions");
      Check(OverlayNav.Next(all,-1,-1)==3&&OverlayNav.Next(all,9,1)==1,"from outside the list it starts at the first (forward) or last (back) enabled entry");
      Check(OverlayNav.Next(new bool[]{false,false},0,1)==-1&&OverlayNav.Next(new bool[0],-1,1)==-1&&OverlayNav.Next(null,-1,1)==-1,"nothing enabled gives -1");
      Check(OverlayNav.Next(new bool[]{false,true},1,1)==1,"a single enabled entry stays where it is");
      var help=OverlayMenus.Items("Help");bool hasKb=false;foreach(MenuItemSpec m in help)if(m.Id=="overlay-keyboard"&&m.Extra)hasKb=true;
      Check(hasKb,"the Help menu has the keyboard control row, marked as ours");
    }
    string path=Path.Combine(scratch,"overlay.txt");
    var d=OverlaySettings.Load(path);
    Check(d.Mods==3&&d.Vk==0x4F&&!d.FullScreenOnly&&!d.PauseOnOpen,"defaults without a file: Ctrl+Alt+O, bar in both modes, no pause on open");
    var s=new OverlaySettings{Mods=HotkeyRules.Ctrl,Vk=0x79-0,FullScreenOnly=true};
    s.Save(path);
    var back=OverlaySettings.Load(path);
    Check(back.Mods==HotkeyRules.Ctrl&&back.Vk==0x79&&back.FullScreenOnly&&!back.PauseOnOpen,"settings round trip");
    byte[] bytes=File.ReadAllBytes(path);
    Check(!(bytes.Length>=3&&bytes[0]==0xEF&&bytes[1]==0xBB&&bytes[2]==0xBF),"file has no byte-order mark");
    string text=Encoding.UTF8.GetString(bytes);
    Check(text.StartsWith("# Flutter Deck overlay settings\r\n")&&text.Contains("hotkey = Ctrl+F10\r\n")&&text.Contains("full-screen-only = yes\r\n")&&text.Contains("pause-on-open = no\r\n"),"written shape");
    s.FullScreenOnly=false;s.Save(path);
    Check(File.Exists(path+".bak")&&OverlaySettings.Load(path+".bak").FullScreenOnly,"the previous file is kept with .bak");
    Check(Directory.GetFiles(scratch,"*.tmp").Length==0,"no temporary file is left");
    File.WriteAllText(path,"hotkey = Ctrl+F11\r\nfull-screen-only = yes\r\n");
    var bad=OverlaySettings.Load(path);
    Check(bad.Mods==3&&bad.Vk==0x4F&&bad.FullScreenOnly,"a forbidden hotkey in the file is replaced by the default, other lines still count");
    File.WriteAllText(path,"hotkey = banana\r\n# comment\r\nunknown = 1\r\nnonsense line\r\npause-on-open = yes\r\n");
    var odd=OverlaySettings.Load(path);
    Check(odd.Vk==0x4F&&odd.PauseOnOpen,"damaged and unknown lines are ignored");
    File.WriteAllText(path,new String('x',OverlaySettings.MaxBytes+10));
    var big=OverlaySettings.Load(path);
    Check(big.Mods==3&&big.Vk==0x4F&&!big.FullScreenOnly,"an oversized file gives the defaults");
    File.WriteAllBytes(path,new byte[]{0xFF,0xFE,0x00,0x41});
    Check(OverlaySettings.Load(path).Vk==0x4F,"a binary file gives the defaults");
    Check(OverlaySettings.PathName.EndsWith(Path.Combine("FlutterDeck","overlay.txt")),"the real path is in the launcher's settings folder");
  }

  static void Menus(){
    Check(String.Join(",",OverlayMenus.Names)=="Library,Session,View,Saves,Audio,Help","the menus are the kit's six, in order");
    var view=OverlayMenus.Items("View");
    Check(view.Count>=6&&view[0].Label=="Fullscreen"&&view[0].Shortcut=="F11"&&view[5].Label=="UI scale\u2026"&&view[5].Arrow,"the View rows follow the kit");
    int kit=0;foreach(string n in OverlayMenus.Names)foreach(MenuItemSpec m in OverlayMenus.Items(n))if(!m.Extra)kit++;
    Check(kit==18,"18 retained kit rows; launcher music rows removed");
    var ids=new Dictionary<string,bool>();bool unique=true;
    foreach(string n in OverlayMenus.Names)foreach(MenuItemSpec m in OverlayMenus.Items(n)){if(ids.ContainsKey(m.Id))unique=false;ids[m.Id]=true;}
    Check(unique,"menu row ids are unique");
    Check(OverlayMenus.Items("Nope").Count==0,"an unknown menu has no rows");
    MenuItemSpec gs=null;foreach(MenuItemSpec m in OverlayMenus.Items("Session"))if(m.Id=="ses-settings")gs=m;
    Check(gs!=null&&gs.Arrow&&gs.Extra&&gs.Label=="Game settings\u2026","Session has a Game settings row that opens a panel (not one of the kit's rows)");
  }

  static void GameSettingModel(){
    var sw=new GameSetting{Id="a",Label="A",Value=1};
    Check(sw.IsSwitch&&sw.Ticked&&sw.Next()==0&&sw.ValueText()=="","a switch that is on shows a tick and a click turns it off");
    sw.Value=0;Check(!sw.Ticked&&sw.Next()==1,"a switch that is off shows no tick and a click turns it on");
    var ch=new GameSetting{Id="b",Label="B",Choices=new[]{"one","two","three"},Value=1};
    Check(!ch.IsSwitch&&!ch.Ticked&&ch.ValueText()=="two"&&ch.Next()==2,"a choice shows its entry and a click goes to the next one");
    ch.Value=2;Check(ch.Next()==0&&ch.ValueText()=="three","a click on the last entry wraps round to the first");
    ch.Value=9;Check(ch.ValueText()=="three"&&ch.Next()==0,"a value past the end is held at the last entry");
    ch.Value=-4;Check(ch.ValueText()=="one"&&ch.Next()==1,"a value below zero is held at the first entry");
    var empty=new GameSetting{Id="c",Choices=new string[0],Value=0};Check(empty.IsSwitch&&empty.Next()==1,"an empty choice list is treated as a switch");
    Check(new GameSetting().Enabled&&new GameSetting().Hint=="","a setting is on offer unless it is marked otherwise");
  }

  static void Features(){
    var none=OverlayFeatures.None;
    var pill=OverlayLayout.Pill(none);
    Check(String.Join(",",pill.ToArray())=="pause,restart,stop,fullscreen,menu","the pill always shows the kit's five controls");
    Check(OverlayLayout.Enabled("stop",none)&&OverlayLayout.Enabled("menu",none)&&!OverlayLayout.Enabled("pause",none)&&!OverlayLayout.Enabled("fullscreen",none),"without live control only stop and menu are enabled; the rest are drawn disabled");
    Check(!OverlayLayout.Offered("pause",none)&&!OverlayLayout.Offered("fullscreen",none)&&!OverlayLayout.Offered("restart",none),"nothing that needs the game is offered");
    var id=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{"contract","2.0"},{"features","card-new, exe-from-disc,fail-file,volume"}};
    var f=OverlayFeatures.FromIdentity(id);
    Check(f.Has("volume")&&f.Has("card-new")&&f.Has("EXE-FROM-DISC")&&!f.Has("pause"),"identity features are read, trimmed and case-blind");
    Check(!OverlayLayout.Enabled("pause",f)&&!OverlayLayout.Enabled("fullscreen",f),"the 2.0 game build's features offer no live control");
    var guess=new OverlayFeatures(new[]{"pause","restart","fullscreen","volume","screenshot"});
    Check(!OverlayLayout.Enabled("pause",guess)&&!OverlayLayout.Enabled("fullscreen",guess)&&!OverlayLayout.Enabled("volume",guess)&&!OverlayLayout.Enabled("screenshot",guess),"a feature named like the control is not enough: only the contract's word (live-control) offers them");
    var live=new OverlayFeatures(new[]{OverlayLayout.LiveControl});
    Check(OverlayLayout.Enabled("pause",live)&&OverlayLayout.Enabled("fullscreen",live)&&OverlayLayout.Enabled("volume",live)&&OverlayLayout.Enabled("screenshot",live)&&OverlayLayout.Enabled("speed",live)&&!OverlayLayout.Enabled("speed",guess),"live-control offers pause, fullscreen, volume and screenshot");
    Check(!OverlayLayout.Offered("restart",live)&&OverlayLayout.Enabled("stop",live)&&OverlayLayout.Enabled("menu",live),"live-control does not touch Restart (the launcher's own); Stop and Menu always work");
    Check(OverlayFeatures.FromIdentity(null)==OverlayFeatures.None&&OverlayFeatures.FromIdentity(new Dictionary<string,string>())==OverlayFeatures.None,"no identity, no features");
    Check(!OverlayFeatures.None.Has(null)&&!OverlayFeatures.None.Has(""),"empty names are never present");
  }
}
