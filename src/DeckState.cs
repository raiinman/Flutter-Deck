using System;
using System.Text;

// The launcher's state machines, named in one place. This file is pure: no WPF, no file,
// network or audio access, and no dependency on the other source files, so it can be
// compiled and tested alone (see DeckStatePureTest.cs / check-state.ps1).
//
// Machine            Stored in                                   Read through
// Screen             Deck.screen (string id)                     IsScreen/Heading/ShowsLibrary/ShowsTransport/CatalogCategory
// Music              DeckAudio.Index/Playing/Ready/Paused/Failed Music()
// News               Deck.newsPhase (+ newsBusy, newsEndpoint)   NewsPhase
// Scaling            Deck.scalingOutput (null or active)         Snapshot(scalingActive)
// Setup wizard       Deck.screen=="Setup", setupStep, loadFailed Setup()
// Update             Deck.updatePhase (Updater.cs does the work)  UpdatePhase
// Play               Deck.playPhase (GameRuntime.cs does the work)  PlayPhase; game 2 is always unavailable
//
// Snapshot() is what Deck publishes on the window's UI Automation ItemStatus. It carries
// enumerated tokens only: never paths, file names, track titles, window titles or the feed address.
public enum MusicPhase { Empty, Loading, Playing, Paused, Stopped, Failed }
public enum NewsPhase { Unconfigured, ConfigError, Idle, Checking, Updated, Failed }
public enum SetupPhase { Closed, ChooseTarget, SelectFiles, Review, Blocked }
// Off: automatic check disabled and nothing checked this session. Idle: enabled, not checked yet.
// Play: Unavailable for a game with no native program; NeedsSetup until every file is in place; Failed after a bad exit.
public enum PlayPhase { Unavailable, NeedsSetup, Ready, Running, Failed }
public enum UpdatePhase { Off, Idle, Checking, Current, Available, Unreachable, Untrusted, Downloading, Ready, InstallFailed }

public static class DeckState {
  // Navigation order first, then the screens reached from other controls.
  static readonly string[] Ids={"Home","Scaling","Library","Downloads","Music","Settings","Advanced","Saves","Preflight","Help","Setup","Gamepad"};
  public static string[] Screens {get{return (string[])Ids.Clone();}}
  public static bool IsScreen(string name){return name!=null&&Array.IndexOf(Ids,name)>=0;}
  // Settings is the page of game settings (developer, 2026-11-20); what it showed before (the feature catalog) is Advanced Settings.
  public static string Heading(string name){return name=="Downloads"?"Updates":name=="Advanced"?"Advanced Settings":name=="Gamepad"?"Gamepad buttons":name;}
  public static string NavLabel(string name){return name=="Downloads"?"Updates":name=="Advanced"?"Advanced Settings":name;}
  public static bool ShowsLibrary(string name){return name=="Library";}
  public static bool ShowsTransport(string name){return name=="Library"||name=="Music";}
  // Feature-catalog screens and their starting category; null for every other screen.
  public static string CatalogCategory(string name){return name=="Advanced"?"All":name=="Saves"?"Saves":null;}

  public static MusicPhase Music(int index,bool playing,bool ready,bool paused,bool failed){
    if(index<0)return MusicPhase.Empty;
    if(failed)return MusicPhase.Failed;
    if(!ready)return MusicPhase.Loading;
    if(playing)return MusicPhase.Playing;
    return paused?MusicPhase.Paused:MusicPhase.Stopped;
  }
  // Text for the lower-deck level box when nothing is audible; empty while playing.
  public static string MusicLabel(MusicPhase phase){
    switch(phase){
      case MusicPhase.Playing:return "";
      case MusicPhase.Paused:return "Paused";
      case MusicPhase.Loading:return "Loading";
      case MusicPhase.Failed:return "Audio unavailable";
      case MusicPhase.Empty:return "No track";
      default:return "Stopped";
    }
  }
  public static SetupPhase Setup(string screen,int step,bool loadFailed){
    if(screen!="Setup")return SetupPhase.Closed;
    if(loadFailed)return SetupPhase.Blocked;
    return step<=0?SetupPhase.ChooseTarget:step==1?SetupPhase.SelectFiles:SetupPhase.Review;
  }
  static string Token(string value){var b=new StringBuilder();foreach(char c in value){if(Char.IsUpper(c)&&b.Length>0)b.Append('-');b.Append(Char.ToLowerInvariant(c));}return b.ToString();}
  public static string Snapshot(string screen,int game,MusicPhase music,int tracks,bool repeat,bool shuffle,bool muted,NewsPhase news,int unread,bool scalingActive,SetupPhase setup,int textPercent,UpdatePhase update,PlayPhase play){
    return "screen="+(IsScreen(screen)?screen:"unknown")
      +";game="+(game>=1&&game<=3?game:1)
      +";music="+Token(music.ToString())+";tracks="+Math.Max(0,tracks)+";repeat="+(repeat?"on":"off")+";shuffle="+(shuffle?"on":"off")+";muted="+(muted?"on":"off")
      +";news="+Token(news.ToString())+";unread="+Math.Max(0,unread)
      +";scaling="+(scalingActive?"active":"stopped")
      +";setup="+Token(setup.ToString())
      +";play="+Token(play.ToString())
      +";text="+textPercent
      +";update="+Token(update.ToString());
  }
}

// Back and Forward between screens (developer, 2026-11-20). Like a web browser:
// a new screen is remembered on the Back list and empties the Forward list; Back and Forward move between the two lists. Screens only,
// not the state inside a screen. At most Limit screens are kept on each list. Pure: no UI, file or network access.
public sealed class NavHistory {
  public const int Limit=50;
  readonly System.Collections.Generic.List<string> back=new System.Collections.Generic.List<string>(),forward=new System.Collections.Generic.List<string>();
  public bool CanBack {get{return back.Count>0;}}
  public bool CanForward {get{return forward.Count>0;}}
  public int BackCount {get{return back.Count;}}
  public int ForwardCount {get{return forward.Count;}}
  static void Push(System.Collections.Generic.List<string> list,string id){list.Add(id);if(list.Count>Limit)list.RemoveAt(0);}
  // A visit from FROM to TO. Nothing happens for the same screen or an unknown one.
  public void Visit(string from,string to){if(!DeckState.IsScreen(from)||!DeckState.IsScreen(to)||from==to)return;Push(back,from);forward.Clear();}
  // The screen Back goes to from CURRENT, or null when there is none. CURRENT goes onto the Forward list.
  public string Back(string current){if(back.Count==0)return null;string to=back[back.Count-1];back.RemoveAt(back.Count-1);if(DeckState.IsScreen(current))Push(forward,current);return to;}
  public string Forward(string current){if(forward.Count==0)return null;string to=forward[forward.Count-1];forward.RemoveAt(forward.Count-1);if(DeckState.IsScreen(current))Push(back,current);return to;}
}
