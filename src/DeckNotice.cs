using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

// Big notices: a large window the player answers with one of two buttons. This is the same window class as Program.cs, in its own file.
// Rules of this file:
//  - A notice has no close box. Only its buttons give an answer (two, or one for a notice that is only read). A notice that is closed another way (Alt+F4, or Esc where allowed) gives no answer, and the caller changes nothing.
//  - The headline and the buttons do not scroll: only the text between them does, and it starts at its top. The keyboard scrolls it with Up, Down, Page Up, Page Down, Home and End.
//  - The notice fits the screen the launcher is on, and is kept inside it.
//  - The texts are not in this file: the caller passes them, so tests can read them without a window.
public partial class Deck {
  ScrollViewer bigNoticeText;   // the scrolling text of the notice that is open
  // The content of a big notice: a headline and a lead line, parts (each a heading and a text) that scroll, and the two buttons side by side. second null: one button over the whole width.
  Grid BigNoticeContent(string head,string lead,string[][] parts,Button first,Button second){
    var g=new Grid{Margin=new Thickness(40,28,40,28)};
    g.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});g.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});g.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
    var top=new StackPanel();
    var h=Label(head,32);h.FontWeight=FontWeights.Bold;h.Margin=new Thickness(0,0,0,8);Id(h,"notice-head");top.Children.Add(h);
    var l=Label(lead,21,"#FFD27A");l.FontWeight=FontWeights.SemiBold;l.Margin=new Thickness(0,0,0,6);Id(l,"notice-lead");top.Children.Add(l);
    g.Children.Add(top);
    var body=new StackPanel{Margin=new Thickness(0,0,14,0)};
    for(int i=0;i<parts.Length;i++){
      var t=Label(parts[i][0],20,"#5ECDEB");t.FontWeight=FontWeights.SemiBold;t.Margin=new Thickness(0,12,0,2);body.Children.Add(t);
      var b=Label(parts[i][1],18);b.Margin=new Thickness(0,0,0,2);b.LineHeight=26;Id(b,"notice-part-"+i.ToString(System.Globalization.CultureInfo.InvariantCulture));body.Children.Add(b);
    }
    var scroll=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Focusable=false};Id(scroll,"notice-text");bigNoticeText=scroll;Grid.SetRow(scroll,1);g.Children.Add(scroll);
    var bar=new Grid{Margin=new Thickness(0,22,0,0)};bar.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});bar.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
    foreach(var button in new[]{first,second}){if(button==null)continue;button.FontSize=22;button.MinHeight=64;button.FontWeight=FontWeights.SemiBold;}
    Id(first,"notice-first");bar.Children.Add(first);
    if(second==null){first.Margin=new Thickness(0);Grid.SetColumnSpan(first,2);}
    else{first.Margin=new Thickness(0,0,10,0);second.Margin=new Thickness(10,0,0,0);Id(second,"notice-second");Grid.SetColumn(second,1);bar.Children.Add(second);}
    Grid.SetRow(bar,2);g.Children.Add(bar);
    return g;
  }
  // The usable area of the screen the launcher is on, in the units WPF uses. The primary screen's area when it cannot be read.
  Rect NoticeArea(){
    var area=SystemParameters.WorkArea;
    try{
      var handle=new System.Windows.Interop.WindowInteropHelper(this).Handle;var source=PresentationSource.FromVisual(this);
      if(handle!=IntPtr.Zero&&source!=null&&source.CompositionTarget!=null){
        var m=source.CompositionTarget.TransformToDevice;var wa=System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        if(m.M11>0&&m.M22>0&&wa.Width>0&&wa.Height>0)area=new Rect(wa.Left/m.M11,wa.Top/m.M22,wa.Width/m.M11,wa.Height/m.M22);
      }
    }catch(Exception){}
    return area;
  }
  // Shows a big notice and waits for the answer. true: the first button. false: the second button. null: closed without an answer. secondLabel null: a notice with one button.
  // firstFocused: which button has the keyboard at the start. escapeCloses: Esc closes the notice without an answer.
  bool? ShowBigNotice(string title,string head,string lead,string[][] parts,string firstLabel,string secondLabel,bool firstFocused,bool escapeCloses,string id){
    bool? answer=null;Window w=null;
    var first=Control(firstLabel,delegate{answer=true;w.Close();});var second=secondLabel==null?null:Control(secondLabel,delegate{answer=false;w.Close();});
    var focused=firstFocused||second==null?first:second;focused.BorderBrush=Ink("#5ECDEB");focused.BorderThickness=new Thickness(2);
    var area=NoticeArea();
    var frame=new Border{BorderBrush=Ink("#5ECDEB"),BorderThickness=new Thickness(3),Background=Ink("#0E2233"),Child=BigNoticeContent(head,lead,parts,first,second)};
    w=new Window{Owner=this,Title=title,Width=Math.Max(320,Math.Min(980,area.Width-20)),MaxHeight=Math.Max(240,area.Height-20),SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStyle=WindowStyle.None,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Ink("#0E2233"),ShowInTaskbar=false,Content=frame};
    Id(w,id);
    w.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){if(e.ButtonState==MouseButtonState.Pressed)try{w.DragMove();}catch(InvalidOperationException){}};   // no title bar: the notice is moved by its body
    var text=bigNoticeText;
    w.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){
      if(e.Key==Key.Escape){e.Handled=true;if(escapeCloses)w.Close();return;}
      if(text==null||Keyboard.Modifiers!=ModifierKeys.None)return;   // the buttons have the keyboard and are outside the text, so the window scrolls the text for them
      if(e.Key==Key.Down)text.ScrollToVerticalOffset(text.VerticalOffset+48);else if(e.Key==Key.Up)text.ScrollToVerticalOffset(text.VerticalOffset-48);else if(e.Key==Key.PageDown)text.PageDown();else if(e.Key==Key.PageUp)text.PageUp();else if(e.Key==Key.Home)text.ScrollToHome();else if(e.Key==Key.End)text.ScrollToEnd();else return;
      e.Handled=true;
    };
    w.Loaded+=delegate{   // kept inside the screen the launcher is on
      if(w.Left+w.ActualWidth>area.Right-10)w.Left=area.Right-10-w.ActualWidth;if(w.Left<area.Left+10)w.Left=area.Left+10;
      if(w.Top+w.ActualHeight>area.Bottom-10)w.Top=area.Bottom-10-w.ActualHeight;if(w.Top<area.Top+10)w.Top=area.Top+10;
    };
    w.ContentRendered+=delegate{focused.Focus();};
    w.ShowDialog();   // every launcher window waits for the answer
    return answer;
  }
  // Game contract 2.42: the diagnostics recorder is on by default, so the player is told in a big notice and chooses.
  // reading: opened again from Settings (the first time it is shown before Play). on: the recorder's state now. The first button always means "recording on".
  bool? ShowDiagnosticsNotice(bool reading,bool on){
    string[] buttons=GameRuntime.DiagnosticsNoticeButtons(reading,on);
    return ShowBigNotice(GameRuntime.DiagnosticsNoticeTitle,GameRuntime.DiagnosticsNoticeHead,GameRuntime.DiagnosticsNoticeLead(reading,on),GameRuntime.DiagnosticsNoticeParts,buttons[0],buttons[1],GameRuntime.DiagnosticsNoticeFirstFocused(reading,on),reading,"diagnostics-notice");
  }
  // The guide "How to use the new features": only read, one Close button, Esc closes it. first: shown by itself before Play.
  void ShowGuide(int topics,bool first){
    var parts=GameRuntime.GuideParts(topics);if(parts.Length==0)return;
    ShowBigNotice(GameRuntime.GuideTitle,GameRuntime.GuideHead,GameRuntime.GuideLead(first),parts,GameRuntime.GuideClose,null,true,true,"feature-guide");
  }
  // True when this Flutter Deck has a newer builder than the one that built the game program in use (the same rule as the builder line of the Settings screen).
  int GuideBuilderKit(){var g=BuildCore.Game(Kind.Id);return g==null?0:BuildToolsNow(g).KitVersion;}   // the builder version this Flutter Deck has for the chosen game; 0 when it has none
  bool GuideBuilderNewer(){var g=BuildCore.Game(Kind.Id);return g!=null&&BuildCore.BuilderIsNewer(BuildCore.ReadMarker(BuildFolderOf(g)),g,BuildToolsNow(g).KitVersion,playCheck.Hash);}
}
