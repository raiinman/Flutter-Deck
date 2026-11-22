using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

// Multiplayer for Mega Man Legends (SPEC-multiplayer.md; game-side contract MP-1). This file is pure: the invite code,
// the two settings the game reads, the status line the game writes, address kinds, the texts of the manual router steps,
// and the small record of router rules this launcher asked for. No window, no network, no program start.
// Rules of this file (check-multiplayer.ps1 reads it):
//  - The key and the invite code are never written to a file here. The record of router rules holds no key.
public sealed class MpInvite {
  public byte[] Address=new byte[4];public int Port;public byte[] Key=new byte[16];
  public string AddressText {get{return MpCore.Dotted(Address);}}
}
public sealed class MpStatus { public string State="";public int Joins;public bool Shown; }
// One port rule that this launcher asked a router box for. No key and no invite code.
public sealed class MpRule {
  public string Box="",Control="",Service="",Client="";public int Port;public DateTime MadeUtc=DateTime.MinValue;public int Lease;
}
public static class MpCore {
  public const string Alphabet="0123456789ABCDEFGHJKMNPQRSTVWXYZ";   // 32 signs; no I, L, O, U
  public const int DefaultPort=47400,Signs=39;
  public const string SettingName="RMX_MP",StatusName="RMX_MP_STATUS",Feature="multiplayer";
  public const int LeaseSeconds=14400,KeepRecordDays=60,MaxRules=8;
  public static string RulesPathName {get{return Path.Combine(DeckPaths.Root,"multiplayer-rules.txt");}}

  // ---- the invite code: "RMX-" and 39 signs for 24 bytes: version, IPv4 address, port, key, check byte ----
  static byte Check(byte[] b){int sum=0;for(int i=0;i<23;i++)sum=(sum*31+b[i])&255;return (byte)sum;}
  public static string Encode(byte[] address,int port,byte[] key){
    if(address==null||address.Length!=4)throw new ArgumentException("The address must be four numbers.");
    if(key==null||key.Length!=16)throw new ArgumentException("The key must be 16 bytes.");
    if(port<1024||port>65535)throw new ArgumentException("The port must be between 1024 and 65535.");
    var b=new byte[24];b[0]=1;Array.Copy(address,0,b,1,4);b[5]=(byte)(port>>8);b[6]=(byte)(port&255);Array.Copy(key,0,b,7,16);b[23]=Check(b);
    var s=new StringBuilder("RMX-");
    for(int i=0;i<Signs;i++){
      int v=0;
      for(int j=0;j<5;j++){int bit=i*5+j;v<<=1;if(bit<192&&(b[bit/8]&(0x80>>(bit%8)))!=0)v|=1;}
      if(i>0&&i%5==0)s.Append('-');
      s.Append(Alphabet[v]);
    }
    return s.ToString();
  }
  // Reads a code that a person typed or pasted. Throws FormatException with a sentence for the screen.
  public static MpInvite Decode(string code){
    var t=new StringBuilder();
    foreach(char raw in (code??"").ToUpperInvariant()){
      if(Char.IsWhiteSpace(raw)||raw=='-')continue;
      char c=raw=='O'?'0':(raw=='I'||raw=='L')?'1':raw;
      t.Append(c);
      if(t.Length>Signs+8)break;
    }
    string text=t.ToString();
    if(!text.StartsWith("RMX",StringComparison.Ordinal))throw new FormatException("The invite code must start with RMX.");
    text=text.Substring(3);
    if(text.Length!=Signs)throw new FormatException("The invite code has the wrong length ("+text.Length+" signs after RMX, "+Signs+" expected).");
    var b=new byte[24];
    for(int i=0;i<Signs;i++){
      int v=Alphabet.IndexOf(text[i]);
      if(v<0)throw new FormatException("The invite code holds a sign that is not allowed.");
      for(int j=0;j<5;j++){
        int bit=i*5+j;bool on=(v&(16>>j))!=0;
        if(bit<192){if(on)b[bit/8]|=(byte)(0x80>>(bit%8));}
        else if(on)throw new FormatException("The invite code is mistyped (the last sign does not fit).");
      }
    }
    if(Check(b)!=b[23])throw new FormatException("The invite code is mistyped (the check sign does not fit).");
    if(b[0]!=1)throw new FormatException("The invite code is from another version of Flutter Deck.");
    var invite=new MpInvite{Port=b[5]*256+b[6]};
    Array.Copy(b,1,invite.Address,0,4);Array.Copy(b,7,invite.Key,0,16);
    if(invite.Port<1024)throw new FormatException("The invite code holds a port that is not allowed.");
    if(!Usable(invite.Address))throw new FormatException("The invite code holds an address that cannot be reached.");
    return invite;
  }
  public static byte[] NewKey(){var key=new byte[16];using(var random=System.Security.Cryptography.RandomNumberGenerator.Create())random.GetBytes(key);return key;}
  public static string Hex(byte[] bytes){var s=new StringBuilder();foreach(byte x in bytes)s.Append(x.ToString("x2",CultureInfo.InvariantCulture));return s.ToString();}

  // ---- what the game reads (contract section 3). These strings hold the key: they go into the game's environment only. ----
  public static string HostSetting(int port,byte[] key){return "host:"+port.ToString(CultureInfo.InvariantCulture)+":"+Hex(key)+":any";}
  public static string JoinSetting(MpInvite invite){return "join:"+invite.AddressText+":"+invite.Port.ToString(CultureInfo.InvariantCulture)+":"+Hex(invite.Key);}

  // ---- what the game writes (contract section 6): "state=<word> joins=<n> shown=<0|1>" ----
  public static MpStatus ParseStatus(string line){
    if(line==null)return null;
    var status=new MpStatus();bool state=false;
    foreach(string part in line.Trim().Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries)){
      int at=part.IndexOf('=');if(at<1)continue;
      string name=part.Substring(0,at),value=part.Substring(at+1);
      if(name=="state"){if(value!="waiting"&&value!="joining"&&value!="connected"&&value!="silent")return null;status.State=value;state=true;}
      else if(name=="joins"){int n;if(Int32.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out n))status.Joins=n;}
      else if(name=="shown")status.Shown=value=="1";
    }
    return state?status:null;
  }
  public static string ReadStatus(string path){
    try{using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(stream)){var buffer=new char[200];int n=reader.Read(buffer,0,buffer.Length);return new string(buffer,0,n);}}
    catch(Exception){return null;}
  }
  // The sentence for the screen. host: this launcher started the hosting side.
  public static string StatusWords(MpStatus status,bool host){
    if(status==null)return host?"The game is starting. Your friend can use the code when the game is in play.":"The game is starting.";
    switch(status.State){
      case "waiting":return status.Joins>0?"Your friend left. The code still works while this game runs.":"Waiting for your friend to join.";
      case "joining":return "Looking for your friend's game. If this stays, the code is old, the host's port is closed, or the host's game is not running.";
      case "connected":return status.Shown?"Connected. You are in the same area and see each other.":"Connected. You see each other when both are in play in the same area.";
      case "silent":return "Your friend's game stopped answering. It may be loading, or it was closed.";
    }
    return "";
  }

  // ---- addresses ----
  public static string Dotted(byte[] a){return a[0].ToString(CultureInfo.InvariantCulture)+"."+a[1].ToString(CultureInfo.InvariantCulture)+"."+a[2].ToString(CultureInfo.InvariantCulture)+"."+a[3].ToString(CultureInfo.InvariantCulture);}
  // Only plain "1.2.3.4": no names, no hex, no short forms.
  public static bool TryAddress(string text,out byte[] address){
    address=new byte[4];
    string[] parts=(text??"").Trim().Split('.');if(parts.Length!=4)return false;
    for(int i=0;i<4;i++){
      string p=parts[i];if(p.Length<1||p.Length>3)return false;
      foreach(char c in p)if(c<'0'||c>'9')return false;
      int n=Int32.Parse(p,CultureInfo.InvariantCulture);if(n>255)return false;
      address[i]=(byte)n;
    }
    return true;
  }
  // The provider shares one public address between customers (100.64.0.0/10): nobody can reach in.
  public static bool IsCarrier(byte[] a){return a[0]==100&&a[1]>=64&&a[1]<=127;}
  public static bool IsLoopback(byte[] a){return a[0]==127;}
  // An address that is not on the public internet.
  public static bool IsPrivate(byte[] a){return a[0]==10||(a[0]==192&&a[1]==168)||(a[0]==172&&a[1]>=16&&a[1]<=31)||IsCarrier(a)||(a[0]==169&&a[1]==254)||a[0]==127;}
  // An address that a game can be sent to at all.
  public static bool Usable(byte[] a){return a[0]!=0&&a[0]<224;}
  public static bool IsPrivateText(string text){byte[] a;return TryAddress(text,out a)&&IsPrivate(a);}
  // A router box this launcher may talk to: on the local side, never the internet, never this PC itself.
  public static bool IsBoxAddress(string text){byte[] a;return TryAddress(text,out a)&&IsPrivate(a)&&!IsCarrier(a)&&!IsLoopback(a);}

  // ---- the manual steps, with the player's own addresses ----
  public static string ManualSteps(string gateway,string pc,int port){
    return "This router did not open the port by itself. Do these steps once:\n"+
      "1. Open a web browser and go to http://"+gateway+" (your router's page). Sign in with the router's password.\n"+
      "2. Find the page \"Port forwarding\" (also named \"Virtual server\" or \"NAT\").\n"+
      "3. Add one rule: Protocol UDP, external port "+port+", internal address "+pc+" (this PC), internal port "+port+".\n"+
      "4. Save the rule.\n"+
      "Tip: the router page often has a switch \"UPnP\". With it on, Flutter Deck can do steps 2 to 4 by itself.";
  }
  public static string SecondBoxSteps(string box,string routerOuter,int port){
    return "There is a second box between your router and the internet (often the modem of your internet provider). It did not open the port by itself. Do these steps once on that box:\n"+
      "1. Open a web browser and go to http://"+box+". Sign in with that box's password (often on a label on the box).\n"+
      "2. Find the page \"Port forwarding\" (also named \"Virtual server\", \"NAT\" or \"Port mapping\").\n"+
      "3. Add one rule: Protocol UDP, external port "+port+", internal address "+routerOuter+" (your own router, as the second box sees it), internal port "+port+".\n"+
      "4. Save the rule.\n"+
      "Other ways that also work: switch the second box to \"bridge mode\", or put your router in its \"DMZ\".";
  }
  public const string CarrierText="Your internet provider shares one public address between several customers. A friend cannot reach this PC directly, and no setting on your side changes that. Ask the provider for a public address of your own. You can still join a friend who hosts.";

  // ---- the record of router rules (multiplayer-rules.txt): the number 1, then one rule on each line, tabs between ----
  public static bool ValidRule(MpRule rule){
    if(rule==null||!IsBoxAddress(rule.Box)||!IsPrivateText(rule.Client)||rule.Port<1024||rule.Port>65535)return false;
    if(!System.Text.RegularExpressions.Regex.IsMatch(rule.Service??"","^urn:schemas-upnp-org:service:WAN(IP|PPP)Connection:[12]$"))return false;
    Uri control;if(!Uri.TryCreate(rule.Control??"",UriKind.Absolute,out control))return false;
    return control.Scheme=="http"&&control.Host==rule.Box&&(rule.Control.IndexOf('\t')<0)&&rule.Control.Length<=400;
  }
  public static string RuleLine(MpRule rule){
    return rule.Box+"\t"+rule.Control+"\t"+rule.Service+"\t"+rule.Port.ToString(CultureInfo.InvariantCulture)+"\t"+rule.Client+"\t"+rule.MadeUtc.ToString("yyyy-MM-ddTHH:mm:ssZ",CultureInfo.InvariantCulture)+"\t"+rule.Lease.ToString(CultureInfo.InvariantCulture);
  }
  public static MpRule ParseRule(string line){
    string[] f=(line??"").Split('\t');if(f.Length!=7)return null;
    var rule=new MpRule{Box=f[0],Control=f[1],Service=f[2],Client=f[4]};
    if(!Int32.TryParse(f[3],NumberStyles.None,CultureInfo.InvariantCulture,out rule.Port))return null;
    if(!DateTime.TryParseExact(f[5],"yyyy-MM-ddTHH:mm:ssZ",CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out rule.MadeUtc))return null;
    if(!Int32.TryParse(f[6],NumberStyles.None,CultureInfo.InvariantCulture,out rule.Lease))return null;
    return ValidRule(rule)?rule:null;
  }
  // A missing file is an empty list. A file that cannot be understood throws: the caller then leaves it alone.
  public static List<MpRule> LoadRules(string path){
    var rules=new List<MpRule>();
    if(!File.Exists(path))return rules;
    string[] lines=File.ReadAllLines(path);
    if(lines.Length==0||lines[0].Trim()!="1")throw new InvalidDataException("The record of router rules is not in a format this build knows.");
    for(int i=1;i<lines.Length&&rules.Count<MaxRules;i++){if(lines[i].Trim().Length==0)continue;var rule=ParseRule(lines[i]);if(rule!=null)rules.Add(rule);}
    return rules;
  }
  public static void SaveRules(string path,List<MpRule> rules){
    var text=new StringBuilder("1\r\n");int n=0;
    foreach(var rule in rules){if(!ValidRule(rule)||n>=MaxRules)continue;text.Append(RuleLine(rule)).Append("\r\n");n++;}
    if(n==0){if(File.Exists(path))File.Delete(path);return;}
    Directory.CreateDirectory(Path.GetDirectoryName(path));
    string temp=path+".part";
    try{File.WriteAllBytes(temp,new UTF8Encoding(false).GetBytes(text.ToString()));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
    finally{if(File.Exists(temp))File.Delete(temp);}
  }
  public static bool SameRule(MpRule a,MpRule b){return a.Box==b.Box&&a.Port==b.Port&&a.Client==b.Client;}
  // A rule with a time limit has ended by itself after that time. A rule without one stays on the box, so its record is
  // kept much longer: 60 days of starts in which the box could not be asked.
  public static bool RuleExpired(MpRule rule,DateTime nowUtc){
    if(rule.Lease>0)return nowUtc>rule.MadeUtc.AddSeconds(rule.Lease+600);
    return nowUtc>rule.MadeUtc.AddDays(KeepRecordDays);
  }
}
