using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
#if FD_V1
[assembly: System.Reflection.AssemblyMetadata("FlutterDeckEdition","public-v1")]   // the mark of the public build: build-setup.ps1 packs only a launcher that has it
#endif

// "Play with a friend" for Mega Man Legends (SPEC-multiplayer.md; game-side contract MP-1). This is the same window class
// as Program.cs, in its own file.
// Rules of this file (check-multiplayer.ps1 reads it):
//  - The key, the setting that holds it and the invite code live in memory only (the FriendPlay object). They are never
//    written to a file, a log, the session log, a message of an error or the command line. The invite code reaches the
//    clipboard only when the player presses "Copy the code".
//  - The game is started in one way only: this file presses the one Play button of Program.cs, and only while that button
//    is enabled. Program.cs passes the setting to the game's environment (GameRuntime.Start).
//  - A router is asked only after the player pressed "Host a game" and said yes, or pressed "Check my router" (which
//    changes nothing). At start, only rules that this launcher made and recorded are taken away again.
//  - No firewall rule is changed.
public partial class Deck {
  sealed class FriendPlay { public bool Host,Local;public string Setting="",Code="";public List<MpRule> Rules=new List<MpRule>(); }
  FriendPlay friend;                 // from the press of Host or Join until that game has ended
  bool friendBusy;string friendNote="",friendRuleNote="",friendStatus="",friendTyped="",friendJoinDraft="";HostRoute friendRoute;
  int friendTicket;                  // goes up at each press of Host and each close of the window: an older answer of the router is then not used
  Window friendWindow;StackPanel friendPanel;TextBlock friendStatusLine;DispatcherTimer friendTimer;
  readonly object friendRulesLock=new object();

  // Program.cs asks this at every start of a game: the setting for the game's environment, or null for an ordinary game.
  string FriendSetting(){return friend!=null&&game==1?friend.Setting:null;}
  // Owner, 2026-11-20: in the public version 1 the button stays visible but greyed out until multiplayer works. Only a build made with
  // build.ps1 -PublicV1 (the compile symbol below) does that; the developer's own build keeps friend play for tests on one PC.
#if FD_V1
  static readonly bool FriendSoon=true;
#else
  static readonly bool FriendSoon=false;
#endif
  bool FriendOffered(LibraryItem item){return item!=null&&item.BuiltIn==1&&game==1&&playCheck!=null&&playCheck.HasMultiplayer;}

  // ---- the record of router rules: written before a rule is asked for, so a rule is never forgotten ----
  // Throws when the rule is not in the file afterwards: then no rule is asked for.
  void RecordRule(MpRule rule){
    lock(friendRulesLock){
      if(!MpCore.ValidRule(rule))throw new InvalidOperationException("This rule cannot be recorded.");
      var all=MpCore.LoadRules(MpCore.RulesPathName);
      all.RemoveAll(delegate(MpRule x){return MpCore.SameRule(x,rule);});
      if(all.Count>=MpCore.MaxRules)throw new InvalidOperationException("The record of router rules is full.");
      all.Add(rule);
      MpCore.SaveRules(MpCore.RulesPathName,all);
      if(!MpCore.LoadRules(MpCore.RulesPathName).Exists(delegate(MpRule x){return MpCore.SameRule(x,rule)&&x.MadeUtc==rule.MadeUtc;}))throw new InvalidOperationException("The record of router rules was not written.");
    }
  }
  // Drops the record of exactly this rule. A newer rule for the same box and port (a later hosting) is left alone.
  void ForgetRule(MpRule rule){
    lock(friendRulesLock){
      try{var all=MpCore.LoadRules(MpCore.RulesPathName);if(all.RemoveAll(delegate(MpRule x){return MpCore.SameRule(x,rule)&&x.MadeUtc==rule.MadeUtc;})>0)MpCore.SaveRules(MpCore.RulesPathName,all);}catch(Exception){}
    }
  }
  bool StillRecorded(MpRule rule){
    lock(friendRulesLock){
      try{return MpCore.LoadRules(MpCore.RulesPathName).Exists(delegate(MpRule x){return MpCore.SameRule(x,rule)&&x.MadeUtc==rule.MadeUtc;});}catch(Exception){return false;}
    }
  }
  // One router job at a time on this computer, also across two Flutter Decks. Returns false when the turn did not come.
  static bool RouterJob(Action job){
    using(var gate=new System.Threading.Mutex(false,"Local\\FlutterDeck-router")){
      bool held=false;
      try{
        try{held=gate.WaitOne(90000);}catch(System.Threading.AbandonedMutexException){held=true;}
        if(held)job();
        return held;
      }finally{if(held)gate.ReleaseMutex();}
    }
  }
  // Takes rules away on a background thread and says what could not be removed. A rule whose record was replaced by a
  // later hosting is not touched: it is that hosting's rule now.
  void CloseFriendRules(List<MpRule> rules){
    if(rules==null||rules.Count==0)return;
    var thread=new System.Threading.Thread(delegate(){
      List<string> said=null;
      try{
        RouterJob(delegate{
          var mine=new List<MpRule>();foreach(var rule in rules)if(StillRecorded(rule))mine.Add(rule);
          said=RouterPort.Close(mine,ForgetRule);
        });
      }catch(Exception){said=null;}
      if(said==null)said=new List<string>{"A router rule may still be there. Flutter Deck tries to remove it at a later start."};
      if(said.Count==0)return;
      try{Dispatcher.BeginInvoke(new Action(delegate{friendRuleNote=String.Join(" ",said.ToArray());RenderFriend();}));}catch(Exception){}
    }){IsBackground=true};
    thread.Start();
  }
  // At start, and when the window is opened: rules that an earlier hosting left (the launcher was closed first, or the
  // box was not reachable). Nothing is asked while a hosting is prepared or a game of Mega Man Legends runs, and a rule
  // younger than 15 minutes is left alone: another Flutter Deck may be preparing a hosting with it.
  void CleanFriendRules(){
    lock(friendRulesLock){try{if(MpCore.LoadRules(MpCore.RulesPathName).Count==0)return;}catch(Exception){return;}}
    var thread=new System.Threading.Thread(delegate(){
      try{
        RouterJob(delegate{
          bool active;
          try{active=(bool)Dispatcher.Invoke(new Func<bool>(delegate{return friend!=null||friendBusy||friendRoute!=null;}));}catch(Exception){return;}
          if(active||GameRuntime.CardInUse(GameRuntime.DataFolder))return;
          List<MpRule> rules;
          lock(friendRulesLock){try{rules=MpCore.LoadRules(MpCore.RulesPathName);}catch(Exception){return;}}
          foreach(var rule in rules){
            if(MpCore.RuleExpired(rule,DateTime.UtcNow)){ForgetRule(rule);continue;}
            if(DateTime.UtcNow<rule.MadeUtc.AddMinutes(15))continue;
            try{RouterPort.Remove(RouterPort.BoxOf(rule),rule.Port,rule.Client);ForgetRule(rule);}catch(Exception){}   // not reachable now, or refused: the record stays
          }
        });
      }catch(Exception){}
    }){IsBackground=true};
    thread.Start();
  }
  // ---- the end of a multiplayer game ----
  void EndFriend(){
    var ended=friend;if(ended==null)return;
    friend=null;friendStatus="";
    if(friendTimer!=null)friendTimer.Stop();
    try{System.IO.File.Delete(GameRuntime.MultiplayerStatusPath(GameRuntime.DataFolder));}catch(Exception){}
    friendNote=ended.Host?"The hosted game ended. Its invite code no longer works.":"The game ended.";
    CloseFriendRules(ended.Rules);
    RenderFriend();
  }
  void FriendTick(){
    if(friend==null){friendTimer.Stop();return;}
    if(!OwnGameRunning&&DateTime.UtcNow>=restartBy){EndFriend();Render();return;}   // also when the end of the game was not reported
    string words=MpCore.StatusWords(MpCore.ParseStatus(MpCore.ReadStatus(GameRuntime.MultiplayerStatusPath(GameRuntime.DataFolder))),friend.Host);
    if(words!=friendStatus){friendStatus=words;if(friendStatusLine!=null)friendStatusLine.Text=words;}
  }

  // ---- starting ----
  // "" when Mega Man Legends can be started with multiplayer now, or the sentence that says why not.
  string FriendReady(){
    if(GameRunning)return RunningName+" is running. Close it first.";
    if(game!=1){game=1;librarySelected=-1;}
    UpdatePlay();
    if(gamePlay==null||!gamePlay.IsEnabled)return "Mega Man Legends is not ready to play: "+playCheck.Problem;
    if(!playCheck.HasMultiplayer)return "The chosen game program has no multiplayer. Choose a build with multiplayer in Setup.";
    return "";
  }
  void FriendStart(FriendPlay play){
    friend=play;friendNote="";friendStatus=MpCore.StatusWords(null,play.Host);
    if(friendTimer==null){friendTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};friendTimer.Tick+=delegate{FriendTick();};}
    if(gamePlay!=null&&gamePlay.IsEnabled)gamePlay.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    if(!OwnGameRunning){
      if(friend!=null)EndFriend();
      friendNote=playNote.Length>0?playNote.Split('\n')[0]:"The game did not start.";
    }else{sessionLog.Add(play.Host?"multiplayer-host":"multiplayer-join");friendTimer.Start();}
    Render();RenderFriend();
  }
  void FriendHost(){
    if(friendBusy||friend!=null||friendRoute!=null)return;
    string why=FriendReady();if(why.Length>0){friendNote=why;Render();RenderFriend();return;}
    if(MessageBox.Show(friendWindow,"Hosting asks your router to send UDP port "+MpCore.DefaultPort+" to this PC, so that your friend's game can reach yours. Flutter Deck takes that rule away again when the game ends. If your router does not take the rule, Flutter Deck shows the steps to do by hand.\n\nWhen the game starts, Windows may ask \"Allow this app to communicate on these networks?\". Choose Allow, or your friend cannot reach the game.\n\nGo on?","Flutter Deck - Host a game",MessageBoxButton.OKCancel,MessageBoxImage.Question)!=MessageBoxResult.OK)return;
    int ticket=++friendTicket;
    friendBusy=true;friendRuleNote="";friendNote="Asking your router. This can take some seconds.";RenderFriend();
    var thread=new System.Threading.Thread(delegate(){
      HostRoute route=null;
      try{RouterJob(delegate{route=RouterPort.Open(MpCore.DefaultPort,RecordRule,ForgetRule);});}catch(Exception){route=null;}
      if(route==null)route=new HostRoute{Stop="The router could not be asked now. Try again in a minute."};
      try{Dispatcher.BeginInvoke(new Action(delegate{FriendRouteReady(route,ticket);}));}catch(Exception){}
    }){IsBackground=true};
    thread.Start();
  }
  void FriendRouteReady(HostRoute route,int ticket){
    friendBusy=false;
    if(ticket!=friendTicket||friendWindow==null||updateClosed){CloseFriendRules(route.Rules);friendNote="";RenderFriend();return;}   // the player closed the window meanwhile: hosting is off, also when the window is open again
    if(route.Stop.Length>0){CloseFriendRules(route.Rules);friendNote=route.Stop;RenderFriend();return;}
    if(route.Public.Length==0){friendRoute=route;friendNote="";RenderFriend();return;}            // steps by hand, or the address must be typed
    FriendHostStart(route,route.Public);
  }
  void FriendHostStart(HostRoute route,string address){
    byte[] a;
    if(!MpCore.TryAddress(address,out a)||!MpCore.Usable(a)||MpCore.IsLoopback(a)||MpCore.IsCarrier(a)){friendRoute=route;friendNote="Type the address as four numbers with dots, for example 203.0.113.7.";RenderFriend();return;}   // the route stays: its rules are closed by Cancel or by closing the window
    friendRoute=null;
    string why=FriendReady();if(why.Length>0){CloseFriendRules(route.Rules);friendNote=why;Render();RenderFriend();return;}
    byte[] key=MpCore.NewKey();
    FriendStart(new FriendPlay{Host=true,Local=MpCore.IsPrivate(a),Setting=MpCore.HostSetting(MpCore.DefaultPort,key),Code=MpCore.Encode(a,MpCore.DefaultPort,key),Rules=route.Rules});
  }
  void FriendJoin(string code){
    if(friendBusy||friend!=null||friendRoute!=null)return;
    MpInvite invite;
    try{invite=MpCore.Decode(code);}catch(FormatException e){friendJoinDraft=code.Length>80?code.Substring(0,80):code;friendNote=e.Message;RenderFriend();return;}
    friendJoinDraft="";
    string why=FriendReady();if(why.Length>0){friendNote=why;Render();RenderFriend();return;}
    FriendStart(new FriendPlay{Host=false,Setting=MpCore.JoinSetting(invite)});
  }
  void FriendCheck(){
    if(friendBusy||friendRoute!=null)return;
    friendBusy=true;friendNote="Looking at your router. Nothing is changed.";RenderFriend();
    var thread=new System.Threading.Thread(delegate(){
      string text;
      try{text=RouterPort.Look(MpCore.DefaultPort);}catch(Exception){text="The router could not be asked. Nothing was changed.";}
      try{Dispatcher.BeginInvoke(new Action(delegate{friendBusy=false;friendNote=text;RenderFriend();}));}catch(Exception){}
    }){IsBackground=true};
    thread.Start();
  }

  // ---- the window ----
  void ShowFriendWindow(){
    if(FriendSoon)return;   // not in the public version 1, whoever asks
    if(friendWindow!=null){friendWindow.Activate();return;}
    friendPanel=new StackPanel{Margin=new Thickness(20,16,20,18)};
    friendWindow=new Window{Owner=this,Title="Flutter Deck - Play with a friend",Width=680,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Ink("#0E2233"),ShowInTaskbar=false,
      Content=new ScrollViewer{Content=friendPanel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxHeight=720}};
    Id(friendWindow,"friend-window");
    friendWindow.Closed+=delegate{
      friendTicket++;friendWindow=null;friendPanel=null;friendStatusLine=null;friendJoinDraft="";
      if(friendRoute!=null){CloseFriendRules(friendRoute.Rules);friendRoute=null;}   // hosting was not finished: take the rule away again
    };
    if(friend==null&&!friendBusy)friendNote="";
    RenderFriend();friendWindow.Show();
    CleanFriendRules();
  }
  TextBlock FriendText(string text,double size=14,string color="#F5EAD0"){var t=Label(text,size,color);t.Margin=new Thickness(0,0,0,8);return t;}
  TextBox FriendBox(string text,string id,string name){
    var box=new TextBox{Text=text,FontSize=17,FontFamily=new FontFamily("Consolas"),MinHeight=34,Padding=new Thickness(6,4,6,4),Background=Ink("#0B1C2A"),Foreground=Ink("#F5EAD0"),BorderBrush=Ink("#5985A0"),CaretBrush=Ink("#F5EAD0"),Margin=new Thickness(0,0,0,8)};
    Id(box,id);System.Windows.Automation.AutomationProperties.SetName(box,name);return box;
  }
  void RenderFriend(){
    if(friendPanel==null)return;
    var p=friendPanel;p.Children.Clear();friendStatusLine=null;
    var title=FriendText("Play Mega Man Legends with a friend",20);title.FontWeight=FontWeights.Bold;p.Children.Add(title);
    if(friend!=null){
      if(friend.Host){
        p.Children.Add(FriendText("Your game is hosting. Give this invite code only to the friend you invite. It holds your internet address and a secret key. It works only while this game runs."));
        var code=FriendBox(friend.Code,"friend-code","Invite code");code.IsReadOnly=true;p.Children.Add(code);
        var row=new WrapPanel{Margin=new Thickness(-2,0,0,8)};
        row.Children.Add(LibButton("Copy the code",delegate{try{Clipboard.SetText(friend!=null?friend.Code:"");friendNote="The code is copied.";}catch(Exception){friendNote="The code could not be copied. Select it and copy it by hand.";}RenderFriend();},"friend-copy","Copy the invite code"));
        p.Children.Add(row);
        if(friend.Local)p.Children.Add(FriendText("This code holds a local address. Only someone on your own network can join with it.",13,"#FFD98A"));
      }else p.Children.Add(FriendText("Your game is joining your friend's game."));
      friendStatusLine=FriendText(friendStatus,15,"#8BEFFF");Id(friendStatusLine,"friend-status");p.Children.Add(friendStatusLine);
      p.Children.Add(FriendText("Each of you plays a full game of your own. You see each other only while both are in play in the same stage and area. Enemies, items, damage and the story are not shared. To stop, close the game."+(friend.Host&&friend.Rules.Count>0?" Flutter Deck then takes the router rule away again.":""),13,"#AEC8DB"));
    }else if(friendBusy){
      var busy=FriendText(friendNote,15,"#8BEFFF");Id(busy,"friend-busy");p.Children.Add(busy);
      return;
    }else if(friendRoute!=null){
      if(friendRoute.Steps.Length>0){var steps=FriendText(friendRoute.Steps);Id(steps,"friend-steps");p.Children.Add(steps);}
      else p.Children.Add(FriendText("The port is open, but the router did not tell its internet address."));
      p.Children.Add(FriendText((friendRoute.Steps.Length>0?"When the rule exists, type":"Type")+" your public internet address here. The router's page shows it, often as \"WAN IP\" or \"Internet address\"."));
      var address=FriendBox(friendTyped,"friend-address","Your public internet address");address.Width=240;address.HorizontalAlignment=HorizontalAlignment.Left;p.Children.Add(address);
      var row=new WrapPanel{Margin=new Thickness(-2,0,0,8)};
      row.Children.Add(LibButton("Host with this address",delegate{friendTyped=address.Text.Trim();var route=friendRoute;if(route!=null)FriendHostStart(route,friendTyped);},"friend-address-go","Host with this address"));
      row.Children.Add(LibButton("Cancel",delegate{var route=friendRoute;friendRoute=null;if(route!=null)CloseFriendRules(route.Rules);friendNote="Hosting was cancelled.";RenderFriend();},"friend-address-cancel","Cancel hosting"));
      p.Children.Add(row);
    }else{
      p.Children.Add(FriendText("Each of you plays a full game of your own and sees the other Mega Man in it. Two players. Your friend needs Flutter Deck and a game program with multiplayer, built from their own disc."));
      var hostTitle=FriendText("Host",16,"#8BEFFF");hostTitle.FontWeight=FontWeights.Bold;p.Children.Add(hostTitle);
      p.Children.Add(FriendText("Flutter Deck opens one port on your router, starts the game and shows an invite code for your friend."));
      var row=new WrapPanel{Margin=new Thickness(-2,0,0,10)};
      row.Children.Add(LibButton("Host a game",FriendHost,"friend-host","Host a game for a friend"));
      row.Children.Add(LibButton("Check my router",FriendCheck,"friend-check","Check my router. Changes nothing."));
      p.Children.Add(row);
      var joinTitle=FriendText("Join",16,"#8BEFFF");joinTitle.FontWeight=FontWeights.Bold;p.Children.Add(joinTitle);
      p.Children.Add(FriendText("Paste the invite code that your friend gave you."));
      var typed=FriendBox(friendJoinDraft,"friend-join-code","Invite code from your friend");p.Children.Add(typed);
      var joinRow=new WrapPanel{Margin=new Thickness(-2,0,0,8)};
      joinRow.Children.Add(LibButton("Join the game",delegate{string text=typed.Text;typed.Text="";FriendJoin(text);},"friend-join","Join a friend's game"));
      p.Children.Add(joinRow);
    }
    if(friendNote.Length>0){var note=FriendText(friendNote,14,"#FFD98A");Id(note,"friend-note");p.Children.Add(note);}
    if(friendRuleNote.Length>0){var left=FriendText(friendRuleNote,14,"#FFB08A");Id(left,"friend-rule-note");p.Children.Add(left);}   // stays until the next hosting: a rule that could not be removed
  }
}
