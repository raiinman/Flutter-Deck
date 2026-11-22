using System;
using System.Collections.Generic;
using System.IO;
static class LibraryPureTest {
  static int failed=0;
  static void Check(bool ok,string what){if(!ok){failed++;Console.WriteLine("FAIL: "+what);}}
  static int Main(string[] args){
    string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
    string movie=Path.Combine(root,"Holiday film.MP4");
    // built-in items
    var built=LibraryCore.BuiltIns();
    Check(built.Count==3&&built[0].BuiltIn==1&&built[1].BuiltIn==2&&built[2].BuiltIn==3&&built[0].Kind=="game"&&built[2].Kind=="game"&&built[0].Title=="Mega Man Legends"&&built[1].Title=="Mega Man Legends 2"&&built[2].Title=="The Misadventures of Tron Bonne","three built-in games, in order");
    Check(!LibraryCore.CanOpen(built[0])&&!LibraryCore.CanOpen(built[1]),"a built-in game is never opened as a file");
    // kinds
    Check(LibraryCore.Kinds.Length==LibraryCore.KindNames.Length&&LibraryCore.Kinds.Length==LibraryCore.KindPlurals.Length,"kind tables have the same length");
    Check(LibraryCore.KindName("movie")=="Movie"&&LibraryCore.KindName("nonsense")=="Other","kind names");
    // movie files
    Check(LibraryCore.IsMovieFile(movie)&&LibraryCore.IsMovieFile(@"C:\a\b.mkv"),"a local video file is a movie file");
    foreach(string no in new[]{null,"","film.mp4",@"relative\film.mp4",@"\\server\share\film.mp4",@"C:film.mp4","C:/x/film.mp4",@"C:\x\program.exe",@"C:\x\script.bat",@"C:\x\film.mp4.exe",@"C:\x\film",@"C:\x\link.lnk",@"C:\x\page.url","https://example.invalid/film.mp4",@"C:\x\"+new string('a',600)+".mp4"})
      Check(!LibraryCore.IsMovieFile(no),"not a movie file: "+(no??"(null)"));
    // normalize
    string problem;
    var m=LibraryCore.Normalize("movie","",movie,out problem);
    Check(m!=null&&m.Title=="Holiday film"&&m.Path==movie&&LibraryCore.CanOpen(m),"a movie without a title takes the file name");
    Check(LibraryCore.Normalize("movie","Film",@"C:\x\program.exe",out problem)==null&&problem.Length>0,"a movie needs a video file");
    Check(LibraryCore.Normalize("movie","Film","",out problem)==null,"a movie needs a file");
    var g=LibraryCore.Normalize("game","  Mega Man 8\r\n ",@"C:\x\game.exe",out problem);
    Check(g!=null&&g.Title=="Mega Man 8"&&g.Path==""&&!LibraryCore.CanOpen(g),"an added game is listed only: its file is not kept and it cannot be opened");
    Check(LibraryCore.Normalize("game","   ","",out problem)==null&&problem=="Type a title.","a title is needed");
    Check(LibraryCore.Normalize("tool","X","",out problem)==null,"unknown kind refused");
    Check(LibraryCore.Normalize("other",new string('x',400),"",out problem).Title.Length==LibraryCore.MaxTitle,"long title cut");
    var forged=new LibraryItem{Kind="movie",Title="Forged",Path=@"C:\x\program.exe"};
    Check(!LibraryCore.CanOpen(forged)&&!LibraryCore.CanOpen(new LibraryItem{Kind="game",Title="G",Path=movie})&&!LibraryCore.CanOpen(new LibraryItem{Kind="movie",Title="B",Path=movie,BuiltIn=1})&&!LibraryCore.CanOpen(null),"only an added movie with a video file can be opened");
    // add, duplicates, filter
    var user=new List<LibraryItem>();
    Check(LibraryCore.Add(user,m,out problem)&&LibraryCore.Add(user,g,out problem)&&user.Count==2,"items added");
    Check(!LibraryCore.Add(user,LibraryCore.Normalize("game","mega man 8","",out problem),out problem)&&problem.Length>0&&user.Count==2,"the same item is not added twice");
    Check(!LibraryCore.Add(user,LibraryCore.Normalize("game","Mega Man Legends","",out problem),out problem),"a copy of a built-in game is not added");
    Check(!LibraryCore.Add(user,null,out problem),"nothing is not added");
    var all=LibraryCore.All(user);
    Check(all.Count==5&&all[0].BuiltIn==1&&all[2].BuiltIn==3&&all[3]==m,"built-in items come first");
    Check(LibraryCore.Filter(all,null).Count==5&&LibraryCore.Filter(all,"").Count==5&&LibraryCore.Count(all,"game")==4&&LibraryCore.Count(all,"movie")==1&&LibraryCore.Count(all,"other")==0,"filter by kind");
    // search, order and the status line
    Check(LibraryCore.Search(all,"").Count==5&&LibraryCore.Search(all,null).Count==5&&LibraryCore.Search(all,"tron").Count==1&&LibraryCore.Search(all,"tron")[0].BuiltIn==3&&LibraryCore.Search(all,"  LEGENDS ").Count==2&&LibraryCore.Search(all,"holiday")[0]==m&&LibraryCore.Search(all,"zzz").Count==0,"search by title");
    var ordered=LibraryCore.Arrange(all,null,"",true);Check(ordered.Count==5&&ordered[0]==m&&ordered[1]==g&&ordered[2].BuiltIn==1&&ordered[3].BuiltIn==2&&ordered[4].BuiltIn==3,"order by title");
    var asAdded=LibraryCore.Arrange(all,"game","man",false);Check(asAdded.Count==3&&asAdded[0].BuiltIn==1&&asAdded[2]==g&&all.Count==5&&all[0].BuiltIn==1,"kind and search together, in the order added; the list itself is not changed");
    Check(LibraryCore.Status(built[0],false)=="Game"&&LibraryCore.Status(built[1],false).Contains("features coming soon")&&LibraryCore.Status(built[2],false).Contains("features coming soon")&&!LibraryCore.Status(built[1],false).Contains("early build")&&LibraryCore.Status(m,true)=="Movie"&&LibraryCore.Status(m,false).Contains("file missing")&&LibraryCore.Status(g,false).StartsWith("Game")&&LibraryCore.Status(g,false).Contains("listed only"),"status line");
    // format and parse
    string text=LibraryCore.Format(all);
    Check(text.StartsWith("1\r\n")&&!text.Contains("Mega Man Legends 2")&&text.Contains("movie\tHoliday film\t"+movie+"\r\n")&&text.Contains("game\tMega Man 8\t\r\n"),"only added items are written");
    var back=LibraryCore.Parse(text);
    Check(back.Count==2&&back[0].Kind=="movie"&&back[0].Path==movie&&back[1].Title=="Mega Man 8"&&back[1].BuiltIn==0,"round trip");
    var mixed=LibraryCore.Parse("1\nmovie\tBad\tC:\\x\\program.exe\ngame\tKept\t\njunk line\ntool\tX\t\ngame\t\t\nmovie\tOk\tC:\\v\\a.mkv\ngame\tKept\t\n");
    Check(mixed.Count==2&&mixed[0].Title=="Kept"&&mixed[1].Title=="Ok","bad lines are left out, good lines are kept, repeats are dropped");
    Check(LibraryCore.Parse(null).Count==0&&LibraryCore.Parse("").Count==0&&LibraryCore.Parse("2\ngame\tX\t\n").Count==0&&LibraryCore.Parse("game\tX\t\n").Count==0,"unknown format gives an empty list");
    var many=new System.Text.StringBuilder("1\n");for(int i=0;i<LibraryCore.MaxItems+50;i++)many.Append("other\tItem "+i+"\t\n");
    Check(LibraryCore.Parse(many.ToString()).Count==LibraryCore.MaxItems,"item limit");
    var full=LibraryCore.Parse(many.ToString());Check(!LibraryCore.Add(full,LibraryCore.Normalize("other","One more","",out problem),out problem)&&problem.Contains("full"),"a full library refuses another item");
    // file
    string file=Path.Combine(root,"sub folder","library.txt");
    Check(LibraryCore.Load(file).Count==0,"missing file gives an empty list");
    LibraryCore.Save(file,user);LibraryCore.Save(file,user);
    Check(File.Exists(file)&&File.Exists(file+".bak")&&Directory.GetFiles(Path.GetDirectoryName(file),"*.tmp").Length==0,"saved with a backup and no temporary file left");
    var loaded=LibraryCore.Load(file);Check(loaded.Count==2&&loaded[0].Path==movie,"load after save");
    // a file this build cannot read is an error, so that the caller refuses to replace it; an empty file is an empty library
    foreach(string unreadable in new[]{new string('x',LibraryCore.MaxFileBytes+1),"2\r\ngame\tFrom a newer build\t\r\n","game\tNo format line\t\r\n"}){File.WriteAllText(file,unreadable);bool threw=false;try{LibraryCore.Load(file);}catch(InvalidDataException){threw=true;}Check(threw,"a file that cannot be read is an error, not an empty library");}
    File.WriteAllText(file,"");Check(LibraryCore.Load(file).Count==0,"empty file gives an empty list");File.WriteAllText(file," \r\n");Check(LibraryCore.Load(file).Count==0,"blank file gives an empty list");
    // a list that would be too large to read back is not saved, and the file on disk stays as it was
    var huge=new List<LibraryItem>();string wide=new string('\u6620',LibraryCore.MaxTitle);for(int i=0;i<LibraryCore.MaxItems;i++)huge.Add(new LibraryItem{Kind="movie",Title=wide.Substring(3)+i.ToString("000"),Path=@"C:\v\"+new string('\u753B',400)+i+".mp4"});
    LibraryCore.Save(file,user);bool refused=false;try{LibraryCore.Save(file,huge);}catch(InvalidDataException){refused=true;}Check(refused&&LibraryCore.Load(file).Count==2&&Directory.GetFiles(Path.GetDirectoryName(file),"*.tmp").Length==0,"a list too large to read back is not saved");
    // Open refuses everything that is not an added movie with a video file that exists. (A real movie is never opened by this test.)
    string program=Path.Combine(root,"program.exe");File.WriteAllText(program,"x");string script=Path.Combine(root,"film.mp4.bat");File.WriteAllText(script,"x");
    foreach(var no in new[]{null,built[0],g,new LibraryItem{Kind="movie",Title="Forged",Path=program},new LibraryItem{Kind="movie",Title="Forged",Path=script},new LibraryItem{Kind="game",Title="Forged",Path=program},new LibraryItem{Kind="other",Title="Forged",Path=program},new LibraryItem{Kind="movie",Title="Gone",Path=Path.Combine(root,"gone.mp4")},new LibraryItem{Kind="movie",Title="Built",Path=program,BuiltIn=1}})
      Check(LibraryOpen.Open(no).Length>0,"Open refused: "+(no==null?"(null)":no.Kind+" "+no.Title));
    // test seam
    string real=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FlutterDeck","library.txt");
    string before=Environment.GetEnvironmentVariable(LibraryCore.OverrideVariable);
    try{
      Environment.SetEnvironmentVariable(LibraryCore.OverrideVariable,null);Check(LibraryCore.PathName==real,"real file by default");
      Environment.SetEnvironmentVariable(LibraryCore.OverrideVariable,file);Check(LibraryCore.PathName==file,"override used");
      foreach(string no in new[]{@"relative\library.txt","library.txt",Path.Combine(root,"library.xml"),"   ",@"C:library.txt",@"\\server\share\library.txt","C:/x/library.txt"}){Environment.SetEnvironmentVariable(LibraryCore.OverrideVariable,no);Check(LibraryCore.PathName==real,"override ignored: "+no);}
    }finally{Environment.SetEnvironmentVariable(LibraryCore.OverrideVariable,before);}
    // ---- how the user looks at the Library: favourites, hidden items, order, last opened (LibraryView.cs) ----
    var vFilm1=new LibraryItem{Kind="movie",Title="Zebra film",Path=movie};var vFilm2=new LibraryItem{Kind="movie",Title="alpha film",Path=movie+"2.mp4"};var vNotes=new LibraryItem{Kind="other",Title="Notes"};
    var vAll=LibraryCore.All(new List<LibraryItem>{vFilm1,vFilm2,vNotes});var vs=new LibraryViewState();
    Check(LibraryView.Key(vAll[0])=="builtin:1"&&LibraryView.Key(vAll[1])=="builtin:2"&&LibraryView.Key(vNotes)=="other|Notes|"&&LibraryView.Key(vFilm1)=="movie|Zebra film|"+movie,"an entry is tied to its item by a key");
    Check(LibraryView.Count(vAll,vs,"game")==3&&LibraryView.Count(vAll,vs,"movie")==2&&LibraryView.Count(vAll,vs,"other")==1&&LibraryView.Count(vAll,vs,"fav")==0,"tab counts");
    Check(String.Join(",",LibraryView.Arrange(vAll,vs,"game","").ConvertAll(i=>i.Title).ToArray())=="Mega Man Legends,Mega Man Legends 2,The Misadventures of Tron Bonne"&&String.Join(",",LibraryView.Arrange(vAll,vs,"movie","").ConvertAll(i=>i.Title).ToArray())=="Zebra film,alpha film","a tab shows its kind, as added");
    vs.Sort="titleAZ";Check(String.Join(",",LibraryView.Arrange(vAll,vs,"movie","").ConvertAll(i=>i.Title).ToArray())=="alpha film,Zebra film","order by title A-Z without regard to capital letters");
    vs.Sort="titleZA";Check(String.Join(",",LibraryView.Arrange(vAll,vs,"game","").ConvertAll(i=>i.Title).ToArray())=="The Misadventures of Tron Bonne,Mega Man Legends 2,Mega Man Legends"&&LibraryView.Arrange(vAll,vs,"movie","ZEB").Count==1,"order by title Z-A; the search still applies");
    LibraryView.SetFavorite(vs,vNotes,true);LibraryView.SetFavorite(vs,vAll[0],true);
    Check(LibraryView.IsFavorite(vs,vNotes)&&LibraryView.Count(vAll,vs,"fav")==2&&LibraryView.Arrange(vAll,vs,"fav","").Count==2&&!LibraryView.IsFavorite(vs,vFilm1),"favourites of any kind are in the Favorites tab");
    LibraryView.SetFavorite(vs,vNotes,false);Check(!LibraryView.IsFavorite(vs,vNotes)&&LibraryView.Count(vAll,vs,"fav")==1,"a favourite can be taken back");
    LibraryView.SetHidden(vs,vAll[0],true);vs.Sort="added";
    Check(LibraryView.IsHidden(vs,vAll[0])&&LibraryView.Count(vAll,vs,"game")==2&&LibraryView.HiddenCount(vAll,vs,"game")==1&&LibraryView.Arrange(vAll,vs,"game","").Count==2&&LibraryView.Arrange(vAll,vs,"fav","").Count==0,"a hidden item is not shown and not counted");
    vs.ShowHidden=true;Check(LibraryView.Arrange(vAll,vs,"game","").Count==3&&LibraryView.Count(vAll,vs,"game")==2,"hidden items are shown when asked for; the count stays");vs.ShowHidden=false;
    LibraryView.SetHidden(vs,vAll[0],false);Check(!LibraryView.IsHidden(vs,vAll[0])&&LibraryView.HiddenCount(vAll,vs,"game")==0,"an item can be unhidden");
    var t0=new DateTime(2026,11,10,12,0,0,DateTimeKind.Utc);
    LibraryView.MarkOpened(vs,vFilm1,t0);LibraryView.MarkOpened(vs,vFilm2,t0.AddHours(1));LibraryView.MarkOpened(vs,vFilm2,t0.AddHours(2));vs.Sort="recent";
    Check(String.Join(",",LibraryView.Arrange(vAll,vs,"movie","").ConvertAll(i=>i.Title).ToArray())=="alpha film,Zebra film"&&LibraryView.TimesOpened(vs,vFilm2)==2&&LibraryView.TimesOpened(vs,vFilm1)==1&&LibraryView.TimesOpened(vs,vNotes)==0&&LibraryView.LastOpened(vs,vFilm2)==t0.AddHours(2).Ticks,"the item opened last comes first; times are counted");
    Check(String.Join(",",LibraryView.Arrange(vAll,vs,"game","").ConvertAll(i=>i.Title).ToArray())=="Mega Man Legends,Mega Man Legends 2,The Misadventures of Tron Bonne","items never opened keep their order");
    var now=new DateTime(2026,11,18,18,0,0,DateTimeKind.Utc);
    Check(LibraryView.WhenText(0,now)=="Never"&&LibraryView.WhenText(-5,now)=="Never"&&LibraryView.WhenText(now.AddMinutes(-5).Ticks,now)=="Today"&&LibraryView.WhenText(now.AddDays(-5).Ticks,now)=="5 days ago"&&LibraryView.WhenText(now.AddDays(-400).Ticks,now).Contains("2025")&&LibraryView.WhenText(long.MaxValue,now)=="Never"&&LibraryView.WhenText(1,now)=="Never"&&LibraryView.WhenText(now.AddDays(3).Ticks,now).Contains("2026")&&LibraryView.WhenText(now.AddDays(-1).Ticks,now)=="Yesterday","when an item was opened, in words");
    // the largest view this build can make still fits in its file
    var vBig=new LibraryViewState();for(int i=0;i<LibraryView.MaxEntries+50;i++){var big=new LibraryItem{Kind="movie",Title=new string('t',LibraryCore.MaxTitle-4)+i.ToString("0000"),Path=@"C:\v\"+new string('p',LibraryCore.MaxPath-12)+".mp4"};LibraryView.SetFavorite(vBig,big,true);LibraryView.SetHidden(vBig,big,true);LibraryView.MarkOpened(vBig,big,t0);}
    Check(vBig.Favorites.Count==LibraryView.MaxEntries&&vBig.Hidden.Count==LibraryView.MaxEntries&&vBig.Opened.Count==LibraryView.MaxEntries&&System.Text.Encoding.UTF8.GetByteCount(LibraryView.Format(vBig))<=LibraryView.MaxFileBytes,"the number of entries is limited, and the largest view fits in the file: "+System.Text.Encoding.UTF8.GetByteCount(LibraryView.Format(vBig)));
    vs.Sort="titleZA";vs.View="details";LibraryView.SetHidden(vs,vNotes,true);
    var vBack=LibraryView.Parse(LibraryView.Format(vs));
    Check(vBack.Sort=="titleZA"&&vBack.View=="details"&&!vBack.ShowHidden&&vBack.Favorites.SetEquals(vs.Favorites)&&vBack.Hidden.SetEquals(vs.Hidden)&&vBack.Opened.Count==2&&LibraryView.LastOpened(vBack,vFilm2)==LibraryView.LastOpened(vs,vFilm2)&&LibraryView.TimesOpened(vBack,vFilm2)==2&&LibraryView.IsFavorite(vBack,new LibraryItem{Kind="game",Title="x",BuiltIn=1}),"the view is written and read back");
    var vOdd=LibraryView.Parse("1\r\nsort=byMagic\r\nview=wall\r\nfav\t\r\nfav\tbad\u0001key\r\nhide\t"+new string('k',LibraryView.MaxKey+1)+"\r\nopen\tsoon\t1\tk\r\nopen\t5\t0\tk\r\nopen\t-5\t1\tk\r\nopen\t5\t1\r\nsomething else\r\nfav\tother|Kept|\r\n");
    Check(vOdd.Sort=="added"&&vOdd.View=="grid"&&vOdd.Favorites.Count==1&&vOdd.Hidden.Count==0&&vOdd.Opened.Count==0,"lines that are not understood are left out; the rest is kept");
    Check(LibraryView.Parse("2\r\nsort=recent\r\n").Sort=="added"&&LibraryView.Parse(null).Sort=="added"&&LibraryView.Parse("").View=="grid","another format gives the default view");
    LibraryView.Forget(vs,vFilm2);Check(LibraryView.TimesOpened(vs,vFilm2)==0&&LibraryView.LastOpened(vs,vFilm2)==0,"an item's entries go away with the item");
    string viewFile=Path.Combine(root,@"view\library-view.txt");
    Check(LibraryView.Load(viewFile).Sort=="added","no file is the default view");
    LibraryView.Save(viewFile,vs);LibraryView.Save(viewFile,vs);Check(LibraryView.Load(viewFile).View=="details"&&File.Exists(viewFile+".bak")&&Directory.GetFiles(Path.GetDirectoryName(viewFile),"*.tmp").Length==0&&File.ReadAllText(viewFile).StartsWith("1\r\nsort=titleZA\r\nview=details\r\n"),"the view file is saved; the file before is kept; no temporary file is left");
    foreach(string unreadable in new[]{"2\r\nsort=recent\r\n","sort=recent\r\n",new string('x',LibraryView.MaxFileBytes+1)}){File.WriteAllText(viewFile,unreadable);bool threw=false;try{LibraryView.Load(viewFile);}catch(InvalidDataException){threw=true;}Check(threw,"a view file that cannot be read is an error, so that it is not replaced");}
    File.WriteAllText(viewFile," \r\n");Check(LibraryView.Load(viewFile).Sort=="added","a blank view file is the default view");
    string viewBefore=Environment.GetEnvironmentVariable(LibraryView.OverrideVariable);
    try{Environment.SetEnvironmentVariable(LibraryView.OverrideVariable,Path.Combine(root,"seam-view.txt"));Check(LibraryView.PathName==Path.Combine(root,"seam-view.txt"),"the test variable moves the view file");
      foreach(string no in new[]{"view.txt",@"\\server\share\view.txt",Path.Combine(root,"view.xml"),""}){Environment.SetEnvironmentVariable(LibraryView.OverrideVariable,no);Check(LibraryView.PathName==Path.Combine(DeckPaths.Root,"library-view.txt"),"a view override that is not a full local .txt path is ignored");}
    }finally{Environment.SetEnvironmentVariable(LibraryView.OverrideVariable,viewBefore);}    Console.WriteLine(failed==0?"library core: all checks passed":"library core: "+failed+" failed");
    return failed==0?0:1;
  }
}
