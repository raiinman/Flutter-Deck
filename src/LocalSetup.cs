using System;
using System.IO;
using System.Xml;
using System.Text.RegularExpressions;

public sealed class SetupState {
  public const int Games=3;   // Mega Man Legends, Mega Man Legends 2, The Misadventures of Tron Bonne
  public readonly string[] Game={"","",""};
  public readonly string[] Bios={"","",""};
}
public static class LocalSetup {
  public static SetupState Copy(SetupState state){var copy=new SetupState();for(int i=0;i<SetupState.Games;i++){copy.Game[i]=state.Game[i];copy.Bios[i]=state.Bios[i];}return copy;}
  public static SetupState MergeSelected(SetupState saved,SetupState draft,int target){if(target<0||target>=SetupState.Games)throw new ArgumentOutOfRangeException("target");var result=Copy(saved);result.Game[target]=draft.Game[target];result.Bios[target]=draft.Bios[target];return result;}
  public static string ConfigPath {get{return Path.Combine(DeckPaths.Root,"setup.xml");}}
  public static string ReferenceStatus(string game,string bios){
    if(String.IsNullOrWhiteSpace(game)||String.IsNullOrWhiteSpace(bios))return "Setup needed: select your game and BIOS.";
    try{
      if(!File.Exists(game)||!File.Exists(bios))return "A saved file is missing or moved. Reopen Setup to locate it.";
      string problem=CheckGame(game);if(problem.Length==0)problem=CheckBios(bios);
      return problem.Length==0?"References ready; native runtime not connected.":"Reference needs attention: "+problem;
    }catch{return "Reference could not be checked. Check access and reopen Setup.";}
  }
  public static string CheckGame(string path){
    if(String.IsNullOrWhiteSpace(path)||!File.Exists(path))return "Select an existing game image.";
    var f=new FileInfo(path);string ext=f.Extension.ToLowerInvariant();
    if(ext!=".cue"&&ext!=".bin"&&ext!=".iso")return "Choose CUE, BIN or ISO; executable files are not launched.";
    if(f.Length==0)return "Game image is empty.";
    if(ext!=".cue")return "";
    if(f.Length>65536)return "CUE exceeds the 64 KiB metadata limit.";
    string text=File.ReadAllText(path);var matches=Regex.Matches(text,"^\\s*FILE\\s+(?:\"([^\"]+)\"|([^\\s]+))\\s+",RegexOptions.Multiline|RegexOptions.IgnoreCase);
    if(matches.Count==0)return "CUE contains no track references.";
    string folder=Path.GetFullPath(Path.GetDirectoryName(path)).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
    foreach(Match match in matches){string name=match.Groups[1].Success?match.Groups[1].Value:match.Groups[2].Value;
      if(Path.IsPathRooted(name)||name.IndexOf(':')>=0||Path.GetFileName(name)!=name)return "CUE track paths must be relative.";
      string track=Path.GetFullPath(Path.Combine(folder,name));
      if(!track.StartsWith(folder,StringComparison.OrdinalIgnoreCase))return "CUE track escapes its directory.";
      if(!File.Exists(track))return "A CUE track is missing.";
      if((File.GetAttributes(track)&FileAttributes.ReparsePoint)!=0)return "Linked CUE tracks are not accepted by this preview.";
    }
    return "";
  }
  public static string CheckBios(string path){if(String.IsNullOrWhiteSpace(path)||!File.Exists(path))return "Select an existing BIOS file.";var f=new FileInfo(path);string ext=f.Extension.ToLowerInvariant();if(ext!=".bin"&&ext!=".rom")return "Choose a BIN or ROM BIOS candidate.";return f.Length==524288?"":"BIOS candidate must be 524,288 bytes for this PS1 shape check.";}
  public static SetupState Load(string path){var state=new SetupState();if(!File.Exists(path))return state;var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=32768};var doc=new XmlDocument{XmlResolver=null};using(var reader=XmlReader.Create(path,settings))doc.Load(reader);if(doc.DocumentElement==null||doc.DocumentElement.Name!="flutterDeck"||doc.DocumentElement.GetAttribute("schema")!="1")throw new InvalidDataException("Unsupported setup configuration.");foreach(XmlNode node in doc.DocumentElement.SelectNodes("game")){int n;if(node.Attributes["id"]==null||!Int32.TryParse(node.Attributes["id"].Value,out n)||n<1||n>SetupState.Games)continue;state.Game[n-1]=node.Attributes["image"]==null?"":node.Attributes["image"].Value;state.Bios[n-1]=node.Attributes["bios"]==null?"":node.Attributes["bios"].Value;}return state;}
  public static void Save(string path,SetupState state){string folder=Path.GetDirectoryName(path);Directory.CreateDirectory(folder);string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{var settings=new XmlWriterSettings{Indent=true};using(var writer=XmlWriter.Create(temp,settings)){writer.WriteStartElement("flutterDeck");writer.WriteAttributeString("schema","1");for(int i=0;i<SetupState.Games;i++){writer.WriteStartElement("game");writer.WriteAttributeString("id",(i+1).ToString());writer.WriteAttributeString("image",state.Game[i]);writer.WriteAttributeString("bios",state.Bios[i]);writer.WriteEndElement();}writer.WriteEndElement();}if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
}
