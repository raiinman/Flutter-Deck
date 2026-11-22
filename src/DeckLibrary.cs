using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

// The Library screen (SPEC-library.md): cover cards with an action layer, a menu on each card, an item panel over the dimmed grid,
// tabs, a "Filter and sort" pane and a details view. This is the same window class as Program.cs, in its own file.
// Rules of this file (check-library.ps1 reads it):
//  - It starts no game and no program itself, and it reaches no network.
//  - Every Play control here goes through PressPlay, which presses the one Play button of Program.cs and only while that button is enabled.
//    This file never changes whether that button is enabled.
//  - A movie is opened only through OpenLibraryItem in Program.cs.
public partial class Deck {
  LibraryViewState libraryState=new LibraryViewState();bool libraryStateLoadFailed;
  string libraryTab="game",libraryFocusKey="";LibraryItem libraryOpen,libraryDetailsItem;bool libraryFilterOpen,libraryRefocus;
  readonly Grid libraryOverlay=new Grid{Visibility=Visibility.Collapsed},libraryContent=new Grid();
  readonly Border libraryFilterPane=new Border{Visibility=Visibility.Collapsed};
  readonly StackPanel libraryTabs=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
  ScrollViewer libraryScroll;Button libraryViewButton;

  static void Id(DependencyObject e,string id){System.Windows.Automation.AutomationProperties.SetAutomationId(e,id);}
  Button LibButton(string text,Action action,string id,string name){
    var b=Control(text,action,false,name??text);b.FontSize=14;b.MinHeight=30;b.Margin=new Thickness(2);b.Padding=new Thickness(10,2,10,2);Id(b,id);return b;
  }
  Button GlyphButton(string glyph,Action action,string id,string name){
    var b=LibButton(glyph,action,id,name);b.Padding=new Thickness(0);b.Width=32;b.Height=32;b.ToolTip=name;
    var text=b.Content as TextBlock;if(text!=null){text.FontFamily=new FontFamily("Segoe MDL2 Assets");text.FontSize=15;}
    return b;
  }
  void SaveLibraryState(){if(libraryStateLoadFailed)return;try{LibraryView.Save(LibraryView.PathName,libraryState);}catch{}}
  bool LibraryHas(LibraryItem item){if(item==null)return false;if(item.BuiltIn!=0||libraryUser.Contains(item))return true;if(item.Found)foreach(var known in libraryFound)if(LibraryCore.Same(known,item))return true;return false;} // a found item is known by what it is: a search makes new objects for the same files
  // The built-in items are made new each time they are listed, so two items are the same when their keys are the same.
  static bool SameItem(LibraryItem a,LibraryItem b){return a!=null&&b!=null&&String.Equals(LibraryView.Key(a),LibraryView.Key(b),StringComparison.OrdinalIgnoreCase);}
  static bool MenuKey(KeyEventArgs e){return e.Key==Key.Apps||((e.Key==Key.F10||(e.Key==Key.System&&e.SystemKey==Key.F10))&&(Keyboard.Modifiers&ModifierKeys.Shift)!=0);}
  bool MoviePresent(LibraryItem item){try{return item.Kind=="movie"&&LibraryCore.CanOpen(item)&&System.IO.File.Exists(item.Path);}catch{return false;}}

  // Called once, when the window is built.
  void BuildLibraryView(){
    try{libraryState=LibraryView.Load(LibraryView.PathName);}catch{libraryStateLoadFailed=true;}
    libraryGridView.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});libraryGridView.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
    var bar=new Grid{Margin=new Thickness(0,0,8,6)};
    bar.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});bar.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
    for(int i=0;i<3;i++)bar.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
    System.Windows.Automation.AutomationProperties.SetName(libraryTabs,"Library tabs");bar.Children.Add(libraryTabs);
    var search=new TextBox{Margin=new Thickness(10,2,6,2),MinHeight=32,FontSize=15,Padding=new Thickness(10,3,8,3),VerticalContentAlignment=VerticalAlignment.Center,ToolTip="Search the library by title"};
    System.Windows.Automation.AutomationProperties.SetName(search,"Search library");Id(search,"library-search");
    var hint=Label("Search library",15,"#7F9DB3");hint.Margin=new Thickness(22,0,0,0);hint.VerticalAlignment=VerticalAlignment.Center;hint.IsHitTestVisible=false;
    search.TextChanged+=delegate{librarySearch=search.Text;hint.Visibility=librarySearch.Length==0?Visibility.Visible:Visibility.Collapsed;RenderLibraryList();};
    var wrap=new Grid();wrap.Children.Add(search);wrap.Children.Add(hint);Grid.SetColumn(wrap,1);bar.Children.Add(wrap);
    libraryViewButton=LibButton("Grid",delegate{libraryState.View=libraryState.View=="grid"?"details":"grid";SaveLibraryState();libraryOpen=null;RenderLibrary();},"library-view","View");Grid.SetColumn(libraryViewButton,2);bar.Children.Add(libraryViewButton);
    var filter=LibButton("Filter and sort",delegate{libraryFilterOpen=!libraryFilterOpen;RenderLibraryFilter();},"library-filter-sort","Filter and sort");Grid.SetColumn(filter,3);bar.Children.Add(filter);
    var add=LibButton("+ Add",delegate{libraryPage=true;libraryAdding=true;libraryOpen=null;libraryFilterOpen=false;libraryNote="";addKind="movie";addTitle="";addPath="";Render();},"library-add","Add to library");Grid.SetColumn(add,4);bar.Children.Add(add);
    libraryScroll=new ScrollViewer{Content=libraryIcons,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false,PanningMode=PanningMode.VerticalOnly,CanContentScroll=false};
    Id(libraryScroll,"library-list");System.Windows.Automation.AutomationProperties.SetName(libraryScroll,"Library items");
    Grid.SetRow(libraryContent,1);Grid.SetRowSpan(libraryOverlay,2);Grid.SetRowSpan(libraryFilterPane,2);
    libraryGridView.Children.Add(bar);libraryGridView.Children.Add(libraryContent);libraryGridView.Children.Add(libraryOverlay);libraryGridView.Children.Add(libraryFilterPane);
    foreach(var entry in nav)if((string)entry.Tag=="Library")entry.Click+=delegate{if(libraryOpen!=null||libraryFilterOpen){libraryOpen=null;libraryFilterOpen=false;if(screen=="Library")RenderLibrary();}};
    // Escape on the Library closes what is open, innermost first. On the other screens Program.cs handles Escape.
    PreviewKeyDown+=delegate(object sender,KeyEventArgs e){
      if(e.Key!=Key.Escape||screen!="Library"||libraryPage)return;
      if(libraryFilterOpen){libraryFilterOpen=false;RenderLibraryFilter();e.Handled=true;}
      else if(libraryOpen!=null){CloseLibraryPanel();e.Handled=true;}
    };
  }

  void RenderLibrary(){
    ScaleLibrary();
    // Other screens ask for "the page of the current item" with libraryPage. That now means: show the item's panel.
    if(libraryPage&&!libraryAdding){libraryPage=false;ShowLibraryItem(LibraryCurrent(),false);}
    libraryGridView.Visibility=libraryPage?Visibility.Collapsed:Visibility.Visible;libraryPageView.Visibility=libraryPage?Visibility.Visible:Visibility.Collapsed;
    libraryDetailBody.Children.Clear();
    if(libraryPage){RenderLibraryAdd();return;}
    if(!LibraryHas(libraryOpen))libraryOpen=null;
    if(!LibraryHas(libraryDetailsItem))libraryDetailsItem=null;
    libraryViewButton.Content=new TextBlock{Text=libraryState.View=="grid"?"Grid":"Details",TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    System.Windows.Automation.AutomationProperties.SetName(libraryViewButton,libraryState.View=="grid"?"View: grid. Press for the details view":"View: details. Press for the grid view");
    RenderLibraryTabs();RenderLibraryList();RenderLibraryOverlay();RenderLibraryFilter();
  }
  // Makes an item the one that is shown: in the panel (grid view) or in the right half (details view).
  void ShowLibraryItem(LibraryItem item,bool render){
    if(item==null)return;
    if(item.BuiltIn!=0){game=item.BuiltIn;librarySelected=-1;}else librarySelected=libraryUser.IndexOf(item);
    if(!LibraryView.InTab(libraryState,item,libraryTab))libraryTab=item.Kind=="show"?"movie":item.Kind;
    if(LibraryView.IsHidden(libraryState,item))libraryState.ShowHidden=true;
    libraryNote="";libraryFocusKey=LibraryView.Key(item);
    if(libraryState.View=="details"){libraryDetailsItem=item;libraryOpen=null;}else libraryOpen=item;
    if(render)Render();
  }
  // The Settings screen chose another game: the Library shows and marks that game's card the next time it is opened (the card is found by its number).
  int libraryFollow;
  void LibraryFollowGame(){libraryOpen=null;libraryFollow=game;libraryFocusKey="builtin:"+game;}
  // Makes the game of a built-in Library item the game in use, before a button of that item opens a screen that works on the game in use.
  void UseItemGame(LibraryItem item){if(item!=null&&item.BuiltIn>=1&&item.BuiltIn<=runtimes.Length&&item.BuiltIn!=game)game=item.BuiltIn;}   // the screen it opens is drawn next, and drawing a screen refreshes the Play state
  void CloseLibraryPanel(){if(libraryOpen!=null){libraryFocusKey=LibraryView.Key(libraryOpen);libraryRefocus=true;}libraryOpen=null;libraryNote="";RenderLibrary();}

  void RenderLibraryTabs(){
    libraryTabs.Children.Clear();var all=LibraryAll();
    for(int i=0;i<LibraryView.Tabs.Length;i++){
      string tab=LibraryView.Tabs[i];bool selected=tab==libraryTab;int count=LibraryView.Count(all,libraryState,tab);
      var b=Control(LibraryView.TabNames[i]+"  "+count,delegate{libraryTab=tab;libraryOpen=null;libraryDetailsItem=null;RenderLibrary();},true,LibraryView.TabNames[i]+", "+count+(count==1?" item":" items")+(selected?", selected":""));
      b.FontSize=15;b.MinHeight=32;b.Margin=new Thickness(1,2,7,2);b.Padding=new Thickness(6,2,6,2);b.FontWeight=selected?FontWeights.Bold:FontWeights.Normal;Id(b,"library-tab-"+tab);
      var line=new Border{BorderBrush=Ink(selected?"#73F4FF":"#00000000"),BorderThickness=new Thickness(0,0,0,3),Child=b};libraryTabs.Children.Add(line);
    }
  }

  void RenderLibraryList(){
    libraryIcons.Children.Clear();libraryContent.Children.Clear();
    var all=LibraryAll();var items=LibraryView.Arrange(all,libraryState,libraryTab,librarySearch);int hidden=LibraryView.HiddenCount(all,libraryState,libraryTab);
    FrameworkElement footer=null;
    if(hidden>0){
      var show=LibButton(libraryState.ShowHidden?"Stop showing hidden items":"Show hidden items ("+hidden+")",delegate{libraryState.ShowHidden=!libraryState.ShowHidden;RenderLibrary();},"library-show-hidden",null);
      show.HorizontalAlignment=HorizontalAlignment.Left;show.Margin=new Thickness(6,4,6,8);footer=show;
    }
    string empty=items.Count>0?"":librarySearch.Trim().Length>0?"No results found. Try other words.":libraryTab=="fav"?"No favorites yet. Use the heart on an item to add it here.":hidden>0?"Everything here is hidden.":"Nothing here yet. Use Add to put an item in the library.";
    // A very large tab shows its first items only; the search box finds the others. This bounds the work of drawing one screen.
    if(items.Count>MostShown){
      var more=Label("The first "+MostShown+" of "+items.Count+" items are shown. Type in the search box to find the others.",14,"#AEC8DB");more.Margin=new Thickness(8,4,8,8);Id(more,"library-more-note");
      items=items.GetRange(0,MostShown);
      if(footer==null)footer=more;else{var both=new StackPanel();both.Children.Add(more);both.Children.Add(footer);footer=both;}
    }
    if(libraryState.View=="details"){RenderLibraryDetails(items,empty,footer);return;}
    int row=0;
    foreach(var item in items)libraryIcons.Children.Add(LibraryTile(item,row++));
    if(empty.Length>0){var none=Label(empty,15,"#AEC8DB");none.Margin=new Thickness(8);Id(none,"library-empty");libraryIcons.Children.Add(none);}
    if(footer!=null){var line=new Border{Width=1140,Child=footer};libraryIcons.Children.Add(line);}
    libraryContent.Children.Add(libraryScroll);libraryRefocus=false; // one time only: a later re-render (a search, an order) leaves the focus where it is
  }

  string LibraryStatus(LibraryItem item){
    string status=LibraryCore.Status(item,item.Found&&item.Kind!="movie"?MediaFolders.Present(item):MoviePresent(item));
    if(item.BuiltIn!=0&&RunningGame==item.BuiltIn)status="Game · running";
    return LibraryView.IsHidden(libraryState,item)?status+" · hidden":status;
  }
  // The word on an item's main button. A missing movie and a listed item have no action of their own.
  string LibraryActionText(LibraryItem item){
    if(item.BuiltIn!=0)return RunningGame==item.BuiltIn?"Running":"Play";
    if(MoviePresent(item))return "Open";
    return "Details";
  }
  // The main action of an item. Play goes through PressPlay. A movie goes through OpenLibraryItem. Anything else shows the item.
  void LibraryPrimary(LibraryItem item){LibraryPrimary(item,item);}
  // counted: the Library item whose "last opened" is written. It is the item itself, or the show when the item is one of its episodes.
  void LibraryPrimary(LibraryItem item,LibraryItem counted){
    if(item.BuiltIn!=0){PressPlay(item);return;}
    if(MoviePresent(item)){LibraryView.MarkOpened(libraryState,counted,DateTime.UtcNow);SaveLibraryState();OpenLibraryItem(item);return;}
    ShowLibraryItem(counted,true);
  }
  // The one way this file starts the game: it presses the Play button of the game page (Program.cs), and only while that button is
  // enabled. Program.cs alone decides that (UpdatePlay), from the same readiness checks as before. When the button is not enabled,
  // nothing is pressed and the item is shown with the reason.
  void PressPlay(LibraryItem item){
    if(item==null||item.BuiltIn<1||item.BuiltIn>runtimes.Length)return;
    game=item.BuiltIn;librarySelected=-1;UpdatePlay();
    if(gamePlay==null||!gamePlay.IsEnabled){ShowLibraryItem(item,true);return;}
    LibraryView.MarkOpened(libraryState,item,DateTime.UtcNow);SaveLibraryState();
    gamePlay.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    Render();
  }

  // One card: the cover only. Pointing at it, or moving the keyboard focus into it, shows a heart and the main action.
  // A click, a right click and the menu key all show the item's card, which has every action (developer, 2026-11-21: no three-dot menu,
  // "just have our cards pop up because they have more information").
  FrameworkElement LibraryTile(LibraryItem item,int row){
    bool favorite=LibraryView.IsFavorite(libraryState,item),hiddenItem=LibraryView.IsHidden(libraryState,item);string key=LibraryView.Key(item);
    var root=new Grid{Width=146,Height=195,Margin=new Thickness(5,2,5,10),Opacity=hiddenItem?0.55:1};
    var open=Control("",delegate{ShowLibraryItem(item,true);},true,item.Title+" - "+LibraryStatus(item));
    Id(open,"library-item-"+row);open.Padding=new Thickness(0);open.Margin=new Thickness(0);open.MinHeight=0;open.HorizontalContentAlignment=HorizontalAlignment.Stretch;open.VerticalContentAlignment=VerticalAlignment.Stretch;open.ToolTip=item.Title;
    var face=new Grid();var picture=LibraryPicture(item,146,195,44);bool last=libraryFocusKey==key;
    picture.BorderBrush=Ink(last?"#73F4FF":"#41637D");picture.BorderThickness=new Thickness(last?2:1);face.Children.Add(picture);
    if(LibraryCover(item,true)==null){ // no cover: the title is on the card, or every such card would look the same
      var name=Label(item.Title,13);name.FontWeight=FontWeights.SemiBold;name.TextAlignment=TextAlignment.Center;name.TextTrimming=TextTrimming.CharacterEllipsis;name.MaxHeight=56;name.Margin=new Thickness(8,0,8,10);name.VerticalAlignment=VerticalAlignment.Bottom;name.IsHitTestVisible=false;face.Children.Add(name);
    }
    if(favorite){var mark=Label("",13,"#FF8FA3");mark.FontFamily=new FontFamily("Segoe MDL2 Assets");mark.Margin=new Thickness(8,8,0,0);mark.HorizontalAlignment=HorizontalAlignment.Left;mark.VerticalAlignment=VerticalAlignment.Top;mark.IsHitTestVisible=false;face.Children.Add(mark);}
    open.Content=face;root.Children.Add(open);
    var layer=new Grid{Visibility=Visibility.Collapsed};
    layer.Children.Add(new Border{Background=Ink("#99061018"),CornerRadius=new CornerRadius(8),IsHitTestVisible=false});
    var heart=GlyphButton(favorite?"":"",delegate{LibraryView.SetFavorite(libraryState,item,!favorite);SaveLibraryState();libraryFocusKey=key;libraryRefocus=true;RenderLibrary();},"library-fav-"+row,favorite?"Remove "+item.Title+" from favorites":"Add "+item.Title+" to favorites");
    heart.HorizontalAlignment=HorizontalAlignment.Left;heart.VerticalAlignment=VerticalAlignment.Top;heart.Margin=new Thickness(6);layer.Children.Add(heart);
    var act=LibButton(LibraryActionText(item),delegate{LibraryPrimary(item);},"library-action-"+row,LibraryActionText(item)+": "+item.Title);
    act.VerticalAlignment=VerticalAlignment.Bottom;act.Margin=new Thickness(8);act.IsEnabled=!(item.BuiltIn!=0&&GameRunning);layer.Children.Add(act);
    root.Children.Add(layer);
    Action refresh=delegate{layer.Visibility=root.IsMouseOver||root.IsKeyboardFocusWithin?Visibility.Visible:Visibility.Collapsed;};
    root.MouseEnter+=delegate{refresh();};root.MouseLeave+=delegate{refresh();};root.IsKeyboardFocusWithinChanged+=delegate{refresh();};
    root.MouseRightButtonUp+=delegate(object sender,MouseButtonEventArgs e){ShowLibraryItem(item,true);e.Handled=true;};
    root.KeyDown+=delegate(object sender,KeyEventArgs e){if(MenuKey(e)){ShowLibraryItem(item,true);e.Handled=true;}};
    if(last&&libraryRefocus&&libraryOpen==null)Dispatcher.BeginInvoke(new Action(delegate{if(screen=="Library"&&open.IsVisible){open.Focus();root.BringIntoView();}}),System.Windows.Threading.DispatcherPriority.Loaded);
    return root;
  }

  // Removing goes through RemoveLibraryItem in Program.cs (it asks first and deletes no file). The item's view entries go with it.
  void RemoveLibraryEntry(LibraryItem item){
    int before=libraryUser.Count;RemoveLibraryItem(item);
    if(libraryUser.Count<before){LibraryView.Forget(libraryState,item);SaveLibraryState();if(SameItem(libraryOpen,item))libraryOpen=null;if(SameItem(libraryDetailsItem,item))libraryDetailsItem=null;RenderLibrary();}
  }

  string libraryPictureShown=""; // the picture of an album that is shown large
  const int MostShown=200;
  TextBlock FactLine(string name,string value){var t=Label(name+":  "+value,14,"#D8E6F0");t.Margin=new Thickness(0,1,0,1);return t;}
  // What is shown about one item: in the panel over the grid, and in the right half of the details view.
  FrameworkElement LibraryDetail(LibraryItem item){
    bool favorite=LibraryView.IsFavorite(libraryState,item),hiddenItem=LibraryView.IsHidden(libraryState,item),movie=item.Kind=="movie",present=MoviePresent(item);string key=LibraryView.Key(item);
    var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
    var left=new StackPanel{Margin=new Thickness(0,0,14,0)};var picture=LibraryPicture(item,270,152,44);picture.BorderBrush=Ink("#41637D");picture.BorderThickness=new Thickness(1);left.Children.Add(picture);
    var kindLine=Label(LibraryStatus(item),13,"#AEC8DB");kindLine.TextAlignment=TextAlignment.Center;left.Children.Add(kindLine);grid.Children.Add(left);
    var body=new StackPanel();Grid.SetColumn(body,1);grid.Children.Add(body);
    var title=Label(item.Title,22);title.FontWeight=FontWeights.Bold;title.Margin=new Thickness(0,0,40,2);Id(title,"library-title");body.Children.Add(title);
    string about=item.BuiltIn!=0?PlayStatusText():movie?(present?"This movie opens in the Windows program that you use for this kind of file. Flutter Deck does not play it itself.":"The movie file is not at its saved place. Put the file back, or remove this item and add it again."):item.Kind=="show"?(MediaFolders.Present(item)?"A show from your library folder. Each episode opens in the Windows program that you use for video files.":"The folder of this show was not found. Its drive may not be connected."):item.Kind=="art"?(MediaFolders.Present(item)?"Pictures from your library folder. Choose one to see it larger.":"The folder of these pictures was not found. Its drive may not be connected."):item.Found&&item.Kind=="game"?(MediaFolders.Present(item)?"This disc image was found in your library folder. Flutter Deck lists it; it cannot start it.":"This disc image was not found. Its drive may not be connected."):item.Kind=="game"?"This game is listed only. Flutter Deck cannot start it yet.":"This item is listed only.";
    var aboutLine=Label(about,14,"#F5EAD0");aboutLine.Margin=new Thickness(0,0,0,4);Id(aboutLine,"library-status");body.Children.Add(aboutLine);
    if(item.BuiltIn==3){var limits3=Label("Features coming soon. Checked by machine only, up to the first mission. Saving, the one movie on the disc and the sound are not tested; no analog stick and no vibration. Nobody has played it through Flutter Deck yet.",13,"#FFD98A");limits3.Margin=new Thickness(0,0,0,4);Id(limits3,"library-limits");body.Children.Add(limits3);}
    if(item.BuiltIn==2){var limits=Label("Features coming soon. Only a small part of the game has run so far, saving inside the game is not tested, and the analog stick is off until you switch it on in Settings.",13,"#FFD98A");limits.Margin=new Thickness(0,0,0,4);Id(limits,"library-limits");body.Children.Add(limits);}
    if(libraryNote.Length>0){var note=Label(libraryNote,14,"#FFD98A");Id(note,"library-note");body.Children.Add(note);}
    var actions=new WrapPanel{Margin=new Thickness(-2,0,0,4)};
    if(item.BuiltIn!=0){
      bool mine=RunningGame==item.BuiltIn;
      var play=LibButton(mine?"Running":"Play",delegate{PressPlay(item);},"library-play",mine?item.Title+" is running":"Play "+item.Title);
      play.FontWeight=FontWeights.Bold;play.MinWidth=120;play.Background=Ink("#FF1E7FB8");play.BorderBrush=Ink("#8BEFFF");play.IsEnabled=!GameRunning;actions.Children.Add(play);
    }else if(movie){
      var openMovie=LibButton("Open",delegate{LibraryPrimary(item);},"library-open","Open "+item.Title);openMovie.FontWeight=FontWeights.Bold;openMovie.MinWidth=120;openMovie.Background=Ink("#FF1E7FB8");openMovie.BorderBrush=Ink("#8BEFFF");openMovie.IsEnabled=present;actions.Children.Add(openMovie);
    }
    actions.Children.Add(GlyphButton(favorite?"":"",delegate{LibraryView.SetFavorite(libraryState,item,!favorite);SaveLibraryState();RenderLibrary();},"library-favorite",favorite?"Remove "+item.Title+" from favorites":"Add "+item.Title+" to favorites"));
    actions.Children.Add(LibButton(hiddenItem?"Unhide":"Hide",delegate{LibraryView.SetHidden(libraryState,item,!hiddenItem);SaveLibraryState();if(!hiddenItem){libraryOpen=null;libraryDetailsItem=null;}libraryFocusKey=key;RenderLibrary();},"library-hide",hiddenItem?"Unhide "+item.Title:"Hide "+item.Title+" from the library"));
    if(item.BuiltIn!=0){
      actions.Children.Add(LibButton("Setup",delegate{UseItemGame(item);Navigate("Preflight");},"library-setup","Game setup and readiness"));
      actions.Children.Add(LibButton("Settings",delegate{UseItemGame(item);Navigate("Settings");},"library-settings","Game settings of "+item.Title));   // owner, 2026-11-21: one click from the card to that game's settings (the Legends 2 free camera rows)
      actions.Children.Add(LibButton("Saves",delegate{UseItemGame(item);Navigate("Saves");},"library-saves","Saves"));
      if(FriendSoon&&FriendOffered(item)){var later=LibButton("Play with a friend...",delegate{},"library-friend","Play with a friend, coming soon");later.IsEnabled=false;later.ToolTip="Playing with a friend is coming soon.";ToolTipService.SetShowOnDisabled(later,true);actions.Children.Add(later);}
      else if(FriendOffered(item))actions.Children.Add(LibButton("Play with a friend...",delegate{ShowFriendWindow();},"library-friend","Play with a friend"));
      if(BuildOffered(item))actions.Children.Add(LibButton("Build my game...",delegate{ShowBuildWindow(item.BuiltIn);},"library-build","Build my game from my disc"));
      actions.Children.Add(LibButton("Files",delegate{OpenGameFiles(item.BuiltIn);},"library-files","Files of the built game"));
      // The same action and the same enabled state as the "Picture preset..." button of Program.cs.
      var presets=LibButton("Picture preset...",delegate{ShowPicturePicker();},"library-picture-preset","Picture preset...");
      if(pictureButton!=null)presets.SetBinding(UIElement.IsEnabledProperty,new Binding("IsEnabled"){Source=pictureButton,Mode=BindingMode.OneWay});else presets.IsEnabled=false;
      presets.ToolTip="Works while a game runs: start the game first. Picture presets need DSI Prism, which is not part of this version yet.";ToolTipService.SetShowOnDisabled(presets,true);   // why it is greyed out
      actions.Children.Add(presets);
    }
    if(item.BuiltIn==0&&!item.Found)actions.Children.Add(LibButton("Remove from library",delegate{RemoveLibraryEntry(item);},"library-remove","Remove "+item.Title+" from the library"));
    body.Children.Add(actions);
    // A show lists its episodes; each goes through LibraryPrimary as a movie file, the one place a file is opened from. An album shows its pictures inside the launcher.
    if(item.Found&&item.Kind=="show"){
      var list=new StackPanel{Margin=new Thickness(-2,2,0,4)};int number=0;
      foreach(string episode in MediaFolders.Episodes(item.Path)){
        string file=episode;number++;
        var row=LibButton(LibraryCore.TitleFromPath(file),delegate{LibraryPrimary(new LibraryItem{Kind="movie",Title=LibraryCore.TitleFromPath(file),Path=file},item);RenderLibrary();},"library-episode-"+number,"Open episode "+LibraryCore.TitleFromPath(file));
        row.HorizontalContentAlignment=HorizontalAlignment.Left;row.HorizontalAlignment=HorizontalAlignment.Stretch;list.Children.Add(row);
      }
      if(number==0&&MediaFolders.Present(item)){var none=Label("No episode files were found in this folder.",14,"#AEC8DB");list.Children.Add(none);}
      body.Children.Add(list);
    }
    if(item.Found&&item.Kind=="art"){
      var pictures=MediaFolders.Pictures(item.Path);
      if(libraryPictureShown.Length>0&&pictures.Contains(libraryPictureShown)){var big=new Image{MaxHeight=280,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,2,0,6)};SetPictureLater(big,libraryPictureShown,900);Id(big,"library-picture");body.Children.Add(big);}
      var wrap=new WrapPanel{Margin=new Thickness(-2,2,0,4)};int number=0;
      foreach(string shot in pictures){
        if(number>=48)break;string file=shot;number++;
        var small=new Image{Stretch=Stretch.UniformToFill};SetPictureLater(small,file,160);
        var thumb=new Button{Width=92,Height=70,Margin=new Thickness(2),Padding=new Thickness(0),Background=Ink("#1B3F5C"),BorderBrush=Ink(file==libraryPictureShown?"#8BEFFF":"#41637D"),Content=small};
        Id(thumb,"library-art-"+number);System.Windows.Automation.AutomationProperties.SetName(thumb,"Show picture "+number+": "+LibraryCore.TitleFromPath(file));thumb.Click+=delegate{libraryPictureShown=file;RenderLibrary();};wrap.Children.Add(thumb);
      }
      body.Children.Add(wrap);
      if(pictures.Count>number)body.Children.Add(Label("The first "+number+" of "+pictures.Count+" pictures are shown.",13,"#AEC8DB"));
    }
    var facts=new Grid{Margin=new Thickness(0,4,0,0)};facts.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});facts.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
    var activity=new StackPanel{Margin=new Thickness(0,0,8,0)};var head1=Label("Activity",15);head1.FontWeight=FontWeights.Bold;head1.Margin=new Thickness(0,0,0,2);activity.Children.Add(head1);
    bool opens=item.BuiltIn!=0||movie;
    if(opens){activity.Children.Add(FactLine(movie?"Last opened":"Last played from here",LibraryView.WhenText(LibraryView.LastOpened(libraryState,item),DateTime.UtcNow)));activity.Children.Add(FactLine(movie?"Times opened":"Times started from here",LibraryView.TimesOpened(libraryState,item).ToString()));}
    else activity.Children.Add(FactLine("Activity","none; this item cannot be started here"));
    var details=new StackPanel();Grid.SetColumn(details,1);var head2=Label("Details",15);head2.FontWeight=FontWeights.Bold;head2.Margin=new Thickness(0,0,0,2);details.Children.Add(head2);
    details.Children.Add(FactLine("Kind",LibraryCore.KindName(item.Kind)));
    if(item.BuiltIn>=1&&item.BuiltIn<=coverCode.Length&&coverCode[item.BuiltIn-1].Length>0)details.Children.Add(FactLine("Disc code",coverCode[item.BuiltIn-1]));
    if(movie||item.Found){var file=FactLine(item.Kind=="show"||item.Kind=="art"?"Folder":"File",item.Path);file.TextWrapping=TextWrapping.Wrap;details.Children.Add(file);}
    facts.Children.Add(activity);facts.Children.Add(details);body.Children.Add(facts);
    return grid;
  }

  // The item panel: it lies over the grid, and the grid behind it is dimmed. The close button, Escape and a click beside it close it.
  void RenderLibraryOverlay(){
    libraryOverlay.Children.Clear();
    if(libraryOpen==null||libraryState.View=="details"){libraryOverlay.Visibility=Visibility.Collapsed;return;}
    var item=libraryOpen;
    var dim=new Border{Background=Ink("#C4040A10"),CornerRadius=new CornerRadius(10)};dim.MouseLeftButtonUp+=delegate{CloseLibraryPanel();};libraryOverlay.Children.Add(dim);
    var inner=new Grid();
    inner.Children.Add(new ScrollViewer{Content=LibraryDetail(item),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false,PanningMode=PanningMode.VerticalOnly});
    var close=GlyphButton("",delegate{CloseLibraryPanel();},"library-close","Close");close.HorizontalAlignment=HorizontalAlignment.Right;close.VerticalAlignment=VerticalAlignment.Top;close.Margin=new Thickness(0,0,14,0);inner.Children.Add(close);
    var box=new Border{Margin=new Thickness(40,6,40,6),Background=Ink("#FF14304A"),BorderBrush=Ink("#5ECDEB"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(14,12,6,10),Child=inner};
    System.Windows.Automation.AutomationProperties.SetName(box,item.Title);Id(box,"library-panel");KeyboardNavigation.SetTabNavigation(box,KeyboardNavigationMode.Cycle);
    libraryOverlay.Children.Add(box);libraryOverlay.Visibility=Visibility.Visible;
    Dispatcher.BeginInvoke(new Action(delegate{if(SameItem(libraryOpen,item)&&screen=="Library"&&!box.IsKeyboardFocusWithin)close.Focus();}),System.Windows.Threading.DispatcherPriority.Loaded);
  }

  // "Filter and sort": a pane at the right side with the order and what is shown.
  void RenderLibraryFilter(){
    libraryFilterPane.Visibility=libraryFilterOpen?Visibility.Visible:Visibility.Collapsed;libraryFilterPane.Child=null;
    if(!libraryFilterOpen)return;
    libraryFilterPane.HorizontalAlignment=HorizontalAlignment.Right;libraryFilterPane.Width=300;libraryFilterPane.Background=Ink("#FF14304A");libraryFilterPane.BorderBrush=Ink("#5ECDEB");libraryFilterPane.BorderThickness=new Thickness(1);libraryFilterPane.CornerRadius=new CornerRadius(10);libraryFilterPane.Padding=new Thickness(12,8,8,8);
    Id(libraryFilterPane,"library-filter-pane");System.Windows.Automation.AutomationProperties.SetName(libraryFilterPane,"Filter and sort");
    var list=new StackPanel();
    var head=new Grid();head.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});head.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});head.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
    var name=Label("Filter and sort",16);name.FontWeight=FontWeights.Bold;name.VerticalAlignment=VerticalAlignment.Center;head.Children.Add(name);
    var clear=LibButton("Clear",delegate{libraryState.Sort=LibraryView.Sorts[0];libraryState.ShowHidden=false;SaveLibraryState();RenderLibrary();},"library-filter-clear","Clear the order and the filter");Grid.SetColumn(clear,1);head.Children.Add(clear);
    var close=GlyphButton("",delegate{libraryFilterOpen=false;RenderLibraryFilter();},"library-filter-close","Close filter and sort");Grid.SetColumn(close,2);head.Children.Add(close);list.Children.Add(head);
    var sortHead=Label("Sort",15);sortHead.FontWeight=FontWeights.Bold;sortHead.Margin=new Thickness(0,6,0,2);list.Children.Add(sortHead);
    for(int i=0;i<LibraryView.Sorts.Length;i++){
      string key=LibraryView.Sorts[i];
      var choice=new RadioButton{Content=LibraryView.SortNames[i],GroupName="library-sort",IsChecked=libraryState.Sort==key,Foreground=Ink("#F5EAD0"),FontSize=15,Margin=new Thickness(2,3,0,3),VerticalContentAlignment=VerticalAlignment.Center};
      Id(choice,"library-sort-"+key);choice.Checked+=delegate{if(libraryState.Sort==key)return;libraryState.Sort=key;SaveLibraryState();RenderLibraryList();};list.Children.Add(choice);
    }
    var showHead=Label("Status",15);showHead.FontWeight=FontWeights.Bold;showHead.Margin=new Thickness(0,8,0,2);list.Children.Add(showHead);
    var hiddenBox=new CheckBox{Content="Show hidden items",IsChecked=libraryState.ShowHidden,Foreground=Ink("#F5EAD0"),FontSize=15,Margin=new Thickness(2,3,0,3),VerticalContentAlignment=VerticalAlignment.Center};Id(hiddenBox,"library-show-hidden-box");
    RoutedEventHandler changed=delegate{bool on=hiddenBox.IsChecked==true;if(libraryState.ShowHidden==on)return;libraryState.ShowHidden=on;RenderLibraryTabs();RenderLibraryList();};hiddenBox.Checked+=changed;hiddenBox.Unchecked+=changed;list.Children.Add(hiddenBox);
    libraryFilterPane.Child=new ScrollViewer{Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false};
  }

  // The details view: the titles at the left, the chosen item at the right.
  void RenderLibraryDetails(List<LibraryItem> items,string empty,FrameworkElement footer){
    var split=new Grid();split.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(330)});split.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
    var rows=new StackPanel();
    if(libraryFollow!=0){foreach(var known in items)if(known.BuiltIn==libraryFollow){libraryDetailsItem=known;break;}libraryFollow=0;}
    LibraryItem chosen=null;foreach(var known in items)if(SameItem(known,libraryDetailsItem)){chosen=known;break;}libraryDetailsItem=chosen!=null?chosen:items.Count>0?items[0]:null;
    int row=0;
    foreach(var each in items){
      var item=each;bool selected=SameItem(item,libraryDetailsItem);
      var line=new Grid();line.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});line.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
      var small=LibraryPicture(item,30,40,16);small.CornerRadius=new CornerRadius(4);small.Margin=new Thickness(0,0,10,0);line.Children.Add(small);
      var text=new StackPanel{VerticalAlignment=VerticalAlignment.Center};var t=Label(item.Title,15,MoviePresent(item)||item.BuiltIn!=0?"#F5EAD0":"#AEC8DB");t.Margin=new Thickness(0);t.TextWrapping=TextWrapping.NoWrap;t.TextTrimming=TextTrimming.CharacterEllipsis;t.FontWeight=selected?FontWeights.Bold:FontWeights.Normal;text.Children.Add(t);
      var st=Label(LibraryStatus(item),12,"#AEC8DB");st.Margin=new Thickness(0);st.TextWrapping=TextWrapping.NoWrap;st.TextTrimming=TextTrimming.CharacterEllipsis;text.Children.Add(st);Grid.SetColumn(text,1);line.Children.Add(text);
      var b=Control("",delegate{ShowLibraryItem(item,true);},true,item.Title+" - "+LibraryStatus(item)+(selected?", selected":""));Id(b,"library-item-"+row++);b.Content=line;b.HorizontalContentAlignment=HorizontalAlignment.Stretch;b.Padding=new Thickness(6,3,6,3);b.Margin=new Thickness(0,1,6,1);b.MinHeight=0;
      if(selected){b.Background=Ink("#FF225F8E");}
      b.MouseRightButtonUp+=delegate(object sender,MouseButtonEventArgs e){ShowLibraryItem(item,true);e.Handled=true;};
      b.KeyDown+=delegate(object sender,KeyEventArgs e){if(MenuKey(e)){ShowLibraryItem(item,true);e.Handled=true;}};
      rows.Children.Add(selected?(UIElement)new Border{Background=Ink("#FF225F8E"),CornerRadius=new CornerRadius(6),Margin=new Thickness(0,0,6,0),Child=b}:b);
    }
    if(empty.Length>0){var none=Label(empty,15,"#AEC8DB");none.Margin=new Thickness(8);Id(none,"library-empty");rows.Children.Add(none);}
    if(footer!=null)rows.Children.Add(footer);
    var listScroll=new ScrollViewer{Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false,PanningMode=PanningMode.VerticalOnly};Id(listScroll,"library-list");System.Windows.Automation.AutomationProperties.SetName(listScroll,"Library items");
    split.Children.Add(listScroll);
    if(libraryDetailsItem!=null){var right=new ScrollViewer{Content=LibraryDetail(libraryDetailsItem),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false,PanningMode=PanningMode.VerticalOnly,Padding=new Thickness(10,0,6,0)};Id(right,"library-detail");Grid.SetColumn(right,1);split.Children.Add(right);}
    libraryContent.Children.Add(split);
  }
}
