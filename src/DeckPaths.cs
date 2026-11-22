using System;
using System.IO;
// One data root for portable runs. No directory is created until an existing
// settings owner saves its own file. --data-dir never redirects program binaries.
public static class DeckPaths {
  static string alternate;
  public static bool IsPortable {get{return alternate!=null;}}
  public static string Root {get{return alternate??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FlutterDeck");}}
  public static void Configure(string[] args){
    if(args==null||args.Length==0)return;
    if(args.Length!=2||args[0]!="--data-dir")throw new ArgumentException("Use --data-dir with one absolute local folder.");
    string value=args[1];
    if(String.IsNullOrWhiteSpace(value)||value.Length<3||!Char.IsLetter(value[0])||value[1]!=':'||(value[2]!='\\'&&value[2]!='/')||value.StartsWith("\\\\"))throw new ArgumentException("The data folder must be an absolute local path.");
    alternate=Path.GetFullPath(value);
  }
  // What a launcher that restarts itself (after an update or a rollback) gives the new copy, so that it keeps the
  // same data folder: "" for the usual folder, else the same --data-dir option. A path cannot hold a quote sign; a
  // backslash before the closing quote is doubled, as Windows command lines need.
  public static string RestartArguments {get{
    if(alternate==null)return "";
    if(alternate.IndexOf('"')>=0)throw new InvalidOperationException("The data folder cannot be passed on.");
    int tail=0;while(tail<alternate.Length&&alternate[alternate.Length-1-tail]=='\\')tail++;
    return "--data-dir \""+alternate+new string('\\',tail)+"\"";
  }}
}
