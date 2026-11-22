using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

// Starting a native Mega Man Legends game program. Legends 2 (GameKind, contract L2-1) uses the same code with its own program, data folder and card.
// Legends 1: per the game side's LAUNCHER-CONTRACT.md (version 2.0:
// the program reads ROCK_NEO.EXE from the disc image itself, creates an empty memory card when there is none,
// takes a volume level, and answers --identity). The earlier 1.x program is recognised and refused with advice.
// No WPF here; the screen lives in Program.cs. Every folder is a parameter so the same code is tested with a
// stand-in program (see GameRuntimePureTest.cs).
// Rules:
//  - The game program and the disc's program file are the user's own files. The launcher stores where they are
//    and never bundles, downloads, copies or releases them.
//  - The launcher owns the memory card, the key map and the session folder for this game.
//  - A memory card is only ever copied in from a file the user picks, and never over an existing card.
//  - The program's SHA-256 is checked at every start; an unknown build needs the user's acknowledgement of that exact hash.
public sealed class RuntimeSettings { public string Program="",AcceptedHash=""; public int Scale=3,Volume=100; public bool Mute=false,OriginalPicture=false,OriginalMovement=false,OriginalControls=false,InvertLookY=false,NoRumble=false,ZTargetOff=false,AimStyleCamera=false,NoIntroMovie=false,AnalogPad=false,FreeCamera=false,LockMarker2=false,NoLockFlick=false,Teleport=true,Widescreen=false; public int RumbleStrength=100,MusicVolume=100,VoiceVolume=100,EffectsVolume=100; public int WindowHeight=720,LookSensitivity=100,CameraDistance=100,FarView=160; public bool CameraWalls=false;   /* experimental camera wall collision (--camera-walls 1), off by default */ }
public sealed class RuntimeCheck { public bool Ready,KnownBuild,NeedsAcknowledgement,HasPictureOption,HasWindowHeight,HasMovementOption,HasThirdPerson,HasPadMap,HasLookOptions,HasRumble,HasZTarget,HasCameraDistance,HasCameraWalls,HasIntroMovie,HasPadType,HasMultiplayer,HasVolumeSplit,HasTeleport,HasWidescreen,HasFarView,HasFreeCamera,HasFreeCameraDistance,HasLockMarker2,HasLockFlick; public string Problem="",Hash="",Build=""; }
// What differs between the two games (the game side's LAUNCHER-CONTRACT-MML2.md, version L2-1). Everything else is the same runtime.
// GmeImport: only Legends 1 may be given a .gme save; the two games never share a card file, a .gme import or a data folder.
public sealed class GameKind { public int Id; public string Name="",Folder="",DiscProgram="",GameLine="",ProgramName="",ProgramHint="",KnownHash=""; public bool GmeImport; }
public sealed class GameSession { public Process Process; public DateTime StartedUtc; public bool Reported; public string SessionsFolder="",Arguments=""; public readonly StringBuilder ErrorText=new StringBuilder(); }

public static class GameRuntime {
  // The one build the game side tested for contract 2.0 (rock_neo_rt_fd.exe). A rebuild of the game has a different hash.
  public const string KnownHash="DB6339B4A904FCCE3A81D5EC6D697B069ACA7C0696C664026402731DABF819C3";
  // The build the game side tested for contract 2.4 (rock_neo_rt_hd.exe): sharper smooth picture, first-start question, --original-picture.
  // The build the game side tested for contract 2.5 (rock_neo_rt_hp.exe): the 2.4 build plus --window-height.
  public const string HpHash="541E11ADA72B61D933EFBB0434D405EEA9A6D7F7B59CDE008C47D4F1010026D7";
  // The build the game side tested for contract 2.6 (rock_neo_rt_hc.exe): the 2.5 build plus PlayStation 5 pads over HID, the right stick and all four Xbox slots.
  public const string HcHash="FC39A96D31C1034F71E432E7AAB897B30AF03EC2706E99D697C948EA1CB7D97E";
  // The second build of contract 2.6 (rock_neo_rt_hc2.exe): the same options, with the four reconnect problems of the PlayStation 5 pad code fixed.
  public const string Hc2Hash="DA3672C87A0D50E27A93269B07701266808CFF89E81E0B06E46FCCBC22971327";
  // The build the game side tested for contract 2.7 (rock_neo_rt_mm.exe): modern movement, --movement modern|original, reports the feature 'movement'.
  // The build the game side tested for contract 2.9 (rock_neo_rt_mr.exe): the movement build plus vibration, --no-rumble, --rumble N, reports the feature 'rumble'.
  public const string MrHash="9FC2F859C09F185F920FEEC159FDD22BF8878FD5F3577664AD7CC143DE205873";
  // The build the game side tested for contract 2.10 (rock_neo_rt_mr2.exe): richer vibration (shaped hits, buster and weapon ticks, low-life heartbeat, landings), --rumble N as a strength.
  public const string Mr2Hash="B862821B451A2B25CE2AC994A855CAA9E7F494D166A9ABC69C51C53592E3B6F2";
  // The build the game side tested for contract 2.12 (rock_neo_rt_mi.exe): the opening Capcom movie before the game, --no-intro-movie, reports the feature 'intro-movie'.
  public const string MiHash="74BA59D64A7DBB0765EF355C359920F4568F053AC60E39956A9FADC9682E94A0";
  public const string VsHash="5B1C1599EDE897096A63830C966C1A3AA0432D092A913D093015F56E44DF9048";   // rock_neo_rt_vs.exe, contract 2.13 (separate music, voice and effects levels)
  public const string TeleHash="3D59D406B4712F7FF31EBAAEB6CBF3CE823A8DE1B97B1624C79BE3B984B4A3FC";   // rock_neo_rt_tele.exe, contract 2.14 (teleport by request file)
  public const string TmHash="DFA37D8DB5D7D5A07585CACF1D68B6D52E69EAC943F6809FA8CE0D8F1DA01D57";   // rock_neo_rt_tm.exe, contract 2.15 (teleport panel in the pause menu, opened with L2 + R2)
  public const string TlHash="ABEE8B7ADFA4A5EEC75BF941DA75EF629ACAB895AE7459E6CD8765C33E6A11D1";   // rock_neo_rt_tl.exe, contract 2.16 (a Teleport line under Close in the pause menu)
  public const string TfHash="A7E494EDF029404A733929AD01F63E41531C11CC18D76E4A5DA28CCB41F451CE";   // rock_neo_rt_tf.exe, contract 2.16 (the Teleport line drawn with the game's own letters and hand)
  public const string FbHash="7BBEB88DB3071E5E8410C02762EAAB920C1722CEDF145809C218454B8298AF1D";   // rock_neo_rt_fb.exe, contract 2.21 (the sharper picture no longer stops where the game copies the screen onto itself; includes the widescreen and the Teleport changes of 22:36)
  public const string AllHash="C446C6BED2ECD224063023FC685E0CCD74B77E769DFC0B945DAA514C3951FCD8";   // rock_neo_rt_all2.exe, contract 2.23: the one Mega Man Legends program with every feature (everything of rock_neo_rt_he2.exe, contract 2.22 with the gauges and the map at the wide edges and the map fix, plus multiplayer in the same program). Owner, 2026-11-20: "all of the features commited, merged and playable as 1".
  public const string Fv2Hash="D74BBE13357BE999FFA930622857D4541C6D11E42A021BEA7560D2AA56C4D747";   // rock_neo_rt_fv2.exe, contract 2.25: all5 plus --far-view (draw distance), on by default at 160 percent
  public const string All6Hash="BB41376814AAA844AC17083E2D664770E317298EBBABEF2AC5658EA9A598D3B2";   // rock_neo_rt_all6.exe, contract 2.27: every feature in one program, built the kit's way from a fresh translation: all5 plus draw distance (2.25), camera wall collision, the widened fade rectangle (2.26) and the pause-menu background (2.27).
  public const string All7Hash="84EBF9C5228DD7805E3617E04068ECDBE9ADDC8F76793187BDC6E4042DE23871";   // rock_neo_rt_all7.exe, contract 2.28: all6 plus the last widescreen round (the gauges and the map stay inside the original picture while no wide picture is shown).
  public const string All5Hash="97BE6D7440B6FF3B8C9267E0EBFB8A450DED4069A24E6D08E391A94CA8AD7065";   // rock_neo_rt_all5.exe, contract 2.24: the one program with every feature, with the widescreen fixes of 2026-11-20 (the fill of the enemy gauge moves with its frame; the title and card menus keep the original shape; 3D cutscenes stay wide).
  public const string SkHash="4A210152E0A914BF0547C7B940C2788DAE9FFAE1E223618D93A4F7A7DE5B458E";   // rock_neo_rt_sk.exe, contracts 2.19 and 2.20 (--widescreen PIXELS: a wider picture; the sky tiles drawn in the margins); a changed game when the option is on
  public const string TgHash="D2B139E2AD31029A283F8B093B723493FF66CF817C982685AA61E98D027180EC";   // rock_neo_rt_tg.exe, contract 2.16 (Teleport line without tearing in the added rows)
  public const string ThHash="4A494D8E0039C60DD5CC966CEDB617C135E51ECEA185D76F2CB59B870C46598E";   // rock_neo_rt_th.exe, contract 2.16 (more stage names in the Teleport panel)
  public const string TiHash="9260CA3CA6404943E8B38F3EBEEECB230AD7C9CAE428C27F3568ADEBDD702927";   // rock_neo_rt_ti.exe, contract 2.16 (stage 8 named Uptown)
  public const string MmHash="00649A4C8A4E7BF5AECFA674D1CBB9DAAB304BBA3C1936970EE6BE652E1681D0";
  // The build the game side tested for contract 2.8 (rock_neo_rt_tp.exe): third-person controls, --controls, --pad-map, --look-sensitivity, --look-invert; features thirdperson, pad-map, look-options.
  public const string TpHash="D22E1F090300E6F355B434098ADCE928EED856511071A9BAAEA9D442C2830AA0";
  public const string TpHashFourth="B1AA2E20E205FF45C4C74F06AD7FEE2502F800111C6FF509DF6A8292814FAE39";   // 2.8 with the message flag, before the title-menu fix (it turns DOWN into UP at the start menu)
  public const string TpHashThird="DFB6DB792FDCA37AEA8AA434300D4A93E54CB928DA003920B07A1C7045393571";   // the 2.8 build the developer first played (movement into prompts was wrong there)
  public const string TpHashSecond="136E7F9C74428666B254902D25551F25BA147B20D4BE3AE82F000BBF28FA1A87";   // the 2.8 build after the independent check's first six fixes
  public const string TpHashFirst="8F8F3EF61B595DFA012B78675012630C88DFA2D3BA6646FE746BAAFB0DC88E46";   // the first 2.8 build, before the independent check's fixes (kept known so an older copy is not flagged)
  public static readonly int[] WindowHeights=new[]{480,720,1080,1440,2160};
  public static int HeightFromScale(int scale){int want=scale*240,best=720;foreach(int h in WindowHeights)if(Math.Abs(h-want)<Math.Abs(best-want))best=h;return best;}
  public static int ClampHeight(int h){return Array.IndexOf(WindowHeights,h)>=0?h:720;}
  public const string HdHash="8833AC18015C64129C62DEA97A53DEDBB5DEABC812FC6A7949B1241682C908CC";
  // The earlier program (contract 1.x). It lacks the options this launcher now passes, so it is refused by name.
  public const string EarlierHash="FA12B6F06885B9F53AE1E52C52F6EF503D613D902000D8AD81DFA861FD446670";
  public const string DiscProgramName="ROCK_NEO.EXE"; // read from the disc image by the game itself
  // The Legends 2 build the game side tested for its launcher contract L2-1 (mml2_rt_014.exe, runtime contract 2.6).
  public const string Mml2Hash="D57DF0A7CE4C5779B3E2DADE37176A40E06AAFD2CEB4F8EB470046B7690DCE11";
  // The Legends 2 build of contract L2-2 (mml2_rt_017.exe): the same, plus the feature pad-type (--pad-type digital|analog; the launcher does not pass it yet, so the game keeps its digital pad).
  public const string Mml2Hash17="150A53E760515B70E3EDA3AE536A9B791EE611A800BD431DE006323B302EC5D8";
  // The Legends 2 build of contract L2-3 (mml2_rt_018.exe): the 017 build, and a refusal of a Legends 1 location now also writes the fail file.
  public const string Mml2Hash18="D9CDC5CCF8A3F97578273FFB835D292EBD0117011F84F761E1965BA7E89DA676";
  // The Legends 2 build of contract L2-4 (mml2_rt_023.exe): the 022 build, plus the feature free-camera (--mv2-free, --mv2-look-speed, --mv2-look-invert). With none of those options it matches 022 byte for byte on the 16,000-frame new-game script.
  public const string Mml2Hash23="1A7CCAE02EA251CF9EC690C7234BD589D9730202859920762DA1445C5C98DDA8";
  // The Legends 2 build of contract L2-5 (mml2_rt_024.exe): the 023 build, plus the feature free-camera-distance (--mv2-camera-distance) and an upward pitch limit of the free camera (no more ceiling in low rooms). With no options it equals 022 on the 16,000-frame new-game script.
  public const string Mml2Hash24="C981E6A310E0922AACCF350940DFC6F10B7320A025E24CB8B57D7EBAB382A782";
  public static bool IsKnownLegends2(string hash,string knownHash){return String.Equals(hash,knownHash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,Mml2Hash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,Mml2Hash17,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,Mml2Hash18,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,Mml2Hash23,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,Mml2Hash24,StringComparison.OrdinalIgnoreCase);}
  public static readonly GameKind Legends1=new GameKind{Id=1,Name="Mega Man Legends",Folder="mml1",DiscProgram=DiscProgramName,GameLine="Mega Man Legends (USA) SLUS-00603",ProgramName="rock_neo_rt",ProgramHint="rock_neo_rt_hc2.exe or rock_neo_rt_fd.exe",KnownHash=KnownHash,GmeImport=true};
  public static readonly GameKind Legends2=new GameKind{Id=2,Name="Mega Man Legends 2",Folder="mml2",DiscProgram="SLUS_011.40",GameLine="Mega Man Legends 2 (USA) SLUS-01140",ProgramName="mml2_rt",ProgramHint="mml2_rt_018.exe",KnownHash=Mml2Hash18,GmeImport=false};
  // The Tron Bonne build of its launcher contract T-2 (tron_rt_020.exe, runtime contract 2.6).
  public const string TronHash20="6B52B3321FC1B0965A007E76C4A824EE084601F7E0B3F5688AF2AF4C055BEBB8";
  public static bool IsKnownTron(string hash,string knownHash){return String.Equals(hash,knownHash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,TronHash20,StringComparison.OrdinalIgnoreCase);}
  public static readonly GameKind Tron=new GameKind{Id=3,Name="The Misadventures of Tron Bonne",Folder="tron",DiscProgram="TRON.EXE",GameLine="The Misadventures of Tron Bonne (USA) SLUS-00896",ProgramName="tron_rt",ProgramHint="tron_rt_020.exe",KnownHash=TronHash20,GmeImport=false};
  // Three games (contract T-2 added the third). Every place that holds one thing for each game uses this count.
  public const int GameCount=3;
  public static bool IsGame(int id){return id>=1&&id<=GameCount;}
  public static GameKind Game(int id){return id==3?Tron:id==2?Legends2:Legends1;}
  // A game's name inside a sentence: "the Mega Man Legends 2", but "The Misadventures of Tron Bonne" has its own article.
  static string The(GameKind kind){return kind.Name.StartsWith("The ",StringComparison.Ordinal)?kind.Name:"the "+kind.Name;}
  public static string DataFolderFor(int id){return Path.Combine(DeckPaths.Root,"games",Game(id).Folder);}
  public const int CardBytes=131072,GmeBytes=3904+131072,MinScale=2,MaxScale=6; // the sizes the setup screen offers

  public static string SettingsPath {get{return Path.Combine(DeckPaths.Root,"runtime.xml");}}
  public static string DataFolder {get{return Path.Combine(DeckPaths.Root,"games","mml1");}}
  public static string CardPath(string data){return Path.Combine(data,"card1.mcd");}
  public static string ImportPath(string data){return Path.Combine(data,"card-import.gme");}
  public static string KeysPath(string data){return Path.Combine(data,"keys.ini");}
  public static string TeleportRequestPath(string data){return Path.Combine(data,"teleport-request.txt");}
  public static string TeleportTablePath(string data){return Path.Combine(data,"teleport-table.txt");}
  // The list of teleport places of a kit-built game (builder kit 17 and later). The kit writes teleport-table.txt beside its marker, in the
  // folder two levels above the program (<folder>\build\game\<program>). The game looks for the list in its data folder, beside the request
  // file. So before a start the launcher copies it there when the player has none. It never replaces a list the player already has, it copies
  // an ordinary file of at most TeleportListLimit bytes that starts like a list, and it never stops a start: every failure means "no copy".
  // The list is made from the player's own disc; it is copied on this PC only and is never logged or sent.
  public const long TeleportListLimit=262144;
  public static bool TakeBuiltTeleportList(string program,string data){
    string part=null;
    try{
      if(String.IsNullOrEmpty(program)||String.IsNullOrEmpty(data))return false;
      string target=TeleportTablePath(data);if(File.Exists(target)||Directory.Exists(target))return false;
      string gameDir=Path.GetDirectoryName(Path.GetFullPath(program));string buildDir=gameDir==null?null:Path.GetDirectoryName(gameDir);string outDir=buildDir==null?null:Path.GetDirectoryName(buildDir);
      if(outDir==null||!String.Equals(Path.GetFileName(gameDir),"game",StringComparison.OrdinalIgnoreCase)||!String.Equals(Path.GetFileName(buildDir),"build",StringComparison.OrdinalIgnoreCase))return false;
      if(!File.Exists(Path.Combine(outDir,"rmx-kit-build.json")))return false;
      var source=new FileInfo(Path.Combine(outDir,"teleport-table.txt"));
      if(!source.Exists||source.Length==0||source.Length>TeleportListLimit||(source.Attributes&(FileAttributes.ReparsePoint|FileAttributes.Directory))!=0)return false;
      byte[] bytes=File.ReadAllBytes(source.FullName);
      if(bytes.Length==0||bytes.Length>TeleportListLimit||!Encoding.ASCII.GetString(bytes,0,Math.Min(bytes.Length,22)).StartsWith("# teleport-table.txt:",StringComparison.Ordinal))return false;
      Directory.CreateDirectory(data);part=target+"."+Guid.NewGuid().ToString("N")+".part";File.WriteAllBytes(part,bytes);   // a name of its own: another start, or a leftover file, cannot be in the way
      try{File.Move(part,target);}catch(IOException){try{File.Delete(part);}catch(Exception){}part=null;return false;}   // another start put a list there first: that list stays
      part=null;return true;
    }catch(Exception){try{if(part!=null&&File.Exists(part))File.Delete(part);}catch(Exception){}return false;}
  }
  public static string PadMapPath(string data){return Path.Combine(data,"pad-map.txt");}   // contract 2.8: the gamepad button map, edited by the user; the launcher only creates the template
  public static readonly int[] LookSensitivities=new[]{50,75,100,150,200};
  public static readonly int[] CameraDistances=new[]{60,80,100,130,160};   // percent of the game's own camera distance (contract 2.11, --camera-distance)
  public static int ClampDistance(int v){int best=100;foreach(int x in CameraDistances)if(Math.Abs(x-v)<Math.Abs(best-v))best=x;return best;}
  public static readonly int[] FarViews=new[]{0,120,140,160,180,200};   // contract 2.25, --far-view: 0 = the game's own draw distance, else percent of it
  public static int ClampFarView(int v){int best=160;foreach(int x in FarViews)if(Math.Abs(x-v)<Math.Abs(best-v))best=x;return best;}
  public static int ClampLook(int v){int best=100;foreach(int x in LookSensitivities)if(Math.Abs(x-v)<Math.Abs(best-v))best=x;return best;}
  public static string SessionsPath(string data){return Path.Combine(data,"sessions");}
  public static string FailFilePath(string data){return Path.Combine(data,"start-failure.txt");}
  public static int ClampScale(int scale){return scale<MinScale||scale>MaxScale?3:scale;}
  public static int ClampVolume(int volume){return volume<0||volume>100?100:volume;}

  // ---- settings file ----
  public static RuntimeSettings Load(string path,int id=1){
    var s=new RuntimeSettings();if(!File.Exists(path)||!IsGame(id))return s;   // a number that is no game never reads another game's entry
    var doc=new XmlDocument{XmlResolver=null};
    using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=32768}))doc.Load(reader);
    var root=doc.DocumentElement;if(root==null||root.Name!="flutterDeckRuntime"||root.GetAttribute("schema")!="1")throw new InvalidDataException("Unsupported game program settings.");
    var game=root.SelectSingleNode("game[@id='"+id.ToString(System.Globalization.CultureInfo.InvariantCulture)+"']") as XmlElement;if(game==null)return s;
    s.Program=game.GetAttribute("program");s.AcceptedHash=game.GetAttribute("acceptedHash").ToUpperInvariant(); // an old "discProgram" attribute is ignored
    int scale;s.Scale=Int32.TryParse(game.GetAttribute("scale"),out scale)?ClampScale(scale):3;s.Mute=game.GetAttribute("mute")=="1";s.OriginalPicture=game.GetAttribute("originalPicture")=="1";s.OriginalMovement=game.GetAttribute("originalMovement")=="1";s.NoRumble=game.GetAttribute("noRumble")=="1";s.NoIntroMovie=game.GetAttribute("noIntroMovie")=="1";s.Teleport=game.GetAttribute("teleport")!="0";   /* on unless the player switched it off (a saved file without the choice means on) */s.Widescreen=game.GetAttribute("widescreen")=="1";   /* widescreen is the other way round: off unless the file says 1 */int mvol,vvol,evol;s.MusicVolume=Int32.TryParse(game.GetAttribute("musicVolume"),out mvol)?ClampVolume(mvol):100;s.VoiceVolume=Int32.TryParse(game.GetAttribute("voiceVolume"),out vvol)?ClampVolume(vvol):100;s.EffectsVolume=Int32.TryParse(game.GetAttribute("effectsVolume"),out evol)?ClampVolume(evol):100;s.AnalogPad=game.GetAttribute("analogPad")=="1";s.FreeCamera=game.GetAttribute("freeCamera")=="1";s.LockMarker2=game.GetAttribute("lockMarker2")=="1";s.NoLockFlick=game.GetAttribute("noLockFlick")=="1";int rstr;s.RumbleStrength=Int32.TryParse(game.GetAttribute("rumbleStrength"),out rstr)&&rstr>=0&&rstr<=100?rstr:100;s.OriginalControls=game.GetAttribute("originalControls")=="1";s.InvertLookY=game.GetAttribute("invertLookY")=="1";int cdv;s.CameraDistance=Int32.TryParse(game.GetAttribute("cameraDistance"),out cdv)?ClampDistance(cdv):100;int fvv;s.FarView=Int32.TryParse(game.GetAttribute("farView"),out fvv)?ClampFarView(fvv):160;s.CameraWalls=game.GetAttribute("cameraWalls")=="1";s.ZTargetOff=game.GetAttribute("lockOff")=="1";s.AimStyleCamera=false;/* the "older aim" box was removed (the left trigger gives the camera aim); a saved choice is ignored */int ls;s.LookSensitivity=Int32.TryParse(game.GetAttribute("lookSensitivity"),out ls)?ClampLook(ls):100;int wh;if(Int32.TryParse(game.GetAttribute("windowHeight"),out wh))s.WindowHeight=ClampHeight(wh);else s.WindowHeight=HeightFromScale(s.Scale);   // a file from before the presets: the nearest preset to the old whole-number size
    int volume;s.Volume=Int32.TryParse(game.GetAttribute("volume"),out volume)?ClampVolume(volume):100;
    return s;
  }
  // True when the file has an entry for that game. A file that cannot be read has none.
  static bool HasEntry(string path,int id){
    try{if(!File.Exists(path))return false;var doc=new XmlDocument{XmlResolver=null};using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=32768}))doc.Load(reader);return doc.DocumentElement!=null&&doc.DocumentElement.SelectSingleNode("game[@id='"+id+"']")!=null;}
    catch(Exception){return false;}
  }
  // Saves one game's entry. The other games' entries are read first and written back, so no game loses its settings.
  // A file that is there but cannot be read is never written over: every save is refused then, whichever game it is
  // for, so no entry is lost. (The screens stop saving after a failed load; this is the same rule one level down.)
  public static void Save(string path,RuntimeSettings s,int id=1){
    if(!IsGame(id))throw new ArgumentOutOfRangeException("id");
    if(File.Exists(path))Load(path,1);   // a file that cannot be read stops the save, whichever game is saved
    var all=new RuntimeSettings[GameCount];
    for(int other=1;other<=GameCount;other++){
      if(other==id)all[other-1]=s;
      else if(other==1){if(File.Exists(path))all[0]=Load(path,1);}
      else if(HasEntry(path,other))all[other-1]=Load(path,other);
    }
    Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
    try{
      using(var writer=XmlWriter.Create(temp,new XmlWriterSettings{Indent=true})){writer.WriteStartElement("flutterDeckRuntime");writer.WriteAttributeString("schema","1");for(int n=1;n<=GameCount;n++)if(all[n-1]!=null)WriteGame(writer,n.ToString(System.Globalization.CultureInfo.InvariantCulture),all[n-1]);writer.WriteEndElement();}
      if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);
    }finally{if(File.Exists(temp))File.Delete(temp);}
  }
  static void WriteGame(XmlWriter writer,string id,RuntimeSettings s){
      {writer.WriteStartElement("game");writer.WriteAttributeString("id",id);writer.WriteAttributeString("program",s.Program??"");writer.WriteAttributeString("acceptedHash",s.AcceptedHash??"");writer.WriteAttributeString("scale",ClampScale(s.Scale).ToString());writer.WriteAttributeString("mute",s.Mute?"1":"0");writer.WriteAttributeString("originalPicture",s.OriginalPicture?"1":"0");writer.WriteAttributeString("originalMovement",s.OriginalMovement?"1":"0");writer.WriteAttributeString("noRumble",s.NoRumble?"1":"0");writer.WriteAttributeString("noIntroMovie",s.NoIntroMovie?"1":"0");writer.WriteAttributeString("teleport",s.Teleport?"1":"0");writer.WriteAttributeString("widescreen",s.Widescreen?"1":"0");writer.WriteAttributeString("musicVolume",ClampVolume(s.MusicVolume).ToString());writer.WriteAttributeString("voiceVolume",ClampVolume(s.VoiceVolume).ToString());writer.WriteAttributeString("effectsVolume",ClampVolume(s.EffectsVolume).ToString());if(id=="2")writer.WriteAttributeString("analogPad",s.AnalogPad?"1":"0");writer.WriteAttributeString("freeCamera",s.FreeCamera?"1":"0");writer.WriteAttributeString("lockMarker2",s.LockMarker2?"1":"0");writer.WriteAttributeString("noLockFlick",s.NoLockFlick?"1":"0");writer.WriteAttributeString("rumbleStrength",Math.Max(0,Math.Min(100,s.RumbleStrength)).ToString());writer.WriteAttributeString("originalControls",s.OriginalControls?"1":"0");writer.WriteAttributeString("invertLookY",s.InvertLookY?"1":"0");writer.WriteAttributeString("cameraDistance",ClampDistance(s.CameraDistance).ToString());writer.WriteAttributeString("farView",ClampFarView(s.FarView).ToString());writer.WriteAttributeString("cameraWalls",s.CameraWalls?"1":"0");writer.WriteAttributeString("lockOff",s.ZTargetOff?"1":"0");writer.WriteAttributeString("aimCamera",s.AimStyleCamera?"1":"0");writer.WriteAttributeString("lookSensitivity",ClampLook(s.LookSensitivity).ToString());writer.WriteAttributeString("windowHeight",ClampHeight(s.WindowHeight).ToString());writer.WriteAttributeString("volume",ClampVolume(s.Volume).ToString());writer.WriteEndElement();}
  }

  // ---- checks ----
  public static string FileSha256(string path){using(var sha=SHA256.Create())using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");}
  static readonly Dictionary<string,string> hashCache=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  // The screen asks often; the file is 16 MB. Re-hash only when its size or time changed. Start() always re-hashes.
  public static void ForgetHashes(){lock(hashCache){hashCache.Clear();}lock(identityCache){identityCache.Clear();refusedUntil.Clear();}}
  // Full local or network paths only: a relative path could be checked in one folder and started from another.
  static bool IsFullPath(string path){
    try{bool drive=path.Length>3&&Char.IsLetter(path[0])&&path[1]==':'&&path[2]=='\\';bool share=path.StartsWith(@"\\",StringComparison.Ordinal)&&!path.StartsWith(@"\\?\",StringComparison.Ordinal)&&!path.StartsWith(@"\\.\",StringComparison.Ordinal);return (drive||share)&&path.IndexOf('/')<0&&String.Equals(Path.GetFullPath(path),path,StringComparison.OrdinalIgnoreCase);}
    catch(Exception){return false;}
  }
  public static string CachedHash(string path){var info=new FileInfo(path);string key=path+"|"+info.Length+"|"+info.LastWriteTimeUtc.Ticks;string hash;lock(hashCache){if(hashCache.TryGetValue(key,out hash))return hash;}hash=FileSha256(path);lock(hashCache){hashCache[key]=hash;}return hash;}
  public static string CheckProgram(string path,string hint=null){
    if(String.IsNullOrWhiteSpace(path))return "Choose your game program ("+(hint??Legends1.ProgramHint)+").";
    if(!IsFullPath(path))return "The saved game program location is not a full path. Choose the game program again.";
    try{if(!File.Exists(path))return "The game program was not found. It may have moved.";if(!String.Equals(Path.GetExtension(path),".exe",StringComparison.OrdinalIgnoreCase))return "The game program must be an .exe file.";if(new FileInfo(path).Length==0)return "The game program file is empty.";return "";}
    catch(Exception){return "The game program could not be checked.";}
  }
  // ---- what the program says about itself ----
  // "<program> --identity" prints key=value lines and exits 0 without doing anything else (contract 2.0).
  // It is only ever run for a build whose hash is the known one or one the user has acknowledged.
  // A good answer is remembered for the file's hash (same bytes, same program). A missing or bad answer is
  // remembered only for a short while and only for that file location: the screen redraws often and must not
  // run a slow or broken program again each time, but a fixed copy, or another copy elsewhere, gets asked afresh.
  static readonly Dictionary<string,Dictionary<string,string>> identityCache=new Dictionary<string,Dictionary<string,string>>(StringComparer.OrdinalIgnoreCase);
  static readonly Dictionary<string,DateTime> refusedUntil=new Dictionary<string,DateTime>(StringComparer.OrdinalIgnoreCase);
  public const int IdentityRetrySeconds=30;
  public const int IdentityLineLimit=4000,IdentityTextLimit=16000;   // what is kept of one line and of the whole answer of "--identity"
  public static Dictionary<string,string> Identity(string program,string hash){
    Dictionary<string,string> found;string key=hash+"|"+program;
    lock(identityCache){if(identityCache.TryGetValue(hash,out found))return found;DateTime until;if(refusedUntil.TryGetValue(key,out until)&&DateTime.UtcNow<until)return new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);}
    found=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    try{
      var info=new ProcessStartInfo(program,"--identity"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.GetDirectoryName(program)};
      using(var p=new Process{StartInfo=info})using(var outputClosed=new System.Threading.ManualResetEvent(false)){
        var text=new StringBuilder();
        p.OutputDataReceived+=delegate(object sender,DataReceivedEventArgs e){if(e.Data==null){try{outputClosed.Set();}catch(Exception){}return;}// Limits (2026-11-20): a line was cut at 300 characters, and the feature line of the newer programs is longer (395 for contract 2.23), so the launcher did not see their last features (widescreen, multiplayer, the Teleport line).
        lock(text){if(text.Length<IdentityTextLimit)text.AppendLine(e.Data.Length>IdentityLineLimit?e.Data.Substring(0,IdentityLineLimit):e.Data);}};
        p.ErrorDataReceived+=delegate(object sender,DataReceivedEventArgs e){}; // read and dropped, so the program is never left waiting on a full pipe
        p.Start();p.BeginOutputReadLine();p.BeginErrorReadLine();
        // Both waits are bounded: the program gets five seconds, and its output one more to finish arriving. The
        // unbounded wait would hang here for as long as anything the program started kept the pipe open.
        if(!p.WaitForExit(5000)){try{p.Kill();}catch(Exception){}}
        else{
          outputClosed.WaitOne(1000);
          if(p.ExitCode==0){string all;lock(text){all=text.ToString();}foreach(string line in all.Split('\n')){int eq=line.IndexOf('=');if(eq>0)found[line.Substring(0,eq).Trim()]=line.Substring(eq+1).Trim();}}
        }
        try{p.CancelOutputRead();p.CancelErrorRead();}catch(Exception){}
      }
    }catch(Exception){found.Clear();}
    lock(identityCache){if(CheckIdentity(found).Length==0)identityCache[hash]=found;else refusedUntil[key]=DateTime.UtcNow.AddSeconds(IdentityRetrySeconds);}
    return found;
  }
  // The earlier 1.x program is known by its hash and refused by name, without being run or acknowledged.
  public static bool IsKnownBuild(string hash,string knownHash){return String.Equals(hash,knownHash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,HdHash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,HpHash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,HcHash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,Hc2Hash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,MrHash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,Mr2Hash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,MiHash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,VsHash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,TeleHash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,TmHash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,TlHash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,TfHash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,TgHash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,ThHash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,TiHash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,MmHash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,TpHash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,TpHashFirst,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,TpHashSecond,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,TpHashThird,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,TpHashFourth,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,SkHash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,FbHash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,AllHash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,All5Hash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,Fv2Hash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,All6Hash,StringComparison.OrdinalIgnoreCase)||String.Equals(hash,All7Hash,StringComparison.OrdinalIgnoreCase);}
  public static bool IsEarlierBuild(string hash,string knownHash){return String.Equals(hash,EarlierHash,StringComparison.OrdinalIgnoreCase)&&!String.Equals(knownHash,EarlierHash,StringComparison.OrdinalIgnoreCase);}
  // Empty when the program speaks contract 2.x and has every option this launcher passes; otherwise what is wrong.
  public static string CheckIdentity(Dictionary<string,string> identity,string hint=null){
    string contract;if(identity==null||!identity.TryGetValue("contract",out contract))return "This game program did not answer the launcher's identity question, so it is not a build this Flutter Deck can start. Choose "+(hint==null?"rock_neo_rt_hc2.exe (or rock_neo_rt_fd.exe)":hint)+".";
    if(!contract.StartsWith("2.",StringComparison.Ordinal))return "This game program speaks contract "+contract+", which this Flutter Deck does not know. Update Flutter Deck or choose another build.";
    string features;identity.TryGetValue("features",out features);var have=new HashSet<string>((features??"").Split(','),StringComparer.OrdinalIgnoreCase);
    foreach(string need in new[]{"card-new","exe-from-disc","fail-file","volume"})if(!have.Contains(need))return "This game program lacks the '"+need+"' option this Flutter Deck needs.";
    return "";
  }
  // Each game slot starts only its own game (contract L2-1: tell them apart by the program's game= line).
  // Slots 2 and 3 need the exact answer of their game (game= line and program= name). Slot 1 refuses a program that says it is Legends 2 or Tron Bonne; older Legends 1 builds are not asked for more than before.
  public static string CheckSlot(GameKind kind,Dictionary<string,string> identity){
    string game=null,program=null;if(identity!=null){identity.TryGetValue("game",out game);identity.TryGetValue("program",out program);}game=game??"";program=program??"";
    if(kind.Id>=2)return game==kind.GameLine&&program==kind.ProgramName?"":"This is not "+The(kind)+" game program. Choose "+kind.ProgramHint+".";
    foreach(var other in new[]{Legends2,Tron})if(game.StartsWith(other.Name,StringComparison.Ordinal)||program==other.ProgramName)return "This is "+The(other)+" game program. Choose it for "+other.Name+" instead.";
    return "";
  }
  public static string CheckCue(string path){
    if(String.IsNullOrWhiteSpace(path))return "Choose the disc image of this game in Setup.";
    if(!IsFullPath(path))return "The saved disc image location is not a full path. Choose it again in Setup.";
    if(!String.Equals(Path.GetExtension(path),".cue",StringComparison.OrdinalIgnoreCase))return "The native game needs a CUE disc image. Choose the .cue file in Setup.";
    try{return LocalSetup.CheckGame(path);}catch(Exception){return "The disc image could not be checked.";}
  }
  public static bool HasCard(string data){try{return File.Exists(CardPath(data))&&new FileInfo(CardPath(data)).Length==CardBytes;}catch(Exception){return false;}}
  public static bool HasImport(string data){try{return File.Exists(ImportPath(data))&&new FileInfo(ImportPath(data)).Length==GmeBytes;}catch(Exception){return false;}}
  public static string CheckCard(string data){
    try{string card=CardPath(data);if(File.Exists(card))return new FileInfo(card).Length==CardBytes?"":"The launcher's memory card file is damaged (wrong size). It was not changed.";return "";} // no card yet is fine: the game creates an empty one on first start
    catch(Exception){return "The memory card could not be checked.";}
  }
  // ---- one game per memory card ----
  // While a game runs, session.lock beside the card holds its process id, start time and program name. Any
  // launcher (this one reopened, or a second one) then sees the card is in use, whichever copy of the game
  // program was started. Start() takes a per-card system mutex around "check, mark, start", so two launchers
  // pressing Play at the same moment cannot both pass the check.
  public static string LockPath(string data){return Path.Combine(data,"session.lock");}
  public static string GateName(string data){using(var sha=SHA256.Create())return "Local\\FlutterDeck-card-"+BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(data).TrimEnd('\\').ToUpperInvariant()))).Replace("-","").Substring(0,32);}
  public static bool CardInUse(string data){
    try{
      string path=LockPath(data);if(!File.Exists(path)||new FileInfo(path).Length>200)return false;
      string[] parts=File.ReadAllText(path).Trim().Split('|');int pid;long started;
      if(parts.Length!=3||!Int32.TryParse(parts[0],out pid)||!Int64.TryParse(parts[1],out started)||parts[2].Length==0)return false;
      Process process;try{process=Process.GetProcessById(pid);}catch(ArgumentException){return false;} // no such process: that game has ended
      using(process){
        // The process id may have been reused by something else since. A different program name settles it,
        // and that can be read even for processes whose start time cannot.
        string name;try{name=process.ProcessName;}catch(Exception){return false;}
        if(!String.Equals(name,parts[2],StringComparison.OrdinalIgnoreCase))return false;
        try{return process.StartTime.ToUniversalTime().Ticks==started&&!process.HasExited;}catch(Exception){return true;} // same name, cannot tell more: assume it is the game
      }
    }catch(Exception){return false;}
  }
  static void WriteLock(string data,Process process){
    string temp=LockPath(data)+"."+Guid.NewGuid().ToString("N")+".tmp";
    try{File.WriteAllText(temp,process.Id+"|"+process.StartTime.ToUniversalTime().Ticks+"|"+process.ProcessName);if(File.Exists(LockPath(data)))File.Replace(temp,LockPath(data),null);else File.Move(temp,LockPath(data));} // replaced in one step: there is never a moment with no marker
    finally{try{if(File.Exists(temp))File.Delete(temp);}catch(Exception){}}
  }
  // Removes the marker once the game it names has ended; a marker for a game still running is left alone.
  // It takes the same per-card gate as Start, and gives up at once if a start is in progress, so it can never
  // remove a marker that is being written.
  public static void ReleaseLock(string data){
    try{
      using(var gate=new System.Threading.Mutex(false,GateName(data))){
        bool held=false;
        try{
          try{held=gate.WaitOne(0);}catch(System.Threading.AbandonedMutexException){held=true;}
          if(!held)return;
          if(File.Exists(LockPath(data))&&(!CardInUse(data)||NamesThisProcess(data)))File.Delete(LockPath(data));
        }finally{if(held)gate.ReleaseMutex();}
      }
    }catch(Exception){}
  }
  // True when the mark still names this launcher (the game's own mark could not be written after it started).
  static bool NamesThisProcess(string data){try{return File.ReadAllText(LockPath(data)).StartsWith(Process.GetCurrentProcess().Id+"|",StringComparison.Ordinal);}catch(Exception){return false;}}

  public static RuntimeCheck Readiness(RuntimeSettings s,string cue,string data,string knownHash,bool freshHash,GameKind kind=null){
    var r=new RuntimeCheck();kind=kind??Legends1;
    r.Problem=CheckProgram(s.Program,kind.ProgramHint);if(r.Problem.Length>0)return r;
    r.Problem=CheckCue(cue);if(r.Problem.Length>0)return r;
    r.Problem=CheckCard(data);if(r.Problem.Length>0)return r;
    foreach(string path in new[]{s.Program,cue,data}){if(path.IndexOf('"')>=0){r.Problem="A file location contains a quotation mark, which cannot be passed to the game.";return r;}}
    try{r.Hash=freshHash?FileSha256(s.Program):CachedHash(s.Program);}catch(Exception){r.Problem="The game program could not be read.";return r;}
    if(kind.Id!=2&&IsKnownLegends2(r.Hash,Mml2Hash)){r.Problem="This is the Mega Man Legends 2 game program. Choose it for Mega Man Legends 2 instead.";return r;}
    if(kind.Id!=3&&IsKnownTron(r.Hash,TronHash20)){r.Problem="This is "+The(Tron)+" game program. Choose it for "+Tron.Name+" instead.";return r;}
    if(kind.Id!=1&&(IsKnownBuild(r.Hash,KnownHash)||String.Equals(r.Hash,EarlierHash,StringComparison.OrdinalIgnoreCase))){r.Problem="This is a Mega Man Legends 1 game program. Choose "+kind.ProgramHint+" for "+kind.Name+".";return r;}
    if(kind.Id==1&&IsEarlierBuild(r.Hash,knownHash)){r.Problem="This is the earlier game program (rock_neo_rt.exe). This Flutter Deck needs the newer one: choose rock_neo_rt_hc2.exe (or rock_neo_rt_fd.exe).";return r;}
    r.KnownBuild=kind.Id==3?IsKnownTron(r.Hash,knownHash):kind.Id==2?IsKnownLegends2(r.Hash,knownHash):IsKnownBuild(r.Hash,knownHash);
    if(!r.KnownBuild&&!String.Equals(r.Hash,s.AcceptedHash,StringComparison.OrdinalIgnoreCase)){r.NeedsAcknowledgement=true;r.Problem="This game program is not the build Flutter Deck was written for. Confirm it on the setup and readiness screen before playing.";return r;}
    // Only now, with the build known or acknowledged, is the program itself asked what it is.
    var identity=Identity(s.Program,r.Hash);
    r.Problem=CheckIdentity(identity,kind.Id>=2?kind.ProgramHint:null);if(r.Problem.Length>0)return r;
    r.Problem=CheckSlot(kind,identity);if(r.Problem.Length>0)return r;
    string contract,built;identity.TryGetValue("contract",out contract);identity.TryGetValue("build",out built);r.Build="contract "+contract+(String.IsNullOrEmpty(built)?"":", built "+built);string feat;identity.TryGetValue("features",out feat);r.HasPictureOption=new HashSet<string>((feat??"").Split(','),StringComparer.OrdinalIgnoreCase).Contains("first-boot-picture");r.HasWindowHeight=new HashSet<string>((feat??"").Split(','),StringComparer.OrdinalIgnoreCase).Contains("window-height");r.HasMovementOption=new HashSet<string>((feat??"").Split(','),StringComparer.OrdinalIgnoreCase).Contains("movement");r.HasRumble=new HashSet<string>((feat??"").Split(','),StringComparer.OrdinalIgnoreCase).Contains("rumble");   // contract 2.9: --no-rumble works in this build   // contract 2.7: --movement modern|original
    {var fs=new HashSet<string>((feat??"").Split(','),StringComparer.OrdinalIgnoreCase);r.HasThirdPerson=fs.Contains("thirdperson");r.HasPadMap=fs.Contains("pad-map");r.HasLookOptions=fs.Contains("look-options");r.HasZTarget=fs.Contains("lockon")&&fs.Contains("aim-style");r.HasCameraDistance=fs.Contains("camera-distance");r.HasFarView=fs.Contains("far-view");r.HasCameraWalls=fs.Contains("camera-walls");r.HasIntroMovie=fs.Contains("intro-movie");r.HasVolumeSplit=fs.Contains("volume-split");r.HasTeleport=kind.Id==1&&fs.Contains("teleport");r.HasWidescreen=kind.Id==1&&fs.Contains("widescreen");r.HasPadType=fs.Contains("pad-type");r.HasLockMarker2=kind.Id==1&&fs.Contains("lock-marker-2");r.HasLockFlick=kind.Id==1&&fs.Contains("lock-flick");r.HasFreeCamera=kind.Id==2&&fs.Contains("free-camera");r.HasFreeCameraDistance=kind.Id==2&&fs.Contains("free-camera")&&fs.Contains("free-camera-distance");r.HasMultiplayer=kind.Id==1&&fs.Contains("multiplayer");}   // contract 2.8
    r.Ready=true;return r;
  }

  // ---- memory card: copy in only, never over an existing card ----
  public static void AdoptCard(string source,string data,bool gmeAllowed=true){
    if(File.Exists(CardPath(data))||File.Exists(ImportPath(data)))throw new InvalidOperationException("The launcher already has a memory card for this game.");
    if(String.IsNullOrWhiteSpace(source)||!File.Exists(source))throw new FileNotFoundException("Memory card file not found.");
    long length=new FileInfo(source).Length;string ext=Path.GetExtension(source).ToLowerInvariant();
    if(ext==".gme"&&!gmeAllowed)throw new InvalidDataException("This game does not take a .gme save. Choose a raw memory card file of 131,072 bytes.");
    string target;if(length==CardBytes&&ext!=".gme")target=CardPath(data);else if(length==GmeBytes&&ext==".gme")target=ImportPath(data);else throw new InvalidDataException("Not a memory card this game accepts: a raw card is 131,072 bytes and a .gme save is 134,976 bytes.");
    Directory.CreateDirectory(data);string temp=target+"."+Guid.NewGuid().ToString("N")+".tmp";
    try{
      File.Copy(source,temp,false);File.SetAttributes(temp,FileAttributes.Normal);
      if(FileSha256(temp)!=FileSha256(source))throw new IOException("The copy did not match the original.");
      File.Move(temp,target); // fails rather than replaces if a card appeared meanwhile
    }finally{try{if(File.Exists(temp))File.Delete(temp);}catch(Exception){}}
  }
  // Files the game leaves when a save could not be written. They are the user's progress: never deleted here.
  public static List<string> UnsavedFiles(string data){
    var found=new List<string>();try{if(!Directory.Exists(data))return found;string card=Path.GetFileName(CardPath(data));if(File.Exists(Path.Combine(data,card+".tmp")))found.Add(card+".tmp");foreach(string path in Directory.GetFiles(data,card+".unsaved-*.mcd"))found.Add(Path.GetFileName(path));}catch(Exception){}
    return found;
  }

  // ---- key map: the launcher owns the file. A map replaces the defaults entirely, so all fourteen buttons are written. ----
  public static readonly string[] DefaultKeys={"UP = UP","DOWN = DOWN","LEFT = LEFT","RIGHT = RIGHT","CROSS = X","SQUARE = Z","TRIANGLE = S","CIRCLE = D","L1 = Q","R1 = W","L2 = A","R2 = E","START = ENTER","SELECT = RSHIFT"};
  // A map with nothing in it (empty, or only spaces, line ends or a byte-order mark).
  static bool IsBlankFile(string path){var info=new FileInfo(path);if(info.Length==0)return true;if(info.Length>65536)return false;return File.ReadAllText(path).Trim((char)0xFEFF,' ','\t','\r','\n').Length==0;}
  // contract 2.8: a template of the gamepad button map. Written only when the file is missing; an existing file (the user's) is never touched.
  // Every line is a comment, so the game uses its defaults until the user removes a '#'.
  public static readonly string PadMapTemplate=String.Join("\r\n",new[]{
    "# Flutter Deck gamepad map for Mega Man Legends. One setting per line; a line starting with # does nothing.",
    "# Buttons (Xbox names; a PlayStation 5 pad uses the same places: CROSS = A, CIRCLE = B, SQUARE = X, TRIANGLE = Y,",
    "# L1 = LB, R1 = RB, L2 = LT, R2 = RT, CREATE = BACK, OPTIONS = START):",
    "#   A B X Y LB RB LT RT BACK START L3 R3 DPAD_UP DPAD_DOWN DPAD_LEFT DPAD_RIGHT",
    "# Actions: CROSS CIRCLE SQUARE TRIANGLE L1 R1 L2 R2 START SELECT, and for the third-person controls CAM_RESET (camera back",
    "# behind Mega Man) and AIM (hold: he faces where the camera looks and the left stick strafes). NONE removes a button.",
    "# Several actions on one button: A = CROSS + CAM_RESET",
    "# The defaults, to change one remove its # and edit it:",
    "# A = CROSS",
    "# B = CIRCLE",
    "# X = SQUARE",
    "# Y = TRIANGLE",
    "# LB = L1",
    "# RB = R1",
    "# LT = AIM        (original controls: L2)",
    "# RT = R2         (the game's lock-on)",
    "# BACK = SELECT",
    "# START = START",
    "# R3 = CAM_RESET",
    "# Camera settings (the launcher's own camera speed and invert settings win over these):",
    "# look_sensitivity = 100",
    "# look_invert_y = 0",
    "# stick_deadzone = 20",
    ""});
  public static void EnsurePadMap(string data){
    string path=PadMapPath(data);if(File.Exists(path))return;
    Directory.CreateDirectory(data);string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
    try{File.WriteAllText(temp,PadMapTemplate,new UTF8Encoding(false));File.Move(temp,path);}
    finally{try{if(File.Exists(temp))File.Delete(temp);}catch(Exception){}}
  }
  // Writes the default map only when there is none; an existing map (the user's) is never rewritten.
  public static void EnsureKeys(string data){
    // An empty file would mean "no keys at all" to the game, so it counts as missing. Written through a temporary file.
    string path=KeysPath(data);if(File.Exists(path)&&!IsBlankFile(path))return;
    Directory.CreateDirectory(data);string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
    if(File.Exists(path))File.SetAttributes(path,FileAttributes.Normal);
    try{File.WriteAllText(temp,"# Flutter Deck key map for Mega Man Legends. One line per button: BUTTON = KEY [, KEY ...]\r\n"+String.Join("\r\n",DefaultKeys)+"\r\n",new UTF8Encoding(false));if(File.Exists(path))File.Delete(path);File.Move(temp,path);}
    finally{try{if(File.Exists(temp))File.Delete(temp);}catch(Exception){}}
  }

  // ---- starting ----
  static string Quote(string path){if(path.IndexOf('"')>=0)throw new ArgumentException("Path contains a quotation mark");return "\""+path.TrimEnd('\\')+"\"";}
  // Exactly the play-mode arguments of contract 2.0, plus --original-picture (contract 2.4) only when ticked and the program reports it, and --window-height H instead of --scale N (contract 2.5) only for a program that reports it. Test and diagnosis options are never added.
  // The game reads its program from the disc image and is always given the launcher's card file. If that file does
  // not exist yet the game makes it: from a .gme the user copied in, or else as an empty card (--card-new never
  // touches a card that exists, and the two options may not be combined).
  public static string BuildArguments(RuntimeSettings s,string cue,string data,bool pictureOption=false,bool windowHeight=false,bool movementOption=false,bool thirdPersonOption=false,bool padMapOption=false,bool lookOptions=false,bool rumbleOption=false,bool zTargetOption=false,GameKind kind=null,bool introMovieOption=false,bool padTypeOption=false,bool cameraDistanceOption=false,bool volumeSplitOption=false,bool teleportOption=false,bool widescreenOption=false,bool cameraWallsOption=false,bool farViewOption=false,bool freeCameraOption=false,bool freeCameraDistanceOption=false,bool lockMarker2Option=false,bool lockFlickOption=false){
    var a=new StringBuilder("--live");kind=kind??Legends1;
    a.Append(" --exe-from-disc ").Append(kind.DiscProgram).Append(" --run-dir ").Append(Quote(SessionsPath(data))).Append(" --cue ").Append(Quote(cue)).Append(" --card0 ").Append(Quote(CardPath(data)));
    if(kind.GmeImport&&!File.Exists(CardPath(data))&&HasImport(data))a.Append(" --card-from-gme ").Append(Quote(ImportPath(data)));else a.Append(" --card-new");
    a.Append(" --keys ").Append(Quote(KeysPath(data))).Append(windowHeight?" --window-height ":" --scale ").Append(windowHeight?ClampHeight(s.WindowHeight):ClampScale(s.Scale)).Append(" --volume ").Append(ClampVolume(s.Volume));
    if(s.Mute)a.Append(" --mute");
    a.Append(" --fail-file ").Append(Quote(FailFilePath(data)));
    if(pictureOption&&s.OriginalPicture)a.Append(" --original-picture");   // only for a program that reports 'first-boot-picture' (contract 2.4)
    if(thirdPersonOption)a.Append(s.OriginalControls?" --controls original":" --controls thirdperson");   // contract 2.8: explicit, and thirdperson needs modern movement, so it is only passed with the movement option
    if(movementOption)a.Append((s.OriginalMovement&&(!thirdPersonOption||s.OriginalControls))?" --movement original":" --movement modern");   // third-person controls need the movement ramp: with them on, smooth movement cannot be turned off
    if(padMapOption&&File.Exists(PadMapPath(data)))a.Append(" --pad-map ").Append(Quote(PadMapPath(data)));
    if(rumbleOption){if(s.NoRumble)a.Append(" --no-rumble");else if(s.RumbleStrength!=100)a.Append(" --rumble ").Append(Math.Max(0,Math.Min(100,s.RumbleStrength)));}   // contract 2.9: rumble is on by default in a program that reports it, so only the off choice is passed
    if(teleportOption&&s.Teleport)a.Append(" --teleport-file ").Append(Quote(TeleportRequestPath(data)));   // contract 2.14/2.16: on unless the player switched it off (a new settings file); only for a program that reports 'teleport'; the game's pause menu then shows the Teleport line
    if(widescreenOption&&s.Widescreen&&!(pictureOption&&s.OriginalPicture))a.Append(" --widescreen 53");   // contracts 2.19/2.20: off unless the player ticked it; only for a program that reports 'widescreen' (Legends 1); 53 pixels on each side make the picture 426 x 240, about 16:9; the program needs the sharper picture, so it is not passed with the original picture
    if(volumeSplitOption){if(s.MusicVolume!=100)a.Append(" --music-volume ").Append(ClampVolume(s.MusicVolume));if(s.VoiceVolume!=100)a.Append(" --voice-volume ").Append(ClampVolume(s.VoiceVolume));if(s.EffectsVolume!=100)a.Append(" --effects-volume ").Append(ClampVolume(s.EffectsVolume));}   // contract 2.13: only for a program that reports 'volume-split'; 100 is the default, so only a lower level is passed
    if(lookOptions)a.Append(" --look-sensitivity ").Append(ClampLook(s.LookSensitivity)).Append(" --look-invert ").Append(s.InvertLookY?"1":"0");   // only for a program that reports 'movement' (contract 2.7); always explicit, so the choice never depends on a default
    if(zTargetOption&&thirdPersonOption)a.Append(!s.OriginalControls&&!s.ZTargetOff?" --lockon 1":" --lockon 0").Append(s.AimStyleCamera?" --aim-style camera":" --aim-style free");   // contract 2.11: only for a program that reports lockon and aim-style; always explicit
    if(cameraWallsOption&&s.CameraWalls&&thirdPersonOption&&!s.OriginalControls)a.Append(" --camera-walls 1");   // experimental, only on request and only for a program that reports camera-walls
    if(farViewOption)a.Append(" --far-view ").Append(ClampFarView(s.FarView));   // contract 2.25: only for a program that reports far-view; always explicit
    if(cameraDistanceOption)a.Append(" --camera-distance ").Append(ClampDistance(s.CameraDistance));   // contract 2.11: only for a program that reports camera-distance; always explicit
    if(padTypeOption&&kind.Id==2&&s.AnalogPad)a.Append(" --pad-type analog");   // contract L2-2, Legends 2 only: digital is the program's default, so only the analog choice is passed, and only to a program that reports 'pad-type'
    if(freeCameraOption&&kind!=null&&kind.Id==2&&s.FreeCamera)a.Append(" --mv2-free --mv2-look-speed ").Append(ClampLook(s.LookSensitivity)).Append(" --mv2-look-invert ").Append(s.InvertLookY?"1":"0");if(freeCameraDistanceOption)a.Append(" --mv2-camera-distance ").Append(ClampDistance(s.CameraDistance));   // contract L2-4, Legends 2 only: off unless the player ticked it; only for a program that reports 'free-camera'; the program then asks for the analog pad itself
    if(lockMarker2Option&&s.LockMarker2&&(kind==null||kind.Id==1))a.Append(" --lock-marker 2");   // contract 2.29 (L16), Legends 1 only: the Legends 2 look of the lock-on marker; off unless ticked; the program's own default (the single arrow) stays otherwise; only for a program that reports 'lock-marker-2'
    if(lockFlickOption&&kind==null||lockFlickOption&&kind.Id==1){if(s.NoLockFlick)a.Append(" --lock-flick 0");}   // contract 2.30 (L17), Legends 1 only: a flick of the right stick while locked switches to the next target; on by default in a program that reports 'lock-flick', so only the off choice is passed
    if(introMovieOption&&s.NoIntroMovie)a.Append(" --no-intro-movie");   // contract 2.12: the opening movie plays by default in a play session of a program that reports 'intro-movie', so only the off choice is passed
    return a.ToString();
  }
  // True when a copy of this program is already running, from this launcher or an earlier one: two games must never share a card.
  public static bool AlreadyRunning(string program){
    try{string name=Path.GetFileNameWithoutExtension(program);foreach(var p in Process.GetProcessesByName(name)){try{if(String.Equals(p.MainModule.FileName,program,StringComparison.OrdinalIgnoreCase))return true;}catch(Exception){}finally{p.Dispose();}}}catch(Exception){}
    return false;
  }
  // multiplayer: the value of the game's RMX_MP setting (it holds a secret key), or null for an ordinary game. It goes into the
  // environment of the game process only: never on the command line, into a file or into a message.
  public static GameSession Start(RuntimeSettings s,string cue,string data,string knownHash,GameKind kind=null,string[] otherData=null,string multiplayer=null){
    Directory.CreateDirectory(data);
    // One start at a time on this computer, for either game: without this, two launchers could start the two games at
    // the same moment, each seeing the other game's card still free.
    using(var any=new System.Threading.Mutex(false,AnyStartGate)){
      bool anyHeld=false;
      try{
        try{anyHeld=any.WaitOne(15000);}catch(System.Threading.AbandonedMutexException){anyHeld=true;}
        if(!anyHeld)throw new InvalidOperationException("Another Flutter Deck is starting a game. Try again in a moment.");
        // One launcher at a time may run "check the card is free, mark it, start the game" for this card.
        using(var gate=new System.Threading.Mutex(false,GateName(data))){
          bool held=false;
          try{
            try{held=gate.WaitOne(15000);}catch(System.Threading.AbandonedMutexException){held=true;}
            if(!held)throw new InvalidOperationException("Another Flutter Deck is starting the game. Try again in a moment.");
            return StartHoldingGate(s,cue,data,knownHash,kind??Legends1,otherData,multiplayer);
          }finally{if(held)gate.ReleaseMutex();}
        }
      }finally{if(anyHeld)any.ReleaseMutex();}
    }
  }
  public const string AnyStartGate="Local\\FlutterDeck-game-start";
  public static string MultiplayerStatusPath(string data){return Path.Combine(data,"multiplayer-status.txt");}
  static GameSession StartHoldingGate(RuntimeSettings s,string cue,string data,string knownHash,GameKind kind,string[] otherData,string multiplayer){
    var check=Readiness(s,cue,data,knownHash,true,kind);if(!check.Ready)throw new InvalidOperationException(check.Problem);
    if(CardInUse(data)||AlreadyRunning(s.Program))throw new InvalidOperationException("The game is already running. Close it before starting it again: two games must not share one memory card.");
    if(otherData!=null)foreach(string other in otherData)if(CardInUse(other))throw new InvalidOperationException("Another game is running. Close it before starting this one.");   // one game at a time across all of them
    if(multiplayer!=null&&!check.HasMultiplayer)throw new InvalidOperationException("This game program has no multiplayer. Nothing was started.");
    // An earlier refusal's fail file is removed first: the game only writes it on exit 2 and never clears it.
    try{Directory.CreateDirectory(SessionsPath(data));EnsureKeys(data);if(kind.Id==1)EnsurePadMap(data);if(File.Exists(FailFilePath(data)))File.Delete(FailFilePath(data));if(File.Exists(TeleportRequestPath(data)))File.Delete(TeleportRequestPath(data));}   // an old teleport request must not meet the new game
    catch(Exception){throw new InvalidOperationException("Flutter Deck could not prepare its folder for this game (session folder or key map). Nothing was started.");}
    if(kind.Id==1)TakeBuiltTeleportList(s.Program,data);   // a kit-built game brings its list of teleport places; a player who has a list keeps it
    // Mark the card before the game exists, naming this launcher. If the card cannot be marked, nothing is started:
    // an unmarked running game is exactly what could let a second one share the card.
    var self=Process.GetCurrentProcess();
    try{WriteLock(data,self);}catch(Exception){throw new InvalidOperationException("Flutter Deck could not mark the memory card as in use, so the game was not started. Check the session.lock entry in its game folder.");}
    var session=new GameSession{SessionsFolder=SessionsPath(data),Arguments=BuildArguments(s,cue,data,check.HasPictureOption,check.HasWindowHeight,check.HasMovementOption,check.HasThirdPerson,check.HasPadMap,check.HasLookOptions,check.HasRumble,check.HasZTarget,kind,check.HasIntroMovie,check.HasPadType,check.HasCameraDistance,check.HasVolumeSplit,check.HasTeleport,check.HasWidescreen,check.HasCameraWalls,check.HasFarView,check.HasFreeCamera,check.HasFreeCameraDistance,check.HasLockMarker2,check.HasLockFlick),StartedUtc=DateTime.UtcNow};
    var info=new ProcessStartInfo(s.Program,session.Arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,WorkingDirectory=Path.GetDirectoryName(s.Program)};
    // An ordinary game never gets a multiplayer setting, also not one that this launcher itself was started with.
    info.EnvironmentVariables.Remove("RMX_MP");info.EnvironmentVariables.Remove("RMX_MP_STATUS");
    if(multiplayer!=null){try{File.Delete(MultiplayerStatusPath(data));}catch(Exception){}info.EnvironmentVariables["RMX_MP"]=multiplayer;info.EnvironmentVariables["RMX_MP_STATUS"]=MultiplayerStatusPath(data);}
    var process=new Process{StartInfo=info,EnableRaisingEvents=true};
    // Exit code 2 explains itself only on standard error; keep a bounded amount of it.
    process.ErrorDataReceived+=delegate(object sender,DataReceivedEventArgs e){if(e.Data==null)return;lock(session.ErrorText){if(session.ErrorText.Length<4000)session.ErrorText.AppendLine(e.Data.Length>400?e.Data.Substring(0,400):e.Data);}};
    session.Process=process;
    try{process.Start();}catch{try{File.Delete(LockPath(data));}catch(Exception){}throw;} // nothing started: free the card again
    // Now name the game itself, so the mark outlives this launcher. If that rewrite fails the mark still names
    // this launcher, which keeps the card in use for as long as this launcher is open.
    try{WriteLock(data,process);}catch(Exception){}
    process.BeginErrorReadLine();
    return session;
  }
  // Asks the game to close as if the user closed its window (the contract's way to quit; the card is written).
  public static bool RequestClose(GameSession session){try{return session!=null&&!session.Process.HasExited&&session.Process.CloseMainWindow();}catch(Exception){return false;}}

  // ---- how it ended ----
  // Only a log written since this session started can explain it; an earlier session's log is never shown.
  public static string NewestLog(string sessionsFolder,DateTime sinceUtc){
    try{string best=null;DateTime bestTime=DateTime.MinValue;foreach(string path in Directory.GetFiles(sessionsFolder,"*.log")){DateTime t=File.GetLastWriteTimeUtc(path);if(t>=sinceUtc&&t>bestTime){best=path;bestTime=t;}}return best;}catch(Exception){return null;}
  }
  // The contract: search the whole log for the line; it is not the last one. Bounded so a huge log cannot stall the launcher.
  public static string FindLine(string logPath,string prefix){
    try{string found="";long budget=64L*1024*1024;using(var reader=new StreamReader(new FileStream(logPath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))){string line;while((line=reader.ReadLine())!=null&&budget>0){budget-=line.Length+2;if(line.StartsWith(prefix,StringComparison.Ordinal))found=line.Length>300?line.Substring(0,300):line;}}return found;}
    catch(Exception){return "";}
  }
  // What to show the user for an exit code, following section 6 of the contract. Empty for a normal close.
  public static string ExitReason(int code,string sessionsFolder,DateTime startedUtc,string errorText,string arguments){
    if(code==0)return "";
    string log=NewestLog(sessionsFolder,startedUtc);
    if(code==2){
      // The game writes its reason to the fail file on exit 2 only; standard error is the fallback.
      string said="";try{string fail=FailFilePath(Path.GetDirectoryName(sessionsFolder));if(File.Exists(fail)&&new FileInfo(fail).Length<=4096&&File.GetLastWriteTimeUtc(fail)>=startedUtc){foreach(string line in File.ReadAllLines(fail))if(line.StartsWith("reason=",StringComparison.Ordinal))said=line.Substring(7).Trim();}}catch(Exception){}
      if(said.Length==0)said=(errorText??"").Trim();
      return "The game program refused to start (exit code 2). This is a launcher problem, not your game files."+(said.Length>0?"\nIt said: "+(said.Length>600?said.Substring(0,600):said):"")+"\nStarted with: "+arguments;}
    if(code==3){
      string trap=log==null?"":FindLine(log,"TRAP:");
      // A stop before the log exists (for example a card that could not be created) still says why on standard error.
      if(trap.Length==0){foreach(string line in (errorText??"").Split('\n')){string t=line.Trim();if(t.StartsWith("TRAP:",StringComparison.Ordinal))trap=t.Length>300?t.Substring(0,300):t;}}
      return trap.Length>0?"The game stopped (exit code 3).\n"+trap+"\nSession log: "+log:"The game stopped (exit code 3) and gave no reason."+(log==null?"":"\nSession log: "+log);}
    string end=log==null?"":FindLine(log,"RUN END:");
    return "The game ended unexpectedly (exit code "+code+")."+(end.Length>0?"\n"+end:"")+(log==null?"":"\nSession log: "+log);
  }
}
