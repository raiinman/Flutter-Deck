using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

// Multiplayer hosting: asks the player's own router to send one UDP port to this PC (UPnP Internet Gateway Device), and
// takes the rule away again. SPEC-multiplayer.md; game-side contract MP-1 section 5.
// Rules of this file (check-multiplayer.ps1 reads it):
//  - It talks only to the PC's own gateway and to one second box behind it, both at a local (private) address.
//    The public address comes from those boxes. Nothing on the internet is contacted.
//  - A box is believed only on its own address: an answer from another address, a page on another address, a redirect
//    and a proxy are refused.
//  - It adds a rule only for a port that no other device holds, and removes a rule only while it still points at the
//    address this launcher asked for.
//  - It never sees the key or the invite code, changes no firewall rule, starts no program and writes no file.
public sealed class RouterBox { public string Address="",Control="",Service=""; }
public sealed class HostRoute {
  public string Pc="",Gateway="",SecondBox="",RouterOuter="",Public="";
  public string Stop="";    // hosting cannot work from this connection: the sentence for the screen
  public string Steps="";   // steps the player must do by hand
  public List<MpRule> Rules=new List<MpRule>();   // the rules that were made, in the order they were made
}
public static class RouterPort {
  internal static int SsdpPort=1900;          // the tests use a stand-in box on this PC with another port
  const int MaxAnswer=512*1024;
  const string Description="Flutter Deck multiplayer";
  static readonly Regex ServiceType=new Regex("^urn:schemas-upnp-org:service:WAN(IP|PPP)Connection:[12]$");

  // This PC's address on the network that leads to the internet, and that network's gateway. Sends nothing.
  public static bool LocalNetwork(out string pc,out string gateway){
    pc="";gateway="";
    try{
      string local;
      using(var probe=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp)){probe.Connect(new IPEndPoint(IPAddress.Parse("192.0.2.1"),9));local=((IPEndPoint)probe.LocalEndPoint).Address.ToString();} // choosing a route sends no packet
      foreach(var card in NetworkInterface.GetAllNetworkInterfaces()){
        if(card.OperationalStatus!=OperationalStatus.Up)continue;
        var properties=card.GetIPProperties();bool mine=false;
        foreach(var address in properties.UnicastAddresses)if(address.Address.AddressFamily==AddressFamily.InterNetwork&&address.Address.ToString()==local)mine=true;
        if(!mine)continue;
        foreach(var way in properties.GatewayAddresses)if(way.Address.AddressFamily==AddressFamily.InterNetwork&&way.Address.ToString()!="0.0.0.0"){pc=local;gateway=way.Address.ToString();return true;}
      }
    }catch(Exception){}
    return false;
  }

  // ---- finding the box's port service ----
  // direct: ask the box itself only (the second box is not on this PC's own network).
  public static RouterBox Find(string box,bool direct){
    string location=null;DateTime deadline=DateTime.UtcNow.AddSeconds(6);
    for(int round=0;round<3&&location==null&&DateTime.UtcNow<deadline;round++){
      using(var udp=new UdpClient(AddressFamily.InterNetwork)){
        try{
          foreach(string kind in new[]{"urn:schemas-upnp-org:device:InternetGatewayDevice:1","urn:schemas-upnp-org:device:InternetGatewayDevice:2","upnp:rootdevice"}){
            byte[] ask=Encoding.ASCII.GetBytes("M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 1\r\nST: "+kind+"\r\n\r\n");
            udp.Send(ask,ask.Length,new IPEndPoint(IPAddress.Parse(box),SsdpPort));
            if(!direct&&SsdpPort==1900)udp.Send(ask,ask.Length,new IPEndPoint(IPAddress.Parse("239.255.255.250"),1900));
          }
          DateTime roundEnd=DateTime.UtcNow.AddMilliseconds(1800);
          while(location==null){
            int left=(int)(roundEnd-DateTime.UtcNow).TotalMilliseconds;if(left<=0)break;   // other devices' answers cannot keep the wait alive
            udp.Client.ReceiveTimeout=left;
            var from=new IPEndPoint(IPAddress.Any,0);byte[] answer=udp.Receive(ref from);
            if(from.Address.ToString()!=box||answer.Length>4096)continue;
            var match=Regex.Match(Encoding.ASCII.GetString(answer),@"(?im)^LOCATION:\s*(\S+)");
            if(match.Success)location=match.Groups[1].Value;
          }
        }catch(Exception){}
      }
    }
    if(location==null)return null;
    Uri page;if(!Uri.TryCreate(location,UriKind.Absolute,out page)||page.Scheme!="http"||page.Host!=box)return null;
    int code;string text;
    try{text=Http(page.AbsoluteUri,null,null,out code);}catch(Exception){return null;}
    if(code!=200)return null;
    XmlDocument document;try{document=Parse(text);}catch(Exception){return null;}
    foreach(XmlNode service in document.SelectNodes("//*[local-name()='service']")){
      XmlNode type=service.SelectSingleNode("*[local-name()='serviceType']"),control=service.SelectSingleNode("*[local-name()='controlURL']");
      if(type==null||control==null||!ServiceType.IsMatch(type.InnerText.Trim()))continue;
      Uri full;if(!Uri.TryCreate(page,control.InnerText.Trim(),out full)||full.Scheme!="http"||full.Host!=box)return null;
      return new RouterBox{Address=box,Control=full.AbsoluteUri,Service=type.InnerText.Trim()};
    }
    return null;
  }
  public static RouterBox BoxOf(MpRule rule){return new RouterBox{Address=rule.Box,Control=rule.Control,Service=rule.Service};}

  // ---- one question to a box. No redirect, no proxy, a bounded answer. ----
  static string Http(string url,string action,string body,out int code){
    var request=(HttpWebRequest)WebRequest.Create(url);
    request.Proxy=null;request.AllowAutoRedirect=false;request.Timeout=8000;request.ReadWriteTimeout=8000;request.KeepAlive=false;request.UseDefaultCredentials=false;
    if(body!=null){
      byte[] bytes=new UTF8Encoding(false).GetBytes(body);
      request.Method="POST";request.ContentType="text/xml; charset=\"utf-8\"";request.Headers["SOAPAction"]=action;request.ContentLength=bytes.Length;
      using(var up=request.GetRequestStream())up.Write(bytes,0,bytes.Length);
    }
    HttpWebResponse response;
    try{response=(HttpWebResponse)request.GetResponse();}
    catch(WebException e){response=e.Response as HttpWebResponse;if(response==null)throw;}
    using(response){
      code=(int)response.StatusCode;
      using(var down=response.GetResponseStream()){
        var all=new MemoryStream();var buffer=new byte[8192];int n;
        while((n=down.Read(buffer,0,buffer.Length))>0){all.Write(buffer,0,n);if(all.Length>MaxAnswer)throw new InvalidDataException("The box sent too much.");}
        return Encoding.UTF8.GetString(all.ToArray());
      }
    }
  }
  static XmlDocument Parse(string text){
    var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaxAnswer};
    var document=new XmlDocument{XmlResolver=null};
    using(var reader=XmlReader.Create(new StringReader(text),settings))document.Load(reader);
    return document;
  }
  // Returns the answer document. fault: the box's error number when it refused (0: it did not refuse).
  static XmlDocument Ask(RouterBox box,string action,string[] fields,out int fault){
    fault=0;
    Uri control;if(!Uri.TryCreate(box.Control,UriKind.Absolute,out control)||control.Scheme!="http"||control.Host!=box.Address||!ServiceType.IsMatch(box.Service))throw new InvalidOperationException("The box's address does not fit.");
    var inner=new StringBuilder();
    for(int i=0;i+1<fields.Length;i+=2)inner.Append('<').Append(fields[i]).Append('>').Append(System.Security.SecurityElement.Escape(fields[i+1])).Append("</").Append(fields[i]).Append('>');
    string body="<?xml version=\"1.0\"?><s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\"><s:Body><u:"+action+" xmlns:u=\""+box.Service+"\">"+inner+"</u:"+action+"></s:Body></s:Envelope>";
    int code;string text=Http(box.Control,"\""+box.Service+"#"+action+"\"",body,out code);
    if(code==200)return Parse(text);
    fault=-1;
    try{var node=Parse(text).SelectSingleNode("//*[local-name()='errorCode']");int n;if(node!=null&&Int32.TryParse(node.InnerText.Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out n)&&n>0)fault=n;}catch(Exception){}
    return null;
  }
  static string Field(XmlDocument document,string name){var node=document==null?null:document.SelectSingleNode("//*[local-name()='"+name+"']");return node==null?"":node.InnerText.Trim();}
  static string P(int port){return port.ToString(CultureInfo.InvariantCulture);}

  // The box's address on its internet side, or "" when it does not tell.
  public static string PublicAddress(RouterBox box){
    try{int fault;string text=Field(Ask(box,"GetExternalIPAddress",new string[0],out fault),"NewExternalIPAddress");byte[] a;return MpCore.TryAddress(text,out a)?MpCore.Dotted(a):"";}
    catch(Exception){return "";}
  }
  // Which device the box sends the UDP port to: "" when there is no rule. Throws when the box does not answer this clearly.
  public static string Owner(RouterBox box,int port){
    int fault;var answer=Ask(box,"GetSpecificPortMappingEntry",new[]{"NewRemoteHost","","NewExternalPort",P(port),"NewProtocol","UDP"},out fault);
    if(answer!=null){string who=Field(answer,"NewInternalClient");if(who.Length==0)throw new InvalidOperationException("The box did not say who holds the port.");return who;}
    if(fault==714||fault==713)return "";   // "no such entry"
    throw new InvalidOperationException("The box did not say who holds the port.");
  }
  // Adds the rule. Returns the time limit in seconds that the box took (0: none), or -1 when it refused.
  public static int Add(RouterBox box,int port,string client,out string error){
    error="";
    foreach(int lease in new[]{MpCore.LeaseSeconds,0}){   // some boxes take only rules without a time limit
      try{
        int fault;var answer=Ask(box,"AddPortMapping",new[]{"NewRemoteHost","","NewExternalPort",P(port),"NewProtocol","UDP","NewInternalPort",P(port),"NewInternalClient",client,"NewEnabled","1","NewPortMappingDescription",Description,"NewLeaseDuration",P(lease)},out fault);
        if(answer!=null)return lease;
        error=fault>0?"the box answered with error "+P(fault):"the box refused";
      }catch(Exception){error="the box did not answer";}
    }
    return -1;
  }
  // Removes the rule for the port, but only while it sends the port to client. Returns true when the rule was removed and
  // false when the box holds no rule that points at client. Throws when the box does not answer clearly or refuses: then the
  // rule may still be there, and the caller keeps its record.
  public static bool Remove(RouterBox box,int port,string client){
    if(Owner(box,port)!=client)return false;
    int fault;if(Ask(box,"DeletePortMapping",new[]{"NewRemoteHost","","NewExternalPort",P(port),"NewProtocol","UDP"},out fault)==null)throw new InvalidOperationException("The box refused to remove the rule.");
    return true;
  }
  // An internet address that a box told and that a game can be reached at. "0.0.0.0" and the like mean "not told".
  static bool Told(string text,out byte[] a){return MpCore.TryAddress(text,out a)&&MpCore.Usable(a)&&!MpCore.IsLoopback(a);}

  // The address of a second box between the router and the internet, or "". One echo packet that may travel two steps
  // only is sent toward 192.0.2.1 (a documentation address that nothing answers from); the second step answers.
  public static string SecondBox(string gateway){
    try{
      using(var ping=new Ping()){
        for(int attempt=0;attempt<2;attempt++){
          var reply=ping.Send(IPAddress.Parse("192.0.2.1"),700,new byte[8],new PingOptions(2,true));
          if(reply!=null&&reply.Status==IPStatus.TtlExpired&&reply.Address!=null){string hop=reply.Address.ToString();return hop!=gateway&&MpCore.IsBoxAddress(hop)?hop:"";}
        }
      }
    }catch(Exception){}
    return "";
  }

  // ---- hosting: open the port on the router, and on a second box when there is one ----
  // record is called before a rule is asked for, forget when the box did not take it.
  public static HostRoute Open(int port,Action<MpRule> record,Action<MpRule> forget){
    string pc,gateway;
    if(!LocalNetwork(out pc,out gateway))return new HostRoute{Stop="This PC has no network connection that leads to the internet."};
    if(!MpCore.IsBoxAddress(gateway)||!MpCore.IsPrivateText(pc))return new HostRoute{Pc=pc,Gateway=gateway,Steps="This PC is not behind a home router that Flutter Deck can ask. If this PC has a public address of its own, type it below."};
    return OpenAt(pc,gateway,port,SecondBox,record,forget);
  }
  internal static HostRoute OpenAt(string pc,string gateway,int port,Func<string,string> secondBox,Action<MpRule> record,Action<MpRule> forget){
    var route=new HostRoute{Pc=pc,Gateway=gateway};
    RouterBox first=null;try{first=Find(gateway,false);}catch(Exception){}
    string why="";
    if(first==null||!AddRule(route,first,port,pc,record,forget,out why)){route.Steps=MpCore.ManualSteps(gateway,pc,port);return route;}
    string outer=PublicAddress(first);byte[] a;
    if(!Told(outer,out a))return route;                              // the router does not tell: the player types the address
    if(MpCore.IsCarrier(a)){route.Stop=MpCore.CarrierText;return route;}
    if(!MpCore.IsPrivate(a)){route.Public=outer;return route;}
    // The router's own internet side is a private address: a second box is in front of it.
    route.RouterOuter=outer;
    string box="";try{box=secondBox(gateway)??"";}catch(Exception){}
    if(box.Length==0){route.Stop="Your router has a private address on its internet side, and no second box was found on the way out. Then your internet provider shares your address, and a friend cannot reach this PC directly. You can still join a friend who hosts.";return route;}
    route.SecondBox=box;
    RouterBox second=null;try{second=Find(box,true);}catch(Exception){}
    if(second==null||!AddRule(route,second,port,outer,record,forget,out why)){route.Steps=MpCore.SecondBoxSteps(box,outer,port);return route;}
    string far=PublicAddress(second);
    if(!Told(far,out a))return route;
    if(MpCore.IsPrivate(a)){route.Stop=MpCore.IsCarrier(a)?MpCore.CarrierText:"The second box also has a private address on its internet side. Then your internet provider shares your address, and a friend cannot reach this PC directly. You can still join a friend who hosts.";return route;}
    route.Public=far;return route;
  }
  static bool AddRule(HostRoute route,RouterBox box,int port,string client,Action<MpRule> record,Action<MpRule> forget,out string why){
    why="";
    string owner;try{owner=Owner(box,port);}catch(Exception){why="the box did not say who holds the port";return false;}
    if(owner.Length>0&&owner!=client){why="another device holds the port";return false;}   // some boxes replace a rule without a word
    var rule=new MpRule{Box=box.Address,Control=box.Control,Service=box.Service,Port=port,Client=client,MadeUtc=new DateTime(DateTime.UtcNow.Ticks/TimeSpan.TicksPerSecond*TimeSpan.TicksPerSecond,DateTimeKind.Utc),Lease=0};   // whole seconds, as the record keeps it. No time limit until the box confirmed one: a record must not end before its rule
    try{record(rule);}catch(Exception){why="the record of the rule could not be written";return false;}   // no record, no rule
    int lease=Add(box,port,client,out why);
    bool made=false;
    if(lease>=0){try{made=Owner(box,port)==client;}catch(Exception){made=false;}if(!made)why="the box did not keep the rule";}
    if(!made){
      // The record is dropped only when the box clearly holds no rule of ours any more. If it cannot say, or will not
      // remove it, the record stays and the rule is removed at a later start.
      try{Remove(box,port,client);forget(rule);}catch(Exception){}
      return false;
    }
    rule.Lease=lease;try{record(rule);}catch(Exception){}route.Rules.Add(rule);
    return true;
  }
  // Takes the rules away again, the outer box first. Returns one sentence for each rule that could not be removed.
  public static List<string> Close(List<MpRule> rules,Action<MpRule> forget){
    var said=new List<string>();
    for(int i=rules.Count-1;i>=0;i--){
      var rule=rules[i];
      try{Remove(BoxOf(rule),rule.Port,rule.Client);forget(rule);}   // removed, or the box holds no rule of ours: both end our part. A refusal throws.
      catch(Exception){said.Add("The rule for UDP port "+P(rule.Port)+" on the box at http://"+rule.Box+" could not be removed. "+(rule.Lease>0?"It ends by itself after 4 hours.":"This box sets no time limit: remove the rule on its page, or start Flutter Deck again while the box is reachable."));}
    }
    return said;
  }

  // ---- "Check my router": only looks. Changes nothing on any box. ----
  public static string Look(int port){
    string pc,gateway;
    if(!LocalNetwork(out pc,out gateway))return "This PC has no network connection that leads to the internet.";
    if(!MpCore.IsBoxAddress(gateway)||!MpCore.IsPrivateText(pc))return "This PC is not behind a home router that Flutter Deck can ask.";
    return LookAt(pc,gateway,port,SecondBox);
  }
  internal static string LookAt(string pc,string gateway,int port,Func<string,string> secondBox){
    RouterBox first=null;try{first=Find(gateway,false);}catch(Exception){}
    if(first==null)return "Your router (http://"+gateway+") does not offer to open ports for programs: UPnP is off or missing. Nothing was changed.\n\n"+MpCore.ManualSteps(gateway,pc,port);
    string start="Your router (http://"+gateway+") can open ports for programs. ";
    string outer=PublicAddress(first);byte[] a;
    if(!MpCore.TryAddress(outer,out a))return start+"It did not tell its internet address, so you would type it when you host. Nothing was changed.";
    if(MpCore.IsCarrier(a))return start+MpCore.CarrierText+" Nothing was changed.";
    if(!MpCore.IsPrivate(a))return start+"Its internet address is a public one: a friend can reach this PC once the port is open. Nothing was changed.";
    string box="";try{box=secondBox(gateway)??"";}catch(Exception){}
    if(box.Length==0)return start+"Its internet side has a private address, and no second box was found on the way out. Then your internet provider shares your address, and hosting cannot work from this connection. Nothing was changed.";
    RouterBox second=null;try{second=Find(box,true);}catch(Exception){}
    if(second==null)return start+"There is a second box at http://"+box+" that does not offer to open ports. Nothing was changed.\n\n"+MpCore.SecondBoxSteps(box,outer,port);
    string far=PublicAddress(second);
    if(!MpCore.TryAddress(far,out a))return start+"The second box at http://"+box+" can open ports too, but it did not tell its internet address, so you would type it when you host. Nothing was changed.";
    if(MpCore.IsPrivate(a))return start+"The second box at http://"+box+" also has a private internet address: your provider shares your address, and hosting cannot work from this connection. Nothing was changed.";
    return start+"The second box at http://"+box+" can open ports too, and its internet address is a public one: both ports can be opened automatically. Nothing was changed.";
  }
}
