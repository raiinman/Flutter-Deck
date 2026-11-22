using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
// Tests for MediaFolders.cs on a scratch folder tree. Which disc holds which game, and which program is known, are stand-ins.
// No disc image and no program file is read, nothing is opened, and no launcher setting is touched.
public static class MediaFoldersPureTest {
  static int failures=0,checks=0;
  static void Check(bool ok,string name){checks++;if(!ok){failures++;Console.WriteLine("FAIL: "+name);}}
  static void Throws<T>(Action action,string name) where T:Exception {checks++;try{action();failures++;Console.WriteLine("FAIL (no error): "+name);}catch(T){}}
  static void Put(string root,string relative){string path=Path.Combine(root,relative);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,new byte[3]);}
  static int Disc(string cue){string n=Path.GetFileName(cue);if(n.Contains("unreadable"))return -1;if(n.Contains("Legends 2"))return 2;if(n.Contains("Mega Man Legends"))return 1;return 0;}
  static int Program(string exe){string n=Path.GetFileName(exe).ToLowerInvariant();if(n.Contains("unknown"))return 0;return n.StartsWith("mml2_rt")?2:n.StartsWith("rock_neo_rt")?1:0;}
  public static int Main(string[] args){
    string root=Path.GetFullPath(args[0]);
    if(Directory.Exists(root))Directory.Delete(root,true);
    string lib=Path.Combine(root,"My Media");
    foreach(string f in new[]{
      @"Games\Other Game (USA)\Other Game (USA).cue",@"Games\Other Game (USA)\Other Game (USA).bin",@"Games\Other Game (USA)\cover.jpg",
      @"Games\Legends\Mega Man Legends (USA).cue",@"Games\Legends 2\Mega Man Legends 2 (USA).cue",@"Games\Broken\unreadable.cue",
      @"Games\programs\rock_neo_rt_hc2.exe",@"Games\programs\mml2_rt_018.exe",@"Games\programs\mml2_rt_unknown.exe",@"Games\programs\setup.exe",
      @"Movies\Film A (2001).mkv",@"Movies\Film A (2001).jpg",@"Movies\notes.txt",
      @"Movies\Film B\Film B.mp4",@"Movies\Film B\poster.png",
      @"Movies\Two\First_Part.mp4",@"Movies\Two\Second Part.mp4",@"Movies\Two\cover.jpg",@"Movies\Two\sample.mkv",
      @"TV\Show One\poster.jpg",@"TV\Show One\Season 1\Show.One.S01E01.mkv",@"TV\Show One\Season 1\Show.One.S01E02.mkv",@"TV\Show One\Season 2\Show.One.S02E01.mkv",@"TV\Show One\Season 2\art\a.png",@"TV\Show One\Season 2\art\b.png",
      @"TV\Show Two\Show Two 1x01.avi",@"TV\Show Two\Show Two 1x02.avi",@"TV\Show Two\Episode 3.avi",
      @"Art\Sketches\b.jpg",@"Art\Sketches\a.png",@"Art\Sketches\c.gif",@"Art\Single\one.png",
      @"Stray S01E05.mkv",@"Work\.git\a.png",@"Work\.git\b.png",@"Work\node_modules\x\a.png",@"Work\node_modules\x\b.png",@"Work\dump\a.png",@"Work\dump\b.png",@"Work\dump\a.dat",@"Work\dump\b.dat",@"Work\dump\c.dat"})Put(lib,f);
    var scan=MediaFolders.Scan(lib,Disc,Program);
    Check(scan.Problem==""&&scan.Complete&&scan.Root==lib,"a normal folder is searched completely: "+scan.Problem);
    Func<string,List<LibraryItem>> kind=k=>scan.Items.Where(i=>i.Kind==k).ToList();
    // games
    Check(scan.Discs[0].Count==1&&scan.Discs[0][0].EndsWith("Mega Man Legends (USA).cue")&&scan.Discs[1].Count==1&&scan.Discs[1][0].EndsWith("Mega Man Legends 2 (USA).cue"),"each Legends disc image goes to its own game");
    Check(kind("game").Count==1&&kind("game")[0].Title=="Other Game (USA)"&&kind("game")[0].Path.EndsWith(".cue")&&kind("game")[0].Cover.EndsWith(@"Other Game (USA)\cover.jpg"),"another disc image is listed as a game, with the cover beside it; a disc image that cannot be read is left out");
    Check(scan.Programs[0].Count==1&&scan.Programs[0][0].EndsWith("rock_neo_rt_hc2.exe")&&scan.Programs[1].Count==1&&scan.Programs[1][0].EndsWith("mml2_rt_018.exe"),"only known game programs are reported, each for its own game");
    // movies
    var movies=kind("movie");
    Check(movies.Count==5,"five movies: "+String.Join(" | ",movies.Select(m=>m.Title)));
    Check(movies.Any(m=>m.Title=="Film A (2001)"&&m.Cover.EndsWith("Film A (2001).jpg")),"a picture with the movie's name is its cover");
    Check(movies.Any(m=>m.Title=="Film B"&&m.Cover.EndsWith("poster.png")),"a movie alone in its folder takes the folder's poster");
    Check(movies.Any(m=>m.Title=="First Part"&&m.Cover=="")&&movies.Any(m=>m.Title=="Second Part"&&m.Cover==""),"two movies in one folder do not share a cover; an underscore reads as a space");
    Check(movies.Any(m=>m.Title=="Stray S01E05"),"an episode directly in the library folder is a movie: the library folder is never a show");
    Check(!scan.Items.Any(i=>i.Title.ToLowerInvariant().Contains("sample")),"a sample file is left out");
    // shows
    var shows=kind("show");
    Check(shows.Count==2&&shows.Any(s=>s.Title=="Show One"&&s.Path.EndsWith(@"TV\Show One")&&s.Cover.EndsWith(@"Show One\poster.jpg"))&&shows.Any(s=>s.Title=="Show Two"&&s.Cover==""),"episodes are one show for each show folder; season folders belong to their show");
    var eps=MediaFolders.Episodes(shows.First(s=>s.Title=="Show One").Path);
    Check(eps.Count==3&&eps[0].EndsWith("S01E01.mkv")&&eps[1].EndsWith("S01E02.mkv")&&eps[2].EndsWith("S02E01.mkv"),"the episodes of a show are listed in season and name order");
    Check(MediaFolders.Episodes(shows.First(s=>s.Title=="Show Two").Path).Count==3&&MediaFolders.Episodes(Path.Combine(lib,"gone")).Count==0&&MediaFolders.Episodes("relative").Count==0,"episodes of a flat show; none for a folder that is gone or not a full path");
    // art
    var art=kind("art");
    Check(art.Count==1&&art[0].Title=="Sketches"&&art[0].Cover.EndsWith("a.png"),"a folder of pictures is one album; one picture alone is not; pictures inside a show are not an album: "+String.Join(" | ",art.Select(a=>a.Path)));
    var pics=MediaFolders.Pictures(art[0].Path);Check(pics.Count==3&&pics[0].EndsWith("a.png")&&pics[2].EndsWith("c.gif"),"the pictures of an album in name order");
    Check(scan.Items.All(i=>i.Found&&i.Root==lib&&MediaFolders.Inside(lib,i.Path)&&MediaFolders.Present(i)),"every found item lies inside its library folder and is there");
    Check(MediaFolders.Describe(scan)=="Found 3 games, 5 movies, 2 shows and 1 art album.","the sentence counts the two Legends games too: "+MediaFolders.Describe(scan));
    Check(!scan.Items.Any(i=>i.Path.Contains(@"\Work\")),"tool folders, hidden folders, and a folder that mostly holds other files are not albums");
    string deep=Path.Combine(root,"Deep");Put(deep,@"1\2\3\4\5\6\7\8\9\too deep.mp4");Put(deep,@"1\2\3\ok.mp4");var deepScan=MediaFolders.Scan(deep,Disc,Program);
    Check(deepScan.Deeper&&deepScan.Complete&&deepScan.Items.Count==1&&MediaFolders.Describe(deepScan).Contains("levels down"),"folders below the depth limit are not searched, and the search still counts as complete");
    // games only (the developer's choice): the same folder gives the games and nothing else
    var gamesOnly=MediaFolders.Scan(lib,Disc,Program,true);
    Check(gamesOnly.Complete&&gamesOnly.Items.Count==1&&gamesOnly.Items[0].Kind=="game"&&gamesOnly.Discs[0].Count==1&&gamesOnly.Discs[1].Count==1&&gamesOnly.Programs[0].Count==1&&gamesOnly.Programs[1].Count==1&&MediaFolders.Describe(gamesOnly,true)=="Found 3 games.","a games-only search lists disc images and game programs, and no movie, show or album: "+MediaFolders.Describe(gamesOnly,true));
    Check(MediaFolders.Merge(MediaFolders.Merge(new List<LibraryItem>(),scan),gamesOnly).All(i=>i.Kind=="game"),"a complete games-only search takes earlier movies, shows and albums of that folder out of the list");
    // layouts the independent check found wrong
    string odd=Path.Combine(root,"Odd");
    foreach(string f in new[]{@"Movies\Star Wars Episode 4 - A New Hope.mkv",@"Movies\Star Wars Episode 5.mkv",@"Movies\Alien.mkv",@"Movies\Heat.mkv",@"Movies\Ronin.mkv",@"Movies\Brazil.mkv",@"Downloads\Some.Show.S02E03.mkv",@"Downloads\A Film.mp4",@"Downloads\B Film.mp4",
      @"TV\Long Show\Season 1 (2008)\Long.Show.S01E01.mkv",@"TV\Long Show\Season 2 (2009)\Long.Show.S02E01.mkv",@"TV\Long Show\Season 2 (2009)\Long.Show.S02E02.mkv",
      @"Photos\Trip\a.jpg",@"Photos\Trip\b.jpg",@"Photos\Trip\c.jpg",@"Photos\Trip\Thumbs.db",@"Photos\Trip\desktop.ini",@"Photos\Trip\a.cr2",@"Photos\Trip\b.heic",
      @"Photos\Camera\clip.mp4"})Put(odd,f);
    for(int i=0;i<20;i++)Put(odd,@"Photos\Camera\img"+i.ToString("00")+".jpg");
    var oddScan=MediaFolders.Scan(odd,Disc,Program);
    Check(oddScan.Items.Count(i=>i.Kind=="movie")==10&&oddScan.Items.Count(i=>i.Kind=="show")==1&&oddScan.Items.Any(i=>i.Kind=="show"&&i.Title=="Long Show"),"a movie with Episode in its name, or one episode among movies, does not turn a folder into a show; season folders with a year belong to their show: "+String.Join(" | ",oddScan.Items.Select(i=>i.Kind+":"+i.Title)));
    Check(oddScan.Items.Count(i=>i.Kind=="art")==2&&oddScan.Items.Any(i=>i.Kind=="art"&&i.Title=="Trip")&&oddScan.Items.Any(i=>i.Kind=="art"&&i.Title=="Camera"),"the files Windows leaves, raw photo files and a clip among many pictures do not stop an album");
    var unread=MediaFolders.Scan(lib,Disc,Program);unread.Unread=true;unread.Items.Clear();
    Check(!unread.Complete&&MediaFolders.Merge(MediaFolders.Merge(new List<LibraryItem>(),scan),unread).Count==scan.Items.Count&&MediaFolders.Describe(unread).Contains("could not be read"),"a search that could not read a folder inside is not complete, removes nothing and says so");
    // a folder of many movies stays movies
    string many=Path.Combine(root,"Fifty");for(int i=1;i<=50;i++)Put(many,"Movie "+i.ToString("00")+" (19"+(40+i)+").mp4");
    var fifty=MediaFolders.Scan(many,Disc,Program);Check(fifty.Items.Count==50&&fifty.Items.All(i=>i.Kind=="movie"),"a folder of 50 movies stays 50 movies");
    foreach(string name in new[]{"Show.S01E02","show 1x02","Episode 3","ep_12","Show - s1e3"})Check(MediaFolders.IsEpisodeName(name+".mkv"),"episode name: "+name);
    foreach(string name in new[]{"Film (2001)","1920x1080 demo","Se7en","Alien 3","Apex 2x","Deep Blue"})Check(!MediaFolders.IsEpisodeName(name+".mkv"),"not an episode name: "+name);
    // problems change nothing
    var gone=MediaFolders.Scan(Path.Combine(root,"not there"),Disc,Program);Check(gone.Problem.Contains("not found")&&!gone.Complete,"a folder that is gone is a problem");
    Check(MediaFolders.Scan("relative\\folder",Disc,Program).Problem.Length>0&&MediaFolders.Scan(@"\\server\share\x",Disc,Program).Problem.Length>0&&MediaFolders.Scan(null,Disc,Program).Problem.Length>0,"only a full local path is searched");
    var found=MediaFolders.Merge(new List<LibraryItem>(),scan);Check(found.Count==scan.Items.Count,"a first search adds its items");
    gone.Root=lib;Check(MediaFolders.Merge(found,gone).Count==found.Count,"a search with a problem removes nothing (the drive may be unplugged)");
    File.Delete(Path.Combine(lib,@"Movies\Film B\Film B.mp4"));Directory.Delete(Path.Combine(lib,@"Art\Sketches"),true);Put(lib,@"Movies\New Film.mkv");
    var again=MediaFolders.Scan(lib,Disc,Program);var next=MediaFolders.Merge(found,again);
    Check(!next.Any(i=>i.Title=="Film B")&&!next.Any(i=>i.Kind=="art")&&next.Any(i=>i.Title=="New Film")&&next.Count==found.Count-1,"a complete search drops what is gone and adds what is new");
    var partial=MediaFolders.Scan(lib,Disc,Program);partial.Truncated=true;partial.Items.RemoveAll(i=>i.Kind=="show");
    Check(MediaFolders.Merge(next,partial).Count(i=>i.Kind=="show")==2,"a search that could not finish only adds: it never drops an item");
    var other=MediaFolders.Merge(next,fifty);Check(other.Count==next.Count+50&&MediaFolders.Merge(other,again).Count==other.Count,"each library folder keeps its own items");
    Check(MediaFolders.WithoutFolder(other,many).Count==next.Count&&MediaFolders.WithoutFolder(other,lib).Count==50,"stopping a folder removes only its items");
    var mine=new List<LibraryItem>{new LibraryItem{Kind="movie",Title="My own title",Path=Path.Combine(lib,@"Movies\New Film.mkv")}};
    Check(MediaFolders.Shown(mine,next).Count==next.Count-1&&!MediaFolders.Shown(mine,next).Any(i=>i.Title=="New Film"),"a file the user added by hand is not shown twice");
    var lost=next.First(i=>i.Title=="Film A (2001)");File.Delete(lost.Path);Check(!MediaFolders.Present(lost)&&MediaFolders.Present(next.First(i=>i.Kind=="show")),"an item whose file is gone is not present; the others are");
    // the two settings files
    string folders=Path.Combine(root,"settings","library-folders.txt"),foundFile=Path.Combine(root,"settings","library-found.txt");
    Check(MediaFolders.LoadFolders(folders).Count==0&&MediaFolders.LoadFound(foundFile).Count==0,"no files: no folders and no found items");
    MediaFolders.SaveFolders(folders,new[]{lib,many+"\\"});var back=MediaFolders.LoadFolders(folders);
    Check(back.Count==2&&back[0]==lib&&back[1]==many&&File.ReadAllText(folders)=="1\r\n"+lib+"\r\n"+many+"\r\n","the folders are saved and read back");
    MediaFolders.SaveFolders(folders,new[]{lib});Check(File.Exists(folders+".bak")&&Directory.GetFiles(Path.GetDirectoryName(folders),"*.tmp").Length==0,"the file before is kept and no temporary file is left");
    Throws<ArgumentException>(delegate{MediaFolders.SaveFolders(folders,new[]{"relative"});},"a relative folder is not saved");
    File.WriteAllText(folders,"2\r\n"+lib+"\r\n");Throws<InvalidDataException>(delegate{MediaFolders.LoadFolders(folders);},"an unknown format is an error, so the file is not replaced");
    File.WriteAllText(folders,"1\r\n"+lib+"\r\nnot a path\r\n"+lib.ToUpperInvariant()+"\\\r\n\\\\server\\share\r\n");Check(MediaFolders.LoadFolders(folders).Count==1,"lines that are not full local folders, and a folder named twice, are left out");
    Check(MediaFolders.CheckNewFolder(new[]{lib},lib+"\\").Contains("already")&&MediaFolders.CheckNewFolder(new[]{lib},Path.Combine(lib,"Movies")).Contains("inside")&&MediaFolders.CheckNewFolder(new[]{Path.Combine(lib,"Movies")},lib).Contains("holds")&&MediaFolders.CheckNewFolder(new[]{lib},many)==""&&MediaFolders.CheckNewFolder(new string[0],"x").Length>0,"a folder is added once, and not inside or around another library folder");
    Check(MediaFolders.CheckNewFolder(Enumerable.Range(0,MediaFolders.MaxFolders).Select(i=>@"C:\f"+i).ToArray(),@"C:\more").Contains("at most"),"the number of library folders is limited");
    MediaFolders.SaveFound(foundFile,other);var kept=MediaFolders.LoadFound(foundFile);
    Check(kept.Count==other.Count&&kept.All(k=>other.Any(o=>LibraryCore.Same(o,k)&&o.Cover==k.Cover&&o.Root==k.Root))&&kept.All(k=>k.Found),"the found items are saved and read back");
    string first=File.ReadAllLines(foundFile)[1];
    File.WriteAllText(foundFile,"1\r\n"+first+"\r\nmovie\tOutside\tC:\\Windows\\x.mp4\t\t"+lib+"\r\nprogram\tBad kind\t"+lib+"\\a.exe\t\t"+lib+"\r\nmovie\tNot a video\t"+lib+"\\a.exe\t\t"+lib+"\r\ngame\tNot a cue\t"+lib+"\\a.bin\t\t"+lib+"\r\ntoo\tfew\r\n"+first+"\r\n");
    Check(MediaFolders.LoadFound(foundFile).Count==1,"a line outside its library folder, of an unknown kind, of the wrong file type, or named twice is left out");
    Check(MediaFolders.Make("movie","T",lib+@"\a.mp4",@"C:\Windows\cover.jpg",lib).Cover==""&&MediaFolders.Make("movie","T",lib+@"\a.mp4",lib+@"\a.exe",lib).Cover==""&&MediaFolders.Make("show","",lib+@"\s","",lib)==null&&MediaFolders.Make("show","S",lib+@"\..\s","",lib)==null,"a cover outside the folder or not a picture is dropped; an item needs a title and a place inside its folder");
    File.WriteAllText(foundFile,"9\r\n");Throws<InvalidDataException>(delegate{MediaFolders.LoadFound(foundFile);},"an unknown found-items format is an error");
    // found items never reach library.txt, and the hand-made list still reads as before
    string libraryText=LibraryCore.Format(other);Check(libraryText=="1\r\n"+String.Join("",other.Select(i=>i.Kind+"\t"+i.Title+"\t"+i.Path+"\r\n"))&&LibraryCore.Parse("1\r\nshow\tX\t"+lib+"\r\nart\tY\t"+lib+"\r\n").Count==0,"library.txt has no place for a show or an album");
    Check(LibraryCore.Status(other.First(i=>i.Kind=="show"),true)=="Show"&&LibraryCore.Status(other.First(i=>i.Kind=="show"),false).Contains("missing")&&LibraryCore.Status(kind("game")[0],true).StartsWith("Game")&&LibraryCore.Status(new LibraryItem{Kind="art",Found=true},true)=="Art"&&LibraryCore.KindName("show")=="Show"&&LibraryCore.KindName("art")=="Art","status words for the new kinds");
    Check(LibraryCore.CanOpen(other.First(i=>i.Kind=="movie"))&&!LibraryCore.CanOpen(other.First(i=>i.Kind=="show"))&&!LibraryCore.CanOpen(kind("game")[0]),"only a movie can be opened; a show, an album and a found game cannot");
    if(failures>0){Console.WriteLine(failures+" of "+checks+" library folder check(s) failed");return 1;}
    Console.WriteLine("PASS: "+checks+" library folder checks");return 0;
  }
}
