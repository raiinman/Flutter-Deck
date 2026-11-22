using System;
public static class DeckStatePureTest {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 public static int Main(){
  var screens=DeckState.Screens;Check(screens.Length==12,"screen count");
  foreach(string id in screens){Check(DeckState.IsScreen(id),"known "+id);Check(!String.IsNullOrEmpty(DeckState.Heading(id)),"heading "+id);}
  Check(!DeckState.IsScreen(null)&&!DeckState.IsScreen("")&&!DeckState.IsScreen("home")&&!DeckState.IsScreen("Features")&&!DeckState.IsScreen("Updates")&&!DeckState.IsScreen("Workshop")&&!DeckState.IsScreen("Gallery"),"unknown names refused");
  screens[0]="tampered";Check(DeckState.IsScreen("Home")&&!DeckState.IsScreen("tampered"),"screen list is not shared");
  Check(DeckState.Heading("Downloads")=="Updates"&&DeckState.NavLabel("Downloads")=="Updates"&&DeckState.Heading("Settings")=="Settings"&&DeckState.NavLabel("Settings")=="Settings"&&DeckState.Heading("Advanced")=="Advanced Settings"&&DeckState.NavLabel("Advanced")=="Advanced Settings"&&DeckState.Heading("Help")=="Help","headings");
  int library=0,transport=0,catalog=0;foreach(string id in DeckState.Screens){if(DeckState.ShowsLibrary(id))library++;if(DeckState.ShowsTransport(id))transport++;if(DeckState.CatalogCategory(id)!=null)catalog++;}
  Check(library==1&&DeckState.ShowsLibrary("Library"),"library region");Check(transport==2&&DeckState.ShowsTransport("Library")&&DeckState.ShowsTransport("Music"),"transport region");
  Check(catalog==2&&DeckState.CatalogCategory("Advanced")=="All"&&DeckState.CatalogCategory("Settings")==null&&DeckState.CatalogCategory("Saves")=="Saves"&&DeckState.CatalogCategory("Workshop")==null&&DeckState.CatalogCategory("Gallery")==null&&DeckState.CatalogCategory("Music")==null,"catalog routes");

  // Music: every combination of the stored flags maps to exactly one phase.
  // All 32 flag combinations, with and without a track: stated as rules, not as a copy of the mapping.
  for(int bits=0;bits<16;bits++){bool playing=(bits&1)!=0,ready=(bits&2)!=0,paused=(bits&4)!=0,failed=(bits&8)!=0;string at=" at "+bits;
   Check(DeckState.Music(-1,playing,ready,paused,failed)==MusicPhase.Empty,"no track is always Empty"+at);
   var phase=DeckState.Music(0,playing,ready,paused,failed);
   Check(phase!=MusicPhase.Empty,"a track is never Empty"+at);
   Check((phase==MusicPhase.Failed)==failed,"Failed exactly when the open failed"+at);
   Check((phase==MusicPhase.Loading)==(!failed&&!ready),"Loading exactly when not failed and not ready"+at);
   Check((phase==MusicPhase.Playing)==(!failed&&ready&&playing),"Playing only when audible"+at);
   Check((phase==MusicPhase.Paused)==(!failed&&ready&&!playing&&paused),"Paused only when ready and paused"+at);
   Check((phase==MusicPhase.Stopped)==(!failed&&ready&&!playing&&!paused),"Stopped is the ready idle state"+at);
  }
  Check(DeckState.Music(-1,true,true,true,true)==MusicPhase.Empty,"empty wins");
  Check(DeckState.Music(0,true,false,false,true)==MusicPhase.Failed,"failed is not playing");
  Check(DeckState.Music(0,true,false,false,false)==MusicPhase.Loading&&DeckState.Music(0,false,false,false,false)==MusicPhase.Loading,"loading");
  Check(DeckState.Music(0,true,true,false,false)==MusicPhase.Playing,"playing");
  Check(DeckState.Music(0,false,true,true,false)==MusicPhase.Paused&&DeckState.Music(0,false,true,false,false)==MusicPhase.Stopped,"paused/stopped");
  foreach(MusicPhase phase in Enum.GetValues(typeof(MusicPhase)))Check((DeckState.MusicLabel(phase).Length==0)==(phase==MusicPhase.Playing),"label only when silent: "+phase);

  Check(DeckState.Setup("Library",2,true)==SetupPhase.Closed&&DeckState.Setup("Setup",0,true)==SetupPhase.Blocked&&DeckState.Setup("Setup",0,false)==SetupPhase.ChooseTarget&&DeckState.Setup("Setup",1,false)==SetupPhase.SelectFiles&&DeckState.Setup("Setup",2,false)==SetupPhase.Review,"setup phases");

  {var h=new NavHistory();Check(!h.CanBack&&!h.CanForward&&h.Back("Home")==null&&h.Forward("Home")==null,"empty history: nothing to go back or forward to");
   h.Visit("Home","Library");h.Visit("Library","Settings");Check(h.CanBack&&!h.CanForward&&h.BackCount==2,"two visits remembered");
   Check(h.Back("Settings")=="Library"&&h.CanForward&&h.Back("Library")=="Home"&&!h.CanBack&&h.ForwardCount==2,"Back walks the visits in reverse");
   Check(h.Forward("Home")=="Library"&&h.Forward("Library")=="Settings"&&!h.CanForward&&h.BackCount==2,"Forward walks them again");
   h.Back("Settings");h.Visit("Library","Music");Check(!h.CanForward&&h.Back("Music")=="Library","a new visit after Back empties the Forward list");
   var same=new NavHistory();same.Visit("Home","Home");same.Visit("Home","nowhere");same.Visit(null,"Home");Check(!same.CanBack,"the same screen, an unknown screen or none is not remembered");
   var many=new NavHistory();for(int i=0;i<200;i++)many.Visit(i%2==0?"Home":"Library",i%2==0?"Library":"Home");Check(many.BackCount==NavHistory.Limit,"at most the limit is kept");
   int steps=0;while(many.CanBack){many.Back("Home");steps++;}Check(steps==NavHistory.Limit&&many.ForwardCount==NavHistory.Limit,"the kept visits can all be walked back");}
  string snap=DeckState.Snapshot("Library",1,MusicPhase.Stopped,1,true,false,false,NewsPhase.Unconfigured,2,false,SetupPhase.Closed,100,UpdatePhase.Idle,PlayPhase.Unavailable);
  Check(snap=="screen=Library;game=1;music=stopped;tracks=1;repeat=on;shuffle=off;muted=off;news=unconfigured;unread=2;scaling=stopped;setup=closed;play=unavailable;text=100;update=idle","snapshot shape: "+snap);
  string odd=DeckState.Snapshot("C:\\private\\path",7,MusicPhase.Failed,-3,false,true,true,NewsPhase.ConfigError,-1,true,SetupPhase.SelectFiles,120,UpdatePhase.InstallFailed,PlayPhase.NeedsSetup);
  Check(odd=="screen=unknown;game=1;music=failed;tracks=0;repeat=off;shuffle=on;muted=on;news=config-error;unread=0;scaling=active;setup=select-files;play=needs-setup;text=120;update=install-failed","snapshot bounds: "+odd);
  foreach(char c in snap+odd)Check(Char.IsLetterOrDigit(c)||c=='='||c==';'||c=='-',"snapshot carries tokens only");
  Console.WriteLine("PASS: screen table, region and catalog routes, music phase rules over all 32 flag combinations, setup phases, token-only state snapshot. Pure logic; no UI, audio, file or network executed.");return 0;
 }
}
