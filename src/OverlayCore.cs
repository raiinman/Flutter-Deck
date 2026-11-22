using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

// The in-game overlay's decisions, with no window code: where the bar goes, when it shows and hides, which
// hotkeys are allowed, what is saved and which controls this game build can offer. GameOverlay.cs draws it and
// OverlayPureTest.cs tests this file. Written for the old C# 5 compiler (see SPEC-overlay.md).

// A rectangle in physical screen pixels. Right and Bottom are exclusive.
public struct PxRect {
  public int Left,Top,Right,Bottom;
  public PxRect(int left,int top,int right,int bottom){Left=left;Top=top;Right=right;Bottom=bottom;}
  public int Width {get{return Right-Left;}}
  public int Height {get{return Bottom-Top;}}
  public bool IsEmpty {get{return Right<=Left||Bottom<=Top;}}
  public bool Contains(int x,int y){return x>=Left&&x<Right&&y>=Top&&y<Bottom;}
  public bool SameAs(PxRect o){return Left==o.Left&&Top==o.Top&&Right==o.Right&&Bottom==o.Bottom;}
  public bool NearlyEquals(PxRect o,int slack){return Math.Abs(Left-o.Left)<=slack&&Math.Abs(Top-o.Top)<=slack&&Math.Abs(Right-o.Right)<=slack&&Math.Abs(Bottom-o.Bottom)<=slack;}
  public override string ToString(){return Left+","+Top+" to "+Right+","+Bottom;}
}

public static class OverlayGeometry {
  public const int ZoneHeightDip=90,BarMarginDip=27;
  public static int Scaled(int dip,double scale){return Math.Max(1,(int)Math.Round(dip*(scale>0?scale:1)));}
  // The band at the top edge of the game picture where the mouse wakes the bar: the whole width, 90 pixels deep.
  // The numbers follow how another emulator's top pill behaves (REFERENCE-pill-behaviour.md). The bar's own width
  // is not needed any more; the parameter stays so callers and tests keep one shape.
  public static PxRect HotZone(PxRect game,int barWidthPx,double scale){
    if(game.IsEmpty)return new PxRect();
    return new PxRect(game.Left,game.Top,game.Right,game.Top+Math.Min(Scaled(ZoneHeightDip,scale),game.Height));
  }
  // The pill sits centred under the top edge, 27 pixels down (the kit's value), and never outside the game picture.
  public static PxRect PlaceBar(PxRect game,int widthPx,int heightPx,double scale){
    if(game.IsEmpty)return new PxRect();
    int w=Math.Max(1,Math.Min(widthPx,game.Width)),h=Math.Max(1,Math.Min(heightPx,game.Height));
    int left=game.Left+(game.Width-w)/2;
    int top=game.Top+Math.Min(Scaled(BarMarginDip,scale),Math.Max(0,game.Height-h));
    return new PxRect(left,top,left+w,top+h);
  }
  // How much the whole overlay is shrunk so its tallest layout (worstDip, in device-independent pixels, with the 27 pixel margin above)
  // fits under the top of a game picture gameDip tall. 1 when it fits. Steps of 0.05 so it does not flicker, never below 0.6.
  public static double FitFactor(double gameDip,double worstDip){
    if(worstDip<=0||gameDip<=0)return 1;
    double f=Math.Floor((gameDip-BarMarginDip)/worstDip*20+1e-9)/20;
    return Math.Max(0.6,Math.Min(1,f));
  }
  // Where the first menu starts, in device-independent pixels from the pill's left edge: the kit's 237, pulled left when the
  // menu and its submenu would not fit in the picture, but never past the pill's own left edge.
  public static double MenuLeftFor(double availDip,double menuW,double subW,double gap,double kitLeft,bool sub){
    double need=menuW+(sub?gap+subW:0);
    return Math.Max(0,Math.Min(kitLeft,availDip-need));
  }
  // Borderless full screen: the game picture is the whole monitor, give or take two pixels.
  public static bool IsFullScreen(PxRect game,IEnumerable<PxRect> monitors){
    if(game.IsEmpty||monitors==null)return false;
    foreach(PxRect m in monitors)if(!m.IsEmpty&&game.NearlyEquals(m,2))return true;
    return false;
  }
}

public enum OverlayMode {Hidden,Bar,Menu,Keys}

// What the controller saw on one tick. Cursor is in screen pixels.
public struct OverlayObservation {
  public bool GameFound,GameForeground,OverlayForeground,FullScreen,CursorKnown;
  public int CursorX,CursorY;
  public PxRect Game,Zone,Ui; // Ui is where the overlay is drawn now; empty while it is hidden
}

// When the bar shows and hides. Time is passed in (milliseconds) so tests do not wait.
public sealed class OverlayState {
  // No dwell: the bar shows on the first tick the cursor is in the band. It hides 1.2 s after the cursor last touched the
  // band or the bar. An open menu does not time out; it ends with a click on its button, the hotkey, or the game losing focus.
  public const int DwellMs=0,LeaveMs=1200;
  public OverlayMode Mode=OverlayMode.Hidden;
  public bool Pinned;          // opened by the hotkey: stays until the hotkey is used again
  public bool FullScreenOnly;  // the hover bar only wakes in full screen; the hotkey still works
  public bool HoverEnabled=true; // false: the hover never opens the bar; the hotkey still works
  long zoneSince=-1,lastInside;
  bool suppressed; // closed on purpose (hotkey, Stop, Close): stays closed until the cursor has left the band
  public bool Visible {get{return Mode!=OverlayMode.Hidden;}}
  void Hide(){Mode=OverlayMode.Hidden;Pinned=false;zoneSince=-1;}
  static PxRect KeepRect(OverlayObservation o){
    if(o.Ui.IsEmpty)return o.Zone;
    // down to the bottom of the band as well as the bar, or a cursor resting between them would wake and hide the bar in turn
    return new PxRect(Math.Min(o.Zone.Left,o.Ui.Left),o.Game.Top,Math.Max(o.Zone.Right,o.Ui.Right),Math.Max(o.Zone.Bottom,o.Ui.Bottom));
  }
  public void Update(OverlayObservation o,long now){
    if(!o.GameFound){Hide();return;}
    if(Mode==OverlayMode.Keys)return; // the controller ends this mode itself
    bool active=o.GameForeground||o.OverlayForeground;
    if(Mode==OverlayMode.Hidden){
      bool inZone=o.GameForeground&&HoverEnabled&&o.CursorKnown&&(!FullScreenOnly||o.FullScreen)&&o.Zone.Contains(o.CursorX,o.CursorY);
      if(!inZone){zoneSince=-1;suppressed=false;return;}
      if(suppressed)return;
      if(zoneSince<0)zoneSince=now;
      if(now-zoneSince>=DwellMs){Mode=OverlayMode.Bar;Pinned=false;lastInside=now;zoneSince=-1;}
      return;
    }
    if(!active){Hide();return;}          // the player switched to another program
    if(Pinned)return;
    bool inside=o.CursorKnown&&KeepRect(o).Contains(o.CursorX,o.CursorY);
    if(inside)lastInside=now;
    else if(Mode!=OverlayMode.Menu&&now-lastInside>=LeaveMs)Hide();
  }
  // The hotkey: closes the overlay when it is open; opens it (and keeps it open) only while the game is in front.
  public void Hotkey(bool gameForeground,long now){
    if(Mode==OverlayMode.Keys)return;
    if(Visible){Hide();suppressed=true;return;}
    if(gameForeground){Mode=OverlayMode.Bar;Pinned=true;lastInside=now;}
  }
  public void OpenMenu(long now){if(Mode==OverlayMode.Bar){Mode=OverlayMode.Menu;lastInside=now;}}
  public void CloseMenu(long now){if(Mode==OverlayMode.Menu){Mode=OverlayMode.Bar;lastInside=now;}}
  public void EnterKeys(){if(Visible)Mode=OverlayMode.Keys;}
  public void LeaveKeys(long now){if(Mode==OverlayMode.Keys){Mode=OverlayMode.Menu;Pinned=false;lastInside=now;}}
  public void Close(){Hide();suppressed=true;}
}

// Which keys may open the overlay. The hotkey is a global key combination the launcher registers with Windows
// (RegisterHotKey); Windows keeps it from every program, the game included.
public static class HotkeyRules {
  public const int Alt=1,Ctrl=2,Shift=4;
  static readonly string[] FixedNames={"Space","Insert","Delete","Home","End","PageUp","PageDown"};
  static readonly int[] FixedCodes={0x20,0x2D,0x2E,0x24,0x23,0x21,0x22};
  public const int DefaultMods=Ctrl|Alt,DefaultKey=0x4F; // Ctrl+Alt+O
  // The name of a key that may be used, or null.
  public static string KeyName(int vk){
    if(vk>=0x41&&vk<=0x5A)return ((char)vk).ToString();
    if(vk>=0x30&&vk<=0x39)return ((char)vk).ToString();
    if(vk>=0x70&&vk<=0x79)return "F"+(vk-0x6F);
    for(int i=0;i<FixedCodes.Length;i++)if(FixedCodes[i]==vk)return FixedNames[i];
    return null;
  }
  public static int KeyCode(string name){
    if(String.IsNullOrEmpty(name))return -1;
    if(name.Length==1){char c=Char.ToUpperInvariant(name[0]);if((c>='A'&&c<='Z')||(c>='0'&&c<='9'))return c;}
    if(name.Length>=2&&(name[0]=='F'||name[0]=='f')){int n;if(Int32.TryParse(name.Substring(1),NumberStyles.None,CultureInfo.InvariantCulture,out n)&&n>=1&&n<=10)return 0x6F+n;}
    for(int i=0;i<FixedNames.Length;i++)if(String.Equals(FixedNames[i],name,StringComparison.OrdinalIgnoreCase))return FixedCodes[i];
    return -1;
  }
  public static string Format(int mods,int vk){
    var parts=new List<string>();
    if((mods&Ctrl)!=0)parts.Add("Ctrl");if((mods&Alt)!=0)parts.Add("Alt");if((mods&Shift)!=0)parts.Add("Shift");
    parts.Add(KeyName(vk)??("0x"+vk.ToString("X2",CultureInfo.InvariantCulture)));
    return String.Join("+",parts.ToArray());
  }
  public static bool TryParse(string text,out int mods,out int vk){
    mods=0;vk=-1;if(String.IsNullOrWhiteSpace(text))return false;
    string[] parts=text.Split('+');
    for(int i=0;i<parts.Length;i++){
      string p=parts[i].Trim();
      if(i<parts.Length-1){
        if(String.Equals(p,"Ctrl",StringComparison.OrdinalIgnoreCase))mods|=Ctrl;
        else if(String.Equals(p,"Alt",StringComparison.OrdinalIgnoreCase))mods|=Alt;
        else if(String.Equals(p,"Shift",StringComparison.OrdinalIgnoreCase))mods|=Shift;
        else return false;
      }else vk=KeyCode(p);
    }
    return vk>=0;
  }
  // Empty when the combination may be used, otherwise the reason. gameKeys holds the virtual-key codes the
  // game's own key map uses: a bare overlay key may not be one of them, and a modifier may not be one of them.
  public static string Check(int mods,int vk,IEnumerable<int> gameKeys){
    var game=new HashSet<int>();if(gameKeys!=null)foreach(int k in gameKeys)game.Add(k);
    if((mods&~(Ctrl|Alt|Shift))!=0)return "That combination is not supported.";
    if(vk==0x09||vk==0x7A||vk==0x7B||vk==0x0D)return "The game keeps Tab, F11, F12 and Alt+Enter for itself. Choose another key.";
    string name=KeyName(vk);
    if(name==null)return "Use a letter, a digit, F1 to F10, Space, Insert, Delete, Home, End, Page Up or Page Down.";
    if(vk==0x73&&(mods&Alt)!=0)return "Alt+F4 closes windows. Choose another combination.";
    if(vk==0x2E&&(mods&Ctrl)!=0&&(mods&Alt)!=0)return "Windows keeps Ctrl+Alt+Delete. Choose another combination.";
    bool fkey=vk>=0x70&&vk<=0x79;
    if(mods==0){
      if(!fkey)return "A key on its own would stop working in every program. Add Ctrl or Alt, or use F1 to F10.";
      if(game.Contains(vk))return "The game uses that key. Choose another.";
    }else if(!fkey&&(mods&(Ctrl|Alt))==0)return "With a letter, digit or Space, include Ctrl or Alt. Shift alone would break typing in other programs.";
    if((mods&Ctrl)!=0&&(game.Contains(0xA2)||game.Contains(0xA3)))return "Ctrl is one of the game's keys, so the game would see it. Choose a combination without Ctrl.";
    if((mods&Alt)!=0&&(game.Contains(0xA4)||game.Contains(0xA5)))return "Alt is one of the game's keys, so the game would see it. Choose a combination without Alt.";
    if((mods&Shift)!=0&&(game.Contains(0xA0)||game.Contains(0xA1)))return "Shift is one of the game's keys, so the game would see it. Choose a combination without Shift.";
    return "";
  }
}

// The overlay's own choices, in one small text file next to the launcher's other settings.
public sealed class OverlaySettings {
  public int Mods=HotkeyRules.DefaultMods,Vk=HotkeyRules.DefaultKey;
  public bool FullScreenOnly=false;
  public bool HoverBar=true;   // developer decision 2026-11-17: false turns the quick bar (hover) off for good; only the hotkey opens the overlay
  public bool Enabled=true;    // false: the launcher starts no overlay at all (no pill, no hotkey)
  public bool KeyboardMenu=true; // 2026-11-19: when the overlay key opens the overlay, arrow keys, Enter and Esc work the menus (the hover bar never takes the keyboard)
  public bool PauseOnOpen=false; // developer decision 2026-11-16: no. Kept for when the game can pause (not built).
  public const int MaxBytes=4096;
  public static string PathName {get{return Path.Combine(DeckPaths.Root,"overlay.txt");}}
  public string HotkeyText {get{return HotkeyRules.Format(Mods,Vk);}}
  static bool Yes(string v){return String.Equals(v,"yes",StringComparison.OrdinalIgnoreCase);}
  static bool No(string v){return String.Equals(v,"no",StringComparison.OrdinalIgnoreCase);}
  // A missing, oversized or damaged file gives the defaults; a hotkey that breaks the rules is replaced by the default.
  public static OverlaySettings Load(string path){
    var s=new OverlaySettings();
    try{
      if(!File.Exists(path)||new FileInfo(path).Length>MaxBytes)return s;
      foreach(string raw in File.ReadAllLines(path)){
        string line=raw.Trim();if(line.Length==0||line[0]=='#')continue;
        int eq=line.IndexOf('=');if(eq<1)continue;
        string name=line.Substring(0,eq).Trim().ToLowerInvariant(),value=line.Substring(eq+1).Trim();
        if(name=="hotkey"){int m,v;if(HotkeyRules.TryParse(value,out m,out v)&&HotkeyRules.Check(m,v,null).Length==0){s.Mods=m;s.Vk=v;}}
        else if(name=="full-screen-only")s.FullScreenOnly=Yes(value);
        else if(name=="pause-on-open")s.PauseOnOpen=Yes(value);
        else if(name=="quick-bar")s.HoverBar=!No(value);
        else if(name=="overlay")s.Enabled=!No(value);
        else if(name=="keyboard-menu")s.KeyboardMenu=!No(value);
      }
    }catch(Exception){return new OverlaySettings();}
    return s;
  }
  public string Format(){
    var t=new StringBuilder();
    t.Append("# Flutter Deck overlay settings\r\n");
    t.Append("hotkey = ").Append(HotkeyText).Append("\r\n");
    t.Append("full-screen-only = ").Append(FullScreenOnly?"yes":"no").Append("\r\n");
    t.Append("quick-bar = ").Append(HoverBar?"yes":"no").Append("\r\n");
    t.Append("overlay = ").Append(Enabled?"yes":"no").Append("\r\n");
    t.Append("keyboard-menu = ").Append(KeyboardMenu?"yes":"no").Append("\r\n");
    t.Append("pause-on-open = ").Append(PauseOnOpen?"yes":"no").Append("\r\n");
    return t.ToString();
  }
  public void Save(string path){
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
    string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
    try{
      File.WriteAllText(temp,Format(),new UTF8Encoding(false));
      if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);
    }finally{if(File.Exists(temp))File.Delete(temp);}
  }
}

// What this game build can do while it runs. The names come from the game's own answer to --identity
// ("features=a,b,c"). The live-control channel does not exist yet (REQUEST-game-live-control.md), so no build
// names any of the controls below and the overlay offers only what the launcher can do by itself.
public sealed class OverlayFeatures {
  readonly HashSet<string> have=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  public static readonly OverlayFeatures None=new OverlayFeatures(null);
  public OverlayFeatures(IEnumerable<string> names){if(names!=null)foreach(string n in names)if(!String.IsNullOrWhiteSpace(n))have.Add(n.Trim());}
  public bool Has(string name){return !String.IsNullOrEmpty(name)&&have.Contains(name);}
  public static OverlayFeatures FromIdentity(Dictionary<string,string> identity){
    string list;if(identity==null||!identity.TryGetValue("features",out list)||list==null)return None;
    return new OverlayFeatures(list.Split(','));
  }
}

public static class OverlayLayout {
  // Controls that need the game to listen while it runs, and the feature name the game will use for each once its
  // contract has one. A null name means "not named yet": the control is never offered. When the game side names
  // them, fill the names in here and add the matching button in GameOverlay.cs. Never guess a name.
  static readonly Dictionary<string,string> GameControls=new Dictionary<string,string>{{"pause",null},{"restart",null},{"fullscreen",null},{"volume",null},{"screenshot",null}};
  public static bool Offered(string control,OverlayFeatures features){
    string name;
    return features!=null&&GameControls.TryGetValue(control,out name)&&name!=null&&features.Has(name);
  }
  // The pill's five controls in the order of the developer's kit: pause, restart, stop, full screen, menu. All five are always
  // shown, as the kit draws them; the ones that need the game to listen are drawn disabled until the game offers them.
  public static List<string> Pill(OverlayFeatures features){
    return new List<string>{"pause","restart","stop","fullscreen","menu"};
  }
  // Stop and menu are the launcher's own and always work; pause and full screen only when the game offers them. Restart is the
  // launcher's own too, but it is enabled by the launcher passing OverlayHost.RestartGame, not by this table.
  public static bool Enabled(string control,OverlayFeatures features){
    return control=="stop"||control=="menu"||Offered(control,features);
  }
}

// Moving a highlight through a row of entries, some of them greyed out (keyboard control of the menus).
public static class OverlayNav {
  // The next enabled entry from 'from' in direction dir (+1 or -1), wrapping round. from -1 (or outside the list) starts before the
  // first entry going forward, after the last going back. Returns -1 when no entry is enabled.
  public static int Next(bool[] enabled,int from,int dir){
    if(enabled==null||enabled.Length==0)return -1;
    int n=enabled.Length,step=dir<0?-1:1,i=from;
    if(i<0||i>=n)i=step>0?-1:n;
    for(int k=0;k<n;k++){i=((i+step)%n+n)%n;if(enabled[i])return i;}
    return -1;
  }
}

// One play setting the launcher can change for the next start of the game (SPEC-overlay.md, "Game settings"). The launcher
// builds the list from what the game program reports; the overlay only shows it and asks the launcher to change a value.
// A switch has Choices null and Value 0 or 1. A choice has a list of texts and Value is the index of the current one.
public sealed class GameSetting {
  public string Id="",Label="",Hint="";public string[] Choices;public int Value;
  public bool Enabled=true; // false: drawn greyed out; Hint says why
  public bool IsSwitch{get{return Choices==null||Choices.Length==0;}}
  // The value a click gives: a switch flips, a choice goes to the next entry and wraps round.
  public int Next(){return IsSwitch?(Value==0?1:0):(Math.Max(0,Math.Min(Choices.Length-1,Value))+1)%Choices.Length;}
  // What the row shows on its right: nothing for a switch (it has a tick), the current entry for a choice.
  public string ValueText(){return IsSwitch?"":Choices[Math.Max(0,Math.Min(Choices.Length-1,Value))];}
  public bool Ticked{get{return IsSwitch&&Value!=0;}}
}

// One row of the pill's menus.
public sealed class MenuItemSpec {
  public string Id,Label,Shortcut;public bool Arrow,Extra;
  public MenuItemSpec(string id,string label,string shortcut,bool arrow,bool extra){Id=id;Label=label;Shortcut=shortcut;Arrow=arrow;Extra=extra;}
}

// The pill's menus, with the names and rows of the developer's kit (labels.json). Rows marked Extra are ours: they keep
// working features reachable (music track, overlay key, hover setting) and the game controls the contract names or the
// developer has asked for (volume, mute, screenshot, reload keys, status, picture preset); the latter are disabled until the game or DSI Prism offers them. None is in the kit. A row only works when the
// overlay gives it an action; the rest are drawn disabled, as the kit says unsupported actions must be.
public static class OverlayMenus {
  public static readonly string[] Names={"Library","Session","View","Saves","Audio","Help"};
  static MenuItemSpec I(string id,string label){return new MenuItemSpec(id,label,null,false,false);}
  static MenuItemSpec X(string id,string label){return new MenuItemSpec(id,label,null,false,true);}
  public static List<MenuItemSpec> Items(string menu){
    var l=new List<MenuItemSpec>();
    switch(menu){
      case "Library":l.Add(I("lib-games","Game library"));l.Add(I("lib-folder","Choose game folder…"));l.Add(I("lib-return","Return to launcher"));break;
      case "Session":l.Add(I("ses-resume","Resume game"));l.Add(I("ses-restart","Restart session…"));l.Add(I("ses-stop","Stop session…"));
        l.Add(X("ses-reload-keys","Reload key map"));l.Add(X("ses-status","Game status"));l.Add(new MenuItemSpec("ses-settings","Game settings…",null,true,true));break;
      case "View":
        l.Add(new MenuItemSpec("view-fullscreen","Fullscreen","F11",false,false));l.Add(I("view-fit","Fit game to window"));l.Add(I("view-aspect","Preserve 4:3 aspect"));
        l.Add(I("view-perf","Performance overlay"));l.Add(I("view-status","Deck status bar"));l.Add(new MenuItemSpec("view-scale","UI scale…",null,true,false));
        l.Add(new MenuItemSpec("view-screenshot","Take screenshot","F12",false,true));l.Add(new MenuItemSpec("view-preset","Picture preset…",null,true,true));
        l.Add(X("overlay-hover","Quick bar (hover)"));l.Add(X("overlay-fullscreen-only","Quick bar only in full screen"));break;
      case "Saves":l.Add(I("sav-states","Save states…"));l.Add(I("sav-load","Load state…"));l.Add(I("sav-folder","Open save folder"));break;
      case "Audio":l.Add(new MenuItemSpec("aud-game-volume","Game volume...",null,true,true));l.Add(X("aud-game-mute","Mute game audio"));break;
      case "Help":l.Add(I("overlay-keys","Controls reference"));l.Add(I("help-setup","Setup guide"));l.Add(I("help-about","About Flutter Deck"));l.Add(X("overlay-hotkey","Overlay key"));l.Add(X("overlay-keyboard","Keyboard control (overlay key)"));break;
    }
    return l;
  }
}
