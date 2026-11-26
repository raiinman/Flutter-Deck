using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Reflection;
using System.Collections.Generic;
using System.Windows.Input;

public partial class Deck : Window { // the Library screen is in DeckLibrary.cs
  readonly Canvas scene = new Canvas { Width=1672, Height=941 };
  readonly StackPanel page = new StackPanel();
  readonly List<Button> nav = new List<Button>();
  readonly DeckAudio audio=new DeckAudio();readonly List<UIElement> transportControls=new List<UIElement>();readonly List<System.Windows.Shapes.Rectangle> waveform=new List<System.Windows.Shapes.Rectangle>();Button playButton,previousButton,nextButton,stopButton;const string PodInk="#0F2034";Slider seekSlider;TextBlock musicInfo;bool syncingMusic=false;
  readonly List<UIElement> libraryControls = new List<UIElement>();TextBlock waveIdle;
  readonly Dictionary<string,int> featureChoices = new Dictionary<string,int>();
  DsiScaling.ScaleSurface scalingOutput; readonly Border panel = new Border();
  // Library (SPEC-library.md): a grid of icons; one click on an icon opens that item's page. librarySelected is the place of an
  // added item in libraryUser, or -1 when a built-in game (the field "game") is selected. libraryPage: an item's page is open.
  List<LibraryItem> libraryUser=new List<LibraryItem>(),libraryFound=new List<LibraryItem>();List<string> libraryFolders=new List<string>();bool foldersLoadFailed=false,folderPending=false;int folderBusy=0;string foldersNote="";readonly Dictionary<string,ImageSource> foundPictures=new Dictionary<string,ImageSource>(StringComparer.OrdinalIgnoreCase); /* library folders (SPEC-library-folders.md) */ int librarySelected=-1;string libraryKind="",libraryNote="",addKind="movie",addTitle="",addPath="";bool libraryPage=false,libraryAdding=false,libraryLoadFailed=false,libraryFilling=false;string librarySearch="";int librarySort=0;LibraryItem libraryFocus;
  readonly Border libraryBack=new Border(),librarySide=new Border();readonly Grid libraryGridView=new Grid(),libraryPageView=new Grid();readonly WrapPanel libraryIcons=new WrapPanel();readonly StackPanel libraryDetailBody=new StackPanel(),librarySideBody=new StackPanel();ComboBox libraryFilter;readonly List<UIElement> gameOnly=new List<UIElement>();BitmapSource shipArt;
  TextBlock gameTitle, status, heading,readiness; ScrollViewer bodyScroll; bool setupLoadFailed=false; readonly SafeSessionLog sessionLog=new SafeSessionLog(); double contentScale=1;
  int setupTarget=0,setupStep=0; SetupState localSetup=new SetupState(),setupDraft=new SetupState(); string setupNotice="";string gameFolder="",gameFolderNote="";DiscScan discScan;volatile bool coversOn=false;bool coverPending;string coverNote="";readonly string[] coverCode={"","",""};readonly Dictionary<string,ImageSource> coverPictures=new Dictionary<string,ImageSource>();int coverBusy; // the folder the user named for disc images (SPEC-disc-folder.md)
  string screen="Home"; int game=1; Button gamePlay; readonly RuntimeSettings[] runtimes={new RuntimeSettings(),new RuntimeSettings(),new RuntimeSettings()}; RuntimeSettings runtime {get{return runtimes[game-1];}} int sessionGame=1,discScanGame=1; GameKind Kind {get{return GameRuntime.Game(game);}} string GameData {get{return GameRuntime.DataFolderFor(game);}} string[] OtherData {get{var others=new List<string>();for(int id=1;id<=GameRuntime.GameCount;id++)if(id!=game)others.Add(GameRuntime.DataFolderFor(id));return others.ToArray();}} RuntimeCheck playCheck=new RuntimeCheck(); GameSession gameSession; bool runtimeLoadFailed=false,playFailed=false,musicPausedForGame=false,keyDirty=false,keyLoadFailed=false; KeyMap keyDraft; string keyCapture=null,keyNote="",cardNote=""; string playNote=""; PlayPhase playPhase=PlayPhase.Unavailable; DeckChoices choices=new DeckChoices(),choicesBase=new DeckChoices(); bool choicesLoading=true; System.Windows.Threading.DispatcherTimer choiceTimer; Slider volumeSlider; int bundledTracks=0;
  Button pictureButton;DeckPrismBackend pictureBackend;bool pictureBusy;string pictureStatus="Choose a picture preset.",pictureSelected="";
  GameOverlay overlay; // overlay: the in-game overlay (SPEC-overlay.md); started by StartOverlay, ended by StopOverlay
  readonly string[] screens={"Home","Scaling","Library","Downloads","Music","Settings","Advanced"};
  static SolidColorBrush Ink(string c){return (SolidColorBrush)new BrushConverter().ConvertFromString(c);}
  TextBlock Label(string value,double size=18,string color="#F5EAD0") {return new TextBlock{Text=value,FontSize=size,Foreground=Ink(color),FontFamily=new FontFamily("Segoe UI"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,6)};}
  void Place(UIElement e,double x,double y,double w,double h){var f=e as FrameworkElement;f.Width=w;f.Height=h;f.Margin=new Thickness(0);f.MinHeight=0;Canvas.SetLeft(e,x);Canvas.SetTop(e,y);scene.Children.Add(e);if(x>=350 && y>=281 && y<632 && e!=panel)libraryControls.Add(e);if(y>=646&&y<701)transportControls.Add(e);}
  Button Control(string label,Action action,bool glass=false,string accessibleName=null){var b=new Button{Content=new TextBlock{Text=label,TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap},Margin=new Thickness(6),MinHeight=36,Foreground=Ink("#FFF1D8"),Background=glass?Brushes.Transparent:Ink("#C8102639"),BorderBrush=glass?Brushes.Transparent:Ink("#5985A0"),BorderThickness=new Thickness(glass?0:1),FontSize=18,HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center,Padding=new Thickness(12,4,12,4),Cursor=Cursors.Hand};var template=new ControlTemplate(typeof(Button));
    var border=new FrameworkElementFactory(typeof(Border));border.Name="Surface";
    border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(System.Windows.Controls.Control.BackgroundProperty));
    border.SetValue(Border.BorderBrushProperty,new TemplateBindingExtension(System.Windows.Controls.Control.BorderBrushProperty));
    border.SetValue(Border.BorderThicknessProperty,new TemplateBindingExtension(System.Windows.Controls.Control.BorderThicknessProperty));
    border.SetValue(Border.CornerRadiusProperty,new CornerRadius(glass?8:4));
    var content=new FrameworkElementFactory(typeof(ContentPresenter));
    content.SetValue(ContentPresenter.HorizontalAlignmentProperty,new TemplateBindingExtension(System.Windows.Controls.Control.HorizontalContentAlignmentProperty));
    content.SetValue(ContentPresenter.VerticalAlignmentProperty,VerticalAlignment.Center);
    content.SetValue(ContentPresenter.MarginProperty,new TemplateBindingExtension(System.Windows.Controls.Control.PaddingProperty));
    border.AppendChild(content);template.VisualTree=border;
    foreach(var prop in new[]{UIElement.IsMouseOverProperty,UIElement.IsKeyboardFocusWithinProperty}){
      var trigger=new Trigger{Property=prop,Value=true};
      trigger.Setters.Add(new Setter(Border.BorderBrushProperty,Ink("#8BEFFF"),"Surface"));
      trigger.Setters.Add(new Setter(Border.BorderThicknessProperty,new Thickness(2),"Surface"));
      trigger.Setters.Add(new Setter(Border.BackgroundProperty,Ink(glass?"#3618A9CD":"#D52B526A"),"Surface"));template.Triggers.Add(trigger);
    }
    var pressed=new Trigger{Property=System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty,Value=true};pressed.Setters.Add(new Setter(Border.BackgroundProperty,Ink("#174566"),"Surface"));template.Triggers.Add(pressed);var disabled=new Trigger{Property=UIElement.IsEnabledProperty,Value=false};disabled.Setters.Add(new Setter(UIElement.OpacityProperty,0.45));template.Triggers.Add(disabled);b.Template=template;
    System.Windows.Automation.AutomationProperties.SetName(b,accessibleName??label);b.ToolTip=accessibleName??label;b.Click+=delegate{action();};return b;}
  Button DeckControl(string text,Action action,string name,string sprite=null){var b=Control(text,action,false,name);b.FontSize=14;b.Padding=new Thickness(6,2,6,2);b.Background=Ink("#A8102639");b.BorderBrush=Ink("#606D8190");if(sprite!=null&&Sprite(sprite)!=null){b.Background=Brushes.Transparent;b.BorderBrush=Brushes.Transparent;b.BorderThickness=new Thickness(0);Dress(b,sprite,null);}return b;}
  // Existing ship artwork is the visual control; native buttons provide input and accessibility only.
  Button MusicIcon(string symbol,Action action,string name){
    var b=new Button{Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(0),Cursor=Cursors.Hand,ToolTip=name,FocusVisualStyle=null};
    var template=new ControlTemplate(typeof(Button));var hit=new FrameworkElementFactory(typeof(Border));hit.SetValue(Border.BackgroundProperty,Brushes.Transparent);hit.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));template.VisualTree=hit;b.Template=template;
    System.Windows.Automation.AutomationProperties.SetName(b,name);System.Windows.Automation.AutomationProperties.SetAutomationId(b,"music-"+symbol);b.Click+=delegate{action();};return b;
  }
  // ---- sprite controls: the developer's Visual Controls Pack v1, cut by make-sprites.ps1 and embedded in this program (SPEC-controls.md) ----
  static readonly Dictionary<string,ImageSource> sprites=new Dictionary<string,ImageSource>();
  static ImageSource Sprite(string name){
    ImageSource found;if(sprites.TryGetValue(name,out found))return found;
    try{using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("S-"+name)){if(s!=null){var img=new BitmapImage();img.BeginInit();img.CacheOption=BitmapCacheOption.OnLoad;img.StreamSource=s;img.EndInit();img.Freeze();found=img;}}}catch{found=null;}
    sprites[name]=found;return found;
  }
  static ControlTemplate BareTemplate(){var template=new ControlTemplate(typeof(Button));var hit=new FrameworkElementFactory(typeof(Border));hit.SetValue(Border.BackgroundProperty,Brushes.Transparent);hit.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));template.VisualTree=hit;return template;}
  sealed class Dressing { public string Name; public Image Art; public Border Shade,Ring,Mark; }
  readonly Dictionary<Button,Dressing> dressed=new Dictionary<Button,Dressing>();
  // Draws a button with a sprite. Where the pack gives four state sprites (name-normal/-hover/-pressed/-disabled) the
  // art itself changes; where it gives one, hover lightens it, pressing darkens it and a disabled button fades.
  // Keyboard focus always shows its own ring, and the accessible name and tooltip are untouched. A backing colour
  // covers an icon painted into the ship art underneath. If a sprite is missing the button keeps working undressed.
  void Dress(Button b,string name,string backing){
    if(Sprite(name)==null&&Sprite(name+"-normal")==null)return;
    var d=new Dressing{Name=name,Art=new Image{Stretch=Stretch.Uniform,IsHitTestVisible=false},Shade=new Border{IsHitTestVisible=false,CornerRadius=new CornerRadius(9)},Ring=new Border{IsHitTestVisible=false,BorderBrush=Ink("#F5EAD0"),BorderThickness=new Thickness(2),CornerRadius=new CornerRadius(9),Margin=new Thickness(-3),Visibility=Visibility.Collapsed},Mark=new Border{IsHitTestVisible=false,Height=4,Background=Ink("#5ECDEB"),CornerRadius=new CornerRadius(2),VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(10,0,10,-3),Visibility=Visibility.Collapsed}};
    RenderOptions.SetBitmapScalingMode(d.Art,BitmapScalingMode.HighQuality);
    var art=new Grid{HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};art.Children.Add(d.Art);art.Children.Add(d.Shade);art.Children.Add(d.Ring);art.Children.Add(d.Mark);
    var whole=new Grid();if(backing!=null)whole.Children.Add(new Border{Background=Ink(backing),IsHitTestVisible=false});whole.Children.Add(art);
    b.Template=BareTemplate();b.FocusVisualStyle=null;b.Content=whole;b.Padding=new Thickness(0);b.HorizontalContentAlignment=HorizontalAlignment.Stretch;b.VerticalContentAlignment=VerticalAlignment.Stretch;dressed[b]=d;
    EventHandler changed=delegate{Redress(b);};
    System.ComponentModel.DependencyPropertyDescriptor.FromProperty(UIElement.IsMouseOverProperty,typeof(Button)).AddValueChanged(b,changed);
    System.ComponentModel.DependencyPropertyDescriptor.FromProperty(System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty,typeof(Button)).AddValueChanged(b,changed);
    System.ComponentModel.DependencyPropertyDescriptor.FromProperty(UIElement.IsKeyboardFocusedProperty,typeof(Button)).AddValueChanged(b,changed);
    b.IsEnabledChanged+=delegate{Redress(b);};
    Redress(b);
  }
  void Redress(Button b){
    Dressing d;if(!dressed.TryGetValue(b,out d))return;
    string state=!b.IsEnabled?"disabled":b.IsPressed?"pressed":b.IsMouseOver?"hover":"normal";
    bool ownStates=Sprite(d.Name+"-normal")!=null;
    d.Art.Source=ownStates?Sprite(d.Name+"-"+state):Sprite(d.Name);
    d.Art.Opacity=!ownStates&&state=="disabled"?0.4:1;
    d.Shade.Background=ownStates?Brushes.Transparent:state=="hover"?Ink("#2EFFFFFF"):state=="pressed"?Ink("#55000000"):Brushes.Transparent;
    d.Ring.Visibility=b.IsKeyboardFocused?Visibility.Visible:Visibility.Collapsed;
  }
  void SetSprite(Button b,string name){Dressing d;if(dressed.TryGetValue(b,out d)&&d.Name!=name){d.Name=name;Redress(b);}}
  // "On" for a toggle (shuffle, repeat) is a bar under the button, not a glow, so it does not rely on colour alone.
  void SetMark(Button b,bool on){Dressing d;if(dressed.TryGetValue(b,out d))d.Mark.Visibility=on?Visibility.Visible:Visibility.Collapsed;}
  // ---- ceiling lamps: the pack's still frames over the four painted lamps, with a rare brief dip ----
  readonly List<Image> lamps=new List<Image>();readonly Random lampRandom=new Random();
  void AddLamps(){
    if(Sprite("lamp-on")==null)return;
    foreach(double cx in new[]{285.0,597.0,1070.0,1397.0}){var lamp=new Image{Source=Sprite("lamp-on"),Stretch=Stretch.Uniform,IsHitTestVisible=false};RenderOptions.SetBitmapScalingMode(lamp,BitmapScalingMode.HighQuality);Place(lamp,cx-55.5,240-44,111,111);lamps.Add(lamp);}
    // People who have asked Windows to reduce animation get steady lamps.
    if(!SystemParameters.ClientAreaAnimation||Sprite("lamp-dim")==null)return;
    var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(4+lampRandom.NextDouble()*3)};
    timer.Tick+=delegate{
      timer.Interval=TimeSpan.FromSeconds(4+lampRandom.NextDouble()*3);
      var lamp=lamps[lampRandom.Next(lamps.Count)];lamp.Source=Sprite("lamp-dim");
      var back=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100+lampRandom.Next(61))};
      back.Tick+=delegate{back.Stop();lamp.Source=Sprite("lamp-on");};back.Start();
    };
    timer.Start();Closed+=delegate{timer.Stop();};
  }
  // Keep the platform ScrollBar/Track/commands intact; replace only the paint of the thumb and the four line-arrow buttons.
  readonly System.Collections.Generic.Dictionary<System.Windows.Input.ICommand,ControlTemplate> scrollArrowTemplates=new System.Collections.Generic.Dictionary<System.Windows.Input.ICommand,ControlTemplate>();
  static ControlTemplate ScrollArrowTemplate(string arrow){return (ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate TargetType='RepeatButton' xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'><Border x:Name='Bd' Background='#102638'><Path x:Name='Arrow' Data='"+arrow+@"' Fill='#F5EAD0' HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Bd' Property='Background' Value='#163B54'/><Setter TargetName='Arrow' Property='Fill' Value='#5ECDEB'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='Bd' Property='Background' Value='#41637D'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");}
  void BuildScrollArrowTemplates(){
    scrollArrowTemplates[System.Windows.Controls.Primitives.ScrollBar.LineUpCommand]=ScrollArrowTemplate("M0,5 L4,0 L8,5 Z");scrollArrowTemplates[System.Windows.Controls.Primitives.ScrollBar.LineDownCommand]=ScrollArrowTemplate("M0,0 L8,0 L4,5 Z");
    scrollArrowTemplates[System.Windows.Controls.Primitives.ScrollBar.LineLeftCommand]=ScrollArrowTemplate("M5,0 L0,4 L5,8 Z");scrollArrowTemplates[System.Windows.Controls.Primitives.ScrollBar.LineRightCommand]=ScrollArrowTemplate("M0,0 L5,4 L0,8 Z");
  }
  // Escape with a dropdown list open belongs to the list: it closes the list and keeps the selection.
  // Returns the open ComboBox the key event came from (the box itself or one of its rows), or null.
  static ComboBox OpenDropDown(object source){
    var node=source as DependencyObject;
    while(node!=null){
      var box=node as ComboBox;if(box!=null)return box.IsDropDownOpen?box:null;
      var row=node as ComboBoxItem;if(row!=null){var owner=ItemsControl.ItemsControlFromItemContainer(row) as ComboBox;return owner!=null&&owner.IsDropDownOpen?owner:null;}
      var up=node is Visual?VisualTreeHelper.GetParent(node):null;node=up!=null?up:LogicalTreeHelper.GetParent(node); // a list's popup has no visual parent; its logical parent leads back to the box
    }
    return null;
  }
  void ScrollbarLoaded(object sender,RoutedEventArgs e){var bar=(System.Windows.Controls.Primitives.ScrollBar)sender;bar.ApplyTemplate();SkinScrollThumb(bar);}
  void SkinScrollThumb(DependencyObject node){
    var arrowButton=node as System.Windows.Controls.Primitives.RepeatButton;ControlTemplate arrowTemplate;
    if(arrowButton!=null&&arrowButton.Command!=null&&scrollArrowTemplates.TryGetValue(arrowButton.Command,out arrowTemplate)){arrowButton.Template=arrowTemplate;return;}
    var thumb=node as System.Windows.Controls.Primitives.Thumb;
    if(thumb!=null){
      var template=new ControlTemplate(typeof(System.Windows.Controls.Primitives.Thumb));var surface=new FrameworkElementFactory(typeof(Border));surface.Name="Grip";surface.SetValue(Border.BackgroundProperty,Ink("#3C7B98"));surface.SetValue(Border.BorderBrushProperty,Ink("#89CDE2"));surface.SetValue(Border.BorderThicknessProperty,new Thickness(1));surface.SetValue(Border.CornerRadiusProperty,new CornerRadius(6));surface.SetValue(FrameworkElement.MarginProperty,new Thickness(3,1,3,1));template.VisualTree=surface;
      foreach(var state in new[]{UIElement.IsMouseOverProperty,System.Windows.Controls.Primitives.Thumb.IsDraggingProperty}){var trigger=new Trigger{Property=state,Value=true};trigger.Setters.Add(new Setter(Border.BackgroundProperty,Ink("#65C6DD"),"Grip"));trigger.Setters.Add(new Setter(Border.BorderBrushProperty,Ink("#F5EAD0"),"Grip"));template.Triggers.Add(trigger);}
      thumb.Template=template;return;
    }
    for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)SkinScrollThumb(VisualTreeHelper.GetChild(node,i));
  }
  public Deck(){
    sessionLog.Add("session-started");
    try{contentScale=UiPreferences.Load(UiPreferences.PathName);}catch{contentScale=1;}try{libraryUser=LibraryCore.Load(LibraryCore.PathName);}catch{libraryUser=new List<LibraryItem>();libraryLoadFailed=true;}try{libraryFolders=MediaFolders.LoadFolders(MediaFolders.PathName);libraryFound=MediaFolders.LoadFound(MediaFolders.FoundPathName);}catch{libraryFolders=new List<string>();libraryFound=new List<LibraryItem>();foldersLoadFailed=true;}libraryFound.RemoveAll(i=>!libraryFolders.Exists(f=>MediaFolders.SameFolder(f,i.Root))); // an item of a folder that is no longer used is not shown
    // The remembered music choices are applied before any track is opened, so nothing is heard at the wrong volume.
    choices=DeckChoices.Load(DeckChoices.PathName);audio.Muted=false;audio.Repeat=false;audio.Shuffle=false;audio.Volume=choices.Volume/100.0;page.LayoutTransform=new ScaleTransform(UiPreferences.Drawn(contentScale),UiPreferences.Drawn(contentScale));
    try{localSetup=LocalSetup.Load(LocalSetup.ConfigPath);}catch{setupLoadFailed=true;setupNotice="Saved setup could not be loaded; it has not been overwritten.";sessionLog.Add("setup-read-failed");}
    try{coversOn=CoverArt.LoadEnabled(CoverArt.SettingPath);}catch{} // cover art switch (SPEC-covers.md), read before anything can ask for a cover
    // The game folder is searched at start only when the saved disc image does not work. A saved image that works is left alone.
    try{gameFolder=DiscFinder.LoadFolder(DiscFinder.PathName);}catch{gameFolderNote="The saved game folder could not be read. Choose it again.";}
    if(gameFolder.Length>0&&!setupLoadFailed&&GameRuntime.CheckCue(localSetup.Game[game-1]).Length>0)FindDisc();FetchCovers(false);ScanFolders(null);CleanFriendRules();
    foreach(Type type in new[]{typeof(ComboBox),typeof(TextBox),typeof(CheckBox)}){var style=new Style(type);style.Setters.Add(new Setter(System.Windows.Controls.Control.FontSizeProperty,18.0));style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty,36.0));if(type==typeof(TextBox)){style.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty,Ink("#0B1C2A")));style.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty,Ink("#F5EAD0")));style.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty,Ink("#5985A0")));style.Setters.Add(new Setter(System.Windows.Controls.Primitives.TextBoxBase.CaretBrushProperty,Ink("#F5EAD0")));}
      // Dark dropdown: the system ComboBox template ignores Background on current Windows, so the closed box and the list are templated here.
      if(type==typeof(ComboBox)){style.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty,Ink("#F5EAD0")));style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty,null));style.Setters.Add(new Setter(System.Windows.Controls.Control.TemplateProperty,(ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate TargetType='ComboBox' xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'><Grid SnapsToDevicePixels='True'><ToggleButton Focusable='False' ClickMode='Press' IsChecked='{Binding IsDropDownOpen,Mode=TwoWay,RelativeSource={RelativeSource TemplatedParent}}'><ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border x:Name='Box' Background='#0B1C2A' BorderBrush='#5985A0' BorderThickness='1' CornerRadius='4'><Path HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,12,0' Data='M0,0 L10,0 L5,6 Z' Fill='#F5EAD0'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Box' Property='BorderBrush' Value='#5ECDEB'/></Trigger><Trigger Property='IsChecked' Value='True'><Setter TargetName='Box' Property='BorderBrush' Value='#5ECDEB'/></Trigger></ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template></ToggleButton><ContentPresenter IsHitTestVisible='False' Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' Margin='10,4,32,4' VerticalAlignment='Center' HorizontalAlignment='Left'/><Border x:Name='FocusRing' IsHitTestVisible='False' BorderBrush='#5ECDEB' BorderThickness='2' CornerRadius='4' Visibility='Collapsed'/><Popup x:Name='PART_Popup' Placement='Bottom' AllowsTransparency='True' Focusable='False' PopupAnimation='None' IsOpen='{TemplateBinding IsDropDownOpen}'><Border x:Name='Drop' Background='#0B1C2A' BorderBrush='#5985A0' BorderThickness='1' MinWidth='{TemplateBinding ActualWidth}' MaxHeight='{TemplateBinding MaxDropDownHeight}'><ScrollViewer VerticalScrollBarVisibility='Auto' HorizontalScrollBarVisibility='Disabled'><ItemsPresenter KeyboardNavigation.DirectionalNavigation='Contained'/></ScrollViewer></Border></Popup></Grid><ControlTemplate.Triggers><Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='FocusRing' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='HasItems' Value='False'><Setter TargetName='Drop' Property='MinHeight' Value='72'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.55'/></Trigger></ControlTemplate.Triggers></ControlTemplate>")));}
      Resources.Add(type,style);}
    BuildScrollArrowTemplates();var scrollStyle=new Style(typeof(System.Windows.Controls.Primitives.ScrollBar));scrollStyle.Setters.Add(new Setter(FrameworkElement.WidthProperty,22.0));scrollStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty,Ink("#102638")));scrollStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty,Ink("#102638")));
    scrollStyle.Resources[SystemColors.ControlBrushKey]=Ink("#163B54");scrollStyle.Resources[SystemColors.ControlDarkBrushKey]=Ink("#41637D");scrollStyle.Resources[SystemColors.ScrollBarBrushKey]=Ink("#102638");
    scrollStyle.Setters.Add(new EventSetter(FrameworkElement.LoadedEvent,new RoutedEventHandler(ScrollbarLoaded)));Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar),scrollStyle);
    var itemStyle=new Style(typeof(ComboBoxItem));itemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.FontSizeProperty,18.0));itemStyle.Setters.Add(new Setter(FrameworkElement.MinHeightProperty,36.0));itemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.PaddingProperty,new Thickness(10,4,10,4)));
    itemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty,Ink("#F5EAD0")));itemStyle.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty,null));itemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.TemplateProperty,(ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate TargetType='ComboBoxItem' xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'><Border x:Name='Bd' Background='Transparent' BorderBrush='Transparent' BorderThickness='3,0,0,0' Padding='{TemplateBinding Padding}' SnapsToDevicePixels='True'><ContentPresenter VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsSelected' Value='True'><Setter TargetName='Bd' Property='Background' Value='#163B54'/><Setter TargetName='Bd' Property='BorderBrush' Value='#5ECDEB'/></Trigger><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='Bd' Property='Background' Value='#41637D'/></Trigger></ControlTemplate.Triggers></ControlTemplate>")));
    Resources.Add(typeof(ComboBoxItem),itemStyle);
    Deactivated+=delegate{if(keyCapture!=null){keyCapture=null;keyNote="No change.";Render();}};PreviewMouseDown+=delegate(object sender,MouseButtonEventArgs e){if(e.ChangedButton==MouseButton.XButton1){GoBack();e.Handled=true;}else if(e.ChangedButton==MouseButton.XButton2){GoForward();e.Handled=true;}};PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(keyCapture!=null){CaptureKey(e);return;}if((e.Key==Key.System&&Keyboard.Modifiers==ModifierKeys.Alt&&(e.SystemKey==Key.Left||e.SystemKey==Key.Right))||e.Key==Key.BrowserBack||e.Key==Key.BrowserForward){if(e.SystemKey==Key.Left||e.Key==Key.BrowserBack)GoBack();else GoForward();e.Handled=true;return;}if(e.Key==Key.Escape && screen!="Library"){if(OpenDropDown(e.OriginalSource)!=null)return;Navigate("Library");nav[2].Focus();e.Handled=true;}};
    Title="Flutter Deck | 0.4.2 Beta";System.Windows.Automation.AutomationProperties.SetAutomationId(this,"FlutterDeck");var prefs=LauncherPreferences.Load(LauncherPreferences.PathName);Width=prefs.Width;Height=Width*941.0/1672.0;Topmost=prefs.Topmost;game=GameRuntime.IsGame(prefs.Game)?prefs.Game:1;Closing+=delegate{FlushChoices();audio.Dispose();StopScaling();SaveWindowPreferences();StopOverlay();};MinWidth=1200;MinHeight=675;WindowStartupLocation=WindowStartupLocation.CenterScreen;
    WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ResizeMode=ResizeMode.CanMinimize;
    var art=new BitmapImage();using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("Ship")){art.BeginInit();art.CacheOption=BitmapCacheOption.OnLoad;art.StreamSource=s;art.EndInit();art.Freeze();}shipArt=art;
    var artwork=new Image{Source=art,Stretch=Stretch.Fill,IsHitTestVisible=false};RenderOptions.SetBitmapScalingMode(artwork,BitmapScalingMode.LowQuality);Place(artwork,0,0,1672,941);
    var host=new Grid();host.Children.Add(new Viewbox{Stretch=Stretch.Uniform,Child=scene});
    var grip=new System.Windows.Controls.Primitives.Thumb{Width=34,Height=34,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Cursor=Cursors.SizeNWSE,ToolTip="Drag then release to resize Flutter Deck"};
    var gripTemplate=new ControlTemplate(typeof(System.Windows.Controls.Primitives.Thumb));var gripBorder=new FrameworkElementFactory(typeof(Border));gripBorder.SetValue(Border.BackgroundProperty,Ink("#163B54"));gripBorder.SetValue(Border.CornerRadiusProperty,new CornerRadius(8));var gripText=new FrameworkElementFactory(typeof(TextBlock));gripText.SetValue(TextBlock.TextProperty,"\u2921");gripText.SetValue(TextBlock.FontSizeProperty,26.0);gripText.SetValue(TextBlock.ForegroundProperty,Ink("#83E8FF"));gripText.SetValue(TextBlock.HorizontalAlignmentProperty,HorizontalAlignment.Center);gripBorder.AppendChild(gripText);gripTemplate.VisualTree=gripBorder;grip.Template=gripTemplate;
    System.Windows.Automation.AutomationProperties.SetName(grip,"Resize Flutter Deck");
    double pendingWidth=Width,resizeStartWidth=Width;Point resizeOrigin=new Point();var resizeHint=Label("",20);resizeHint.HorizontalAlignment=HorizontalAlignment.Right;resizeHint.VerticalAlignment=VerticalAlignment.Bottom;resizeHint.Margin=new Thickness(0,0,42,0);resizeHint.Background=Ink("#E6102638");resizeHint.Visibility=Visibility.Collapsed;resizeHint.IsHitTestVisible=false;host.Children.Add(resizeHint);
    // A single frozen-art ghost, not a second live control tree. Never resize the real window during drag.
    var ghostScale=new ScaleTransform(1,1);
    var ghostShape=new Grid{Width=1672,Height=941,RenderTransform=ghostScale,IsHitTestVisible=false};
    var resizeGhost=new Image{Source=art,Stretch=Stretch.Fill,Opacity=0.48,IsHitTestVisible=false};
    RenderOptions.SetBitmapScalingMode(resizeGhost,BitmapScalingMode.LowQuality);
    ghostShape.Children.Add(resizeGhost);
    ghostShape.Children.Add(new Border{BorderBrush=Ink("#83E8FF"),BorderThickness=new Thickness(2),CornerRadius=new CornerRadius(14),IsHitTestVisible=false});
    var ghostCanvas=new Canvas{IsHitTestVisible=false};ghostCanvas.Children.Add(ghostShape);
    var ghostOverlay=new Window{Content=ghostCanvas,WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent,ShowInTaskbar=false,ShowActivated=false,Focusable=false,IsHitTestVisible=false,ResizeMode=ResizeMode.NoResize};
    var ghostTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(16)};
    ghostTimer.Tick+=delegate{ghostScale.ScaleX=pendingWidth/1672.0;ghostScale.ScaleY=ghostScale.ScaleX;};
    Action dismissGhost=delegate{ghostTimer.Stop();ghostOverlay.Hide();resizeHint.Visibility=Visibility.Collapsed;};
    Closed+=delegate{ghostTimer.Stop();ghostOverlay.Close();};Deactivated+=delegate{if(grip.IsDragging)grip.CancelDrag();dismissGhost();};
    grip.DragStarted+=delegate{if(WindowState!=WindowState.Normal){grip.CancelDrag();return;}resizeStartWidth=Width;resizeOrigin=Mouse.GetPosition(host);pendingWidth=Width;ghostScale.ScaleX=pendingWidth/1672.0;ghostScale.ScaleY=ghostScale.ScaleX;ghostOverlay.Left=SystemParameters.VirtualScreenLeft;ghostOverlay.Top=SystemParameters.VirtualScreenTop;ghostOverlay.Width=SystemParameters.VirtualScreenWidth;ghostOverlay.Height=SystemParameters.VirtualScreenHeight;Canvas.SetLeft(ghostShape,Left-ghostOverlay.Left);Canvas.SetTop(ghostShape,Top-ghostOverlay.Top);ghostOverlay.Owner=this;ghostOverlay.Show();ghostTimer.Start();resizeHint.Text="Ghost shows proposed size; release to apply";resizeHint.Visibility=Visibility.Visible;};
    grip.DragDelta+=delegate(object sender,System.Windows.Controls.Primitives.DragDeltaEventArgs e){if(WindowState!=WindowState.Normal)return;double ratio=1672.0/941.0;Point pointer=Mouse.GetPosition(host);pendingWidth=ResizeGeometry.WidthFromPointer(resizeStartWidth,pointer.X-resizeOrigin.X,pointer.Y-resizeOrigin.Y,Math.Max(MinWidth,MinHeight*ratio),3000);resizeHint.Text="Release: "+Math.Round(pendingWidth)+" x "+Math.Round(pendingWidth/ratio);};
    grip.DragCompleted+=delegate(object sender,System.Windows.Controls.Primitives.DragCompletedEventArgs e){dismissGhost();if(!e.Canceled&&WindowState==WindowState.Normal){Width=pendingWidth;Height=pendingWidth*941.0/1672.0;}};
    host.Children.Add(grip);Content=host;
    var drag=new Border{Background=Brushes.Transparent};drag.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){if(e.ClickCount==2)WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;else DragMove();};Place(drag,340,165,1000,54);
    // Owner, 2026-11-20: no "FLUTTER DECK" text and no Features or Setup buttons on the top deck. Advanced Settings is in
    // the navigation; the setup wizard opens from Home and from the game setup screen.
    Place(DeckControl("A-",delegate{SetContentScale(contentScale-0.1);},"Decrease content text size","scale-down"),1014,174,44,34);
    Place(DeckControl("A+",delegate{SetContentScale(contentScale+0.1);},"Increase content text size","scale-up"),1066,174,44,34);
    Place(DeckControl("Reset",delegate{SetContentScale(1);},"Reset content text size to 100 percent","reset"),1120,174,64,34);
    Place(DeckControl("Help",delegate{Navigate("Help");},"Help and safe session log","help"),1200,174,76,34);
    Place(DeckControl("-",delegate{WindowState=WindowState.Minimized;},"Minimize","minimize"),1350,174,34,34);
    Place(DeckControl("\u25A1",delegate{WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;},"Maximize or restore","maximize"),1394,174,34,34);
    Place(DeckControl("x",delegate{Close();},"Close Flutter Deck","close"),1438,174,34,34);
    Place(new Border{Background=Ink("#102638"),CornerRadius=new CornerRadius(8),IsHitTestVisible=false},138,281,200,377);
    backButton=Control("\u2190  Back",GoBack,false,"Back to the screen before");forwardButton=Control("Forward  \u2192",GoForward,false,"Forward to the next screen");
    foreach(var hb in new[]{backButton,forwardButton}){hb.FontSize=15;hb.Padding=new Thickness(4,0,4,0);hb.Background=Ink("#FF122B40");hb.BorderBrush=Ink("#41637D");hb.IsEnabled=false;ToolTipService.SetShowOnDisabled(hb,true);}
    System.Windows.Automation.AutomationProperties.SetAutomationId(backButton,"nav-back");System.Windows.Automation.AutomationProperties.SetAutomationId(forwardButton,"nav-forward");
    backButton.ToolTip="Back to the screen before (Alt+Left)";forwardButton.ToolTip="Forward again (Alt+Right)";
    string[] symbols={"\uE80F","\uE740","\uE7FC","\uE896","\uE8D6","\uE713","\uE90F"};
    for(int i=0;i<screens.Length;i++){
      string name=screens[i];string shown=DeckState.NavLabel(name);var b=Control("",delegate{if(name=="Library"){libraryPage=false;libraryAdding=false;libraryNote="";}Navigate(name);},false,shown);b.Tag=name;System.Windows.Automation.AutomationProperties.SetAutomationId(b,"nav-"+name);b.Padding=new Thickness(6,0,2,0);
      var layout=new Grid();layout.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(26)});layout.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
      var icon=Label(symbols[i],24);icon.FontFamily=new FontFamily("Segoe MDL2 Assets");icon.VerticalAlignment=VerticalAlignment.Center;icon.Margin=new Thickness(0);
      var text=Label(shown,17);text.VerticalAlignment=VerticalAlignment.Center;text.Margin=new Thickness(0);text.TextAlignment=TextAlignment.Center;text.TextWrapping=TextWrapping.NoWrap; /* 17 for every name (owner, 2026-11-20): the longest name then has a clear gap to the pill's edge, and all names keep one size */ /* one line for every name, also "Advanced Settings": the icon column is 26 wide for that, and a name that is still too wide (larger Windows text) is drawn smaller, never cut */var fit=new Viewbox{Child=text,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(4,0,8,0)};Grid.SetColumn(fit,1);layout.Children.Add(icon);layout.Children.Add(fit);b.Content=layout;b.HorizontalContentAlignment=HorizontalAlignment.Stretch;
      Place(b,142,287+i*46,192,41);nav.Add(b);
    }
    Place(backButton,142,287+screens.Length*46+4,94,36);Place(forwardButton,240,287+screens.Length*46+4,94,36);
    // Library icons and pages (SPEC-library.md). The icon grid covers the painted cards and the painted game picture. A built-in
    // game's page shows the painted picture again, with a side panel over the two painted cards. Other pages stay on the cover.
    libraryBack.Background=Ink("#FF102638");libraryBack.BorderBrush=Ink("#5ECDEB");libraryBack.BorderThickness=new Thickness(1);libraryBack.CornerRadius=new CornerRadius(14);libraryBack.Padding=new Thickness(10,8,6,8);libraryBack.Visibility=Visibility.Collapsed;
    BuildLibraryView(); // tabs, search, cards, item panel, filter pane (DeckLibrary.cs)
    libraryPageView.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});libraryPageView.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
    var libraryPageBack=LibraryBackButton();libraryPageBack.HorizontalAlignment=HorizontalAlignment.Left;libraryPageBack.Width=210;libraryPageBack.Margin=new Thickness(2,2,2,6);
    var libraryPageScroll=new ScrollViewer{Content=libraryDetailBody,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false,PanningMode=PanningMode.VerticalOnly,CanContentScroll=false,Padding=new Thickness(10,0,12,0)};Grid.SetRow(libraryPageScroll,1);
    libraryPageView.Children.Add(libraryPageBack);libraryPageView.Children.Add(libraryPageScroll);
    var libraryViews=new Grid();libraryViews.Children.Add(libraryGridView);libraryViews.Children.Add(libraryPageView);libraryBack.Child=libraryViews;Place(libraryBack,346,274,1176,360);libraryControls.Remove(libraryBack);
    librarySide.Background=Ink("#FF102638");librarySide.BorderBrush=Ink("#41637D");librarySide.BorderThickness=new Thickness(1);librarySide.CornerRadius=new CornerRadius(14);librarySide.Padding=new Thickness(8);librarySide.Child=librarySideBody;librarySide.Visibility=Visibility.Collapsed;Place(librarySide,350,284,305,343);libraryControls.Remove(librarySide);
    gameTitle=Label("MEGA MAN LEGENDS",24);gameTitle.FontWeight=FontWeights.Bold;gameTitle.Background=Ink("#BF102638");gameTitle.Padding=new Thickness(12,5,12,5);Place(gameTitle,1020,292,470,48);gameOnly.Add(gameTitle);
    var play=Control("",delegate{},true,"Play unavailable: no verified native runtime is connected");play.IsEnabled=false;gamePlay=play;play.Click+=delegate{PlayGame();};Place(play,1014,399,157,157);gameOnly.Add(play);readiness=Label("",16);readiness.Background=Ink("#F0102638");readiness.Padding=new Thickness(8);Place(readiness,680,338,810,58);gameOnly.Add(readiness);
    IconAction("Game setup and readiness",delegate{Navigate("Preflight");},1227);IconAction("Saves",delegate{Navigate("Saves");},1323);IconAction("Files",delegate{OpenGameFiles(game);},1416);
    previousButton=MusicIcon("previous",delegate{audio.Previous();NoteTrack();},"Previous track (requires at least two tracks)");Place(previousButton,626,646,76,50);Dress(previousButton,"previous",PodInk);
    playButton=MusicIcon("play",delegate{musicPausedForGame=false;audio.Toggle();choices.MusicOn=audio.Playing;NoteChoices();},"Play or pause music");Place(playButton,710,646,72,50);Dress(playButton,"play",PodInk);
    nextButton=MusicIcon("next",delegate{audio.Next();NoteTrack();},"Next track (requires at least two tracks)");Place(nextButton,787,646,72,50);Dress(nextButton,"next",PodInk);
    stopButton=MusicIcon("stop",delegate{musicPausedForGame=false;audio.Stop();choices.MusicOn=false;NoteChoices();},"Stop music");Place(stopButton,866,646,74,50);Dress(stopButton,"stop",PodInk);
    var wavePanel=new Grid{Background=Ink("#102638"),ToolTip="Waveform levels from current audio samples; not hardware-output measurement"};
    for(int i=0;i<16;i++){wavePanel.ColumnDefinitions.Add(new ColumnDefinition());var bar=new System.Windows.Shapes.Rectangle{Fill=Ink("#B3EF42"),Height=0,Margin=new Thickness(2,0,2,0),VerticalAlignment=VerticalAlignment.Bottom};Grid.SetColumn(bar,i);wavePanel.Children.Add(bar);waveform.Add(bar);}waveIdle=Label("",14,"#AEC8DB");waveIdle.Margin=new Thickness(0);waveIdle.HorizontalAlignment=HorizontalAlignment.Center;waveIdle.VerticalAlignment=VerticalAlignment.Center;waveIdle.IsHitTestVisible=false;Grid.SetColumnSpan(waveIdle,16);wavePanel.Children.Add(waveIdle);Place(new Border{Background=Ink("#102638"),BorderBrush=Ink("#41637D"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(8,4,8,4),Child=wavePanel},350,646,270,50);
    var volume=(Slider)System.Windows.Markup.XamlReader.Parse(@"<Slider xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Minimum='0' Maximum='100' Value='20' IsMoveToPointEnabled='True' ToolTip='Music volume' Background='#102638'><Slider.Template><ControlTemplate TargetType='Slider'><Border Background='#102638' CornerRadius='12' Padding='10,0'><Grid><Border Height='8' Background='#354F65' CornerRadius='4'/><Track x:Name='PART_Track' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}' Value='{TemplateBinding Value}'><Track.DecreaseRepeatButton><RepeatButton Command='Slider.DecreaseLarge'><RepeatButton.Template><ControlTemplate><Border Height='8' Background='#B3EF42' CornerRadius='4'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.Thumb><Thumb Width='20' Height='26'><Thumb.Template><ControlTemplate><Border Background='#F4D7A3' BorderBrush='#694E38' BorderThickness='1' CornerRadius='10'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='Slider.IncreaseLarge'><RepeatButton.Template><ControlTemplate><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton></Track></Grid></Border></ControlTemplate></Slider.Template></Slider>");
    System.Windows.Automation.AutomationProperties.SetName(volume,"Music volume");volumeSlider=volume;volume.Value=choices.Volume;volume.ValueChanged+=delegate{audio.Volume=volume.Value/100;};Place(new Border{Background=Ink(PodInk),IsHitTestVisible=false},940,646,546,50);Place(volume,940,646,448,48);
    // Owner, 2026-11-21: no shuffle or repeat buttons, and the volume icon works. The backing covers the painted shuffle and repeat
    // glyphs and the painted speaker and bars of the ship art; the slider takes the freed room. The speaker and five bars drawn here
    // in the art's colours are live: the speaker mutes and unmutes (a slash when muted), the bars show the volume and a click on
    // bar N sets it to N x 20%. Moving the slider or picking a bar unmutes.
    volume.ValueChanged+=delegate{if(audio.Muted)audio.ToggleMute();UpdateVolumeIcon();};
    muteButton=VolumeIconButton("music-mute","Mute music");muteButton.Content=SpeakerDrawing();muteButton.Click+=delegate{audio.ToggleMute();UpdateVolumeIcon();NoteChoices();};Place(muteButton,1394,648,38,46);
    var bars=new StackPanel{Orientation=Orientation.Horizontal};
    for(int i=1;i<=5;i++){int level=i;var bar=new System.Windows.Shapes.Rectangle{Width=7,Height=6+5*i,VerticalAlignment=VerticalAlignment.Bottom,RadiusX=1,RadiusY=1};volumeBars.Add(bar);
      var hit=VolumeIconButton("volume-bar-"+i,"Set music volume to "+(20*i)+" percent");hit.Width=10;hit.Height=46;hit.Content=bar;hit.Click+=delegate{volumeSlider.Value=20*level;};bars.Children.Add(hit);}
    Place(bars,1434,648,52,46);UpdateVolumeIcon();
    AddLamps(); // no mute button (developer, 2026-11-18): the volume slider, Pause and Stop do that work
    audio.Changed+=UpdateMusic;audio.Changed+=NoteChoices;
    var audioTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};audioTimer.Tick+=delegate{UpdateMusic();};audioTimer.Start();Closed+=delegate{audioTimer.Stop();};
    // Bundled loops, in playing order. Each is loaded by itself so one missing file does not lose the others.
    foreach(var bundled in new[]{new[]{"FlutterDeck-DawnLaunch.wav","Dawn Launch"},new[]{"FlutterDeck-SkywayRun.wav","Skyway Run"},new[]{"FlutterDeck-OriginalLoop.wav","Skyward Circuit"}}){try{audio.Add(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets",bundled[0]),bundled[1]);}catch{}}
    // The remembered bundled track is chosen again. From here on, a change of a choice is saved.
    bundledTracks=audio.Tracks.Count;for(int i=0;i<bundledTracks;i++)if(audio.Tracks[i].Title==choices.Track&&i!=audio.Index){audio.Select(i,false);break;}
    SyncChoices();choicesBase=DeckChoices.Copy(choices);choicesLoading=false;
    if(audio.Tracks.Count==0)audio.Notice="Bundled loops unavailable. Use Music to add a supported WAV.";
    // Music starts with the launcher (developer, 2026-11-16) unless the user last left it paused or stopped (developer, 2026-11-17).
    Loaded+=delegate{if(choices.MusicOn&&audio.Index>=0&&!audio.Playing&&!GameRunning)audio.Toggle();};
    pictureButton=Control("Picture preset...",ShowPicturePicker);System.Windows.Automation.AutomationProperties.SetAutomationId(pictureButton,"launcher-picture-preset");Place(pictureButton,1225,598,265,40);gameOnly.Add(pictureButton);
    status=Label("NATIVE GAME NOT SET UP  •  OPEN SETUP AND READINESS",12,"#CBEAFE");status.Background=Ink("#D7102638");status.Padding=new Thickness(10,3,10,3);Place(status,680,603,540,28);gameOnly.Add(status);
    panel.Background=Ink("#FF102638");panel.BorderBrush=Ink("#5ECDEB");panel.BorderThickness=new Thickness(1);panel.CornerRadius=new CornerRadius(14);bodyScroll=new ScrollViewer{Content=page,Padding=new Thickness(16,10,10,10),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,PanningMode=PanningMode.VerticalOnly,CanContentScroll=false};bodyScroll.PreviewMouseWheel+=delegate(object sender,MouseWheelEventArgs e){DependencyObject source=e.OriginalSource as DependencyObject;while(source!=null&&source!=bodyScroll){var combo=source as ComboBox;if(combo!=null&&combo.IsDropDownOpen)return;var nested=source as ScrollViewer;if(nested!=null&&nested.ScrollableHeight>0)return;source=source is Visual?VisualTreeHelper.GetParent(source):null;}bodyScroll.ScrollToVerticalOffset(bodyScroll.VerticalOffset-e.Delta);e.Handled=true;};panel.Child=bodyScroll;Place(panel,350,281,1168,420);InitializeNews();InitializeUpdates();InitializeRuntime();Render();
  }
  string newsEndpoint="",newsFolder="",newsState="Bundled notes; publisher not configured.";bool newsBusy=false,newsClosed=false;NewsPhase newsPhase=NewsPhase.Unconfigured;int newsUnread=0;string publishedState="";List<NewsItem> newsItems=new List<NewsItem>();HashSet<string> newsRead=new HashSet<string>();
  void InitializeNews(){
    try{string config=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"publisher-feed.txt");if(System.IO.File.Exists(config)){if(new System.IO.FileInfo(config).Length>2048)throw new Exception();newsEndpoint=System.IO.File.ReadAllText(config).Trim();}newsEndpoint=NewsFeed.Endpoint(NewsFeed.Choose(newsEndpoint)).AbsoluteUri;newsFolder=NewsFeed.Folder(newsEndpoint);string xml=null;try{xml=NewsFeed.LoadCache(newsFolder);if(xml!=null)newsState="Cached notices; not yet refreshed this session.";}catch{newsState="Cached notices could not be read; using bundled notes.";}if(xml==null){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("News"))xml=NewsFeed.ReadBounded(stream);if(newsState.StartsWith("Bundled notes; publisher"))newsState="Bundled notes; not yet refreshed this session.";}newsItems=NewsFeed.Parse(xml);try{newsRead=NewsFeed.LoadRead(newsFolder);}catch{newsState+=" Read markers unavailable; notices shown unread.";}newsPhase=newsEndpoint.Length>0?NewsPhase.Idle:NewsPhase.Unconfigured;}
    catch{newsEndpoint="";newsPhase=NewsPhase.ConfigError;newsState="News configuration unavailable. Publisher setup needs attention.";}
    var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMinutes(15)};timer.Tick+=delegate{RefreshNews();};timer.Start();Closed+=delegate{newsClosed=true;timer.Stop();};Loaded+=delegate{RefreshNews();};
  }
  async void RefreshNews(){if(newsBusy||newsClosed||newsEndpoint.Length==0)return;newsBusy=true;newsPhase=NewsPhase.Checking;newsState="Checking publisher notices...";if(screen=="Home")Render();try{string xml=await System.Threading.Tasks.Task.Run(delegate{return NewsFeed.Fetch(newsEndpoint);});if(newsClosed)return;newsItems=NewsFeed.Parse(xml);newsPhase=NewsPhase.Updated;newsState="Updated "+DateTime.Now.ToString("g")+" (local time).";try{NewsFeed.SaveCache(newsFolder,xml);}catch{newsState+=" Could not save offline copy.";}}
    catch{newsPhase=NewsPhase.Failed;if(!newsClosed)newsState="Refresh failed. Keeping existing notices; they may be out of date.";}
    finally{newsBusy=false;if(!newsClosed){UpdateNewsBadge();if(screen=="Home")Render();}}
  }
  void UpdateNewsBadge(){int unread=0;foreach(var item in newsItems)if(!newsRead.Contains(item.Fingerprint))unread++;newsUnread=unread;var layout=nav[0].Content as Grid;if(layout!=null)((TextBlock)((Viewbox)layout.Children[1]).Child).Text=unread>0?"Home ("+unread+")":"Home";}
  void RenderHome(){
    UpdateNewsBadge();page.Children.Add(Label(newsState,16,"#AEC8DB"));page.Children.Add(Control("Check game setup and readiness",delegate{Navigate("Preflight");}));
    var actions=new Grid();var refresh=Control("Refresh notices",delegate{RefreshNews();});refresh.IsEnabled=newsEndpoint.Length>0&&!newsBusy;refresh.ToolTip=newsEndpoint.Length==0?"An approved publisher feed must be configured before online refresh":"Fetch public notices; no game files or personal settings sent";actions.Children.Add(refresh);
    actions.Children.Add(Control("Mark all read",delegate{try{NewsFeed.SaveRead(newsFolder,newsItems);newsRead=new HashSet<string>();foreach(var item in newsItems)newsRead.Add(item.Fingerprint);Render();}catch{newsState="Read markers could not be saved.";Render();}}));actions.Children.Add(Control("Open library",delegate{Navigate("Library");}));actions.Children.Add(Control("Setup",delegate{OpenSetup();}));LayoutToolbar(actions);page.Children.Add(actions);
    page.Children.Add(Label("Notices refresh while Flutter Deck is open, not push while closed. Earlier copies need this upgrade. Notices never install updates or launch games.",14,"#AEC8DB"));
    if(newsItems.Count==0)page.Children.Add(Label("No notices from the publisher yet.",18));
    foreach(var item in newsItems){var card=new StackPanel();card.Children.Add(Label((newsRead.Contains(item.Fingerprint)?"":"NEW / ")+item.Kind.ToUpperInvariant()+" / "+item.Date.ToLocalTime().ToString("g"),14,"#8BEFFF"));card.Children.Add(Label(item.Title,22));card.Children.Add(Label(item.Body,18));page.Children.Add(new Border{Child=card,Padding=new Thickness(14),Margin=new Thickness(0,5,0,9),Background=Ink("#163B54"),BorderBrush=Ink("#41637D"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8)});}
  }
  // The speaker and volume bars over the painted ones (developer, 2026-11-21).
  Button muteButton;readonly List<System.Windows.Shapes.Rectangle> volumeBars=new List<System.Windows.Shapes.Rectangle>();System.Windows.Shapes.Path speakerSlash;
  Button VolumeIconButton(string id,string name){
    var b=new Button{Cursor=Cursors.Hand,Focusable=true,ToolTip=name,Padding=new Thickness(0),Background=Brushes.Transparent,BorderThickness=new Thickness(0)};
    b.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate TargetType='Button' xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'><Border x:Name='B' Background='Transparent' BorderBrush='Transparent' BorderThickness='1' CornerRadius='4'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='B' Property='BorderBrush' Value='#5ECDEB'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='B' Property='BorderBrush' Value='#F5EAD0'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
    b.FocusVisualStyle=null;System.Windows.Automation.AutomationProperties.SetAutomationId(b,id);System.Windows.Automation.AutomationProperties.SetName(b,name);return b;
  }
  UIElement SpeakerDrawing(){
    var c=new Canvas{Width=30,Height=30};var cream=Ink("#F5EAD0");
    c.Children.Add(new System.Windows.Shapes.Path{Fill=cream,Data=Geometry.Parse("M2,10 L8,10 L15,3 L15,27 L8,20 L2,20 Z")});
    c.Children.Add(new System.Windows.Shapes.Path{Stroke=cream,StrokeThickness=2.5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Data=Geometry.Parse("M19,10 Q23,15 19,20")});
    speakerSlash=new System.Windows.Shapes.Path{Stroke=Ink("#FF7A6B"),StrokeThickness=3,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Data=Geometry.Parse("M3,27 L27,3"),Visibility=Visibility.Collapsed};
    c.Children.Add(speakerSlash);return c;
  }
  void UpdateVolumeIcon(){
    if(muteButton==null||volumeSlider==null)return;
    string name=audio.Muted?"Unmute music":"Mute music";muteButton.ToolTip=name;System.Windows.Automation.AutomationProperties.SetName(muteButton,name);
    if(speakerSlash!=null)speakerSlash.Visibility=audio.Muted?Visibility.Visible:Visibility.Collapsed;
    int lit=audio.Muted?0:(int)Math.Ceiling(volumeSlider.Value/20.0-0.0001);
    for(int i=0;i<volumeBars.Count;i++)volumeBars[i].Fill=Ink(i<lit?"#B3EF42":"#354F65");
  }
  void IconAction(string name,Action action,double x){var b=Control("",action,true,name);b.ToolTip=name;Place(b,x,529,84,84);gameOnly.Add(b);Dress(b,name=="Game setup and readiness"?"settings":name=="Saves"?"save":"folder",null);}
  void UpdateMusic(){
    if(playButton==null)return;if((string)playButton.Tag!=(audio.Playing?"pause":"play")){playButton.Tag=audio.Playing?"pause":"play";System.Windows.Automation.AutomationProperties.SetName(playButton,audio.Playing?"Pause music":"Play music");playButton.ToolTip=audio.Playing?"Pause music":"Play music";}SetSprite(playButton,audio.Playing?"pause":"play");playButton.IsEnabled=audio.Index>=0;stopButton.IsEnabled=audio.Index>=0&&(audio.Playing||audio.Paused);previousButton.IsEnabled=nextButton.IsEnabled=audio.Tracks.Count>1;
    UpdateVolumeIcon();
    if(waveIdle!=null){string idle=DeckState.MusicLabel(MusicNow());if(waveIdle.Text!=idle)waveIdle.Text=idle;waveIdle.Visibility=idle.Length==0?Visibility.Collapsed:Visibility.Visible;}
    var levels=audio.Levels(waveform.Count);for(int i=0;i<levels.Length;i++)waveform[i].Height=Math.Min(38,levels[i]*80);
    string info=audio.Notice+" / "+(audio.Index<0?"No track":audio.Tracks[audio.Index].Title)+" / "+TimeSpan.FromSeconds(audio.Position).ToString(@"mm\:ss")+" / "+TimeSpan.FromSeconds(audio.Duration).ToString(@"mm\:ss");
    info+=(audio.Muted?" / Muted":"")+(audio.Repeat?" / Repeat":"")+(audio.Shuffle?" / Shuffle":"");
    if(status!=null)status.Text=info;if(musicInfo!=null)musicInfo.Text=info;
    if(seekSlider!=null){syncingMusic=true;seekSlider.Maximum=Math.Max(1,audio.Duration);seekSlider.Value=audio.Position;syncingMusic=false;}
    PublishState();
  }
  // One readable statement of every state machine, published for UI Automation only when it changes. Tokens only: see DeckState.cs.
  MusicPhase MusicNow(){return DeckState.Music(audio.Index,audio.Playing,audio.Ready,audio.Paused,audio.Failed);}
  string StateNow(){return DeckState.Snapshot(screen,game,MusicNow(),audio.Tracks.Count,audio.Repeat,audio.Shuffle,audio.Muted,newsPhase,newsUnread,scalingOutput!=null,DeckState.Setup(screen,setupStep,setupLoadFailed),(int)Math.Round(contentScale*100),updatePhase,playPhase);}
  void PublishState(){string value=StateNow();if(value==publishedState)return;publishedState=value;System.Windows.Automation.AutomationProperties.SetItemStatus(this,value);}
  void RenderMusic(){
    musicInfo=Label("",18);page.Children.Add(musicInfo);
    page.Children.Add(Label("Dawn Launch, Skyway Run and Skyward Circuit are 30-second loops made for Flutter Deck. They use no recordings from any game. Music starts with the launcher, at 20% volume the first time, and pauses while a game runs. Flutter Deck remembers whether you left the music on or off, and the volume, repeat, shuffle and track. It is local only; nothing is uploaded.",16));
    var tracks=new ComboBox();foreach(var t in audio.Tracks)tracks.Items.Add(t.Title);tracks.SelectedIndex=audio.Index;tracks.SelectionChanged+=delegate{audio.Select(tracks.SelectedIndex,audio.Playing);NoteTrack();};Row("Playlist","Previous, next and shuffle need at least two tracks. Add local WAVs for more.",tracks);
    seekSlider=new Slider{Minimum=0,Maximum=Math.Max(1,audio.Duration),IsMoveToPointEnabled=true,ToolTip="Seek within current track"};seekSlider.ValueChanged+=delegate{if(!syncingMusic)audio.Seek(seekSlider.Value);};page.Children.Add(seekSlider);
    var actions=new Grid();actions.Children.Add(Control("Add local WAVs",delegate{
      var pick=new Microsoft.Win32.OpenFileDialog{Filter="PCM WAV audio (*.wav)|*.wav",CheckFileExists=true,Multiselect=true};
      if(pick.ShowDialog(this)==true){int rejected=0;foreach(string path in pick.FileNames){try{audio.Add(path,System.IO.Path.GetFileNameWithoutExtension(path));}catch{rejected++;}}Navigate("Music");if(rejected>0)musicInfo.Text="Some files were not added. Use PCM16 mono/stereo WAV, up to 32 MiB and 10 minutes; playlist limits also apply.";}
    }));actions.Children.Add(Control("Stop",delegate{musicPausedForGame=false;audio.Stop();choices.MusicOn=false;NoteChoices();}));LayoutToolbar(actions);page.Children.Add(actions);
    page.Children.Add(Label("Volume, playback, shuffle and repeat are on the lower deck. Repeat may have a device-dependent seam; gapless output is not verified. The visualizer uses samples at the playback position, not a decorative animation.",16));UpdateMusic();
  }
  // ---- Library (SPEC-library.md) ----
  void ScaleLibrary(){libraryGridView.LayoutTransform=new ScaleTransform(UiPreferences.Drawn(contentScale),UiPreferences.Drawn(contentScale));libraryDetailBody.LayoutTransform=new ScaleTransform(UiPreferences.Drawn(contentScale),UiPreferences.Drawn(contentScale));} // the side panel on a game page keeps its size, like the ship labels beside it
  LibraryItem LibraryCurrent(){if(librarySelected>=0&&librarySelected<libraryUser.Count)return libraryUser[librarySelected];foreach(var item in LibraryCore.BuiltIns())if(item.BuiltIn==game)return item;return null;}
  Button LibraryBackButton(){var back=Control("←  Library",delegate{libraryPage=false;libraryAdding=false;libraryNote="";libraryFocus=LibraryCurrent();Render();},false,"Back to the library");back.FontSize=15;back.MinHeight=32;System.Windows.Automation.AutomationProperties.SetAutomationId(back,"library-back");return back;}
  ImageSource LibraryCover(LibraryItem item,bool tall){
    if(item.Found&&item.Cover.Length>0){var own=FoundPicture(item.Cover,240);if(own!=null)return own;} // a picture that lies beside the item in the user's library folder
    if(coversOn&&item.BuiltIn>=1&&item.BuiltIn<=GameRuntime.GameCount&&coverCode[item.BuiltIn-1].Length>0){var kept=CoverImage(coverCode[item.BuiltIn-1]);if(kept!=null)return kept;} // the downloaded cover of the user's own disc image, when there is one
    // No default picture for a built-in game (developer, 2026-11-20).
    // Until then the cards of Mega Man Legends 1 and 2 showed two painted pictures cut from the ship art. A card now shows the user's own picture, the cover
    // downloaded for the user's own disc image, or the plain game symbol. Do not bring a made or bundled game picture back.
    return null;
  }
  Border LibraryPicture(LibraryItem item,double width,double height,double glyphSize){
    var frame=new Border{Width=width,Height=height,CornerRadius=new CornerRadius(8),Background=Ink("#1B3F5C"),ClipToBounds=true,HorizontalAlignment=HorizontalAlignment.Center};
    var cover=LibraryCover(item,height>width);
    if(cover!=null){var picture=new Image{Source=cover,Stretch=cover is BitmapImage?Stretch.Uniform:Stretch.UniformToFill,IsHitTestVisible=false};RenderOptions.SetBitmapScalingMode(picture,BitmapScalingMode.HighQuality);frame.Child=picture;}
    else{var glyph=Label(item.Kind=="movie"?"":item.Kind=="game"?"":"",glyphSize,"#8BEFFF");glyph.FontFamily=new FontFamily("Segoe MDL2 Assets");glyph.Margin=new Thickness(0);glyph.HorizontalAlignment=HorizontalAlignment.Center;glyph.VerticalAlignment=VerticalAlignment.Center;frame.Child=glyph;}
    return frame;
  }
  // Open is for an added movie only. LibraryOpen.cs checks the item and hands the file to Windows. This is not a game Play path.
  void OpenLibraryItem(LibraryItem item){string problem=LibraryOpen.Open(item);if(problem.Length>0)libraryNote=problem;else{bool paused=audio.Playing;if(paused)audio.Toggle();libraryNote=paused?"Opened in another program. The launcher music is paused.":"Opened in another program.";}Render();}
  bool SaveLibrary(List<LibraryItem> next){
    if(libraryLoadFailed){libraryNote="The library file could not be read when Flutter Deck started (it is too large, or from another version). Nothing was changed.";return false;}
    try{LibraryCore.Save(LibraryCore.PathName,libraryUser=next);return true;}catch{libraryNote="The library could not be saved. Nothing was changed.";return false;}
  }
  void RemoveLibraryItem(LibraryItem item){
    if(MessageBox.Show(this,"Remove \""+item.Title+"\" from the library?\n\nThis removes the entry only. No file is deleted.","Flutter Deck",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
    var before=libraryUser;var next=new List<LibraryItem>(libraryUser);next.Remove(item);libraryNote="";
    if(SaveLibrary(next)){librarySelected=-1;libraryPage=false;}else libraryUser=before;
    Render();
  }
  // ---- library folders: the user names a folder once; MediaFolders.cs finds what is in it. Nothing here starts a program. ----
  List<LibraryItem> LibraryAll(){var all=LibraryCore.All(libraryUser);all.AddRange(MediaFolders.Shown(libraryUser,libraryFound));return all;}
  // A picture file of the user, read into memory at a small size. null when it cannot be read.
  ImageSource FoundPicture(string path,int width){
    string key=width+"|"+path;ImageSource kept;if(foundPictures.TryGetValue(key,out kept))return kept;
    ImageSource made=null;
    try{
      if(MediaFolders.IsLocalFullPath(path)&&MediaFolders.IsPicture(path)&&System.IO.File.Exists(path)&&new System.IO.FileInfo(path).Length<=40L*1024*1024){
        var picture=new BitmapImage();
        using(var stream=new System.IO.FileStream(path,System.IO.FileMode.Open,System.IO.FileAccess.Read,System.IO.FileShare.Read)){picture.BeginInit();picture.CacheOption=BitmapCacheOption.OnLoad;picture.DecodePixelWidth=width;picture.StreamSource=stream;picture.EndInit();}
        picture.Freeze();made=picture;
      }
    }catch{made=null;}
    if(foundPictures.Count>600)foundPictures.Clear();
    foundPictures[key]=made;return made;
  }
  // Sets a picture after the screen is drawn, so that many large pictures never hold up the screen.
  void SetPictureLater(Image image,string path,int width){
    ImageSource kept;if(foundPictures.TryGetValue(width+"|"+path,out kept)){image.Source=kept;return;}
    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,new Action(delegate{image.Source=FoundPicture(path,width);}));
  }
  // Which game a disc image holds, and which game a program file is a known build of. They run on the search thread and only read.
  static int DiscGame(string cue){try{if(DiscFinder.HasProgram(cue,GameRuntime.Legends1.DiscProgram))return 1;if(DiscFinder.HasProgram(cue,GameRuntime.Legends2.DiscProgram))return 2;if(DiscFinder.HasProgram(cue,GameRuntime.Tron.DiscProgram))return 3;return 0;}catch{return -1;}}
  static int ProgramGame(string exe){try{if(new System.IO.FileInfo(exe).Length>200L*1024*1024)return 0;string hash=GameRuntime.FileSha256(exe);if(GameRuntime.IsKnownLegends2(hash,GameRuntime.Mml2Hash))return 2;if(GameRuntime.IsKnownTron(hash,GameRuntime.TronHash20))return 3;if(GameRuntime.IsKnownBuild(hash,GameRuntime.KnownHash))return 1;return 0;}catch{return 0;}}
  // Searches the library folders (all of them, or one) on a background thread, one search at a time.
  void ScanFolders(string only){
    if(foldersLoadFailed)return;
    var todo=only==null?new List<string>(libraryFolders):new List<string>{only};if(todo.Count==0)return;
    if(System.Threading.Interlocked.Exchange(ref folderBusy,1)==1){folderPending=true;return;}
    foldersNote="Looking for games in your folder...";sessionLog.Add("library-folders-search");
    System.Threading.ThreadPool.QueueUserWorkItem(delegate{
      var scans=new List<MediaScan>();
      foreach(string folder in todo){
        MediaScan scan;try{scan=MediaFolders.Scan(folder,DiscGame,ProgramGame,true);}catch{scan=new MediaScan{Root=folder,Problem="The folder could not be searched."};}
        // several known Legends 2 programs: the one this Flutter Deck names is preferred
        try{if(scan.Programs[1].Count>1){var best=new List<string>();foreach(string p in scan.Programs[1])if(String.Equals(GameRuntime.FileSha256(p),GameRuntime.Legends2.KnownHash,StringComparison.OrdinalIgnoreCase))best.Add(p);if(best.Count==1){scan.Programs[1].Clear();scan.Programs[1].Add(best[0]);}}}catch{}
        scans.Add(scan);
      }
      try{Dispatcher.BeginInvoke(new Action(delegate{System.Threading.Interlocked.Exchange(ref folderBusy,0);if(updateClosed)return;ApplyScans(scans);if(folderPending){folderPending=false;ScanFolders(null);}}));}catch{System.Threading.Interlocked.Exchange(ref folderBusy,0);}
    });
  }
  void ApplyScans(List<MediaScan> scans){
    var next=libraryFound;string said="";bool complete=true;int used=0;
    var discs=new[]{new List<string>(),new List<string>(),new List<string>()};var programs=new[]{new List<string>(),new List<string>(),new List<string>()};
    foreach(var scan in scans){
      if(!libraryFolders.Exists(f=>MediaFolders.SameFolder(f,scan.Root)))continue; // the folder was dropped while it was searched
      next=MediaFolders.Merge(next,scan);used++;
      said+=(said.Length>0?" ":"")+(scans.Count>1?System.IO.Path.GetFileName(scan.Root)+": ":"")+MediaFolders.Describe(scan,true);
      if(!scan.Complete)complete=false;
      for(int slot=0;slot<GameRuntime.GameCount;slot++){discs[slot].AddRange(scan.Discs[slot]);programs[slot].AddRange(scan.Programs[slot]);}
    }
    libraryFound=next;
    // Settings are touched only when every folder of this search was read completely: "exactly one" is counted over all of them.
    if(complete&&used>0){said+=SetFoundDiscs(discs);said+=SetFoundPrograms(programs);}
    try{MediaFolders.SaveFound(MediaFolders.FoundPathName,next);}catch{said+=" The list of found items could not be saved, so it is searched again next time.";}
    foldersNote=said;sessionLog.Add("library-folders-searched");UpdatePlay();Render();
  }
  // A found disc image is set for its game only when that game's saved image does not work and exactly one image was found.
  // It names the game slot itself and never changes which game is chosen.
  // Not while the user is on the Setup screen: the choices there are not saved yet and must not be replaced.
  string SetFoundDiscs(List<string>[] discs){
    string said="";if(setupLoadFailed||GameRunning||screen=="Setup")return said;
    for(int slot=0;slot<GameRuntime.GameCount;slot++){
      if(discs[slot].Count!=1||GameRuntime.CheckCue(localSetup.Game[slot]).Length==0||GameRuntime.CheckCue(discs[slot][0]).Length>0)continue;
      try{var candidate=LocalSetup.Copy(localSetup);candidate.Game[slot]=discs[slot][0];LocalSetup.Save(LocalSetup.ConfigPath,candidate);localSetup=candidate;said+=" The disc image for "+GameRuntime.Game(slot+1).Name+" was set.";sessionLog.Add("disc-image-set");}
      catch{said+=" The disc image for "+GameRuntime.Game(slot+1).Name+" was found, but the setup could not be saved.";}
    }
    if(said.Length>0)FetchCovers(false);
    return said;
  }
  // A found game program is set only when it is a known build, the only one found for that game, and that game has no working
  // program setting. An acknowledged build is never changed here, and nothing is acknowledged here.
  string SetFoundPrograms(List<string>[] programs){
    string said="";if(runtimeLoadFailed||GameRunning)return said;
    for(int slot=0;slot<GameRuntime.GameCount;slot++){
      if(programs[slot].Count!=1)continue;
      bool missing;try{missing=runtimes[slot].Program.Length==0||!System.IO.File.Exists(runtimes[slot].Program);}catch{missing=false;}
      if(!missing)continue;
      string before=runtimes[slot].Program;
      try{runtimes[slot].Program=programs[slot][0];GameRuntime.ForgetHashes();GameRuntime.Save(GameRuntime.SettingsPath,runtimes[slot],slot+1);said+=" The game program for "+GameRuntime.Game(slot+1).Name+" was set.";sessionLog.Add("game-program-set");}
      catch{runtimes[slot].Program=before;}
    }
    return said;
  }
  void AddLibraryFolder(){
    if(foldersLoadFailed)return;
    string chosen;
    using(var picker=new System.Windows.Forms.FolderBrowserDialog{Description="Choose a folder that holds your games (disc images, and game programs if you keep them there). Flutter Deck only reads it.",ShowNewFolderButton=false}){
      if(picker.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return;
      chosen=picker.SelectedPath;
    }
    string problem=MediaFolders.CheckNewFolder(libraryFolders,chosen);
    if(problem.Length>0){foldersNote=problem;Render();return;}
    var next=new List<string>(libraryFolders);next.Add(chosen);
    try{MediaFolders.SaveFolders(MediaFolders.PathName,next);}catch{foldersNote="The folder could not be saved. Nothing was changed.";Render();return;}
    libraryFolders=MediaFolders.LoadFolders(MediaFolders.PathName);sessionLog.Add("library-folder-added");
    ScanFolders(chosen);Render();
  }
  void StopLibraryFolder(string folder){
    if(foldersLoadFailed)return;
    if(MessageBox.Show(this,"Stop using this folder?\n\n"+folder+"\n\nIts items leave the Library. No file is changed or deleted.","Flutter Deck - Library folder",MessageBoxButton.OKCancel,MessageBoxImage.Question)!=MessageBoxResult.OK)return;
    var next=new List<string>(libraryFolders);next.RemoveAll(f=>MediaFolders.SameFolder(f,folder));
    try{MediaFolders.SaveFolders(MediaFolders.PathName,next);}catch{foldersNote="The change could not be saved. Nothing was changed.";Render();return;}
    libraryFolders=next;libraryFound=MediaFolders.WithoutFolder(libraryFound,folder);
    try{MediaFolders.SaveFound(MediaFolders.FoundPathName,libraryFound);}catch{}
    foldersNote="The folder is no longer used.";sessionLog.Add("library-folder-stopped");Render();
  }
  void RenderLibraryFolders(StackPanel body){
    body.Children.Add(Label("GAME FOLDERS",14,"#8BEFFF"));
    body.Children.Add(Label("Point Flutter Deck at a folder of games. It finds the disc images and game programs in it, and looks again each time it starts. It only reads the folder.",15,"#D8E6F0"));
    if(foldersLoadFailed){body.Children.Add(Label("The saved library folders could not be read (the file is too large, or from another version). Nothing was changed.",15,"#FFCE83"));return;}
    for(int i=0;i<libraryFolders.Count;i++){
      string folder=libraryFolders[i];var line=new DockPanel{Margin=new Thickness(0,2,0,2),LastChildFill=true};
      var stop=Control("Stop using",delegate{StopLibraryFolder(folder);});System.Windows.Automation.AutomationProperties.SetAutomationId(stop,"library-folder-stop-"+i);System.Windows.Automation.AutomationProperties.SetName(stop,"Stop using library folder "+(i+1));DockPanel.SetDock(stop,Dock.Right);line.Children.Add(stop);
      var again=Control("Look again",delegate{ScanFolders(folder);Render();});again.IsEnabled=folderBusy==0;System.Windows.Automation.AutomationProperties.SetAutomationId(again,"library-folder-again-"+i);System.Windows.Automation.AutomationProperties.SetName(again,"Look again in library folder "+(i+1));DockPanel.SetDock(again,Dock.Right);line.Children.Add(again);
      var name=Label(folder,15,"#F5EAD0");name.VerticalAlignment=VerticalAlignment.Center;name.TextTrimming=TextTrimming.CharacterEllipsis;name.TextWrapping=TextWrapping.NoWrap;System.Windows.Automation.AutomationProperties.SetAutomationId(name,"library-folder-"+i);line.Children.Add(name);
      body.Children.Add(line);
    }
    var add=Control(libraryFolders.Count==0?"Add a folder...":"Add another folder...",delegate{AddLibraryFolder();});System.Windows.Automation.AutomationProperties.SetAutomationId(add,"library-folder-add");add.IsEnabled=libraryFolders.Count<MediaFolders.MaxFolders;add.HorizontalAlignment=HorizontalAlignment.Left;body.Children.Add(add);
    if(foldersNote.Length>0){var note=Label(foldersNote,14,"#FFD98A");System.Windows.Automation.AutomationProperties.SetAutomationId(note,"library-folder-note");body.Children.Add(note);}
  }
  void RenderLibraryAdd(){
    var body=libraryDetailBody;RenderLibraryFolders(body);body.Children.Add(Label("OR ADD ONE ITEM BY HAND",14,"#8BEFFF"));
    var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(110)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});for(int r=0;r<3;r++)grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
    var kindLabel=Label("Kind",16);kindLabel.VerticalAlignment=VerticalAlignment.Center;var kind=new ComboBox{MinHeight=34,Margin=new Thickness(0,3,0,3)};foreach(string name in LibraryCore.KindNames)kind.Items.Add(name);kind.SelectedIndex=Math.Max(0,LibraryCore.KindIndex(addKind));System.Windows.Automation.AutomationProperties.SetName(kind,"Kind of item");System.Windows.Automation.AutomationProperties.SetAutomationId(kind,"library-add-kind");
    kind.SelectionChanged+=delegate{if(kind.SelectedIndex<0)return;addKind=LibraryCore.Kinds[kind.SelectedIndex];libraryNote="";Dispatcher.BeginInvoke(new Action(Render));};Grid.SetColumn(kind,1);grid.Children.Add(kindLabel);grid.Children.Add(kind);
    var titleLabel=Label("Title",16);titleLabel.VerticalAlignment=VerticalAlignment.Center;Grid.SetRow(titleLabel,1);var title=new TextBox{Text=addTitle,MaxLength=LibraryCore.MaxTitle,Padding=new Thickness(8,4,8,4),MinHeight=34,Margin=new Thickness(0,3,0,3),VerticalContentAlignment=VerticalAlignment.Center};System.Windows.Automation.AutomationProperties.SetName(title,"Title");System.Windows.Automation.AutomationProperties.SetAutomationId(title,"library-add-title");title.TextChanged+=delegate{addTitle=title.Text;};Grid.SetRow(title,1);Grid.SetColumn(title,1);grid.Children.Add(titleLabel);grid.Children.Add(title);
    if(addKind=="movie"){
      var fileLabel=Label("File",16);fileLabel.VerticalAlignment=VerticalAlignment.Center;Grid.SetRow(fileLabel,2);
      var choose=Control(addPath.Length==0?"Choose movie file":"Change file: "+System.IO.Path.GetFileName(addPath),delegate{
        string pattern="*"+String.Join(";*",LibraryCore.MovieExtensions);var pick=new Microsoft.Win32.OpenFileDialog{Filter="Video files ("+pattern+")|"+pattern,CheckFileExists=true};
        if(pick.ShowDialog(this)==true){addPath=pick.FileName;if(addTitle.Trim().Length==0)addTitle=LibraryCore.TitleFromPath(addPath);libraryNote="";Render();}
      },false,"Choose movie file");choose.Margin=new Thickness(0,3,0,3);System.Windows.Automation.AutomationProperties.SetAutomationId(choose,"library-add-file");Grid.SetRow(choose,2);Grid.SetColumn(choose,1);grid.Children.Add(fileLabel);grid.Children.Add(choose);
    }
    body.Children.Add(grid);
    body.Children.Add(Label(addKind=="movie"?"A movie opens in the Windows program that you use for this kind of file. Flutter Deck keeps only the title and where the file is. It does not copy or change the file.":addKind=="game"?"A game that you add is listed only. Flutter Deck cannot start it yet.":"This kind of item is listed only.",15,"#AEC8DB"));
    if(libraryNote.Length>0){var note=Label(libraryNote,16,"#FFD98A");System.Windows.Automation.AutomationProperties.SetAutomationId(note,"library-note");body.Children.Add(note);}
    var actions=new Grid();
    var confirm=Control("Add",delegate{
      string problem;var item=LibraryCore.Normalize(addKind,addTitle,addPath,out problem);if(item==null){libraryNote=problem;Render();return;}
      var before=libraryUser;var next=new List<LibraryItem>(libraryUser);if(!LibraryCore.Add(next,item,out problem)){libraryNote=problem;Render();return;}
      libraryNote="";if(!SaveLibrary(next)){libraryUser=before;Render();return;}
      libraryAdding=false;librarySelected=libraryUser.IndexOf(item);if(libraryKind.Length>0&&libraryKind!=item.Kind)libraryKind="";Render();
    },false,"Add this item to the library");System.Windows.Automation.AutomationProperties.SetAutomationId(confirm,"library-add-confirm");actions.Children.Add(confirm);
    var cancel=Control("Cancel",delegate{libraryAdding=false;libraryPage=false;libraryNote="";Render();},false,"Cancel adding");System.Windows.Automation.AutomationProperties.SetAutomationId(cancel,"library-add-cancel");actions.Children.Add(cancel);LayoutToolbar(actions);body.Children.Add(actions);
  }
  void Guard(string action){MessageBox.Show(this,action+" is not connected yet. Nothing was changed.","Flutter Deck — Preview",MessageBoxButton.OK,MessageBoxImage.Information);}
  void Navigate(string name){if(!DeckState.IsScreen(name))return;keyCapture=null;history.Visit(screen,name);ShowScreen(name);}
  // Back and Forward (developer, 2026-11-20): the buttons under the navigation, Alt+Left and Alt+Right, the browser keys and the back and forward buttons of a mouse.
  readonly NavHistory history=new NavHistory();Button backButton,forwardButton;
  void GoBack(){if(keyCapture!=null)return;string to=history.Back(screen);if(to!=null)ShowScreen(to);}
  void GoForward(){if(keyCapture!=null)return;string to=history.Forward(screen);if(to!=null)ShowScreen(to);}
  void ShowScreen(string name){keyCapture=null;cardNote="";if(name=="Settings"&&screen=="Library"&&libraryDetailsItem!=null&&libraryDetailsItem.BuiltIn>=1&&libraryDetailsItem.BuiltIn<=runtimes.Length)game=libraryDetailsItem.BuiltIn;   /* owner, 2026-11-21: Settings opened from the Library is for the game whose card is shown, by whichever button */if(screen=="Setup"&&name!="Setup"){setupDraft=LocalSetup.Copy(localSetup);setupStep=0;setupNotice="";}screen=name;Render();bodyScroll.ScrollToTop();}
  void OpenSetup(){setupDraft=LocalSetup.Copy(localSetup);setupNotice="";setupTarget=game-1;setupStep=0;Navigate("Setup");}
  void SaveWindowPreferences(){try{double w=WindowState==WindowState.Normal?Width:RestoreBounds.Width;LauncherPreferences.Save(LauncherPreferences.PathName,new LauncherPreferences{Width=w,Game=game,Topmost=Topmost});}catch{sessionLog.Add("preferences-save-failed");}}
  void ResetLauncherPreferences(){SetContentScale(1);WindowState=WindowState.Normal;Width=1672;Height=941;Topmost=false;SaveWindowPreferences();
    if(audio.Muted)audio.ToggleMute();audio.Repeat=false;audio.Shuffle=false;if(volumeSlider!=null)volumeSlider.Value=DeckAudio.StartVolume*100;choices.MusicOn=true;choices.Track=bundledTracks>0?audio.Tracks[0].Title:"";choices.ScalingEngine=0;choices.ScalingFilter=0;UpdateMusic();FlushChoices();
    sessionLog.Add("preferences-reset");}
  // The user's music and scaling choices (SPEC-choices.md): saved shortly after each change, and at closing.
  // "Music on" is what the user last asked for with Play, Pause or Stop. It is never read from the player, because
  // a pause for a running game, a failed track and closing the launcher all stop the player without the user asking.
  void SyncChoices(){choices.Muted=audio.Muted;choices.Repeat=audio.Repeat;choices.Shuffle=audio.Shuffle;choices.Volume=(int)Math.Round(audio.Volume*100);}
  // The track is remembered only when the user picks one (Next, Previous or the playlist), not when the playlist moves on by itself.
  void NoteTrack(){if(audio.Index>=0&&audio.Index<bundledTracks)choices.Track=audio.Tracks[audio.Index].Title;NoteChoices();}
  void NoteChoices(){
    if(choicesLoading)return;SyncChoices();if(DeckChoices.Format(choices)==DeckChoices.Format(choicesBase))return;
    if(choiceTimer==null){choiceTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(400)};choiceTimer.Tick+=delegate{FlushChoices();};}
    choiceTimer.Stop();choiceTimer.Start(); // a dragged volume slider is saved once, when it comes to rest
  }
  void FlushChoices(){
    if(choiceTimer!=null)choiceTimer.Stop();if(choicesLoading)return;SyncChoices();if(DeckChoices.Format(choices)==DeckChoices.Format(choicesBase))return;
    try{string path=DeckChoices.PathName;DeckChoices.Save(path,DeckChoices.Merge(DeckChoices.Load(path),choicesBase,choices));choicesBase=DeckChoices.Copy(choices);}catch{sessionLog.Add("preferences-save-failed");}
  }
  void StopScaling(){var active=scalingOutput;scalingOutput=null;if(active!=null){active.Close();active.Dispose();}}
  void RenderScaling(){
    // Game picture: always the first thing on this page (developer, 2026-11-19). Contracts 2.19 and 2.20: widescreen for Mega Man Legends. EXPERIMENTAL (developer, the same day: "a toggle and warning that it's experimental"). Off unless the player ticks it. With it on the game program runs a changed game (it draws walls, floors and sky further out).
    // The switch is the choice of Mega Man Legends (game 1) whichever game is selected. It can be ticked also when the present game program has no widescreen: the option is passed only to a program that reports it, and the line below the switch says which case it is.
    page.Children.Add(Label("Game picture",20));
    var wsBox=new CheckBox{Content="Widescreen (about 16:9) \u2013 EXPERIMENTAL",IsChecked=runtimes[0].Widescreen,Foreground=Ink("#F5EAD0"),FontSize=18,Margin=new Thickness(0,4,0,8),VerticalContentAlignment=VerticalAlignment.Center};
    System.Windows.Automation.AutomationProperties.SetName(wsBox,"Widescreen, experimental");System.Windows.Automation.AutomationProperties.SetAutomationId(wsBox,"widescreen");wsBox.IsEnabled=!runtimeLoadFailed&&!GameRunning;
    RoutedEventHandler wsChanged=delegate{runtimes[0].Widescreen=wsBox.IsChecked==true;try{GameRuntime.Save(GameRuntime.SettingsPath,runtimes[0],1);}catch{sessionLog.Add("preferences-save-failed");}Render();};wsBox.Checked+=wsChanged;wsBox.Unchecked+=wsChanged;page.Children.Add(wsBox);
    page.Children.Add(Label("For Mega Man Legends only (not Mega Man Legends 2 or The Misadventures of Tron Bonne). On: the picture is wider, about 16:9, and the game draws walls, floors and sky further out on both sides. Off: the original 4:3 picture with bars. EXPERIMENTAL: this runs a changed game. It may look wrong in places, and saves are not tested with it, so make a backup of your memory card first (the Saves page). Menus, the map and dialogue stay in the middle. It needs the sharper picture. Applies the next time you press Play.",14,"#F0B35A"));
    string wsState=runtimeLoadFailed?"The saved settings need attention first, so this switch cannot be changed now.":GameRunning?"Close the game to change this switch.":game!=1?"Widescreen is for Mega Man Legends only. Select that game in the Library to see whether its game program has widescreen.":playCheck.HasWidescreen?(runtime.OriginalPicture&&playCheck.HasPictureOption?"Your game program has widescreen, but the original picture is chosen, so widescreen does nothing now.":"Your game program has widescreen."):"Your present game program does not have widescreen, so this switch has no effect yet. It needs the widescreen game program (rock_neo_rt_sk.exe or newer).";
    var wsLine=Label(wsState,14,"#AEC8DB");System.Windows.Automation.AutomationProperties.SetAutomationId(wsLine,"widescreen-state");page.Children.Add(wsLine);
    page.Children.Add(Label("Scale another window",20));
    // Order (developer, 2026-11-19): the state and the buttons first, then the options, then the notes.
    var source=new ComboBox();System.Windows.Automation.AutomationProperties.SetName(source,"Window to scale");
    var engine=new ComboBox();engine.Items.Add("DWM compositor");engine.Items.Add("GDI pixel scaling");engine.SelectedIndex=choices.ScalingEngine;
    var filter=new ComboBox();foreach(var f in new[]{"Integer","Nearest","Bilinear","Bicubic"})filter.Items.Add(f);filter.SelectedIndex=choices.ScalingFilter;filter.IsEnabled=engine.SelectedIndex==1;
    engine.SelectionChanged+=delegate{filter.IsEnabled=engine.SelectedIndex==1;if(engine.SelectedIndex>=0){choices.ScalingEngine=engine.SelectedIndex;NoteChoices();}};filter.SelectionChanged+=delegate{if(filter.SelectedIndex>=0){choices.ScalingFilter=filter.SelectedIndex;NoteChoices();}};
    var display=new ComboBox();var displays=System.Windows.Forms.Screen.AllScreens;for(int i=0;i<displays.Length;i++)display.Items.Add("Display "+(i+1));display.SelectedIndex=0;
    var notice=Label(scalingOutput==null?"Stopped. No window is being scaled.":"Output active. Stop before changing options.",20,"#8BEFFF");System.Windows.Automation.AutomationProperties.SetAutomationId(notice,"scaling-state");page.Children.Add(notice);
    Row("Window to scale","A window that is already open. Press Refresh windows to fill the list. Nothing is saved or sent.",source);
    var actions=new Grid();actions.Children.Add(Control("Refresh windows",delegate{source.Items.Clear();foreach(var window in DsiScaling.Native.Windows())source.Items.Add(window);notice.Text=scalingOutput==null?"Choose a window, then Start. No capture has started.":"Output is still active. Stop it before changing source.";}));
    actions.Children.Add(Control("Start scaling",delegate{
      if(scalingOutput!=null){notice.Text="Stop the current output first.";return;}
      var selected=source.SelectedItem as DsiScaling.WindowInfo;if(selected==null){notice.Text="Select a source window first.";return;}
      try{
        DsiScaling.Native.RequireIdentity(selected);if(!DsiScaling.Native.IsWindow(selected.Handle)||DsiScaling.Native.IsIconic(selected.Handle)){notice.Text="Source closed or minimized. Refresh and choose again.";return;}
        var settings=new DsiScaling.Profile{CaptureMode=engine.SelectedIndex==0?"DWM":"GDI",Filter=(string)filter.SelectedItem,FrameRate=30,Fullscreen=false,ClickThrough=false};
        var output=new DsiScaling.ScaleSurface(selected,displays[display.SelectedIndex],settings);scalingOutput=output;
        output.FormClosed+=delegate{if(scalingOutput==output)scalingOutput=null;notice.Text=String.IsNullOrEmpty(output.LastError)?"Scaling stopped.":"Scaling stopped: source unavailable or unsupported. Try another window or engine.";};
        output.Show();if(scalingOutput!=null)notice.Text="Experimental output active. No game compatibility or performance is verified.";
      }catch{StopScaling();notice.Text="Scaling could not start. Source or capture engine may be unavailable.";}
    }));
    actions.Children.Add(Control("Stop scaling",delegate{StopScaling();notice.Text="Scaling stopped.";}));LayoutToolbar(actions);page.Children.Add(actions);
    page.Children.Add(Label("Options",20));
    Row("Engine","DWM uses compositor thumbnails; GDI captures the selected window client.",engine);
    Row("GDI filter","Integer preserves whole pixel blocks; DWM ignores this selection.",filter);
    Row("Output","The display for the scaled picture. Windowed output only. Stop before you change the window.",display);
    page.Children.Add(Label("Good to know",20));
    page.Children.Add(Label("Experimental. No game is launched. This is not DLSS or frame generation; game compatibility and performance are unverified. Output may be black for protected or accelerated sources. Mouse remapping is not implemented. GDI can be CPU-heavy; a blocked capture may need its source to respond before restarting. Close the output or use Stop scaling.",14,"#AEC8DB"));
  }
  void RenderPreflight(){
    page.Children.Add(Label("Launcher: "+Preflight.LauncherRevision,18));
    page.Children.Add(Label("Selected target: "+Kind.Name+" (your assignment, not detected identity). Game edition / region / revision: not detected from your files."+" Native game program: set up below.",18,"#FFCE83"));
    if(setupLoadFailed){page.Children.Add(Label("Saved setup could not be loaded. Existing references have not been overwritten; open Help before trying setup again.",18));page.Children.Add(Control("Open Help",delegate{Navigate("Help");}));return;}
    page.Children.Add(Label("Checked "+DateTime.Now.ToString("g")+" (local time). Metadata only; no file repair, payload hash, game execution or remote check.",14,"#AEC8DB"));
    foreach(var result in new[]{Preflight.Check(localSetup.Game[game-1],false),Preflight.Check(localSetup.Bios[game-1],true)}){var card=new StackPanel();card.Children.Add(Label(result.Title+": "+result.State,22));card.Children.Add(Label(result.Detail,18));card.Children.Add(Label(result.Action,16,"#8BEFFF"));page.Children.Add(new Border{Child=card,Padding=new Thickness(12),Margin=new Thickness(0,5,0,8),Background=Ink("#163B54"),CornerRadius=new CornerRadius(8)});}
    RenderRuntimeSetup();
    page.Children.Add(Label("Display and controls",22));page.Children.Add(Label("Text/window sizing affects this launcher only. Apart from the native game's window size, volume and sound-off choice above, game resolution, fullscreen, controller mapping and camera options are proposals, not applied settings. The independent scaler is experimental; no game compatibility is verified. BIOS selection is this project's setup policy, not proof a native runtime needs it.",18));
    var actions=new Grid();actions.Children.Add(Control("Recheck",delegate{Render();}));actions.Children.Add(Control("Repair references",delegate{OpenSetup();}));actions.Children.Add(Control("Switch game",delegate{game=game%GameRuntime.GameCount+1;UpdatePlay();Render();}));actions.Children.Add(Control("Help",delegate{Navigate("Help");}));LayoutToolbar(actions);page.Children.Add(actions);
  }
  // ---- native game (Mega Man Legends 1): GameRuntime.cs does the work, following the game side's contract ----
  void InitializeRuntime(){try{for(int id=1;id<=GameRuntime.GameCount;id++)runtimes[id-1]=GameRuntime.Load(GameRuntime.SettingsPath,id);}catch{runtimeLoadFailed=true;}for(int id=1;id<=GameRuntime.GameCount;id++)GameRuntime.ReleaseLock(GameRuntime.DataFolderFor(id));}
  void SaveRuntime(){try{GameRuntime.Save(GameRuntime.SettingsPath,runtime,game);}catch{playNote="That choice could not be saved, so it applies to this session only.";sessionLog.Add("preferences-save-failed");}}
  string KeyData {get{return GameRuntime.DataFolderFor(OwnGameRunning?sessionGame:game);}} // the overlay's key editor always edits the running game's keys
  // The gamepad map is a Mega Man Legends file (contract 2.8); the other two games' programs have no --pad-map yet.
  PadMap padDraft;bool padDirty=false,padLoadFailed=false;string padNote="",padDraftFor;
  string PadData {get{return GameRuntime.DataFolderFor(1);}}
  bool OwnGameRunning {get{try{return gameSession!=null&&!gameSession.Process.HasExited;}catch{return false;}}}
  // Also true for a game started by an earlier or second Flutter Deck: the memory card is in use either way.
  bool GameRunning {get{return RunningGame!=0;}}
  // Which of the two games is running (0: none). One game at a time across both.
  int RunningGame {get{if(OwnGameRunning)return sessionGame;for(int id=1;id<=GameRuntime.GameCount;id++)if(GameRuntime.CardInUse(GameRuntime.DataFolderFor(id)))return id;return 0;}}
  string RunningName {get{int g=RunningGame;return GameRuntime.Game(g==0?game:g).Name;}}
  // The only place Play is switched on: the chosen game (Legends 1, or Legends 2 since its contract L2-1), everything in place for that game, known or acknowledged build, and neither game running.
  void UpdatePlay(){
    if(gamePlay==null)return;
    bool running=GameRunning;
    if(runtimeLoadFailed||setupLoadFailed)playCheck=new RuntimeCheck{Problem="Saved settings need attention. Open Help."};
    else{try{playCheck=BuildReadiness()??GameRuntime.Readiness(runtime,localSetup.Game[game-1],GameData,Kind.KnownHash,false,Kind);}catch{playCheck=new RuntimeCheck{Problem="Game setup could not be checked."};}}
    gamePlay.IsEnabled=GameRuntime.IsGame(game)&&playCheck.Ready&&!running;
    if(pictureButton!=null){pictureButton.IsEnabled=OwnGameRunning&&overlay!=null;pictureButton.ToolTip=pictureButton.IsEnabled?null:"Works while a game runs: start the game first. Picture presets need DSI Prism, which is not part of this version yet.";ToolTipService.SetShowOnDisabled(pictureButton,true);}
    string name=running?RunningName+" is running":gamePlay.IsEnabled?"Play "+Kind.Name:"Play unavailable: "+playCheck.Problem;
    System.Windows.Automation.AutomationProperties.SetName(gamePlay,name);gamePlay.ToolTip=name;
    playPhase=running?PlayPhase.Running:!playCheck.Ready?PlayPhase.NeedsSetup:playFailed?PlayPhase.Failed:PlayPhase.Ready;
    if(status!=null)status.Text=running?RunningName.ToUpperInvariant()+" IS RUNNING":playCheck.Ready?"NATIVE GAME READY  \u2022  "+Kind.Name.ToUpperInvariant()+" (USA)":"NATIVE GAME NOT SET UP  \u2022  OPEN SETUP AND READINESS";
  }
  string PlayStatusText(){
    string text=GameRunning?RunningName+" is running in its own window.":playCheck.Ready?"Ready to play. The game opens in its own window.":playCheck.Problem;
    if(playNote.Length>0)text+=" "+playNote.Split('\n')[0];
    var unsaved=GameRuntime.UnsavedFiles(GameData);if(unsaved.Count>0)text+=" A save may not have been written: see setup and readiness.";
    return text;
  }
  bool diagnosticsNoticeOpen;
  void PlayGame(){
    if(!GameRuntime.IsGame(game)||GameRunning)return;
    if(diagnosticsNoticeOpen)return;   // the notice is waiting for its answer: nothing else may start a game meanwhile (the notice is modal: every launcher window waits for the answer)
    if(GameRuntime.DiagnosticsNoticeDue(runtime,playCheck)){   // game contract 2.42: told once, before the first session that records; the answer is the player's choice and is saved with the game's settings
      var noticeFor=runtime;bool? keepOn;diagnosticsNoticeOpen=true;
      try{keepOn=ShowDiagnosticsNotice(false,true);}finally{diagnosticsNoticeOpen=false;}
      if(!GameRuntime.DiagnosticsNoticeApply(noticeFor,keepOn))return;   // closed without an answer: nothing is chosen and no game starts; the notice comes again at the next Play
      SaveRuntime();
      if(!GameRuntime.IsGame(game)||GameRunning||noticeFor!=runtime)return;   // the state changed while the notice was open: the player presses Play again
    }
    {int guideKit=GuideBuilderKit(),guideDue=GameRuntime.GuideDue(runtime,playCheck,GuideBuilderNewer(),guideKit);   // new features that need instructions are explained once, before the first session that has them
      if(guideDue!=0){
        var guideFor=runtime;diagnosticsNoticeOpen=true;
        try{ShowGuide(guideDue,true);}finally{diagnosticsNoticeOpen=false;}
        GameRuntime.GuideShown(guideFor,guideDue,guideKit);if(guideFor==runtime)SaveRuntime();
        if((guideDue&GameRuntime.GuideBuild)!=0)return;   // the player was just told to build the game again: the old program is not started for them; Play again starts it
        if(!GameRuntime.IsGame(game)||GameRunning||guideFor!=runtime)return;   // the state changed while the guide was open: the player presses Play again
      }
    }
    restartBy=DateTime.MinValue; // a manual Play ends any pending Restart request
    try{
      BuildGate();   /* a program that a build made starts only while its marker proves it (DeckBuilder.cs) */
      var session=GameRuntime.Start(runtime,localSetup.Game[game-1],GameData,Kind.KnownHash,Kind,OtherData,FriendSetting());
      gameSession=session;sessionGame=game;sessionCheck=playCheck;sessionThen=OverlayGameSettings.Copy(runtime);playFailed=false;playNote="";sessionLog.Add("game-started");codeSession=NewCodeSession(session,sessionGame);StartOverlay(session);
      if(audio.Playing){musicPausedForGame=true;audio.Toggle();} // the game has its own sound
      session.Process.Exited+=delegate{Dispatcher.BeginInvoke(new Action(delegate{GameEnded(session);}));};
      bool ended;try{ended=session.Process.HasExited;}catch{ended=false;}if(ended)GameEnded(session); // it may have ended before the handler was attached; if that cannot be read, the Exited handler above still reports the real end
    }
    catch(InvalidOperationException e){playFailed=true;playNote=e.Message;GameRuntime.ForgetHashes();EndFriend();}
    catch(Exception){playFailed=true;playNote="The game program could not be started. Check its location on the setup and readiness screen.";EndFriend();}
    Render();
  }
  // overlay: Restart. The overlay has already asked twice. The launcher asks the game to close and, when it has ended,
  // starts it again. Only within 30 seconds, so a game that ignores the request is not restarted later by surprise.
  RuntimeCheck sessionCheck;RuntimeSettings sessionThen; // what the running game was started with, kept for the overlay (it can be restarted from the setup screen's switch)
  DateTime restartBy=DateTime.MinValue;
  void RestartGameNow(GameSession session){
    if(!GameRuntime.RequestClose(session)){playNote="The game window could not be asked to close. Close it yourself.";Render();return;}
    restartBy=DateTime.UtcNow.AddSeconds(30);
  }
  // overlay: brings the launcher to the front on one of its screens. Windows allows this because the click came from this program.
  void ShowLauncherAt(string where){
    try{
      if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal;
      if(screen!="Setup"){if(where=="Setup")OpenSetup();else if(where=="Help")Navigate("Help");else Navigate("Home");} // never leave or reset a setup the player is in the middle of
      Activate();
    }catch{}
  }
  // overlay: starts the in-game overlay for this game. It must never stop the game from running, so a failure here is swallowed.
  CodeSession codeSession; // the cheat codes of the running game and which are on; it lives as long as the game does, also when the overlay is attached again
  // Only a game that was started with the code option has one (a program that reports 'codes-live', and a code file that could be made).
  CodeSession NewCodeSession(GameSession session,int gid){
    var c=sessionCheck;
    if(c==null||!c.HasLiveCodes||session==null||session.Arguments==null||!session.Arguments.Contains(" --codes-watch")||!GameRuntime.IsGame(gid))return null;
    string data=GameRuntime.DataFolderFor(gid);
    return new CodeSession(CodeBook.FilePath(data),CodeBook.Load(CodeBook.BundledListPath(),CodeBook.ListPath(data)));
  }
  void StartOverlay(GameSession session){
    StopOverlay();
    try{
      var host=new OverlayHost{
        StopGame=delegate{restartBy=DateTime.MinValue;if(!GameRuntime.RequestClose(session)){playNote="The game window could not be asked to close. Close it yourself.";Render();}},
        GameKeyCodes=delegate{var codes=new List<int>();try{var map=KeyMap.Load(GameRuntime.KeysPath(GameRuntime.DataFolderFor(sessionGame)));foreach(string button in KeyMap.Buttons)foreach(string key in map.Keys(button))codes.Add(KeyMap.Code(key));}catch{}return codes;}};
      // overlay: the in-overlay key editor edits this launcher's own draft; saving goes through SaveKeyDraft.
      host.KeyDraft=delegate{NeedKeyDraft();return keyDraft;};
      host.EditKey=delegate(string button,string key){NeedKeyDraft();SetKey(button,key);if(screen=="Setup")Render();return keyNote;};
      host.KeysDirty=delegate{return keyDirty;};
      host.SaveKeys=delegate{NeedKeyDraft();SaveKeyDraft();if(screen=="Setup")Render();return keyNote;};
      host.DiscardKeys=delegate{LoadKeyDraft();keyNote="Changes discarded.";if(screen=="Setup")Render();return keyNote;};
      host.DefaultKeys=delegate{keyDraft=KeyMap.Defaults();keyDirty=true;keyCapture=null;keyNote="Default keys shown. Press Save keys to keep them.";if(screen=="Setup")Render();return keyNote;};
      host.KeyWarning=delegate{if(keyDraft==null)return "";string w=keyLoadFailed?"The saved key map could not be read, so the default keys are shown. Saving replaces it; the old file is kept with .bak added. ":"";if(keyDraft.NotUnderstood>0)w+=keyDraft.NotUnderstood+" line(s) in the saved key map were not understood. Saving writes the keys shown here.";return w.Trim();};
      // overlay: controls the launcher can do on its own (the game side has not built pause, fullscreen and the like)
      host.RestartGame=delegate{RestartGameNow(session);};
      host.ShowLauncher=delegate(string where){ShowLauncherAt(where);};
      host.UiScale=delegate{return contentScale;};host.StepUiScale=delegate(int step){SetContentScale(contentScale+0.1*step);};
      // overlay: Game settings. They belong to the game that is running (not the one shown on screen), are kept in runtime.xml like the setup screen's, and Restart starts the game with them.
      int gid=GameRuntime.IsGame(sessionGame)?sessionGame:1; // the inputs are the RUNNING game's: runtimes[gid-1], and the check and copy taken when it started (see OverlayGameSettings.ForSession)
      var gameSource=OverlayGameSettings.ForSession(runtimes[gid-1],sessionThen,sessionCheck??playCheck,GameRuntime.Game(gid),localSetup.Game[gid-1],GameRuntime.DataFolderFor(gid),
        delegate(RuntimeSettings s){GameRuntime.Save(GameRuntime.SettingsPath,s,gid);},delegate{if(screen=="Setup")Render();},delegate{sessionLog.Add("preferences-save-failed");});
      host.GameSettings=delegate{return gameSource.List();};host.SetGameSetting=gameSource.Change;host.GameSettingsPending=gameSource.Pending;
      // overlay: live control of the game (contract 2.34). The overlay hands over one command line; this writes the game's command file (LiveControl.Send: temp file, then rename). Only a program that reports 'live-control' is offered it.
      var liveCheck=sessionCheck??playCheck;bool liveControl=liveCheck!=null&&liveCheck.HasLiveControl;string liveFile=GameRuntime.ControlFilePath(GameRuntime.DataFolderFor(gid));var liveStart=sessionThen??runtimes[gid-1];
      if(liveControl){host.GameCommand=delegate(string line){if(!LiveControl.Send(liveFile,line))sessionLog.Add("live-control-failed");};host.GameVolume=delegate{return liveStart.Volume/100.0;};host.GameMuted=delegate{return liveStart.Mute;};host.GameState=delegate{var ls=LiveControl.ReadState(liveFile);return ls==null?null:new GameLiveState{Paused=ls.Paused,Muted=ls.Mute,Fullscreen=ls.Fullscreen,Unlimited=ls.Unlimited,Volume=ls.Volume,LastShot=ls.LastShot};};}
      // overlay: cheat codes (contract 2.39). The overlay asks for one switch at a time; CodeSession writes the file the game watches. Only for a game started with the code option.
      var featureNames=new List<string>();if(liveControl)featureNames.Add(OverlayLayout.LiveControl);
      var cs=codeSession;
      if(cs!=null){
        featureNames.Add(OverlayLayout.CodesLive);
        host.Codes=delegate{
          var l=new List<GameCode>();
          for(int i=0;i<cs.Count;i++){MemoryCode m=cs.At(i);bool bad=m.Problem.Length>0;
            l.Add(new GameCode{Name=m.Name,On=cs.IsOn(i),Enabled=!bad,NeedsValue=m.NeedsValue,Width=Math.Max(1,m.Width),Value=cs.Value(i),Hint=bad?"This code has a line the game cannot read":cs.MayNotAct(i)?"Part of this code may not act in this game":m.NeedsValue&&cs.Value(i)<0?"Pick a value first":""});}
          return l;};
        host.SetCode=delegate(int index,bool on){string problem=cs.Set(index,on);if(problem!=null)sessionLog.Add("codes-failed");return problem;};
        host.SetCodeValue=delegate(int index,int digit){string problem=cs.SetValue(index,digit);if(problem!=null)sessionLog.Add("codes-failed");return problem;};
      }
      pictureBackend=new DeckPrismBackend(AppDomain.CurrentDomain.BaseDirectory,DeckPaths.Root);
      host.ApplyPicture=delegate(string mode){ApplyPicture(session,mode);};
      host.PictureStatus=delegate{return pictureStatus;};host.PictureSelected=delegate{return pictureSelected;};host.PictureBusy=delegate{return pictureBusy;};
      host.PicturePresentationForeground=delegate{return pictureBackend!=null&&pictureBackend.OwnsForegroundWindow();};host.LauncherForeground=delegate{return IsActive;};
      var overlayOptions=OverlaySettings.Load(OverlaySettings.PathName);
      if(!overlayOptions.Enabled)return; // the developer switched the overlay off in overlay.txt: no pill and no hotkey
      overlay=GameOverlay.Attach(session.Process.Id,host,overlayOptions,OverlaySettings.PathName,featureNames.Count>0?new OverlayFeatures(featureNames):OverlayFeatures.None,null,true);
    }catch{overlay=null;}
  }
  void PictureChanged(){try{if(overlay!=null)overlay.RefreshPictureStatus();}catch{}}
  async void ApplyPicture(GameSession session,string mode){
    if(pictureBusy||overlay==null||pictureBackend==null)return;
    if(mode!="balanced"&&mode!="performance")return;
    var backend=pictureBackend;var activeOverlay=overlay;
    DeckPrismTarget target;
    try{
      if(session.Process.HasExited||activeOverlay.GameWindow==IntPtr.Zero)throw new InvalidOperationException();
      target=new DeckPrismTarget{Hwnd=activeOverlay.GameWindow.ToInt64(),Pid=session.Process.Id,StartedUtcTicks=session.Process.StartTime.ToUniversalTime().Ticks,Title=session.Process.MainWindowTitle,ExePath=DeckPrismBackend.ProcessImagePath(session.Process)};
    }catch{pictureStatus="The game window is not ready. Try again when it is visible.";PictureChanged();return;}
    pictureBusy=true;pictureSelected="";pictureStatus="Preparing the picture preset...";PictureChanged();
    string path=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"presets","prism-learn-"+mode+".preset");
    try{
      DeckPrismResult result=await System.Threading.Tasks.Task.Run(delegate{return backend.Apply(target,path,delegate(string message){Dispatcher.BeginInvoke(new Action(delegate{if(pictureBackend==backend){pictureStatus=message;PictureChanged();}}));});});
      if(pictureBackend!=backend)return;
      pictureStatus=result.Message;pictureSelected=result.Ready?mode:"";
    }catch(Exception e){if(pictureBackend==backend){pictureStatus=e is System.IO.FileNotFoundException?"Picture presets are not available in this version yet.":"That picture preset could not be applied.";pictureSelected="";sessionLog.Add("picture-preset-failed");}}   // the player gets one plain sentence; the reason for a developer is not shown
    finally{if(pictureBackend==backend){pictureBusy=false;PictureChanged();}}
  }
  void StopOverlay(){
    if(overlay!=null){try{overlay.Dispose();}catch{}overlay=null;}
    var backend=pictureBackend;pictureBackend=null;pictureBusy=false;pictureSelected="";pictureStatus="Choose a picture preset.";
    if(backend!=null)System.Threading.Tasks.Task.Run(delegate{try{backend.StopOwned();backend.Dispose();}catch{}});
  }
  void GameEnded(GameSession session){
    if(session==null||session.Reported)return;session.Reported=true;
    bool restartNow=DateTime.UtcNow<restartBy;restartBy=DateTime.MinValue; // a Restart request is used up by the first end of the game, whatever the reason
    int code=-1;string reason;
    try{session.Process.WaitForExit(1500);code=session.Process.ExitCode;string said;lock(session.ErrorText){said=session.ErrorText.ToString();}reason=GameRuntime.ExitReason(code,session.SessionsFolder,session.StartedUtc,said,session.Arguments);}
    catch{reason="The game ended and its result could not be read.";}
    try{session.Process.Dispose();}catch{}
    if(gameSession==session){gameSession=null;codeSession=null;}StopOverlay();
    if(musicPausedForGame){musicPausedForGame=false;if(!audio.Playing&&!updateClosed)audio.Toggle();}
    for(int id=1;id<=GameRuntime.GameCount;id++)GameRuntime.ReleaseLock(GameRuntime.DataFolderFor(id));
    if(reason.Length==0&&restartNow)game=sessionGame; // overlay Restart starts the same game again, whatever card is chosen on screen now
    if(reason.Length==0){playFailed=false;playNote="";sessionLog.Add("game-ended");}else{playFailed=true;playNote=reason;sessionLog.Add("game-ended-with-error");}
    if(!(reason.Length==0&&restartNow))EndFriend(); /* multiplayer ends with its game; an overlay Restart keeps the same session */
    if(updateClosed)return;
    Render();
    if(reason.Length==0&&restartNow){PlayGame();return;} // overlay Restart: the game has ended, start it again
    if(reason.Length>0)MessageBox.Show(this,reason,"Flutter Deck - Mega Man Legends",MessageBoxButton.OK,MessageBoxImage.Warning);
  }
  // ---- cover art (SPEC-covers.md): the cover of the user's own disc image, downloaded by disc code and kept in the data folder ----
  // One lookup runs at a time. A request that arrives during a lookup is not dropped: it runs when that lookup ends, with the images saved then.
  void FetchCovers(bool again){if(!coversOn)return;if(System.Threading.Interlocked.Exchange(ref coverBusy,1)==1){coverPending=true;return;}string[] images={localSetup.Game[0],localSetup.Game[1],localSetup.Game[2]};string folder=CoverArt.Folder;System.Threading.ThreadPool.QueueUserWorkItem(delegate{var codes=new[]{"","",""};string saved="",missing="",failedCode="";try{for(int i=0;i<codes.Length;i++){string code=DiscFinder.DiscCode(images[i]);codes[i]=code;if(code.Length==0)continue;if(again)CoverArt.ForgetMiss(folder,code);if(CoverArt.Cached(folder,code).Length>0)continue;if(CoverArt.Missed(folder,code)){missing=code;continue;}if(!coversOn)break;string result=CoverArt.Fetch(folder,code);if(result==CoverArt.Saved)saved=code;else if(result==CoverArt.Missing)missing=code;else failedCode=code;}}catch{}finally{System.Threading.Interlocked.Exchange(ref coverBusy,0);}Dispatcher.BeginInvoke(new Action(delegate{for(int i=0;i<coverCode.Length;i++)coverCode[i]=codes[i];coverPictures.Clear();coverNote=CoverNote(codes,saved,missing,failedCode);sessionLog.Add("covers-checked");if(coverPending){coverPending=false;FetchCovers(false);}if(screen=="Library"||screen=="Preflight")Render();}));});}
  static string CoverNote(string[] codes,string saved,string missing,string failedCode){string code=codes[0].Length>0?codes[0]:codes[1].Length>0?codes[1]:codes[2];if(code.Length==0)return "No disc code could be read, so no cover was looked up. A cover needs a saved CUE disc image of a PlayStation disc.";if(failedCode.Length>0)return "The cover for "+failedCode+" could not be downloaded. Check the connection, then look again.";if(missing.Length>0)return "The collection has no cover for "+missing+". The Library shows a plain game symbol.";return saved.Length>0?"The cover for "+saved+" was downloaded and is kept on this computer.":"The cover for "+code+" is kept on this computer.";}
  ImageSource CoverImage(string code){ImageSource got;if(coverPictures.TryGetValue(code,out got))return got;got=null;try{string path=CoverArt.Cached(CoverArt.Folder,code);if(path.Length>0){var picture=new BitmapImage();picture.BeginInit();picture.CacheOption=BitmapCacheOption.OnLoad;picture.CreateOptions=BitmapCreateOptions.IgnoreColorProfile;picture.DecodePixelWidth=480;picture.UriSource=new Uri(path);picture.EndInit();picture.Freeze();got=picture;}}catch{got=null;}coverPictures[code]=got;return got;}
  // ---- game folder (SPEC-disc-folder.md): the user names a folder, and the launcher finds the disc image in it ----
  void FindDisc(){discScanGame=game;discScan=DiscFinder.Scan(gameFolder,Kind.DiscProgram);gameFolderNote=DiscFinder.Describe(discScan,Kind.DiscProgram);sessionLog.Add("game-folder-searched");string todo=DiscFinder.Decide(GameRuntime.CheckCue(localSetup.Game[game-1]).Length==0,discScan.Matches,discScan.Partial);string seen=gameFolderNote;if(todo==DiscFinder.Use){if(UseDiscImage(discScan.Matches[0],true))gameFolderNote=seen+" It is now the disc image for the game.";}else if(todo==DiscFinder.Choose)gameFolderNote+=discScan.Matches.Count==1?" The search was not complete, so the image was not set. Use the button below to set it.":" Choose the one to use below.";}
  bool UseDiscImage(string path,bool found){if(setupLoadFailed){gameFolderNote="The saved setup could not be read, so the disc image was not changed. Open Help.";return false;}string problem=GameRuntime.CheckCue(path);if(problem.Length>0){gameFolderNote=problem;return false;}try{var candidate=LocalSetup.Copy(localSetup);candidate.Game[game-1]=path;LocalSetup.Save(LocalSetup.ConfigPath,candidate);localSetup=candidate;setupDraft=LocalSetup.Copy(localSetup);}catch{gameFolderNote="The disc image was found, but the setup could not be saved.";return false;}gameFolderNote=found?"Found your "+Kind.Name+" disc image in the game folder. It is now the disc image for the game.":"This is now the disc image for the game.";playNote="";sessionLog.Add("disc-image-set");FetchCovers(false);return true;}
  void ChooseGameFolder(){
    string chosen;
    using(var picker=new System.Windows.Forms.FolderBrowserDialog{Description="Choose the folder that holds your Mega Man Legends disc image (a .cue file with its .bin file).",ShowNewFolderButton=false}){
      if(gameFolder.Length>0)picker.SelectedPath=gameFolder;
      if(picker.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return;
      chosen=picker.SelectedPath;
    }
    if(!DiscFinder.IsLocalFullPath(chosen)){gameFolderNote="Choose a folder on a drive of this computer.";Render();return;}
    gameFolder=chosen;discScan=null;bool saved=true;
    try{DiscFinder.SaveFolder(DiscFinder.PathName,gameFolder);}catch{saved=false;}
    FindDisc();
    if(!saved)gameFolderNote+=" The folder could not be saved, so Flutter Deck will not remember it.";
    UpdatePlay();Render();
  }
  void SelectRuntimeFile(){
    var picker=new Microsoft.Win32.OpenFileDialog{Title="Select your game program ("+Kind.ProgramHint+")",CheckFileExists=true,Multiselect=false,Filter="Programs (*.exe)|*.exe"};
    if(picker.ShowDialog(this)!=true)return;
    string problem=GameRuntime.CheckProgram(picker.FileName,Kind.ProgramHint);
    if(problem.Length>0){playNote=problem;Render();return;}
    runtime.Program=picker.FileName;runtime.AcceptedHash="";GameRuntime.ForgetHashes();
    playNote="";playFailed=false;SaveRuntime();Render();
  }
  void SelectMemoryCard(){
    var picker=new Microsoft.Win32.OpenFileDialog{Title="Select a memory card to copy into Flutter Deck",CheckFileExists=true,Multiselect=false,Filter=Kind.GmeImport?"Memory cards (*.mcd;*.mcr;*.gme)|*.mcd;*.mcr;*.gme|All files (*.*)|*.*":"Memory cards (*.mcd;*.mcr)|*.mcd;*.mcr|All files (*.*)|*.*"};
    if(picker.ShowDialog(this)!=true)return;
    if(MessageBox.Show(this,"Copy this memory card into Flutter Deck?\n\nThe file you chose is only read and stays where it is. From now on the game saves to Flutter Deck's own copy, so the two will differ.","Flutter Deck - Memory card",MessageBoxButton.OKCancel,MessageBoxImage.Question)!=MessageBoxResult.OK)return;
    try{GameRuntime.AdoptCard(picker.FileName,GameData,Kind.GmeImport);playNote="Memory card copied. The original was not changed.";playFailed=false;}
    catch(System.IO.InvalidDataException e){playNote=e.Message;}
    catch(InvalidOperationException e){playNote=e.Message;}
    catch{playNote="The memory card could not be copied. Nothing was changed.";}
    Render();
  }
  // overlay: one visible switch in the launcher, so a disabled overlay can always be enabled again.
  void RenderOverlaySwitch(){
    var options=OverlaySettings.Load(OverlaySettings.PathName);
    var toggle=new CheckBox{Content="Enable in-game overlay",IsChecked=options.Enabled,Foreground=Brushes.White,Margin=new Thickness(0,8,0,8)};
    System.Windows.Automation.AutomationProperties.SetAutomationId(toggle,"overlay-enabled");
    toggle.Click+=delegate{
      bool enabled=toggle.IsChecked==true;
      try{options.Enabled=enabled;options.Save(OverlaySettings.PathName);}
      catch{toggle.IsChecked=!enabled;options.Enabled=!enabled;MessageBox.Show(this,"The overlay choice could not be saved.","Flutter Deck");return;}
      if(!enabled)StopOverlay();else if(OwnGameRunning)StartOverlay(gameSession);
    };
    page.Children.Add(toggle);
  }
  void RenderRuntimeSetup(){
    RenderOverlaySwitch();
    page.Children.Add(Label("Native game: "+Kind.Name+" (USA)",22));
    if(game==3){var limits3=Label("The Misadventures of Tron Bonne: features coming soon. It was checked by machine only: the title screen, a new game to the first mission, walking and the START menu. Only a small part of the game's code has run so far. Saving, the sound and the one movie on the disc are not tested (expect a black picture or a stop where the movie plays), and the logo screens before the title do not appear. The game sees a digital pad: no analog stick and no vibration. Smooth movement, third-person controls, lock-on and playing with a friend are Legends 1 only. Nobody has played it through Flutter Deck yet. If the game stops, the reason is shown here.",16,"#FFCE83");System.Windows.Automation.AutomationProperties.SetAutomationId(limits3,"game3-limits");page.Children.Add(limits3);}
    if(game==2){var limits=Label("Mega Man Legends 2: features coming soon. Only a small part of the game's code has run so far, saving inside the game is not tested, and the game sees a digital pad unless you switch on Analog stick on the Settings screen (no vibration). Smooth movement, third-person controls and lock-on are Legends 1 only for now. If the game stops, the reason is shown here.",16,"#FFCE83");System.Windows.Automation.AutomationProperties.SetAutomationId(limits,"game2-limits");page.Children.Add(limits);}
    page.Children.Add(Label("Flutter Deck starts your own copy of the game program. It never contains, downloads or copies the game program or your disc's files; it only remembers where they are. The game reads its program straight from your disc image and opens in its own window.",16,"#AEC8DB"));
    if(runtimeLoadFailed){page.Children.Add(Label("Saved game program settings could not be read. They have not been overwritten; open Help.",18));return;}
    string data=GameData;bool running=GameRunning;
    var stateLine=Label(running?RunningName+" is running.":playCheck.Ready?"Ready. Use Play on the Library screen.":"Not ready: "+playCheck.Problem,18,playCheck.Ready||running?"#8BEFFF":"#FFCE83");System.Windows.Automation.AutomationProperties.SetAutomationId(stateLine,"play-status");page.Children.Add(stateLine);
    if(playNote.Length>0)page.Children.Add(Label(playNote,16,"#FFCE83"));
    var chooseProgram=Control("Choose game program",delegate{SelectRuntimeFile();});chooseProgram.IsEnabled=!running;
    Row("Game program",runtime.Program.Length==0?game>=2?"Not selected. This is "+Kind.ProgramHint+", which you build or receive separately.":"Not selected. This is rock_neo_rt_hc2.exe (sharper picture, window presets, Xbox and PlayStation 5 pads) or rock_neo_rt_fd.exe, which you build or receive separately.":runtime.Program+(playCheck.Build.Length>0?"  ("+playCheck.Build+")":""),chooseProgram);
    string cueProblem=GameRuntime.CheckCue(localSetup.Game[game-1]);
    Row("Disc image",cueProblem.Length==0?localSetup.Game[game-1]:cueProblem,Control("Open Setup",delegate{OpenSetup();}));
    string folderNote=discScanGame==game?gameFolderNote:""; // a search made for the other game says nothing about this one
    var chooseFolder=Control("Choose game folder",delegate{ChooseGameFolder();});chooseFolder.IsEnabled=!running;System.Windows.Automation.AutomationProperties.SetAutomationId(chooseFolder,"game-folder-choose");
    Row("Game folder",gameFolder.Length==0?"Not chosen. Choose the folder that holds your disc image, and Flutter Deck finds the "+Kind.Name+" image in it. It only reads the folder.":gameFolder,chooseFolder);
    if(gameFolder.Length>0||folderNote.Length>0){
      var again=Control("Look again",delegate{FindDisc();UpdatePlay();Render();});again.IsEnabled=!running&&gameFolder.Length>0;System.Windows.Automation.AutomationProperties.SetAutomationId(again,"game-folder-search");
      Row("Found in the game folder",folderNote.Length>0?folderNote:setupLoadFailed?"Not searched: the saved setup could not be read. Open Help.":discScanGame!=game?"Not searched for this game yet. Use Look again.":"Not searched since Flutter Deck started: your saved disc image works, so no search was needed.",again);
      if(discScan!=null&&discScanGame==game)for(int i=0;i<discScan.Matches.Count;i++){
        string match=discScan.Matches[i];bool current=String.Equals(match,localSetup.Game[game-1],StringComparison.OrdinalIgnoreCase);
        var use=Control(current?"In use":"Use this image",delegate{UseDiscImage(match,false);UpdatePlay();Render();});use.IsEnabled=!current&&!running;
        System.Windows.Automation.AutomationProperties.SetAutomationId(use,"game-folder-use-"+i);System.Windows.Automation.AutomationProperties.SetName(use,(current?"In use: disc image ":"Use disc image ")+(i+1));
        Row("Disc image "+(i+1)+" in the folder",match,use);
      }
    }
    var coverSwitch=new CheckBox{Content="Download cover art",IsChecked=coversOn,Foreground=Ink("#F5EAD0"),FontSize=18,VerticalContentAlignment=VerticalAlignment.Center};System.Windows.Automation.AutomationProperties.SetAutomationId(coverSwitch,"covers-download");
    RoutedEventHandler coverChanged=delegate{coversOn=coverSwitch.IsChecked==true;try{CoverArt.SaveEnabled(CoverArt.SettingPath,coversOn);}catch{}coverPictures.Clear();if(coversOn){coverNote="";FetchCovers(false);}Render();};coverSwitch.Checked+=coverChanged;coverSwitch.Unchecked+=coverChanged; // Checked and Unchecked, not Click, so that a screen reader's toggle works too
    Row("Cover art",coversOn?"On. Flutter Deck reads the disc code of your disc image (for example SLUS-00603) and downloads the matching cover picture from a public collection on GitHub. Only the disc code is sent. The picture is kept on this computer.":"Off. Nothing is downloaded, and the Library shows a plain game symbol.",coverSwitch);
    if(coversOn){var coversAgain=Control("Look for covers again",delegate{coverNote="";FetchCovers(true);Render();});System.Windows.Automation.AutomationProperties.SetAutomationId(coversAgain,"covers-again");Row("Cover for your disc image",coverNote.Length>0?coverNote:"Looking for the cover...",coversAgain);}
    bool hasCard=GameRuntime.HasCard(data)||GameRuntime.HasImport(data);
    var chooseCard=Control("Copy in a memory card",delegate{SelectMemoryCard();});chooseCard.IsEnabled=!hasCard&&!running;if(hasCard)chooseCard.ToolTip="Flutter Deck already has a card for this game. To use another card, add it as a backup on the Saves screen and restore it.";
    Row("Memory card",hasCard?"Flutter Deck has its own card for this game; saves go there. Back up and restore it on the Saves screen.":game>=2?"None yet. The game creates an empty one the first time you press Play. To use an existing "+Kind.Name+" card instead, copy in a raw card file (.mcd) before the first Play; the original is only read. Never use a card of another game or a .gme save here.":"None yet. The game creates an empty one the first time you press Play, so saving just works. To continue earlier progress instead, copy in a card file (.mcd, or a .gme save) before the first Play; the original is only read.",chooseCard);
    // The game's settings (window size, sound, picture, movement, camera, vibration, teleport) moved to the Settings screen on 2026-11-20 (RenderGameSettings): one list, one place.
    page.Children.Add(Label("The settings of the game (window size, sound, picture, controls, camera, vibration) are on the Settings screen.",18,"#AEC8DB"));
    {var toSettings=Control("Open Settings",delegate{Navigate("Settings");});System.Windows.Automation.AutomationProperties.SetAutomationId(toSettings,"setup-open-settings");page.Children.Add(toSettings);}
    page.Children.Add(Label("Keyboard keys are set at the bottom of this page. A gamepad also works.",14,"#AEC8DB"));
    if(playCheck.NeedsAcknowledgement){
      page.Children.Add(Label("This game program is not the build Flutter Deck was written for (its fingerprint starts "+playCheck.Hash.Substring(0,12)+"). That is normal after the game is rebuilt. Only continue if you built or received this file yourself.",16,"#FFCE83"));
      string seen=playCheck.Hash;
      page.Children.Add(Control("Use this game program build",delegate{if(MessageBox.Show(this,"Use this build of the game program?\n\nFlutter Deck will start exactly this file. If the file changes again you will be asked again.","Flutter Deck - Game program",MessageBoxButton.OKCancel,MessageBoxImage.Question)!=MessageBoxResult.OK)return;runtime.AcceptedHash=seen;SaveRuntime();Render();}));
    }
    var unsaved=GameRuntime.UnsavedFiles(data);
    if(unsaved.Count>0)page.Children.Add(Label("A save may not have been written. The game left "+String.Join(", ",unsaved.ToArray())+" in "+data+". Flutter Deck has not touched them; keep them until you have checked your progress.",16,"#FFCE83"));
    if(OwnGameRunning){var close=Control("Ask the game to close",delegate{if(!GameRuntime.RequestClose(gameSession)){playNote="The game window could not be asked to close. Close it yourself.";Render();}});close.ToolTip="Closes the game as if you closed its window";page.Children.Add(close);}
    RenderKeyEditor();
  }
  // ---- Settings page (developer, 2026-11-20)
  // labeled ... All game settings should be listed alphbethaically not randomly. Fonts, toggles, selectors need to be clearly labeled size and fonts
  // flow not 1 big another small"). One game selector, then every setting of that game in ONE list. The list is sorted by its titles, not by hand.
  // Every row is built by SettingRow, and the page uses only the three sizes below: no other text size may be written in this part.
  // A new setting is one more add(...) call: its place in the list follows from its title.
  const double SetTitleSize=20,SetHelpSize=15,SetControlSize=17;
  int settingRowCount;
  CheckBox Toggle(bool on){var box=new CheckBox{IsChecked=on,Content=on?"On":"Off",Foreground=Ink("#F5EAD0"),FontSize=SetControlSize,VerticalContentAlignment=VerticalAlignment.Center};RoutedEventHandler shown=delegate{box.Content=box.IsChecked==true?"On":"Off";};box.Checked+=shown;box.Unchecked+=shown;return box;}
  FrameworkElement WithNumber(Slider slider){var both=new Grid();both.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});both.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(52)});var number=Label(((int)Math.Round(slider.Value)).ToString(System.Globalization.CultureInfo.InvariantCulture),SetControlSize);number.TextAlignment=TextAlignment.Right;number.VerticalAlignment=VerticalAlignment.Center;number.Margin=new Thickness(0);slider.ValueChanged+=delegate{number.Text=((int)Math.Round(slider.Value)).ToString(System.Globalization.CultureInfo.InvariantCulture);};Grid.SetColumn(number,1);both.Children.Add(slider);both.Children.Add(number);return both;}
  Button SetButton(string label,Action action){var b=Control(label,action);b.FontSize=SetControlSize;return b;} // every button of the Settings screen, in a row or not
  void SettingRow(string title,string help,FrameworkElement control){
    var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(330)});
    var text=new StackPanel{VerticalAlignment=VerticalAlignment.Center};var head=Label(title,SetTitleSize);head.FontWeight=FontWeights.SemiBold;head.Margin=new Thickness(0,0,0,2);System.Windows.Automation.AutomationProperties.SetAutomationId(head,"setting-title-"+settingRowCount);settingRowCount++;text.Children.Add(head);
    if(!String.IsNullOrEmpty(help)){var line=Label(help,SetHelpSize,"#AEC8DB");line.Margin=new Thickness(0);text.Children.Add(line);}
    row.Children.Add(text);
    if(control!=null){
      if((control is CheckBox||control is ComboBox||control is Slider)&&String.IsNullOrEmpty(System.Windows.Automation.AutomationProperties.GetName(control)))System.Windows.Automation.AutomationProperties.SetName(control,title);
      var sized=control as System.Windows.Controls.Control;if(sized!=null)sized.FontSize=SetControlSize;
      control.Width=Double.NaN;control.Margin=new Thickness(24,0,0,0);control.HorizontalAlignment=HorizontalAlignment.Stretch;control.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(control,1);row.Children.Add(control);
    }
    page.Children.Add(new Border{Child=row,Padding=new Thickness(0,10,0,10),BorderBrush=Ink("#2E5068"),BorderThickness=new Thickness(0,0,0,1)});
  }
  void RenderGameSettings(){
    settingRowCount=0;heading.Text="Settings: "+Kind.Name;
    // The game selector. Every game of the launcher is in it (the names come from the game table, so a new game needs no line here).
    var pick=new ComboBox{FontSize=SetTitleSize,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(24,0,0,0)};
    for(int id=1;id<=GameRuntime.GameCount;id++)pick.Items.Add(GameRuntime.Game(id).Name);
    pick.SelectedIndex=game-1; // set before the handler is attached, so drawing the page never switches the game
    System.Windows.Automation.AutomationProperties.SetName(pick,"Game these settings are for");System.Windows.Automation.AutomationProperties.SetAutomationId(pick,"settings-game");
    pick.SelectionChanged+=delegate{if(pick.SelectedIndex>=0&&pick.SelectedIndex+1!=game){game=pick.SelectedIndex+1;LibraryFollowGame();bodyScroll.ScrollToTop();UpdatePlay();Render();}};
    var pickRow=new Grid();pickRow.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});pickRow.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(430)});
    var pickText=new StackPanel{VerticalAlignment=VerticalAlignment.Center};var pickHead=Label("GAME",SetTitleSize,"#8BEFFF");pickHead.FontWeight=FontWeights.Bold;pickHead.Margin=new Thickness(0,0,0,2);pickText.Children.Add(pickHead);
    var pickHelp=Label("Choose the game first. Every setting below belongs to this game only; each game keeps its own settings.",SetHelpSize);pickHelp.Margin=new Thickness(0);pickText.Children.Add(pickHelp);
    pickRow.Children.Add(pickText);Grid.SetColumn(pick,1);pickRow.Children.Add(pick);
    page.Children.Add(new Border{Child=pickRow,Padding=new Thickness(14,12,14,12),Margin=new Thickness(0,4,0,10),Background=Ink("#163B54"),BorderBrush=Ink("#8BEFFF"),BorderThickness=new Thickness(2),CornerRadius=new CornerRadius(8)});
    if(runtimeLoadFailed){page.Children.Add(Label("Saved game settings could not be read. They have not been overwritten; open Help.",SetTitleSize));page.Children.Add(SetButton("Open Help",delegate{Navigate("Help");}));return;}
    bool running=GameRunning;
    Action rollRefresh=null;   // greys the quick roll row while the movement is the original one, as the in-game panel does
    var state=Label(running?RunningName+" is running. Most settings can be changed again when the game is closed.":playCheck.Ready?"Settings of "+Kind.Name+", in alphabetical order. A change is saved at once and applies the next time you press Play.":"This game is not ready to play yet: "+playCheck.Problem+" Only the settings that every game program has are listed below.",SetHelpSize,playCheck.Ready||running?"#8BEFFF":"#FFCE83");
    System.Windows.Automation.AutomationProperties.SetAutomationId(state,"settings-state");page.Children.Add(state);
    {var builtGame=BuildCore.Game(Kind.Id);string builderLine=builtGame==null?"":BuildCore.BuilderLine(BuildCore.ReadMarker(BuildFolderOf(builtGame)),builtGame,BuildToolsNow(builtGame).KitVersion,playCheck.Hash);   // which builder made the game program in use, and which builder this Flutter Deck has
      if(builderLine.Length>0){var builder=Label(builderLine,SetHelpSize,"#AEC8DB");System.Windows.Automation.AutomationProperties.SetAutomationId(builder,"settings-build");page.Children.Add(builder);}}
    if(!playCheck.Ready&&!running)page.Children.Add(SetButton("Open game setup",delegate{Navigate("Preflight");}));
    var rows=new List<KeyValuePair<string,Action<string>>>();
    Action<string,Action<string>> add=delegate(string title,Action<string> build){rows.Add(new KeyValuePair<string,Action<string>>(title,build));};
    // ---- the settings. Each handler is the one the setup screen had; only the row around it is new. ----
    add("Game window size",delegate(string t){
      var size=new ComboBox();
      if(playCheck.HasWindowHeight){
        string[] names={"480p  (640 x 480)","720p  (960 x 720)","1080p  (1440 x 1080)","1440p  (1920 x 1440)","4K, 2160p  (2880 x 2160)"};
        foreach(string name in names)size.Items.Add(name);size.SelectedIndex=Math.Max(0,Array.IndexOf(GameRuntime.WindowHeights,GameRuntime.ClampHeight(runtime.WindowHeight)));
        System.Windows.Automation.AutomationProperties.SetName(size,"Game window size");size.SelectionChanged+=delegate{if(size.SelectedIndex>=0){runtime.WindowHeight=GameRuntime.WindowHeights[size.SelectedIndex];SaveRuntime();}};
        SettingRow(t,"The height of the game picture. A window larger than your screen's usable area is shrunk to fit (on a 1080p screen 1080p and above end near 1324 x 993); full screen (F11 in the game) fits the whole screen. With the original picture the window is the largest whole multiple of 320 x 240 that fits (for 1080p that is 1280 x 960). 4K is the heaviest choice for the graphics card. Applies the next time you press Play.",size);
      }else{
        for(int n=2;n<=6;n++)size.Items.Add(n+"x  ("+(320*n)+" x "+(240*n)+")");size.SelectedIndex=Math.Max(0,Math.Min(4,runtime.Scale-2));
        System.Windows.Automation.AutomationProperties.SetName(size,"Game window size");size.SelectionChanged+=delegate{if(size.SelectedIndex>=0){runtime.Scale=size.SelectedIndex+2;SaveRuntime();}};
        SettingRow(t,playCheck.HasPictureOption?"With the sharper picture this game program opens a window of 960 x 720 for 2x to 5x and 1920 x 1440 for 6x, smaller when the screen is smaller; the picture is fitted to the window. Applies the next time you press Play.":"The game draws at 320 x 240 and is enlarged by a whole number. Applies the next time you press Play.",size);
      }});
    add("Game volume",delegate(string t){
      var volume=new Slider{Minimum=0,Maximum=100,Value=runtime.Volume,TickFrequency=5,IsSnapToTickEnabled=true,IsMoveToPointEnabled=true,VerticalAlignment=VerticalAlignment.Center};
      System.Windows.Automation.AutomationProperties.SetName(volume,"Game volume");volume.ValueChanged+=delegate{int level=(int)Math.Round(volume.Value);if(level!=runtime.Volume){runtime.Volume=level;SaveRuntime();}};
      SettingRow(t,"0 to 100. The level of all game sound together. Applies the next time you press Play. Lower levels have not been listened to yet on the game side.",WithNumber(volume));});
    add("Sound off at start",delegate(string t){
      var mute=Toggle(runtime.Mute);System.Windows.Automation.AutomationProperties.SetName(mute,"Start the game with sound off");
      RoutedEventHandler muteChanged=delegate{runtime.Mute=mute.IsChecked==true;SaveRuntime();};mute.Checked+=muteChanged;mute.Unchecked+=muteChanged;
      SettingRow(t,"On: the game starts with its sound off. Applies the next time you press Play.",mute);});
    if(playCheck.HasPictureOption)add("Original picture",delegate(string t){
      var original=Toggle(runtime.OriginalPicture);
      System.Windows.Automation.AutomationProperties.SetName(original,"Play the original picture");original.IsEnabled=!running;
      RoutedEventHandler originalChanged=delegate{runtime.OriginalPicture=original.IsChecked==true;SaveRuntime();};original.Checked+=originalChanged;original.Unchecked+=originalChanged;
      SettingRow(t,"Off: this game program draws a sharper, smooth picture ("+(playCheck.HasWindowHeight?"3 to 9 times larger, to match the window size":"3 times larger, 960 x 720 on a large enough screen")+"). The first time it starts, the game asks you once whether to keep it. On: the game plays the original picture from the next start, whatever you answered. Applies the next time you press Play.",original);});
    if(playCheck.HasMovementOption)add("Smooth movement",delegate(string t){
      var smooth=Toggle(!runtime.OriginalMovement);
      System.Windows.Automation.AutomationProperties.SetName(smooth,"Smooth movement");smooth.IsEnabled=!running;
      RoutedEventHandler smoothChanged=delegate{runtime.OriginalMovement=smooth.IsChecked!=true;SaveRuntime();if(rollRefresh!=null)rollRefresh();};smooth.Checked+=smoothChanged;smooth.Unchecked+=smoothChanged;
      SettingRow(t,"On: Mega Man eases into a run and eases to a stop (about a tenth of a second each way) instead of starting and stopping at full speed. Top speed, jumping and firing are the same as the original. Off: the original instant start and stop. Applies the next time you press Play.",smooth);});
    if(playCheck.HasRumble){
      add("Controller vibration",delegate(string t){
        var rb=Toggle(!runtime.NoRumble);
        System.Windows.Automation.AutomationProperties.SetName(rb,"Controller vibration");rb.IsEnabled=!running;
        RoutedEventHandler rbChanged=delegate{runtime.NoRumble=rb.IsChecked!=true;SaveRuntime();};rb.Checked+=rbChanged;rb.Unchecked+=rbChanged;
        SettingRow(t,"Hits, buster and weapon ticks, a low-life heartbeat, landings. The original game has no vibration, so this is added by the game program: Xbox and PlayStation 5 pads buzz when Mega Man takes damage, fires, lands or is almost out of life (an older game program only buzzes when he takes damage). Not tried on a real PlayStation 5 pad. Off: no vibration. Applies the next time you press Play.",rb);});
      add("Vibration strength",delegate(string t){
        var rstr=new Slider{Minimum=0,Maximum=100,Value=Math.Max(0,Math.Min(100,runtime.RumbleStrength)),TickFrequency=5,IsSnapToTickEnabled=true,IsMoveToPointEnabled=true,VerticalAlignment=VerticalAlignment.Center};
        System.Windows.Automation.AutomationProperties.SetName(rstr,"Vibration strength");rstr.IsEnabled=!running;
        rstr.ValueChanged+=delegate{int level=(int)Math.Round(rstr.Value);if(level!=runtime.RumbleStrength){runtime.RumbleStrength=level;SaveRuntime();}};
        SettingRow(t,"0 to 100 (100 is the default and the strongest). It scales every vibration effect the game program makes. 0 turns vibration off. Applies the next time you press Play.",WithNumber(rstr));});
    }
    if(playCheck.HasVolumeSplit){
      // contract 2.13: three separate sound levels, applied by the game program at the sound output only
      Action<string,string,Func<int>,Action<int>,string> levelRow=delegate(string name,string id,Func<int> get,Action<int> set,string help){add(name,delegate(string t){
        var sl=new Slider{Minimum=0,Maximum=100,Value=Math.Max(0,Math.Min(100,get())),TickFrequency=5,IsSnapToTickEnabled=true,IsMoveToPointEnabled=true,VerticalAlignment=VerticalAlignment.Center};
        System.Windows.Automation.AutomationProperties.SetName(sl,name);System.Windows.Automation.AutomationProperties.SetAutomationId(sl,"volume-"+id);sl.IsEnabled=!running;
        sl.ValueChanged+=delegate{int level=(int)Math.Round(sl.Value);if(level!=get()){set(level);SaveRuntime();}};
        SettingRow(t,help,WithNumber(sl));});};
      levelRow("Music level","music",delegate{return runtime.MusicVolume;},delegate(int v){runtime.MusicVolume=v;},"0 to 100 (100 is the default). The background music of the game: the sequenced tunes and the streamed tracks. Applies the next time you press Play. The opening movie is not changed by it; use Game volume for that.");
      levelRow("Voice level","voice",delegate{return runtime.VoiceVolume;},delegate(int v){runtime.VoiceVolume=v;},"0 to 100 (100 is the default). The spoken lines. Tested by machine only: none of the recorded test plays reached a spoken line, so nobody has heard this level change a voice yet. Applies the next time you press Play.");
      levelRow("Effects level","effects",delegate{return runtime.EffectsVolume;},delegate(int v){runtime.EffectsVolume=v;},"0 to 100 (100 is the default). Sound effects: shots, hits, doors, the world around you. The echo that follows the sounds goes down with the level in use. Applies the next time you press Play.");
    }
    if(playCheck.HasTeleport&&game==1)add("Teleport line in the pause menu",delegate(string t){
      // contract 2.14 and 2.16: teleport. One switch only (developer, 2026-11-19): the places are chosen in the game, on the Teleport line of the pause menu. This window has no picker.
      var tpBox=Toggle(runtime.Teleport);
      System.Windows.Automation.AutomationProperties.SetName(tpBox,"Allow teleport");System.Windows.Automation.AutomationProperties.SetAutomationId(tpBox,"teleport-allow");tpBox.IsEnabled=!running;
      RoutedEventHandler tpChanged=delegate{runtime.Teleport=tpBox.IsChecked==true;SaveRuntime();};tpBox.Checked+=tpChanged;tpBox.Unchecked+=tpChanged;
      SettingRow(t,"On: the pause menu of the game gets a new line, Teleport, below Close. There you choose a stage and a room. Teleport skips the story, and a saved game may then no longer play through: make a backup of your memory card first (the Saves page). Applies the next time you press Play.",tpBox);});
    if(playCheck.HasPadType&&game==2)add("Analog stick",delegate(string t){
      var ap=Toggle(runtime.AnalogPad);
      System.Windows.Automation.AutomationProperties.SetName(ap,"Analog stick for Mega Man Legends 2");System.Windows.Automation.AutomationProperties.SetAutomationId(ap,"pad-type-analog");ap.IsEnabled=!running;
      RoutedEventHandler apChanged=delegate{runtime.AnalogPad=ap.IsChecked==true;SaveRuntime();};ap.Checked+=apChanged;ap.Unchecked+=apChanged;
      SettingRow(t,"Off: the game sees a digital pad, and the left stick works like the direction buttons. On: the game sees an analog pad and reads the sticks of the first Xbox-type pad itself. The sticks of a PlayStation 5 pad are not passed yet, and there is no vibration. Tested by machine only: nobody has moved the character with a stick yet. Applies the next time you press Play.",ap);});
    if(playCheck.HasFreeCamera&&game==2){
      add("Free camera",delegate(string t){
        var fc=Toggle(runtime.FreeCamera);
        System.Windows.Automation.AutomationProperties.SetName(fc,"Free camera for Mega Man Legends 2");System.Windows.Automation.AutomationProperties.SetAutomationId(fc,"free-camera");fc.IsEnabled=!running;
        RoutedEventHandler fcChanged=delegate{runtime.FreeCamera=fc.IsChecked==true;SaveRuntime();};fc.Checked+=fcChanged;fc.Unchecked+=fcChanged;
        SettingRow(t,"Off by default. On: the right stick turns the camera round Mega Man and tilts it up and down. The left stick runs him the way the camera looks, and he turns to face where he runs. Aim (Square or Triangle) and fire (R2) keep the game's own lock-on, and the camera stays where you put it while you shoot. It needs a pad with a right stick, so it also switches the game to the analog pad. Applies the next time you press Play.",fc);});
      add("Free camera speed",delegate(string t){
        var fcLook=new ComboBox();foreach(int x in GameRuntime.LookSensitivities)fcLook.Items.Add(x+"%"+(x==100?" (normal)":""));
        fcLook.SelectedIndex=Math.Max(0,Array.IndexOf(GameRuntime.LookSensitivities,GameRuntime.ClampLook(runtime.LookSensitivity)));
        System.Windows.Automation.AutomationProperties.SetName(fcLook,"Free camera speed");fcLook.IsEnabled=!running;fcLook.SelectionChanged+=delegate{if(fcLook.SelectedIndex>=0){runtime.LookSensitivity=GameRuntime.LookSensitivities[fcLook.SelectedIndex];SaveRuntime();}};
        SettingRow(t,"How fast the right stick turns the camera (100% is about 140 degrees a second at a full push). Used with the free camera. Applies the next time you press Play.",fcLook);});
      if(playCheck.HasFreeCameraDistance)add("Free camera distance",delegate(string t){
        var fcDist=new ComboBox();foreach(int x in GameRuntime.CameraDistances)fcDist.Items.Add(x+"%"+(x==100?" (normal)":x<100?" (closer)":" (farther)"));
        fcDist.SelectedIndex=Math.Max(0,Array.IndexOf(GameRuntime.CameraDistances,GameRuntime.ClampDistance(runtime.CameraDistance)));
        System.Windows.Automation.AutomationProperties.SetName(fcDist,"Free camera distance");fcDist.IsEnabled=!running;fcDist.SelectionChanged+=delegate{if(fcDist.SelectedIndex>=0){runtime.CameraDistance=GameRuntime.CameraDistances[fcDist.SelectedIndex];SaveRuntime();}};
        SettingRow(t,"How far the free camera sits from Mega Man, as a percent of the game's own distance. Closer is tighter; farther shows more around him but can go through walls in small rooms. Used with the free camera. Applies the next time you press Play.",fcDist);});
      add("Free camera: invert up and down",delegate(string t){
        var fcInv=Toggle(runtime.InvertLookY);
        System.Windows.Automation.AutomationProperties.SetName(fcInv,"Free camera: invert up and down");fcInv.IsEnabled=!running;
        RoutedEventHandler fcInvChanged=delegate{runtime.InvertLookY=fcInv.IsChecked==true;SaveRuntime();};fcInv.Checked+=fcInvChanged;fcInv.Unchecked+=fcInvChanged;
        SettingRow(t,"On: pushing the right stick up makes the camera look down, and down makes it look up. Used with the free camera. Applies the next time you press Play.",fcInv);});
    }
    if(playCheck.HasIntroMovie)add("Opening movie",delegate(string t){
      var im=Toggle(!runtime.NoIntroMovie);
      System.Windows.Automation.AutomationProperties.SetName(im,"Play the opening movie");im.IsEnabled=!running;
      RoutedEventHandler imChanged=delegate{runtime.NoIntroMovie=im.IsChecked!=true;SaveRuntime();};im.Checked+=imChanged;im.Unchecked+=imChanged;
      SettingRow(t,"On: the game program decodes the Capcom logo movie from your disc and plays it, with its sound, before the game starts (about seven seconds). Press Esc, Enter or Space to skip it. Off: the game starts straight away. Applies the next time you press Play. Nobody has watched or heard it in a real play session yet.",im);});
    if(playCheck.HasThirdPerson){
      add("Third-person controls",delegate(string t){
        var tp=Toggle(!runtime.OriginalControls);
        System.Windows.Automation.AutomationProperties.SetName(tp,"Third-person controls");tp.IsEnabled=!running;
        RoutedEventHandler tpChanged=delegate{runtime.OriginalControls=tp.IsChecked!=true;SaveRuntime();if(rollRefresh!=null)rollRefresh();};tp.Checked+=tpChanged;tp.Unchecked+=tpChanged;
        SettingRow(t,"A free camera on the right stick; you move relative to the camera. They switch on the first time you use a connected Xbox or PlayStation 5 pad; until then, or with only a keyboard, the original controls are used. Left stick: run in any direction, relative to the camera. Right stick: look around, horizontal and vertical. The left trigger: aim where the camera looks and strafe. Right trigger: lock-on (see Target lock-on). Right stick click: put the camera back behind Mega Man. Off: the original turn-and-walk controls. Applies the next time you press Play.",tp);});
      if(playCheck.HasLookOptions){
        add("Camera speed",delegate(string t){
          var look=new ComboBox();foreach(int x in GameRuntime.LookSensitivities)look.Items.Add(x+"%"+(x==100?" (normal)":""));
          look.SelectedIndex=Math.Max(0,Array.IndexOf(GameRuntime.LookSensitivities,GameRuntime.ClampLook(runtime.LookSensitivity)));
          System.Windows.Automation.AutomationProperties.SetName(look,"Camera speed");look.IsEnabled=!running;look.SelectionChanged+=delegate{if(look.SelectedIndex>=0){runtime.LookSensitivity=GameRuntime.LookSensitivities[look.SelectedIndex];SaveRuntime();}};
          SettingRow(t,"How fast the right stick turns the camera. Applies the next time you press Play. Used with third-person controls.",look);});
        add("Invert camera up and down",delegate(string t){
          var inv=Toggle(runtime.InvertLookY);
          System.Windows.Automation.AutomationProperties.SetName(inv,"Invert camera up and down");inv.IsEnabled=!running;
          RoutedEventHandler invChanged=delegate{runtime.InvertLookY=inv.IsChecked==true;SaveRuntime();};inv.Checked+=invChanged;inv.Unchecked+=invChanged;
          SettingRow(t,"On: pushing the right stick up makes the camera look down, and down makes it look up. Applies the next time you press Play. Used with third-person controls.",inv);});
      }
      if(playCheck.HasCameraDistance)add("Camera distance",delegate(string t){
        var cdist=new ComboBox();foreach(int x in GameRuntime.CameraDistances)cdist.Items.Add(x+"%"+(x==100?" (normal)":x<100?" (closer)":" (farther)"));
        cdist.SelectedIndex=Math.Max(0,Array.IndexOf(GameRuntime.CameraDistances,GameRuntime.ClampDistance(runtime.CameraDistance)));
        System.Windows.Automation.AutomationProperties.SetName(cdist,"Camera distance");cdist.IsEnabled=!running;cdist.SelectionChanged+=delegate{if(cdist.SelectedIndex>=0){runtime.CameraDistance=GameRuntime.CameraDistances[cdist.SelectedIndex];SaveRuntime();}};
        SettingRow(t,"How far the free camera sits from Mega Man, as a percent of the game's own distance: closer is tighter, farther shows more around him. Applies the next time you press Play. Used with third-person controls.",cdist);});
      if(playCheck.HasCameraWalls)add("Camera wall collision (experimental)",delegate(string t){
        var cwall=Toggle(runtime.CameraWalls);
        System.Windows.Automation.AutomationProperties.SetName(cwall,"Camera wall collision (experimental)");cwall.IsEnabled=!running;
        RoutedEventHandler cwallChanged=delegate{runtime.CameraWalls=cwall.IsChecked==true;SaveRuntime();};cwall.Checked+=cwallChanged;cwall.Unchecked+=cwallChanged;
        SettingRow(t,"Off by default. On: the free camera stops at walls: it moves in close when a wall is in the way, and turns round Mega Man toward a clear view. It may feel like too much in tight corridors. Applies the next time you press Play. Used with third-person controls.",cwall);});
      if(playCheck.HasFarView)add("Draw distance",delegate(string t){
        var fview=new ComboBox();foreach(int x in GameRuntime.FarViews)fview.Items.Add(x==0?"The game's own":x+"%");
        fview.SelectedIndex=Math.Max(0,Array.IndexOf(GameRuntime.FarViews,GameRuntime.ClampFarView(runtime.FarView)));
        System.Windows.Automation.AutomationProperties.SetName(fview,"Draw distance");fview.IsEnabled=!running;fview.SelectionChanged+=delegate{if(fview.SelectedIndex>=0){runtime.FarView=GameRuntime.FarViews[fview.SelectedIndex];SaveRuntime();}};
        SettingRow(t,"How far away the world is drawn, as a percent of the game's own distance. Higher shows more far scenery. Applies the next time you press Play.",fview);});
      if(playCheck.HasLockFlick)add("Flick to switch target",delegate(string t){
        var lf=Toggle(!runtime.NoLockFlick);
        System.Windows.Automation.AutomationProperties.SetName(lf,"Flick to switch target");System.Windows.Automation.AutomationProperties.SetAutomationId(lf,"lock-flick");lf.IsEnabled=!running;
        RoutedEventHandler lfChanged=delegate{runtime.NoLockFlick=lf.IsChecked!=true;SaveRuntime();};lf.Checked+=lfChanged;lf.Unchecked+=lfChanged;
        SettingRow(t,"On (the default): while you hold R2 on a target, flick the right stick left or right to move the lock to the next target on that side. Let the stick go back to the middle between flicks. Off: the lock stays on the first target until you let go of R2. Used with the target lock-on. Applies the next time you press Play.",lf);});
      if(playCheck.HasLockMarker2)add("Legends 2 lock-on marker",delegate(string t){
        var lm=Toggle(runtime.LockMarker2);
        System.Windows.Automation.AutomationProperties.SetName(lm,"Legends 2 lock-on marker");System.Windows.Automation.AutomationProperties.SetAutomationId(lm,"lock-marker-2");lm.IsEnabled=!running;
        RoutedEventHandler lmChanged=delegate{runtime.LockMarker2=lm.IsChecked==true;SaveRuntime();};lm.Checked+=lmChanged;lm.Unchecked+=lmChanged;
        SettingRow(t,"On by default. On: the marker over the locked target looks like the one in Mega Man Legends 2: four yellow triangles pointing in at it and two arcs turning round it. Yellow means a shot can hit; dull orange means the camera is not on it yet; red means it is off the screen. Off: the single arrow of the original game. Used with the target lock-on. Applies the next time you press Play.",lm);});
      if(playCheck.HasZTarget)add("Target lock-on",delegate(string t){
        var zt=Toggle(!runtime.ZTargetOff);
        System.Windows.Automation.AutomationProperties.SetName(zt,"Target lock-on");zt.IsEnabled=!running;
        RoutedEventHandler ztChanged=delegate{runtime.ZTargetOff=zt.IsChecked!=true;SaveRuntime();};zt.Checked+=ztChanged;zt.Unchecked+=ztChanged;
        SettingRow(t,"On: hold the right trigger to lock the nearest enemy in view. The game's own lock-on names the target (the same enemy the original game would lock, from any direction), Mega Man turns to face it and the camera swings in behind him. The left stick then circles the target and walks toward or away from it; flick the right stick sideways to pick another target. Let go to be free again. Off: the game's own lock-on. Not tried on a real pad yet. Applies the next time you press Play. Used with third-person controls.",zt);});
    }
    // Settings that live on another screen still have their line here, so that nothing can be found only somewhere else.
    // The Jet Skates of Legends 1 (contracts 2.31 to 2.33; owner, 2026-11-22): all on by default, only the off choice is passed
    if(playCheck.HasSkateShoot)add("Shoot on the Jet Skates",delegate(string t){
      var ssh=Toggle(!runtime.NoSkateShoot);
      System.Windows.Automation.AutomationProperties.SetName(ssh,"Shoot on the Jet Skates");System.Windows.Automation.AutomationProperties.SetAutomationId(ssh,"skate-shoot");ssh.IsEnabled=!running;
      RoutedEventHandler sshChanged=delegate{runtime.NoSkateShoot=ssh.IsChecked!=true;SaveRuntime();};ssh.Checked+=sshChanged;ssh.Unchecked+=sshChanged;
      SettingRow(t,"On (the default): the buster and the special weapon fire while you skate. Off: the skates work as in the original game. Applies the next time you press Play.",ssh);});
    if(playCheck.HasSkateLock)add("Lock on while skating",delegate(string t){
      var slk=Toggle(!runtime.NoSkateLock);
      System.Windows.Automation.AutomationProperties.SetName(slk,"Lock on while skating");System.Windows.Automation.AutomationProperties.SetAutomationId(slk,"skate-lock");slk.IsEnabled=!running;
      RoutedEventHandler slkChanged=delegate{runtime.NoSkateLock=slk.IsChecked!=true;SaveRuntime();};slk.Checked+=slkChanged;slk.Unchecked+=slkChanged;
      SettingRow(t,"On (the default): hold R2 to lock an enemy while you skate. The camera follows it and you keep steering the skates. Needs the third-person controls and the target lock-on. Applies the next time you press Play.",slk);});
    if(playCheck.HasSkateToggle)add("Hold Circle to start skates, press Circle to stop",delegate(string t){
      var stg=Toggle(!runtime.NoSkateToggle);
      System.Windows.Automation.AutomationProperties.SetName(stg,"Hold Circle to start skates, press Circle to stop");System.Windows.Automation.AutomationProperties.SetAutomationId(stg,"skate-toggle");stg.IsEnabled=!running;
      RoutedEventHandler stgChanged=delegate{runtime.NoSkateToggle=stg.IsChecked!=true;SaveRuntime();};stg.Checked+=stgChanged;stg.Unchecked+=stgChanged;
      SettingRow(t,"On (the default): hold Circle to put the skates on, then let go and keep skating. Press Circle again to take them off. Off: the skates last only while Circle is held. "+(playCheck.HasRoll?"While quick roll is on, a Circle hold starts the skates only after the walk switch is pressed. ":"")+"Needs smooth movement; with the original movement nothing changes. Applies the next time you press Play.",stg);});
    if(playCheck.HasRoll)add("Quick roll: tap Circle",delegate(string t){
      var qr=Toggle(runtime.Roll);
      System.Windows.Automation.AutomationProperties.SetName(qr,"Quick roll: tap Circle");System.Windows.Automation.AutomationProperties.SetAutomationId(qr,"roll");rollRefresh=delegate{qr.IsEnabled=!running&&OverlayGameSettings.RollUsable(runtime,playCheck);};rollRefresh();
      RoutedEventHandler qrChanged=delegate{runtime.Roll=qr.IsChecked==true;SaveRuntime();};qr.Checked+=qrChanged;qr.Unchecked+=qrChanged;
      SettingRow(t,"Off by default. On: a quick tap of Circle rolls Mega Man out of the way, and a hit that lands in the first moment of the roll does no harm. While it is on, holding Circle does NOT put the Jet Skates on until you press the walk switch: on a gamepad the left stick press (you can move it on the Gamepad buttons screen). The walk switch is off every time the game starts. A press of Circle while Mega Man is walking rolls at once, so stop first to talk or to use a door or a car. The keyboard has no walk switch, so with a keyboard alone the skates cannot be started while quick roll is on. Needs smooth movement; with the original movement nothing changes. Applies the next time you press Play.",qr);});
    if(playCheck.HasShotAim)add("Locked shots aim at the target",delegate(string t){
      var sa=Toggle(!runtime.NoShotAim);
      System.Windows.Automation.AutomationProperties.SetName(sa,"Locked shots aim at the target");System.Windows.Automation.AutomationProperties.SetAutomationId(sa,"shot-aim");sa.IsEnabled=!running;
      RoutedEventHandler saChanged=delegate{runtime.NoShotAim=sa.IsChecked!=true;SaveRuntime();};sa.Checked+=saChanged;sa.Unchecked+=saChanged;
      SettingRow(t,"On (the default): a shot you fire while locked on starts out pointed straight at its target, so shots at a target high above you can hit. Off: the shot flies level and turns toward the target slowly, as in the original game. It matters only for shots fired while locked on a target, which needs the third-person controls and the target lock-on. Applies the next time you press Play.",sa);});
    if(playCheck.HasLockHealth)add("Target switch skips people and signs",delegate(string t){
      var lkh=Toggle(!runtime.NoLockHealth);
      System.Windows.Automation.AutomationProperties.SetName(lkh,"Target switch skips people and signs");System.Windows.Automation.AutomationProperties.SetAutomationId(lkh,"lock-health");lkh.IsEnabled=!running;
      RoutedEventHandler lkhChanged=delegate{runtime.NoLockHealth=lkh.IsChecked!=true;SaveRuntime();};lkh.Checked+=lkhChanged;lkh.Unchecked+=lkhChanged;
      SettingRow(t,"On (the default): flicking the right stick to the next target takes only things that have health, so most town people, signs and destroyed enemies are left out. The first target is always the game's own choice, so a person can still be locked now and then. Off: the older fixed list is used. Needs the third-person controls, the target lock-on, flick to switch target and a gamepad. Applies the next time you press Play.",lkh);});
    if(playCheck.HasDiagnostics)add("Diagnostics recording",delegate(string t){
      var dgn=Toggle(!runtime.NoDiagnostics);
      System.Windows.Automation.AutomationProperties.SetName(dgn,"Diagnostics recording");System.Windows.Automation.AutomationProperties.SetAutomationId(dgn,"diagnostics");dgn.IsEnabled=!running;
      RoutedEventHandler dgnChanged=delegate{runtime.NoDiagnostics=dgn.IsChecked!=true;runtime.DiagnosticsNoticeSeen=true;SaveRuntime();};   /* using the switch is a choice: the first-Play notice is not shown after it */dgn.Checked+=dgnChanged;dgn.Unchecked+=dgnChanged;
      // The big notice can be read again from this row. Its answer is the same choice as the switch.
      var dgnRead=SetButton("Read the notice again",delegate{if(GameRunning||diagnosticsNoticeOpen)return;var readFor=runtime;bool? again;diagnosticsNoticeOpen=true;try{again=ShowDiagnosticsNotice(true,!readFor.NoDiagnostics);}finally{diagnosticsNoticeOpen=false;}if(readFor!=runtime||GameRunning||!GameRuntime.DiagnosticsNoticeApply(readFor,again))return;SaveRuntime();if(dgn.IsLoaded)dgn.IsChecked=again==true;else Render();});   // Esc or Alt+F4: no answer, nothing changes. The page may have been drawn again meanwhile: then it is drawn once more from the saved choice
      System.Windows.Automation.AutomationProperties.SetAutomationId(dgnRead,"diagnostics-read");dgnRead.IsEnabled=!running;dgnRead.Margin=new Thickness(0,8,0,0);dgnRead.HorizontalAlignment=HorizontalAlignment.Left;
      var dgnBoth=new StackPanel();dgnBoth.Children.Add(dgn);dgnBoth.Children.Add(dgnRead);
      SettingRow(t,"On (the default): while you play, the game keeps a record of the last 5 minutes on this PC, so a fault can be looked at afterwards. The record holds the game's memory, its log and the buttons you press. Press F12 in the game (the screenshot key) to keep the record around that moment. The record is written to the game's sessions folder on this PC and is never sent anywhere. It can use up to about 4 GB of disk space; the oldest records are removed first. Off: no new record is written; records already written stay on this PC until you delete them. Applies the next time you press Play.",dgnBoth);});
    {int guideTopics=GameRuntime.GuideTopicsFor(playCheck,GuideBuilderNewer());
      if(guideTopics!=0)add("How to use the new features",delegate(string t){
        var open=SetButton("Open the guide",delegate{if(diagnosticsNoticeOpen||GameRunning)return;var guideFor=runtime;int guideKit=GuideBuilderKit();diagnosticsNoticeOpen=true;try{ShowGuide(guideTopics,false);}finally{diagnosticsNoticeOpen=false;}if(guideFor==runtime){GameRuntime.GuideShown(guideFor,guideTopics,guideKit);SaveRuntime();}});   // a notice is modal and holds the Play guard, so it is not opened while a game runs (a Restart from the in-game menu would be lost)
        System.Windows.Automation.AutomationProperties.SetAutomationId(open,"feature-guide-open");open.IsEnabled=!running;
        SettingRow(t,"Short steps for the new features of this game that work in a way you may not expect: the quick roll with the walk switch and the Jet Skates, cheat codes, Teleport, lock-on, and when to build the game again. Only the features your game program has are listed. The guide is also shown once before Play.",open);});}
    if(playCheck.HasPadMap)add("Gamepad buttons",delegate(string t){SettingRow(t,"Choose what each button of an Xbox or PlayStation 5 pad does in the game. Button changes work with the original controls and with the third-person controls. The same screen has the stick dead zone of the third-person controls. Applies the next time the game starts.",SetButton("Edit gamepad buttons",delegate{if(!padDirty)padDraft=null;Navigate("Gamepad");}));});
    if(!playCheck.HasPadMap)add("Gamepad buttons",delegate(string t){SettingRow(t,game==1?"Xbox and PlayStation 5 pads work. Changing what each pad button does needs the Mega Man Legends game program to be set up (see Game setup and readiness); then the editor opens from here.":"Xbox and PlayStation 5 pads work with their usual buttons. Changing what each pad button does: coming soon for this game.",null);});
    add("Keyboard keys",delegate(string t){SettingRow(t,"Which keyboard key stands for each button of the game's pad. The list of keys is at the bottom of the game setup screen. A gamepad also works.",SetButton("Edit keys",delegate{Navigate("Preflight");}));});
    add("Memory card",delegate(string t){SettingRow(t,"The saves of this game. Back up, restore or replace the memory card on the Saves screen.",SetButton("Open Saves",delegate{Navigate("Saves");}));});
    if(game==1)add("Widescreen (experimental)",delegate(string t){SettingRow(t,"Now: "+(runtimes[0].Widescreen?"On":"Off")+". A wider picture, about 16:9, for Mega Man Legends only. The switch and its full explanation are on the Scaling screen, in the part Game picture.",SetButton("Open Scaling",delegate{Navigate("Scaling");}));});
    rows.Sort(delegate(KeyValuePair<string,Action<string>> a,KeyValuePair<string,Action<string>> b){return String.Compare(a.Key,b.Key,StringComparison.OrdinalIgnoreCase);});
    foreach(var r in rows)r.Value(r.Key);
    var foot=Label("The game program, the disc image, the game folder and cover art are not game settings: they are on the game setup screen.",SetHelpSize,"#AEC8DB");foot.Margin=new Thickness(0,12,0,0);page.Children.Add(foot);
    page.Children.Add(SetButton("Open game setup",delegate{Navigate("Preflight");}));
  }
  // ---- key map editor (SPEC-keys.md). Edits stay in keyDraft until "Save keys"; nothing else writes the file. ----
  string keyDraftFor=""; // the data folder whose key map the draft was loaded from: a draft is never shown for, or saved into, another game's folder
  void NeedKeyDraft(){if(keyDraft==null||!String.Equals(keyDraftFor,KeyData,StringComparison.OrdinalIgnoreCase))LoadKeyDraft();}
  void LoadKeyDraft(){keyDirty=false;keyNote="";keyCapture=null;keyDraftFor=KeyData;try{keyDraft=KeyMap.Load(GameRuntime.KeysPath(KeyData));keyLoadFailed=false;}catch{keyDraft=KeyMap.Defaults();keyLoadFailed=true;}}
  // The only place the key map is written; the Save button and the overlay's key editor both call it.
  void SaveKeyDraft(){
    if(keyDraft==null||!String.Equals(keyDraftFor,KeyData,StringComparison.OrdinalIgnoreCase)){LoadKeyDraft();keyNote="The keys on screen belonged to the other game, so nothing was saved. These are the keys of this game.";sessionLog.Add("keys-save-refused");return;}
    try{keyDraft.Save(GameRuntime.KeysPath(KeyData));LoadKeyDraft();keyNote="Keys saved. They apply the next time you press Play.";sessionLog.Add("keys-saved");}
    catch{keyNote="The keys could not be saved. Nothing was changed.";sessionLog.Add("keys-save-failed");}
  }
  // ---- gamepad map editor (SPEC-padmap.md). Edits stay in padDraft until "Save gamepad buttons"; nothing else writes the file. ----
  void NeedPadDraft(){if(padDraft==null||!String.Equals(padDraftFor,PadData,StringComparison.OrdinalIgnoreCase))LoadPadDraft();}
  bool padDraftNamesWalk(){for(int b=0;b<PadMap.ButtonCount;b++){uint? v=padDraft.Get(b);if(v.HasValue&&(v.Value&PadMap.Walk)!=0)return true;}return false;}
  void LoadPadDraft(){padDirty=false;padNote="";padDraftFor=PadData;try{padDraft=PadMap.Load(GameRuntime.PadMapPath(PadData));padLoadFailed=false;}catch{padDraft=PadMap.Defaults();padLoadFailed=true;}}
  // The only place the gamepad map is written.
  void SavePadDraft(){
    if(padDraft==null||!String.Equals(padDraftFor,PadData,StringComparison.OrdinalIgnoreCase)){LoadPadDraft();padNote="Nothing was saved: the buttons on screen were out of date. These are the saved buttons.";return;}
    try{padDraft.Save(GameRuntime.PadMapPath(PadData));LoadPadDraft();padNote="Gamepad buttons saved. They apply the next time the game starts (Play, or Restart in the in-game menu).";sessionLog.Add("gamepad-saved");}
    catch{padNote="The gamepad buttons could not be saved. Nothing was changed.";sessionLog.Add("gamepad-save-failed");}
  }
  void SetKey(string button,string key){
    try{
      var others=keyDraft.SetSingle(button,key);keyDirty=true;
      keyNote=KeyMap.ButtonLabel(button)+" is now "+KeyMap.KeyLabel(key)+".";
      foreach(string other in others)keyNote+=" "+KeyMap.ButtonLabel(other)+" had that key and now has: "+keyDraft.Describe(other)+".";
    }catch(ArgumentException){keyNote="That key cannot be used.";}
  }
  // The next key pressed becomes the key for the waiting button. A held key's repeats are ignored (so the Enter that
  // pressed "Press a key" is not taken), Alt arrives as a system key, and Esc cancels.
  void CaptureKey(KeyEventArgs e){
    if(e.IsRepeat){e.Handled=true;return;}
    Key pressed=e.Key==Key.System?e.SystemKey:e.Key==Key.ImeProcessed?e.ImeProcessedKey:e.Key==Key.DeadCharProcessed?e.DeadCharProcessedKey:e.Key;e.Handled=true;
    string button=keyCapture;keyCapture=null;
    if(pressed==Key.Escape)keyNote="No change.";
    else{string key=KeyMap.FromVirtualKey(KeyInterop.VirtualKeyFromKey(pressed));if(key==null)keyNote="That key cannot be used here. The game keeps Tab, F11 and F12 for itself, and a few special keys cannot be read.";else SetKey(button,key);}
    Render();
  }
  void RenderKeyEditor(){
    NeedKeyDraft();
    page.Children.Add(Label("Keyboard keys",22));
    page.Children.Add(Label("Choose the keyboard key for each game button: pick from the list, or use \"Press a key\" and then press the key you want (Esc cancels). Changes are kept only when you press Save keys, and they apply the next time you press Play. The game keeps Tab, F11 and F12 for itself. A gamepad also works and is not changed here.",16,"#AEC8DB"));
    if(keyLoadFailed)page.Children.Add(Label("The saved key map could not be read, so the default keys are shown. Saving replaces it; the old file is kept beside it with .bak added.",16,"#FFCE83"));
    if(keyDraft.NotUnderstood>0)page.Children.Add(Label(keyDraft.NotUnderstood+" line(s) in the saved key map were not understood. Saving writes the keys shown here; the old file is kept beside it with .bak added.",16,"#FFCE83"));
    var missing=keyDraft.Missing();
    if(missing.Count>0){var names=new List<string>();foreach(string b in missing)names.Add(KeyMap.ButtonLabel(b));page.Children.Add(Label("No key yet for: "+String.Join(", ",names.ToArray())+". Save is off until every button has a key.",16,"#FFCE83"));}
    var shared=keyDraft.Shared();
    if(shared.Count>0){var names=new List<string>();foreach(string k in shared)names.Add(KeyMap.KeyLabel(k));page.Children.Add(Label("The same key is on more than one button: "+String.Join(", ",names.ToArray())+".",16,"#FFCE83"));}
    var status=Label(keyNote.Length>0?keyNote:keyDirty?"Unsaved key changes.":"These are the saved keys.",16,"#8BEFFF");System.Windows.Automation.AutomationProperties.SetAutomationId(status,"keys-status");page.Children.Add(status);
    foreach(string each in KeyMap.Buttons){
      string button=each;bool capturing=keyCapture==button;var keys=keyDraft.Keys(button);
      var list=new ComboBox();foreach(string n in KeyMap.Names)list.Items.Add(KeyMap.KeyLabel(n));
      int at=keys.Count>0?Array.IndexOf(KeyMap.Names,keys[0]):-1;
      if(keys.Count>0&&at<0){list.Items.Add(KeyMap.KeyLabel(keys[0]));at=list.Items.Count-1;}
      list.SelectedIndex=at;System.Windows.Automation.AutomationProperties.SetName(list,"Key for "+KeyMap.ButtonLabel(button));
      list.SelectionChanged+=delegate{int i=list.SelectedIndex;if(i<0||i>=KeyMap.Names.Length)return;if(keys.Count>0&&keys[0]==KeyMap.Names[i])return;SetKey(button,KeyMap.Names[i]);Dispatcher.BeginInvoke(new Action(Render));};
      var press=Control(capturing?"Press now":"Press a key",delegate{keyNote=capturing?"No change.":"";keyCapture=capturing?null:button;Render();},false,(capturing?"Waiting for a key for ":"Press a key for ")+KeyMap.ButtonLabel(button));
      press.FontSize=14;press.Padding=new Thickness(4,2,4,2);press.Margin=new Thickness(6,0,0,0);
      var both=new Grid();both.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});both.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(104)});
      list.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(press,1);both.Children.Add(list);both.Children.Add(press);
      Row(KeyMap.ButtonLabel(button),capturing?"Press the key you want now. Esc cancels.":keyDraft.Describe(button),both);
    }
    var actions=new Grid();
    var save=Control("Save keys",delegate{
      SaveKeyDraft();
      Render();});
    save.IsEnabled=keyDirty&&keyDraft.Missing().Count==0;
    var discard=Control("Discard key changes",delegate{LoadKeyDraft();keyNote="Changes discarded.";Render();});discard.IsEnabled=keyDirty;
    var defaults=Control("Use the default keys",delegate{keyDraft=KeyMap.Defaults();keyDirty=true;keyCapture=null;keyNote="Default keys shown. Press Save keys to keep them.";Render();});
    actions.Children.Add(save);actions.Children.Add(discard);actions.Children.Add(defaults);LayoutToolbar(actions);page.Children.Add(actions);
  }
  void RenderPadEditor(){
    NeedPadDraft();heading.Text="Gamepad buttons: "+GameRuntime.Game(1).Name;
    bool tps=!runtimes[0].OriginalControls;
    bool walk=game==1&&playCheck.HasRoll;padDraft.WalkKnown=walk;   // the walk switch exists only in a game program that has the quick roll (game contract 2.41)
    page.Children.Add(Label("Choose what each button of your Xbox or PlayStation 5 pad does in Mega Man Legends. Each line names the Xbox button first and the PlayStation 5 button in the same place second. \"Default\" keeps what the game does without changes. Changes are kept only when you press Save gamepad buttons, and they apply the next time the game starts (Play, or Restart in the in-game menu). The keyboard keys are not changed here.",16,"#AEC8DB"));
    page.Children.Add(Label(tps?"Third-person controls are on (Settings), so the defaults shown are theirs: the left trigger aims. Camera behind Mega Man and Aim work only with the third-person controls.":"Third-person controls are off (Settings), so the defaults shown are the original controls: the left trigger gives L2. Camera behind Mega Man and Aim do nothing with the original controls.",16,"#AEC8DB"));
    if(walk)page.Children.Add(Label("Your game program has the quick roll. Its walk switch (default: press the left stick) decides whether holding Circle starts the Jet Skates while the quick roll is on: press it once to allow the hold, press it again to stop. It is off every time the game starts. It does nothing when the quick roll is off.",16,"#AEC8DB"));
    if(!walk&&padDraftNamesWalk())page.Children.Add(Label("The saved gamepad map names the walk switch (WALK), which your game program does not have. The game skips that line. Saving keeps it.",16,"#FFCE83"));
    if(padLoadFailed)page.Children.Add(Label("The saved gamepad map could not be read, so the defaults are shown. Saving replaces it; the old file is kept beside it with .bak added.",16,"#FFCE83"));
    if(padDraft.NotUnderstood>0)page.Children.Add(Label(padDraft.NotUnderstood+" line(s) in the saved gamepad map are not understood by the game, which skips them. Saving writes the buttons shown here. The file you had before the first save is kept beside it as pad-map.txt.before-editor, and the last one as pad-map.txt.bak.",16,"#FFCE83"));
    if(padDraft.LookSensitivity.HasValue||padDraft.LookInvert.HasValue)page.Children.Add(Label("The saved gamepad map also sets the camera speed or the camera invert. The camera settings on the Settings screen win over those lines. Saving keeps them.",16,"#AEC8DB"));
    var missing=padDraft.Lost(tps);
    if(missing.Count>0){var names=new List<string>();foreach(string a in missing)names.Add(PadMap.ActionLabel(a));page.Children.Add(Label("After your changes no pad button gives: "+String.Join(", ",names.ToArray())+". The keyboard keys still do.",16,"#FFCE83"));}
    var shared=padDraft.Shared(tps);
    if(shared.Count>0){var names=new List<string>();foreach(string a in shared)names.Add(PadMap.ActionLabel(a));page.Children.Add(Label("More than one pad button gives: "+String.Join(", ",names.ToArray())+".",16,"#AEC8DB"));}
    var status=Label(padNote.Length>0?padNote:padDirty?"Unsaved gamepad changes.":"These are the saved gamepad buttons.",16,"#8BEFFF");System.Windows.Automation.AutomationProperties.SetAutomationId(status,"gamepad-status");page.Children.Add(status);
    for(int each=0;each<PadMap.ButtonCount;each++){
      int button=each;uint? set=padDraft.Get(button);
      var list=new ComboBox();list.Items.Add("Default: "+PadMap.Describe(PadMap.Default(button,tps,walk)));
      string[] offered=PadMap.ActionsFor(walk);
      foreach(string a in offered)list.Items.Add(PadMap.ActionLabel(a));
      int at=0;
      if(set.HasValue){int single=-1;for(int i=0;i<offered.Length;i++)if(PadMap.Bit(offered[i])==set.Value){single=i;break;}
        if(single>=0)at=single+1;else{list.Items.Add(PadMap.Describe(set.Value));at=list.Items.Count-1;}}
      list.SelectedIndex=at;list.FontSize=16;list.VerticalAlignment=VerticalAlignment.Center;
      System.Windows.Automation.AutomationProperties.SetName(list,"Action for "+PadMap.ButtonLabel(button));System.Windows.Automation.AutomationProperties.SetAutomationId(list,"gamepad-"+PadMap.Buttons[button]);
      list.SelectionChanged+=delegate{int i=list.SelectedIndex;if(i<0||i>offered.Length)return;uint? want=i==0?(uint?)null:PadMap.Bit(offered[i-1]);if(want==padDraft.Get(button))return;padDraft.Set(button,want);padDirty=true;padNote="";Dispatcher.BeginInvoke(new Action(Render));};
      Row(PadMap.ButtonLabel(button),"Now: "+PadMap.Describe(padDraft.Effective(button,tps))+(set.HasValue?"":" (default)"),list);
    }
    Button save=null,discard=null;
    var dz=new Slider{Minimum=PadMap.DeadzoneMin,Maximum=PadMap.DeadzoneMax,Value=padDraft.EffectiveDeadzone,TickFrequency=1,IsSnapToTickEnabled=true,IsMoveToPointEnabled=true,VerticalAlignment=VerticalAlignment.Center};
    System.Windows.Automation.AutomationProperties.SetName(dz,"Stick dead zone");System.Windows.Automation.AutomationProperties.SetAutomationId(dz,"gamepad-deadzone");
    dz.ValueChanged+=delegate{int v=(int)Math.Round(dz.Value);int? want=v==PadMap.DeadzoneDefault?(int?)null:v;if(want==padDraft.Deadzone)return;padDraft.Deadzone=want;padDirty=true;padNote="";status.Text="Unsaved gamepad changes.";if(save!=null)save.IsEnabled=true;if(discard!=null)discard.IsEnabled=true;};
    Row("Stick dead zone","How far a stick must move, in percent of its travel, before the game reacts. "+PadMap.DeadzoneDefault+" is the default; a worn stick that drifts may need more. Used with the third-person controls (the original controls use their own fixed limit).",WithNumber(dz));
    var actions=new Grid();
    save=Control("Save gamepad buttons",delegate{SavePadDraft();Render();});save.IsEnabled=padDirty;
    discard=Control("Discard gamepad changes",delegate{LoadPadDraft();padNote="Changes discarded.";Render();});discard.IsEnabled=padDirty;
    var defaults=Control("Use the default buttons",delegate{var fresh=PadMap.Defaults(walk);fresh.LookSensitivity=padDraft.LookSensitivity;fresh.LookInvert=padDraft.LookInvert;padDraft=fresh;padDirty=true;padNote="Default buttons and the default dead zone shown. Press Save gamepad buttons to keep them.";Render();});
    actions.Children.Add(save);actions.Children.Add(discard);actions.Children.Add(defaults);LayoutToolbar(actions);page.Children.Add(actions);
    var back=Control("Back to Settings",delegate{Navigate("Settings");});page.Children.Add(back);
  }
  void RenderHelp(){
    page.Children.Add(Label("About Flutter Deck",22));
    page.Children.Add(Label("Flutter Deck is your local desktop launcher for Mega Man Legends. The in-game pill provides session controls, music and keyboard settings.",16));
    RenderOverlaySwitch();
    page.Children.Add(Control("Game setup and readiness",delegate{Navigate("Preflight");}));
    page.Children.Add(Label("Your launcher, your files",22));
    page.Children.Add(Label("Setup stores references locally, never copies your game or BIOS. If a file moves, reopen Setup and select its new location.",18));
    page.Children.Add(Label("Play starts your own native Mega Man Legends game program once it is set up under Game setup and readiness. Mega Man Legends 2 has its own, earlier native program and its own setup; choose its card in the Library first. Valid file metadata does not prove compatibility or game boot.",18));
    page.Children.Add(Label("Drag the title strip to move. Drag the lower-right grip to preview size, then release to apply. A-/A+/Reset adjust body text. Feature options and updates are still previews.",18));
    var actions=new Grid();actions.Children.Add(Control("Reopen setup",delegate{OpenSetup();}));actions.Children.Add(Control("Recheck references",delegate{libraryPage=true;libraryAdding=false;librarySelected=-1;Navigate("Library");}));actions.Children.Add(Control("Reset launcher settings",delegate{ResetLauncherPreferences();Navigate("Help");}));LayoutToolbar(actions);page.Children.Add(actions);
    page.Children.Add(Label("Session log (safe event names only)",20));
    page.Children.Add(Label("No paths, filenames, account details or exception text are logged. Events remain in memory until close; nothing is sent.",16));
    var logBox=new TextBox{Text=sessionLog.Text,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,AcceptsReturn=true,MinHeight=100,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};System.Windows.Automation.AutomationProperties.SetName(logBox,"Session log");page.Children.Add(logBox);
    page.Children.Add(Label("Launcher state when this page opened (status words only, no paths or names): "+StateNow().Replace(";","  "),14,"#AEC8DB"));
    if(setupLoadFailed)page.Children.Add(Label("Saved setup could not be read. It is preserved; saving setup is disabled to avoid losing references. Keep a copy before repairing that configuration.",18,"#FFCE83"));
  }
  void SetContentScale(double value){contentScale=UiPreferences.Clamp(value);page.LayoutTransform=new ScaleTransform(UiPreferences.Drawn(contentScale),UiPreferences.Drawn(contentScale));ScaleLibrary();try{UiPreferences.Save(UiPreferences.PathName,contentScale);}catch{MessageBox.Show(this,"Text size changed for this session, but could not be saved.","Flutter Deck");}}
  void LayoutToolbar(Grid grid){grid.ColumnDefinitions.Clear();for(int i=0;i<grid.Children.Count;i++){grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});var child=grid.Children[i] as FrameworkElement;child.Width=Double.NaN;child.Margin=new Thickness(6);child.MinHeight=36;child.HorizontalAlignment=HorizontalAlignment.Stretch;child.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(child,i);}}
  void Row(string title,string detail,UIElement control){var row=new Grid{Margin=new Thickness(0,6,0,10)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(260)});var c=control as FrameworkElement;if(String.IsNullOrEmpty(System.Windows.Automation.AutomationProperties.GetName(control)))System.Windows.Automation.AutomationProperties.SetName(control,title);c.Width=Double.NaN;c.Margin=new Thickness(16,0,0,0);c.HorizontalAlignment=HorizontalAlignment.Stretch;c.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(control,1);row.Children.Add(control);var text=new StackPanel{VerticalAlignment=VerticalAlignment.Center};text.Children.Add(Label(title,22));text.Children.Add(Label(detail,18,"#AEC8DB"));row.Children.Add(text);page.Children.Add(row);}
  void SelectSetupFile(bool bios){
    var picker=new Microsoft.Win32.OpenFileDialog{Title=bios?"Select your BIOS candidate":"Select your game image",CheckFileExists=true,Multiselect=false,Filter=bios?"BIOS candidate (*.bin;*.rom)|*.bin;*.rom":"Game images (*.cue;*.bin;*.iso)|*.cue;*.bin;*.iso"};
    if(picker.ShowDialog(this)!=true)return;
    try{string problem=bios?LocalSetup.CheckBios(picker.FileName):LocalSetup.CheckGame(picker.FileName);if(problem.Length>0){setupNotice=problem;Render();return;}if(bios)setupDraft.Bios[setupTarget]=picker.FileName;else setupDraft.Game[setupTarget]=picker.FileName;setupNotice="Metadata checks passed. Identity and compatibility remain unverified.";}catch{setupNotice="The selected file could not be inspected. No setup was saved.";}Render();
  }
  void RenderSetup(){
    page.Children.Add(Label("LOCAL SETUP / Step "+(setupStep+1)+" of 3",16,"#8BEFFF"));
    page.Children.Add(Label("References only: files stay in place. No game or BIOS is copied, uploaded or executed.",16,"#FFCE83"));
    if(setupNotice.Length>0)page.Children.Add(Label(setupNotice,16,"#FFCE83"));
    if(setupStep==0){
      page.Children.Add(Label("Which game are you setting up?",22));var target=new ComboBox{Width=300,HorizontalAlignment=HorizontalAlignment.Left};for(int id=1;id<=GameRuntime.GameCount;id++)target.Items.Add(GameRuntime.Game(id).Name);target.SelectedIndex=setupTarget;System.Windows.Automation.AutomationProperties.SetName(target,"Setup target game");target.SelectionChanged+=delegate{setupTarget=target.SelectedIndex;setupDraft=LocalSetup.Copy(localSetup);setupNotice="";};page.Children.Add(target);page.Children.Add(Label("Each game keeps separate local references. Edition compatibility is unknown.",16));
    }else if(setupStep==1){
      page.Children.Add(Label(GameRuntime.Game(setupTarget+1).Name,22));
      Row("Your game",setupDraft.Game[setupTarget].Length==0?"Not selected":setupDraft.Game[setupTarget],Control("Browse game",delegate{SelectSetupFile(false);}));
      Row("Your BIOS",setupDraft.Bios[setupTarget].Length==0?"Not selected":setupDraft.Bios[setupTarget],Control("Browse BIOS",delegate{SelectSetupFile(true);}));
      page.Children.Add(Label("Both references are required by this setup policy. A technical BIOS dependency is not verified; no emulator is wired.",16));
    }else{
      page.Children.Add(Label("Review: "+GameRuntime.Game(setupTarget+1).Name,22));page.Children.Add(Label("Game and BIOS references are ready for metadata checks. This does not verify identity, rights, region or native game compatibility.",16));page.Children.Add(Label("Save stores only paths in your local Windows profile. Other game references are preserved. Nothing launches.",16));
    }
    var actions=new Grid{Margin=new Thickness(0,12,0,0)};if(setupStep>0)actions.Children.Add(Control("Back",delegate{setupStep--;Render();}));
    if(setupLoadFailed){page.Children.Add(Label("Setup save disabled: existing configuration could not be read. Open Help; your file is preserved.",18,"#FFCE83"));actions.Children.Add(Control("Help",delegate{Navigate("Help");}));LayoutToolbar(actions);page.Children.Add(actions);return;}
    if(setupStep<2){var next=Control("Next",delegate{setupStep++;Render();});next.IsEnabled=setupStep==0||(setupDraft.Game[setupTarget].Length>0&&setupDraft.Bios[setupTarget].Length>0);actions.Children.Add(next);}else actions.Children.Add(Control("Save local setup",delegate{try{string g=LocalSetup.CheckGame(setupDraft.Game[setupTarget]);string b=LocalSetup.CheckBios(setupDraft.Bios[setupTarget]);if(g.Length>0||b.Length>0){setupNotice=g.Length>0?g:b;Render();return;}var candidate=LocalSetup.MergeSelected(localSetup,setupDraft,setupTarget);LocalSetup.Save(LocalSetup.ConfigPath,candidate);localSetup=candidate;setupDraft=LocalSetup.Copy(localSetup);sessionLog.Add("setup-saved");FetchCovers(false);setupNotice="Local references saved. Compatibility unknown; no game launched.";Render();}catch{sessionLog.Add("setup-save-failed");setupNotice="Setup could not be saved. Existing files were not modified.";Render();}}));
    actions.Children.Add(Control("Exit setup",delegate{libraryPage=true;libraryAdding=false;librarySelected=-1;Navigate("Library");}));LayoutToolbar(actions);page.Children.Add(actions);
  }
  // ---- memory card backups (SPEC-saves.md). Restore is the only place the launcher replaces the card. ----
  void RenderSaveCards(){
    page.Children.Add(Label("Memory card: "+Kind.Name,22));
    string data=GameData;bool running=GameRunning;string state=SaveCards.CardState(data);bool waiting=GameRuntime.HasImport(data)&&state=="none";
    string when="";try{if(state!="none")when=System.IO.File.GetLastWriteTime(GameRuntime.CardPath(data)).ToString("f");}catch{}
    var status=Label(state=="ok"?"Flutter Deck has a memory card for this game. Last written: "+when+".":state=="damaged"?"The memory card file is damaged (wrong size). Restoring a backup repairs it; the damaged file is kept.":waiting?"A save file is waiting to become the card at the first Play.":"No memory card yet. The game creates one the first time you press Play.",18,state=="ok"?"#8BEFFF":"#FFCE83");
    System.Windows.Automation.AutomationProperties.SetAutomationId(status,"card-status");page.Children.Add(status);
    page.Children.Add(Label("A backup is a copy of the card. Restore puts a backup in place of the card; the card you had is kept as a new backup first, so a restore can be undone. Backups are never deleted or replaced by Flutter Deck."+(running?" A game is running: backing up and restoring are off until it closes.":""),16,"#AEC8DB"));
    if(cardNote.Length>0){var note=Label(cardNote,16,"#8BEFFF");System.Windows.Automation.AutomationProperties.SetAutomationId(note,"card-note");page.Children.Add(note);}
    var unsaved=GameRuntime.UnsavedFiles(data);
    if(unsaved.Count>0)page.Children.Add(Label("A save may not have been written. The game left "+String.Join(", ",unsaved.ToArray())+" beside the card. Flutter Deck has not touched them.",16,"#FFCE83"));
    var actions=new Grid();
    var backup=Control("Back up now",delegate{
      try{string name=SaveCards.Backup(GameData);cardNote="Backup made: "+name+".";sessionLog.Add("card-backed-up");}
      catch{cardNote="The backup could not be made. Nothing was changed.";sessionLog.Add("card-backup-failed");}
      Render();});
    backup.IsEnabled=state=="ok"&&!running;
    var add=Control("Add a card file as a backup",delegate{
      var pick=new Microsoft.Win32.OpenFileDialog{Title="Memory card file to add as a backup",Filter="Raw memory card (*.mcd;*.mcr;*.bin)|*.mcd;*.mcr;*.bin|All files (*.*)|*.*",CheckFileExists=true};
      if(pick.ShowDialog(this)!=true)return;
      try{string name=SaveCards.Import(pick.FileName,GameData);cardNote="Added as a backup: "+name+". Your file was only read. Use Restore to play from it.";sessionLog.Add("card-imported");}
      catch(System.IO.InvalidDataException e){cardNote=e.Message;}
      catch{cardNote="That file could not be added. Nothing was changed.";}
      Render();});
    actions.Children.Add(backup);actions.Children.Add(add);LayoutToolbar(actions);page.Children.Add(actions);
    var backups=SaveCards.List(data);
    page.Children.Add(Label(backups.Count==0?"No backups yet.":"Backups, newest first",20));
    foreach(var each in backups){
      var item=each;bool usable=SaveCards.IsRestorable(item.Path,data);
      var restore=Control("Restore",delegate{RestoreCard(item);},false,"Restore the backup "+item.Name);
      restore.IsEnabled=usable&&!running&&!waiting&&!item.SameAsCurrent;
      if(item.SameAsCurrent)restore.ToolTip="This backup is the same as the current card";else if(waiting)restore.ToolTip="Press Play once first: a save file is waiting to become the card";else if(running)restore.ToolTip="Close the game first";else if(!usable)restore.ToolTip="This file is kept for you but cannot be restored from here";
      Row(SaveCards.KindLabel(item.Kind)+", "+item.TimeUtc.ToLocalTime().ToString("g"),item.Name+(item.SameAsCurrent?"  (same as the current card)":""),restore);
    }
    page.Children.Add(Label("Backups are kept in "+SaveCards.BackupsPath(data)+". Removing Flutter Deck's settings does not remove them.",14,"#AEC8DB"));
    page.Children.Add(Label("More save features (proposals)",20));
  }
  void RestoreCard(CardBackup item){
    if(MessageBox.Show(this,"Restore this backup?\n\n"+SaveCards.KindLabel(item.Kind)+", "+item.TimeUtc.ToLocalTime().ToString("g")+"\n"+item.Name+"\n\nThe card you have now is kept as a new backup first, so you can go back.","Flutter Deck - Memory card",MessageBoxButton.OKCancel,MessageBoxImage.Question)!=MessageBoxResult.OK)return;
    try{string kept=SaveCards.Restore(item.Path,GameData,runtime.Program);cardNote="Restored "+item.Name+"."+(kept.Length>0?" The card you had is kept as "+kept+".":"");sessionLog.Add("card-restored");}
    catch(InvalidOperationException e){cardNote=e.Message;sessionLog.Add("card-restore-refused");}
    catch{cardNote="The backup could not be restored. The card was not replaced.";sessionLog.Add("card-restore-failed");}
    Render();
  }
  void RenderCatalog(string initialCategory){
    var localActions=new Grid();localActions.Children.Add(Control("Always on top",delegate{Topmost=!Topmost;},false,"Toggle this launcher window always on top"));localActions.Children.Add(Control("Standard size",delegate{WindowState=WindowState.Normal;Width=1672;Height=941;}));LayoutToolbar(localActions);
    page.Children.Add(Label("Preview options / game support unverified",14,"#FFCE83"));
    var bar=new Grid{Margin=new Thickness(0,4,0,10)};
    var category=new ComboBox{Width=240,Margin=new Thickness(0,0,12,0)};category.Items.Add("All");
    foreach(string name in new[]{"Library","Graphics","Controls","Accessibility","Saves","Mods","Gameplay","Media","Audio","Community","Diagnostics"})category.Items.Add(name);
    category.SelectedItem=initialCategory;
    System.Windows.Automation.AutomationProperties.SetName(category,"Feature category");
    var search=new TextBox{Padding=new Thickness(8,4,8,4),VerticalContentAlignment=VerticalAlignment.Center,ToolTip="Search feature title, dependency or ID"};
    var searchHint=Label("Search features",18,"#8FA9BC");searchHint.IsHitTestVisible=false;searchHint.Margin=new Thickness(12,0,0,0);searchHint.VerticalAlignment=VerticalAlignment.Center;search.TextChanged+=delegate{searchHint.Visibility=search.Text.Length==0?Visibility.Visible:Visibility.Collapsed;};
    var searchBox=new Grid();searchBox.Children.Add(search);searchBox.Children.Add(searchHint);
    System.Windows.Automation.AutomationProperties.SetName(search,"Search feature proposals");
    var target=new ComboBox{Width=210,Margin=new Thickness(0,0,12,0)};target.Items.Add("MML1 target");target.Items.Add("MML2 target");target.Items.Add("Tron Bonne target");target.SelectedIndex=game-1;
    var edition=new ComboBox{Width=280,Margin=new Thickness(0,0,12,0)};foreach(string e in new[]{"Edition unspecified","NTSC-U reference","NTSC-J reference","PAL reference"})edition.Items.Add(e);edition.SelectedIndex=0;
    var unavailable=new CheckBox{Content="Unavailable fixture",Foreground=Brushes.White,VerticalAlignment=VerticalAlignment.Center};
    System.Windows.Automation.AutomationProperties.SetName(target,"Target game context");System.Windows.Automation.AutomationProperties.SetName(edition,"Edition context - support unverified");
    var results=new StackPanel();var count=Label("",18,"#8BEFFF");int pageIndex=0;Button previous=null,nextPage=null;Grid pager=null;
    Action refresh=delegate{
      results.Children.Clear();int shown=0;string selected=category.SelectedItem as string;string query=search.Text.Trim();
      var filtered=new List<Feature>();foreach(var f in FeatureCatalog.All){if(selected!="All"&&selected!=f.Category)continue;if(query.Length>0&&(f.Title+" "+f.Dependency+" "+f.Id).IndexOf(query,StringComparison.OrdinalIgnoreCase)<0)continue;filtered.Add(f);}pageIndex=Math.Max(0,Math.Min(pageIndex,Math.Max(0,(filtered.Count-1)/6)));
      for(int featureIndex=pageIndex*6;featureIndex<Math.Min(filtered.Count,pageIndex*6+6);featureIndex++){var feature=filtered[featureIndex];
        if(selected!="All" && selected!=feature.Category)continue;
        if(query.Length>0 && (feature.Title+" "+feature.Dependency+" "+feature.Id).IndexOf(query,StringComparison.OrdinalIgnoreCase)<0)continue;
        shown++;var item=feature;string key=target.SelectedIndex+"|"+edition.SelectedIndex+"|"+item.Id;
        var grid=new Grid{Margin=new Thickness(10,6,10,6)};grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(300)});
        var text=new StackPanel{Margin=new Thickness(0,0,18,0)};text.Children.Add(Label(item.Title,20));text.Children.Add(Label(unavailable.IsChecked==true&&item.Id!="library-onboarding"&&item.Id!="audio-soundtrack"?"Unavailable (preview)":item.Dependency,16,"#AEC8DB"));text.ToolTip=item.Description+" Target: "+(string)target.SelectedItem+" / "+(string)edition.SelectedItem+". Support unknown.";System.Windows.Automation.AutomationProperties.SetHelpText(text,item.Description);grid.Children.Add(text);
        if(item.Id=="library-onboarding"||item.Id=="audio-soundtrack"){
          var open=Control(item.Id=="library-onboarding"?"Open real setup":"Open music player",delegate{if(item.Id=="library-onboarding"){game=target.SelectedIndex+1;OpenSetup();}else Navigate("Music");});Grid.SetColumn(open,1);grid.Children.Add(open);results.Children.Add(new Border{Background=Ink("#18364D"),BorderBrush=Ink("#315A72"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Margin=new Thickness(0,0,0,8),Child=grid});continue;
        }
        var choice=new ComboBox{VerticalAlignment=VerticalAlignment.Center};foreach(string option in item.Options)choice.Items.Add(option);choice.SelectedIndex=featureChoices.ContainsKey(key)?featureChoices[key]:0;
        choice.IsEnabled=unavailable.IsChecked!=true;choice.ToolTip=unavailable.IsChecked==true?"Simulated unavailable state. No real compatibility check was performed.":"Preview choice only; support is unknown.";
        System.Windows.Automation.AutomationProperties.SetName(choice,item.Title+" preview option");choice.SelectionChanged+=delegate{if(choice.SelectedIndex>=0)featureChoices[key]=choice.SelectedIndex;};Grid.SetColumn(choice,1);grid.Children.Add(choice);
        results.Children.Add(new Border{Background=Ink("#18364D"),BorderBrush=Ink("#315A72"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Margin=new Thickness(0,0,0,8),Child=grid});
      }
      count.Text=filtered.Count==0?"No matches":(pageIndex*6+1)+"-"+(pageIndex*6+shown)+" of "+filtered.Count+" features";if(previous!=null)previous.IsEnabled=pageIndex>0;if(nextPage!=null)nextPage.IsEnabled=(pageIndex+1)*6<filtered.Count;if(pager!=null)pager.Visibility=filtered.Count>6?Visibility.Visible:Visibility.Collapsed;
      if(shown==0)results.Children.Add(Label("No matches. Try a different category or search.",16));
    };
    category.SelectionChanged+=delegate{pageIndex=0;refresh();};search.TextChanged+=delegate{pageIndex=0;refresh();};target.SelectionChanged+=delegate{refresh();};edition.SelectionChanged+=delegate{refresh();};unavailable.Checked+=delegate{refresh();};unavailable.Unchecked+=delegate{refresh();};
    bar.Children.Add(category);bar.Children.Add(searchBox);bar.Children.Add(Control("Reset choices",delegate{featureChoices.Clear();refresh();}));
    var context=new Grid{Margin=new Thickness(0,0,0,10)};context.Children.Add(target);context.Children.Add(edition);context.Children.Add(unavailable);var originalPreset=Control("Original-style controls",delegate{string prefix=target.SelectedIndex+"|"+edition.SelectedIndex+"|";foreach(string id in new[]{"input-style","input-orbit","input-lock","input-deadzone","input-speed","input-remap","input-mouse"})featureChoices[prefix+id]=0;category.SelectedItem="Controls";refresh();},false,"Preview seven classic/default control choices; not an edition-verified original preset");context.Children.Add(originalPreset);unavailable.Checked+=delegate{originalPreset.IsEnabled=false;};unavailable.Unchecked+=delegate{originalPreset.IsEnabled=true;};
    pager=new Grid();previous=Control("Previous 6",delegate{pageIndex--;refresh();});nextPage=Control("Next 6",delegate{pageIndex++;refresh();});pager.Children.Add(previous);pager.Children.Add(nextPage);LayoutToolbar(context);LayoutToolbar(bar);LayoutToolbar(pager);var advancedBody=new StackPanel();advancedBody.Children.Add(context);advancedBody.Children.Add(localActions);var advanced=new Expander{Header="Advanced / window options",IsExpanded=false,Content=advancedBody,Foreground=Ink("#AEC8DB"),FontSize=16,Margin=new Thickness(6,0,6,6)};page.Children.Add(bar);page.Children.Add(advanced);page.Children.Add(count);page.Children.Add(results);page.Children.Add(pager);refresh();
  }
  void ShowPicturePicker(){if(OwnGameRunning&&overlay!=null)overlay.OpenPicturePicker();}
  void Render(){RenderScreen();if(OwnGameRunning&&overlay!=null&&screen!="Library"){var picture=Control("Picture preset...",ShowPicturePicker);System.Windows.Automation.AutomationProperties.SetAutomationId(picture,"launcher-picture-preset");page.Children.Insert(Math.Min(1,page.Children.Count),picture);}PublishState();}
  void RenderScreen(){if(backButton!=null){backButton.IsEnabled=history.CanBack;forwardButton.IsEnabled=history.CanForward;}gameTitle.Text=game==3?"MISADVENTURES OF TRON BONNE":"MEGA MAN LEGENDS"+(game==2?" 2":"");foreach(var b in nav){bool selected=(string)b.Tag==screen;b.FontWeight=selected?FontWeights.SemiBold:FontWeights.Normal; /* semi-bold: the longest name, "Advanced Settings", must fit on one line also when it is the selected one */b.Background=Ink(selected?"#FF225F8E":"#FF122B40");b.BorderBrush=Ink(selected?"#8BEFFF":"#41637D");b.BorderThickness=new Thickness(selected?2:1);}foreach(var item in libraryControls)item.Visibility=DeckState.ShowsLibrary(screen)?Visibility.Visible:Visibility.Collapsed;{bool lib=DeckState.ShowsLibrary(screen),gamePage=false; /* the game page became the item panel of DeckLibrary.cs; the painted game controls stay hidden */foreach(var item in gameOnly)item.Visibility=gamePage?Visibility.Visible:Visibility.Collapsed;librarySide.Visibility=gamePage?Visibility.Visible:Visibility.Collapsed;libraryBack.Visibility=lib&&!gamePage?Visibility.Visible:Visibility.Collapsed;if(lib){UpdatePlay();RenderLibrary();}}panel.Visibility=DeckState.ShowsLibrary(screen)?Visibility.Collapsed:Visibility.Visible;musicInfo=null;seekSlider=null;foreach(var item in transportControls)item.Visibility=DeckState.ShowsTransport(screen)?Visibility.Visible:Visibility.Collapsed;panel.Height=screen=="Music"?351:420;page.Children.Clear();heading=Label(DeckState.Heading(screen),24);heading.TextAlignment=TextAlignment.Center;System.Windows.Automation.AutomationProperties.SetAutomationId(heading,"screen-heading");page.Children.Add(heading);
    UpdatePlay();
    if(screen=="Library"){readiness.Text=setupLoadFailed?"Saved setup needs attention. Open Help.":PlayStatusText();sessionLog.Add("library-opened");sessionLog.Add("references-checked");}
    if(screen=="Home"){RenderHome();return;}
    if(screen=="Music"){RenderMusic();return;}
    if(screen=="Scaling"){RenderScaling();return;}
    if(screen=="Preflight"){RenderPreflight();return;}
    if(screen=="Gamepad"){RenderPadEditor();return;}
    if(screen=="Help"){RenderHelp();return;}
    if(screen=="Setup"){RenderSetup();return;}
    if(screen=="Saves")RenderSaveCards();
    if(screen=="Settings"){RenderGameSettings();return;}
    if(screen=="Advanced"){page.Children.Add(Label("Game settings (sound, picture, controls, camera) are on the Settings screen. The game program, the disc image, the game folder and the keyboard keys are on the game setup screen.",16,"#AEC8DB"));var advActions=new Grid();advActions.Children.Add(Control("Settings",delegate{Navigate("Settings");}));advActions.Children.Add(Control("Game setup and readiness",delegate{Navigate("Preflight");}));LayoutToolbar(advActions);page.Children.Add(advActions);}
    string category=DeckState.CatalogCategory(screen);
    if(category!=null){RenderCatalog(category);return;}
    if(screen=="Downloads"){
      page.Children.Add(Label("Launcher updates",24));
      page.Children.Add(Label("INSTALLED: FLUTTER DECK "+Updater.Version+" (RELEASE "+Updater.Sequence+")",12,"#77DFF3"));
      var updateStatus=Label(updateNote,18);System.Windows.Automation.AutomationProperties.SetAutomationId(updateStatus,"update-status");page.Children.Add(updateStatus);
      if(updateFound!=null&&(updatePhase==UpdatePhase.Available||updatePhase==UpdatePhase.Downloading||updatePhase==UpdatePhase.InstallFailed)){
        page.Children.Add(Label("WHAT'S NEW IN "+updateFound.Info.Version,14,"#8BEFFF"));
        page.Children.Add(Label(updateFound.Info.Notes.Length>0?updateFound.Info.Notes:"No notes were published with this release.",16));
        page.Children.Add(Label("Download: "+(updateFound.Info.Length/1048576.0).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" MB from github.com/raiinman/Flutter-Deck. The publisher's signature on this release was verified.",14,"#AEC8DB"));
      }
      var auto=new CheckBox{Content="Check for updates when Flutter Deck starts",IsChecked=updatePrefs.Auto,Foreground=Ink("#F5EAD0"),FontSize=18,Margin=new Thickness(0,8,0,8),VerticalContentAlignment=VerticalAlignment.Center};
      System.Windows.Automation.AutomationProperties.SetAutomationId(auto,"update-auto");
      RoutedEventHandler autoChanged=delegate{SetAutomaticUpdateCheck(auto.IsChecked==true);};auto.Checked+=autoChanged;auto.Unchecked+=autoChanged;page.Children.Add(auto);
      var actions=new Grid();
      var checkNow=Control("Check now",delegate{CheckForUpdate(true);});checkNow.IsEnabled=!updateBusy&&!updateRestartPending;actions.Children.Add(checkNow);
      var install=Control("Download and install",delegate{InstallUpdate();});install.IsEnabled=!updateBusy&&!updateRestartPending&&updateFound!=null&&(updatePhase==UpdatePhase.Available||updatePhase==UpdatePhase.InstallFailed);actions.Children.Add(install);
      var restart=Control("Restart now",delegate{RestartForUpdate();});restart.IsEnabled=!updateBusy&&updateRestartPending;actions.Children.Add(restart);
      var rollback=Control("Roll back",delegate{RollBackUpdate();});rollback.IsEnabled=!updateBusy&&Updater.CanRollBack(ExePath);rollback.ToolTip="Put back the version that was installed before the last update";actions.Children.Add(rollback);
      LayoutToolbar(actions);page.Children.Add(actions);
      page.Children.Add(Label("A check asks GitHub for one small signed file. No account, game, save or personal data is sent. Nothing installs without your click. An update replaces only FlutterDeck.exe and keeps the previous one for Roll back; your settings, games, BIOS, saves, music and mods are never touched. Game updates are not part of this.",14,"#AEC8DB"));
    }
  }
  UpdatePrefs updatePrefs=new UpdatePrefs();UpdatePhase updatePhase=UpdatePhase.Idle;UpdateCheck updateFound;string updateNote="Not checked yet this session.";bool updateBusy=false,updateClosed=false,updateRestartPending=false;int updatePendingSequence=0;
  static string ExePath {get{return Assembly.GetExecutingAssembly().Location;}}
  void InitializeUpdates(){
    updatePrefs=Updater.LoadPrefs(Updater.PrefsPath);updatePhase=updatePrefs.Auto?UpdatePhase.Idle:UpdatePhase.Off;if(!updatePrefs.Auto)updateNote="Automatic checks are off. Use Check now when you want to look.";
    Updater.CleanUp(ExePath);InstallRecord.RefreshVersion(ExePath);Closed+=delegate{updateClosed=true;};Loaded+=delegate{if(updatePrefs.Auto)CheckForUpdate(false);};
  }
  void UpdateChanged(){if(updateClosed)return;var layout=nav[Array.IndexOf(screens,"Downloads")].Content as Grid;if(layout!=null)((TextBlock)((Viewbox)layout.Children[1]).Child).Text=updatePhase==UpdatePhase.Available?"Updates (1)":"Updates";if(screen=="Downloads")Render();else PublishState();}
  void SetAutomaticUpdateCheck(bool on){
    updatePrefs.Auto=on;if(!on&&updatePhase==UpdatePhase.Idle)updatePhase=UpdatePhase.Off;if(on&&updatePhase==UpdatePhase.Off)updatePhase=UpdatePhase.Idle;
    try{Updater.SavePrefs(Updater.PrefsPath,updatePrefs);if(updatePhase==UpdatePhase.Off)updateNote="Automatic checks are off. Use Check now when you want to look.";else if(updatePhase==UpdatePhase.Idle)updateNote="Automatic checks are on. The next one runs when Flutter Deck starts.";}
    catch{updateNote="That choice could not be saved, so it applies to this session only.";sessionLog.Add("preferences-save-failed");}
    UpdateChanged();
  }
  async void CheckForUpdate(bool manual){
    if(updateBusy||updateClosed||updateRestartPending)return;updateBusy=true;updatePhase=UpdatePhase.Checking;updateNote="Checking for a newer Flutter Deck...";UpdateChanged();
    try{
      var found=await System.Threading.Tasks.Task.Run(delegate{return Updater.Check();});
      if(Updater.Offers(found.Info,Updater.Sequence,updatePrefs.Hold,manual)){updateFound=found;updatePhase=UpdatePhase.Available;updateNote="Flutter Deck "+found.Info.Version+" is available. Nothing is installed until you choose Download and install.";sessionLog.Add("update-available");}
      else{updateFound=null;updatePhase=UpdatePhase.Current;updateNote=found.Info.Sequence>Updater.Sequence?"A newer release exists, but you rolled back from it. Use Check now to see it again.":"You have the latest published version. Checked "+DateTime.Now.ToString("g")+".";}
    }
    catch(System.IO.InvalidDataException){updateFound=null;updatePhase=UpdatePhase.Untrusted;updateNote="The published update information failed its signature or format check, so it was ignored. Your launcher is unchanged. If a release was published in the last few minutes, try again shortly.";sessionLog.Add("update-check-failed");}
    catch(Exception){updateFound=null;updatePhase=UpdatePhase.Unreachable;updateNote="Could not reach the update source. You may be offline, or no public release exists yet. Your launcher is unchanged.";sessionLog.Add("update-check-failed");}
    finally{updateBusy=false;UpdateChanged();}
  }
  async void InstallUpdate(){
    if(updateBusy||updateClosed||updateRestartPending||updateFound==null)return;var found=updateFound;string exe=ExePath,staged=Updater.StagedPath(exe);
    updateBusy=true;updatePhase=UpdatePhase.Downloading;updateNote="Downloading Flutter Deck "+found.Info.Version+"...";UpdateChanged();
    try{
      await System.Threading.Tasks.Task.Run(new Action(delegate{Updater.Download(found.Info,staged);}));if(updateClosed)return;Updater.Install(exe,staged,found.Manifest,found.Signature,Updater.PublicKey,Updater.Sequence);updateRestartPending=true;updatePendingSequence=found.Info.Sequence;
      updatePhase=UpdatePhase.Ready;updateNote="Flutter Deck "+found.Info.Version+" is installed. Choose Restart now to use it. The previous version is kept so you can roll back.";sessionLog.Add("update-installed");
    }
    catch(UnauthorizedAccessException){updatePhase=UpdatePhase.InstallFailed;updateNote="Flutter Deck could not replace its own file, so the update was not installed. The folder may not be writable (for example Program Files) or the file may be read-only. Your launcher still works.";}
    catch(System.IO.InvalidDataException){updatePhase=UpdatePhase.InstallFailed;updateNote="The download did not match the signed release, so it was discarded. Your launcher is unchanged.";}
    catch(Exception){updatePhase=UpdatePhase.InstallFailed;updateNote="The update could not be downloaded or installed. Your launcher still works. You can try again.";}
    finally{updateBusy=false;if(updatePhase==UpdatePhase.InstallFailed){Updater.CleanUp(exe);sessionLog.Add("update-install-failed");}UpdateChanged();}
  }
  void RestartForUpdate(){if(updateBusy||!updateRestartPending)return;try{Updater.Relaunch(ExePath,DeckPaths.RestartArguments);Close();}catch{updateNote="Flutter Deck could not restart itself. Close it and open it again to finish.";UpdateChanged();}}
  void RollBackUpdate(){
    if(updateBusy||!Updater.CanRollBack(ExePath))return;
    if(MessageBox.Show(this,"Put back the previous Flutter Deck version? Your settings, games and saves are not touched.","Flutter Deck - Roll back",MessageBoxButton.OKCancel,MessageBoxImage.Question)!=MessageBoxResult.OK)return;
    bool beforeRestart=updateRestartPending&&updatePendingSequence>0;
    try{
      Updater.RollBack(ExePath);updatePrefs.Hold=beforeRestart?updatePendingSequence:Updater.Sequence;try{Updater.SavePrefs(Updater.PrefsPath,updatePrefs);}catch{sessionLog.Add("preferences-save-failed");}
      sessionLog.Add("update-rolled-back");updateFound=null;
      if(beforeRestart){updateRestartPending=false;updatePendingSequence=0;Updater.CleanUp(ExePath);updatePhase=UpdatePhase.Current;updateNote="The update was undone before it was used. You are still on "+Updater.Version+". That release will not be offered again automatically.";}
      else{updateRestartPending=true;updatePendingSequence=0;updatePhase=UpdatePhase.Ready;updateNote="The previous version is back in place. Choose Restart now to use it. The release you left will not be offered again automatically.";}
    }
    catch{updateNote="The previous version could not be put back. Nothing was changed.";}
    UpdateChanged();
  }
  [STAThread]public static int Main(string[] args){try{DeckPaths.Configure(args);new Application().Run(new Deck());return 0;}catch(Exception){MessageBox.Show("Flutter Deck could not open. Your source game and BIOS files were not modified.","Flutter Deck error");return 1;}}
}
