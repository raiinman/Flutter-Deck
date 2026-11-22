using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

// Tests for MpCore.cs and RouterPort.cs. The router here is a stand-in that listens on this PC only (127.0.0.x).
// No real router, no real network and no game program is contacted or started.
public static class MpPureTest {
  static int failures,checks;
  static void Check(bool ok,string name){checks++;if(!ok){failures++;Console.WriteLine("FAIL: "+name);}}
  static void Refused(string code,string name){try{MpCore.Decode(code);failures++;Console.WriteLine("FAIL (accepted): "+name);}catch(FormatException){}checks++;}

  // ---- a stand-in router box on a loopback address ----
  sealed class FakeBox : IDisposable {
    public string Address,External="203.0.113.9",LocationHost,ControlPath="/ctl",DescriptionExtra="";
    public bool OnlyPermanent,RefuseAdd,Redirect,Forgets,OwnerUnclear,RefuseDelete,BreaksAfterAdd;
    public Dictionary<int,string> Rules=new Dictionary<int,string>();
    public List<string> Log=new List<string>();
    public int HttpPort;
    UdpClient udp;TcpListener tcp;volatile bool stop;Thread a,b;
    public FakeBox(string address,int ssdpPort){
      Address=address;LocationHost=address;
      tcp=new TcpListener(IPAddress.Parse(address),0);tcp.Start();HttpPort=((IPEndPoint)tcp.LocalEndpoint).Port;
      udp=new UdpClient(new IPEndPoint(IPAddress.Parse(address),ssdpPort));
      a=new Thread(Ssdp){IsBackground=true};b=new Thread(Web){IsBackground=true};a.Start();b.Start();
    }
    void Ssdp(){
      while(!stop){
        try{
          var from=new IPEndPoint(IPAddress.Any,0);byte[] ask=udp.Receive(ref from);
          if(!Encoding.ASCII.GetString(ask).StartsWith("M-SEARCH",StringComparison.Ordinal))continue;
          byte[] answer=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nCACHE-CONTROL: max-age=120\r\nST: upnp:rootdevice\r\nLOCATION: http://"+LocationHost+":"+HttpPort+"/desc.xml\r\n\r\n");
          udp.Send(answer,answer.Length,from);
        }catch(Exception){}
      }
    }
    void Web(){
      while(!stop){
        try{
          using(var client=tcp.AcceptTcpClient())using(var stream=client.GetStream()){
            stream.ReadTimeout=3000;
            var head=new StringBuilder();int c;
            while(!head.ToString().EndsWith("\r\n\r\n",StringComparison.Ordinal)&&(c=stream.ReadByte())>=0)head.Append((char)c);
            string text=head.ToString();int length=0;var m=Regex.Match(text,@"(?im)^Content-Length:\s*(\d+)");if(m.Success)length=Int32.Parse(m.Groups[1].Value);
            var body=new byte[length];int got=0;while(got<length){int n=stream.Read(body,got,length-got);if(n<=0)break;got+=n;}
            string answer;int code=200;string extra="";
            if(text.StartsWith("GET /desc.xml",StringComparison.Ordinal)){
              if(Redirect){code=302;extra="Location: http://127.0.0.9:"+HttpPort+"/desc.xml\r\n";answer="";}
              else answer="<?xml version=\"1.0\"?>"+DescriptionExtra+"<root xmlns=\"urn:schemas-upnp-org:device-1-0\"><device><deviceType>urn:schemas-upnp-org:device:InternetGatewayDevice:1</deviceType><serviceList><service><serviceType>urn:schemas-upnp-org:service:Layer3Forwarding:1</serviceType><controlURL>/l3f</controlURL></service></serviceList><deviceList><device><deviceList><device><serviceList><service><serviceType>urn:schemas-upnp-org:service:WANIPConnection:1</serviceType><controlURL>"+ControlPath+"</controlURL></service></serviceList></device></deviceList></device></deviceList></device></root>";
            }else if(text.StartsWith("POST /ctl",StringComparison.Ordinal)){
              string action=Regex.Match(text,@"(?im)^SOAPAction:\s*""[^#]+#(\w+)""").Groups[1].Value,sent=Encoding.UTF8.GetString(body);
              Func<string,string> field=delegate(string name){return Regex.Match(sent,"<"+name+">([^<]*)</"+name+">").Groups[1].Value;};
              lock(Log)Log.Add(action+(action=="AddPortMapping"?" "+field("NewExternalPort")+" "+field("NewProtocol")+" "+field("NewInternalClient")+" "+field("NewLeaseDuration"):""));
              int fault=0;string inner="";
              if(action=="GetExternalIPAddress")inner="<NewExternalIPAddress>"+External+"</NewExternalIPAddress>";
              else if(action=="GetSpecificPortMappingEntry"){
                string who;int port=Int32.Parse(field("NewExternalPort"));
                if(OwnerUnclear)fault=501;else lock(Rules){if(Rules.TryGetValue(port,out who))inner="<NewInternalPort>"+port+"</NewInternalPort><NewInternalClient>"+who+"</NewInternalClient>";else fault=714;}
              }else if(action=="AddPortMapping"){
                if(RefuseAdd)fault=718;else if(OnlyPermanent&&field("NewLeaseDuration")!="0")fault=725;
                else if(BreaksAfterAdd){lock(Rules)Rules[Int32.Parse(field("NewExternalPort"))]=field("NewInternalClient");OwnerUnclear=true;fault=501;}   // the rule is made, the answer says no, and the box is unclear from then on
                else if(!Forgets)lock(Rules)Rules[Int32.Parse(field("NewExternalPort"))]=field("NewInternalClient");
              }else if(action=="DeletePortMapping"){if(RefuseDelete)fault=606;else lock(Rules){if(!Rules.Remove(Int32.Parse(field("NewExternalPort"))))fault=714;}}
              else fault=401;
              if(fault==0)answer="<?xml version=\"1.0\"?><s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\"><s:Body><u:"+action+"Response xmlns:u=\"urn:schemas-upnp-org:service:WANIPConnection:1\">"+inner+"</u:"+action+"Response></s:Body></s:Envelope>";
              else{code=500;answer="<?xml version=\"1.0\"?><s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\"><s:Body><s:Fault><faultcode>s:Client</faultcode><faultstring>UPnPError</faultstring><detail><UPnPError xmlns=\"urn:schemas-upnp-org:control-1-0\"><errorCode>"+fault+"</errorCode><errorDescription>x</errorDescription></UPnPError></detail></s:Fault></s:Body></s:Envelope>";}
            }else{code=404;answer="";}
            byte[] bytes=Encoding.UTF8.GetBytes(answer);
            byte[] top=Encoding.ASCII.GetBytes("HTTP/1.1 "+code+" X\r\nContent-Type: text/xml\r\n"+extra+"Content-Length: "+bytes.Length+"\r\nConnection: close\r\n\r\n");
            stream.Write(top,0,top.Length);stream.Write(bytes,0,bytes.Length);stream.Flush();
          }
        }catch(Exception){}
      }
    }
    public int Count(string action){int n=0;lock(Log)foreach(string line in Log)if(line.StartsWith(action,StringComparison.Ordinal))n++;return n;}
    public void Dispose(){stop=true;try{udp.Close();}catch(Exception){}try{tcp.Stop();}catch(Exception){}}
  }
  static int FreeUdpPort(){using(var u=new UdpClient(new IPEndPoint(IPAddress.Parse("127.0.0.1"),0)))return ((IPEndPoint)u.Client.LocalEndPoint).Port;}

  static void Code(){
    var address=new byte[]{203,0,113,7};var key=new byte[16];for(int i=0;i<16;i++)key[i]=(byte)(i+1);
    const string example="RMX-075G0-W87Q4-M020G-30G2G-C1R81-450P3-0D1R7-H08R";
    Check(MpCore.Encode(address,47400,key)==example,"the example of the contract is written exactly: "+MpCore.Encode(address,47400,key));
    var read=MpCore.Decode(example);Check(read.AddressText=="203.0.113.7"&&read.Port==47400&&MpCore.Hex(read.Key)=="0102030405060708090a0b0c0d0e0f10","the example of the contract is read exactly");
    var typed=MpCore.Decode("  rmx 075go w87q4-mo2og 3og2g c1r81 45op3 od1r7 ho8r \r\n");Check(typed.AddressText=="203.0.113.7"&&typed.Port==47400,"small letters, spaces, missing dashes and the letter O for zero are read");
    Check(MpCore.Decode(example.Replace("1","L")).Port==47400&&MpCore.Decode(example.Replace("1","i")).Port==47400,"the letters L and I are read as the digit 1");
    var random=new Random(20261118);int same=0;
    for(int i=0;i<300;i++){
      var a=new byte[4];var k=new byte[16];random.NextBytes(a);random.NextBytes(k);a[0]=(byte)(1+a[0]%223);int port=1024+random.Next(64512);
      var back=MpCore.Decode(MpCore.Encode(a,port,k));if(back.AddressText==MpCore.Dotted(a)&&back.Port==port&&MpCore.Hex(back.Key)==MpCore.Hex(k))same++;
    }
    Check(same==300,"300 of 300 random codes come back the same: "+same);
    // every one-sign mistake and every swap of two neighbours in the example is refused
    string body=example.Replace("-","").Substring(3);int accepted=0,tried=0;
    for(int i=0;i<body.Length;i++)foreach(char c in MpCore.Alphabet){if(c==body[i])continue;tried++;try{MpCore.Decode("RMX"+body.Substring(0,i)+c+body.Substring(i+1));accepted++;}catch(FormatException){}}
    Check(accepted==0&&tried==39*31,"every one-sign mistake in the example is refused ("+accepted+" of "+tried+" accepted)");
    accepted=0;for(int i=0;i+1<body.Length;i++){if(body[i]==body[i+1])continue;try{MpCore.Decode("RMX"+body.Substring(0,i)+body[i+1]+body[i]+body.Substring(i+2));accepted++;}catch(FormatException){}}
    Check(accepted==0,"every swap of two neighbour signs in the example is refused ("+accepted+" accepted)");
    Refused("","an empty code");Refused(null,"no code");Refused("ABC-"+body,"a code that does not start with RMX");Refused("RMX-"+body.Substring(1),"a code that is too short");Refused(example+"0","a code that is too long");
    Refused(example.Replace('W','U'),"a sign outside the alphabet");Refused(new string('R',5000),"a very long text");
    // other versions, low ports and unusable addresses, each with a right check byte
    Func<byte,byte[],int,string> make=delegate(byte version,byte[] a,int port){
      var b=new byte[24];b[0]=version;Array.Copy(a,0,b,1,4);b[5]=(byte)(port>>8);b[6]=(byte)port;Array.Copy(key,0,b,7,16);int sum=0;for(int i=0;i<23;i++)sum=(sum*31+b[i])&255;b[23]=(byte)sum;
      var s=new StringBuilder("RMX");for(int i=0;i<39;i++){int v=0;for(int j=0;j<5;j++){int bit=i*5+j;v<<=1;if(bit<192&&(b[bit/8]&(0x80>>(bit%8)))!=0)v|=1;}s.Append(MpCore.Alphabet[v]);}return s.ToString();};
    Check(MpCore.Decode(make(1,address,47400)).Port==47400,"the test's own writer makes a code that is read");
    Refused(make(2,address,47400),"a code of version 2");Refused(make(1,address,1023),"a code with port 1023");Refused(make(1,new byte[]{0,0,0,0},47400),"a code with address 0.0.0.0");Refused(make(1,new byte[]{239,1,2,3},47400),"a code with a group address");
    string padded=make(1,address,47400);Refused(padded.Substring(0,padded.Length-1)+MpCore.Alphabet[MpCore.Alphabet.IndexOf(padded[padded.Length-1])|1],"a code whose last sign has spare bits set");
    try{MpCore.Encode(address,80,key);Check(false,"Encode refuses a low port");}catch(ArgumentException){Check(true,"");}
    try{MpCore.Encode(address,47400,new byte[8]);Check(false,"Encode refuses a short key");}catch(ArgumentException){Check(true,"");}
    Check(MpCore.HostSetting(47400,key)=="host:47400:0102030405060708090a0b0c0d0e0f10:any","the host setting is the one of contract section 3");
    Check(MpCore.JoinSetting(read)=="join:203.0.113.7:47400:0102030405060708090a0b0c0d0e0f10","the join setting is the one of contract section 3");
    byte[] k1=MpCore.NewKey(),k2=MpCore.NewKey();Check(k1.Length==16&&k2.Length==16&&MpCore.Hex(k1)!=MpCore.Hex(k2),"each hosting gets a new key of 16 bytes");
  }
  static void StatusAndAddresses(){
    var s=MpCore.ParseStatus("state=connected joins=2 shown=1\r\n");Check(s!=null&&s.State=="connected"&&s.Joins==2&&s.Shown,"a status line is read");
    s=MpCore.ParseStatus("state=waiting joins=0 shown=0");Check(s!=null&&s.State=="waiting"&&s.Joins==0&&!s.Shown,"a waiting line is read");
    Check(MpCore.ParseStatus("")==null&&MpCore.ParseStatus(null)==null&&MpCore.ParseStatus("state=other joins=1 shown=1")==null&&MpCore.ParseStatus("joins=1 shown=1")==null,"a line without a known state is not shown");
    Check(MpCore.ParseStatus("state=silent joins=x shown=7 more=1").State=="silent","unknown parts of a line are ignored");
    Check(MpCore.StatusWords(null,true).Contains("starting")&&MpCore.StatusWords(MpCore.ParseStatus("state=waiting joins=0 shown=0"),true).Contains("Waiting")&&MpCore.StatusWords(MpCore.ParseStatus("state=waiting joins=1 shown=0"),true).Contains("left")&&MpCore.StatusWords(MpCore.ParseStatus("state=connected joins=1 shown=1"),false).Contains("see each other")&&MpCore.StatusWords(MpCore.ParseStatus("state=joining joins=0 shown=0"),false).Contains("Looking")&&MpCore.StatusWords(MpCore.ParseStatus("state=silent joins=0 shown=0"),false).Contains("stopped answering"),"each state has its sentence");
    byte[] a;
    Check(MpCore.TryAddress("203.0.113.7",out a)&&a[0]==203&&a[3]==7&&MpCore.TryAddress(" 10.0.0.1 ",out a),"a plain address is read");
    foreach(string bad in new[]{"","1.2.3","1.2.3.4.5","256.1.1.1","1.2.3.x","0x7f.0.0.1","1..2.3","1.2.3.-4","example.com","1.2.3.4444","::1"})Check(!MpCore.TryAddress(bad,out a),"not an address: "+bad);
    Check(MpCore.IsPrivate(new byte[]{10,1,2,3})&&MpCore.IsPrivate(new byte[]{192,168,1,1})&&MpCore.IsPrivate(new byte[]{172,16,0,1})&&MpCore.IsPrivate(new byte[]{172,31,0,1})&&!MpCore.IsPrivate(new byte[]{172,32,0,1})&&MpCore.IsPrivate(new byte[]{100,64,0,1})&&MpCore.IsPrivate(new byte[]{169,254,1,1})&&MpCore.IsPrivate(new byte[]{127,0,0,1})&&!MpCore.IsPrivate(new byte[]{203,0,113,7})&&!MpCore.IsPrivate(new byte[]{100,128,0,1}),"private and public addresses are told apart");
    Check(MpCore.IsCarrier(new byte[]{100,64,0,1})&&MpCore.IsCarrier(new byte[]{100,127,255,255})&&!MpCore.IsCarrier(new byte[]{100,63,0,1})&&!MpCore.IsCarrier(new byte[]{10,64,0,1}),"the shared provider range is 100.64 to 100.127");
    Check(MpCore.IsBoxAddress("192.168.1.1")&&MpCore.IsBoxAddress("10.0.0.138")&&!MpCore.IsBoxAddress("203.0.113.1")&&!MpCore.IsBoxAddress("100.64.0.1")&&!MpCore.IsBoxAddress("127.0.0.1")&&!MpCore.IsBoxAddress("router.example"),"a box is asked only at a local address");
    string steps=MpCore.ManualSteps("192.168.1.1","192.168.1.23",47400);Check(steps.Contains("http://192.168.1.1")&&steps.Contains("192.168.1.23")&&steps.Contains("UDP")&&steps.Contains("47400"),"the manual steps name the player's own addresses");
    string steps2=MpCore.SecondBoxSteps("10.0.0.1","10.0.0.5",47400);Check(steps2.Contains("http://10.0.0.1")&&steps2.Contains("10.0.0.5")&&steps2.Contains("47400"),"the steps for a second box name its addresses");
  }
  static void Records(string folder){
    var rule=new MpRule{Box="192.168.1.1",Control="http://192.168.1.1:5000/ctl/IPConn",Service="urn:schemas-upnp-org:service:WANIPConnection:1",Port=47400,Client="192.168.1.23",MadeUtc=new DateTime(2026,11,18,20,0,0,DateTimeKind.Utc),Lease=14400};
    Check(MpCore.ValidRule(rule),"a rule on the local router is accepted");
    var back=MpCore.ParseRule(MpCore.RuleLine(rule));Check(back!=null&&back.Box==rule.Box&&back.Control==rule.Control&&back.Service==rule.Service&&back.Port==47400&&back.Client==rule.Client&&back.MadeUtc==rule.MadeUtc&&back.Lease==14400,"a rule line comes back the same");
    Func<Action<MpRule>,MpRule> with=delegate(Action<MpRule> change){var r=MpCore.ParseRule(MpCore.RuleLine(rule));change(r);return r;};
    Check(!MpCore.ValidRule(with(delegate(MpRule r){r.Box="203.0.113.1";r.Control="http://203.0.113.1/ctl";})),"a box on the internet is refused");
    Check(!MpCore.ValidRule(with(delegate(MpRule r){r.Control="http://192.168.1.2:5000/ctl";})),"a control page on another address is refused");
    Check(!MpCore.ValidRule(with(delegate(MpRule r){r.Control="https://192.168.1.1/ctl";})),"a control page that is not plain http is refused");
    Check(!MpCore.ValidRule(with(delegate(MpRule r){r.Control="file:///c:/x";})),"a file address is refused");
    Check(!MpCore.ValidRule(with(delegate(MpRule r){r.Service="urn:schemas-upnp-org:service:Other:1";})),"another service is refused");
    Check(!MpCore.ValidRule(with(delegate(MpRule r){r.Port=80;}))&&!MpCore.ValidRule(with(delegate(MpRule r){r.Client="203.0.113.5";}))&&!MpCore.ValidRule(with(delegate(MpRule r){r.Box="127.0.0.1";r.Control="http://127.0.0.1/ctl";})),"a low port, a public client and this PC as a box are refused");
    Check(MpCore.ParseRule("")==null&&MpCore.ParseRule("a\tb")==null&&MpCore.ParseRule(MpCore.RuleLine(rule).Replace("47400","x"))==null&&MpCore.ParseRule(MpCore.RuleLine(rule).Replace("2026-11-18","soon"))==null,"lines that are not rules are refused");
    string path=Path.Combine(folder,"multiplayer-rules.txt");
    Check(MpCore.LoadRules(path).Count==0,"no file is an empty list");
    var second=with(delegate(MpRule r){r.Box="10.0.0.1";r.Control="http://10.0.0.1/upnp";r.Client="10.0.0.5";r.Lease=0;});
    MpCore.SaveRules(path,new List<MpRule>{rule,second,with(delegate(MpRule r){r.Box="8.8.8.8";})});
    var loaded=MpCore.LoadRules(path);Check(loaded.Count==2&&MpCore.SameRule(loaded[0],rule)&&MpCore.SameRule(loaded[1],second)&&loaded[1].Lease==0,"two rules are saved and loaded; a rule that is not allowed is not written");
    Check(File.ReadAllText(path).StartsWith("1\r\n",StringComparison.Ordinal)&&!File.Exists(path+".part"),"the file starts with its format number and no part file is left");
    File.AppendAllText(path,"not a rule\r\n203.0.113.1\thttp://203.0.113.1/ctl\turn:schemas-upnp-org:service:WANIPConnection:1\t47400\t192.168.1.23\t2026-11-18T20:00:00Z\t0\r\n");
    Check(MpCore.LoadRules(path).Count==2,"lines that were added by hand and are not allowed are not used");
    MpCore.SaveRules(path,new List<MpRule>());Check(!File.Exists(path),"an empty list removes the file");
    File.WriteAllText(path,"9\r\n");try{MpCore.LoadRules(path);Check(false,"an unknown format is refused");}catch(InvalidDataException){Check(true,"");}File.Delete(path);
    Check(!MpCore.RuleExpired(rule,rule.MadeUtc.AddHours(3))&&MpCore.RuleExpired(rule,rule.MadeUtc.AddHours(5))&&!MpCore.RuleExpired(second,rule.MadeUtc.AddDays(30))&&MpCore.RuleExpired(second,rule.MadeUtc.AddDays(61)),"a rule with a time limit ends after it; one without is remembered for 60 days");
  }
  static void Router(){
    RouterPort.SsdpPort=FreeUdpPort();
    const string pc="192.168.77.23";const int port=47400;
    var kept=new List<MpRule>();
    Action<MpRule> record=delegate(MpRule r){kept.RemoveAll(delegate(MpRule x){return MpCore.SameRule(x,r);});kept.Add(r);},forget=delegate(MpRule r){kept.RemoveAll(delegate(MpRule x){return MpCore.SameRule(x,r);});};
    Func<string,string> noSecond=delegate(string g){return "";},second2=delegate(string g){return "127.0.0.2";};
    using(var box=new FakeBox("127.0.0.1",RouterPort.SsdpPort)){
      // the plain questions
      var found=RouterPort.Find("127.0.0.1",true);
      Check(found!=null&&found.Control=="http://127.0.0.1:"+box.HttpPort+"/ctl"&&found.Service=="urn:schemas-upnp-org:service:WANIPConnection:1","the box's port service is found");
      Check(RouterPort.Owner(found,port)==""&&RouterPort.PublicAddress(found)=="203.0.113.9","a free port and the public address are read");
      string error;Check(RouterPort.Add(found,port,pc,out error)==14400&&box.Rules[port]==pc&&RouterPort.Owner(found,port)==pc,"a rule with a time limit of 4 hours is added");
      Check(box.Log.Contains("AddPortMapping 47400 UDP "+pc+" 14400"),"the rule is UDP, the same port inside and outside, to this PC");
      Check(!RouterPort.Remove(found,port,"192.168.77.99")&&box.Rules.ContainsKey(port),"a rule that points at another address is not removed");
      Check(RouterPort.Remove(found,port,pc)&&!box.Rules.ContainsKey(port)&&!RouterPort.Remove(found,port,pc),"our rule is removed, once");
      box.OnlyPermanent=true;Check(RouterPort.Add(found,port,pc,out error)==0&&box.Rules[port]==pc,"a box that takes only rules without a time limit gets one");box.OnlyPermanent=false;box.Rules.Clear();
      box.RefuseAdd=true;Check(RouterPort.Add(found,port,pc,out error)==-1&&error.Contains("718")&&box.Rules.Count==0,"a refusal is reported with the box's error number");box.RefuseAdd=false;
      box.External="not an address";Check(RouterPort.PublicAddress(found)=="","an answer that is not an address is not used");box.External="203.0.113.9";
      // answers that must not be believed
      box.Redirect=true;Check(RouterPort.Find("127.0.0.1",true)==null,"a description page that sends us elsewhere is refused");box.Redirect=false;
      box.LocationHost="127.0.0.9";Check(RouterPort.Find("127.0.0.1",true)==null,"a description page on another address is refused");box.LocationHost="127.0.0.1";
      box.ControlPath="http://127.0.0.9:"+box.HttpPort+"/ctl";Check(RouterPort.Find("127.0.0.1",true)==null,"a control page on another address is refused");box.ControlPath="/ctl";
      box.DescriptionExtra="<!DOCTYPE root [<!ENTITY x SYSTEM \"http://127.0.0.9/x\">]>";Check(RouterPort.Find("127.0.0.1",true)==null,"a description page with a document type is refused");box.DescriptionExtra="";
      try{RouterPort.Owner(new RouterBox{Address="127.0.0.1",Control="http://127.0.0.9:"+box.HttpPort+"/ctl",Service=found.Service},port);Check(false,"a box whose control page is elsewhere is not asked");}catch(InvalidOperationException){Check(true,"");}
      try{RouterPort.Owner(new RouterBox{Address="127.0.0.1",Control=found.Control,Service="urn:other"},port);Check(false,"another service is not asked");}catch(InvalidOperationException){Check(true,"");}
      Check(RouterPort.Find("127.0.0.3",true)==null,"an address where nothing answers gives no box");

      // hosting behind one router with a public address
      box.Log.Clear();
      var route=RouterPort.OpenAt(pc,"127.0.0.1",port,noSecond,record,forget);
      Check(route.Stop==""&&route.Steps==""&&route.Public=="203.0.113.9"&&route.Rules.Count==1&&kept.Count==1&&box.Rules[port]==pc&&kept[0].Lease==14400,"one router: the port is open and the public address is known");
      Check(box.Log.IndexOf("GetSpecificPortMappingEntry")<box.Log.FindIndex(delegate(string l){return l.StartsWith("AddPortMapping",StringComparison.Ordinal);}),"the box is asked who holds the port before a rule is added");
      Check(RouterPort.Close(route.Rules,forget).Count==0&&box.Rules.Count==0&&kept.Count==0,"closing removes the rule and its record");
      // a rule of ours left by an earlier hosting is taken over, and removed at the end
      box.Rules[port]=pc;route=RouterPort.OpenAt(pc,"127.0.0.1",port,noSecond,record,forget);Check(route.Public.Length>0&&route.Rules.Count==1,"a rule that already points at this PC is taken over");RouterPort.Close(route.Rules,forget);Check(box.Rules.Count==0&&kept.Count==0,"and removed at the end");
      // the port belongs to another device
      box.Rules[port]="192.168.77.99";box.Log.Clear();
      route=RouterPort.OpenAt(pc,"127.0.0.1",port,noSecond,record,forget);
      Check(route.Rules.Count==0&&route.Steps.Contains("192.168.77.23")&&box.Count("AddPortMapping")==0&&box.Count("DeletePortMapping")==0&&box.Rules[port]=="192.168.77.99"&&kept.Count==0,"a port that another device holds is left alone, and the manual steps are shown");
      box.Rules.Clear();
      // the box does not say clearly who holds the port
      box.OwnerUnclear=true;box.Log.Clear();route=RouterPort.OpenAt(pc,"127.0.0.1",port,noSecond,record,forget);Check(route.Rules.Count==0&&route.Steps.Length>0&&box.Count("AddPortMapping")==0&&kept.Count==0,"a box that does not say who holds the port gets no rule");box.OwnerUnclear=false;
      // the box refuses, or says yes and keeps nothing
      box.RefuseAdd=true;route=RouterPort.OpenAt(pc,"127.0.0.1",port,noSecond,record,forget);Check(route.Rules.Count==0&&route.Steps.Contains("http://127.0.0.1")&&kept.Count==0&&box.Rules.Count==0,"a refusal gives the manual steps and leaves no record");box.RefuseAdd=false;
      box.Forgets=true;route=RouterPort.OpenAt(pc,"127.0.0.1",port,noSecond,record,forget);Check(route.Rules.Count==0&&route.Steps.Length>0&&kept.Count==0,"a box that says yes and keeps no rule is not believed");box.Forgets=false;
      // a box that refuses to remove: the player is told and the record stays, so a later start tries again
      route=RouterPort.OpenAt(pc,"127.0.0.1",port,noSecond,record,forget);box.RefuseDelete=true;
      try{RouterPort.Remove(RouterPort.BoxOf(route.Rules[0]),port,pc);Check(false,"a refused removal is an error, not a quiet no");}catch(InvalidOperationException){Check(true,"");}
      var refused=RouterPort.Close(route.Rules,forget);Check(refused.Count==1&&kept.Count==1&&box.Rules.ContainsKey(port),"a box that refuses to remove the rule: the player is told and the record stays");
      box.RefuseDelete=false;Check(RouterPort.Close(route.Rules,forget).Count==0&&kept.Count==0&&box.Rules.Count==0,"a later try removes it and its record");
      // the box makes the rule, says no, and is unclear afterwards: the record stays
      box.BreaksAfterAdd=true;route=RouterPort.OpenAt(pc,"127.0.0.1",port,noSecond,record,forget);
      Check(route.Rules.Count==0&&route.Steps.Length>0&&box.Rules.ContainsKey(port)&&kept.Count==1,"a rule that may exist after a failed add keeps its record");
      box.BreaksAfterAdd=false;box.OwnerUnclear=false;Check(RouterPort.Close(new List<MpRule>(kept),forget).Count==0&&box.Rules.Count==0&&kept.Count==0,"and a later clean-up removes it");
      // our earlier rule is still on the box and the box refuses a new add: the old rule is removed, or its record stays
      box.Rules[port]=pc;box.RefuseAdd=true;route=RouterPort.OpenAt(pc,"127.0.0.1",port,noSecond,record,forget);Check(route.Rules.Count==0&&box.Rules.Count==0&&kept.Count==0,"an earlier rule of ours is removed when the new add is refused");
      box.Rules[port]=pc;box.RefuseDelete=true;route=RouterPort.OpenAt(pc,"127.0.0.1",port,noSecond,record,forget);Check(route.Rules.Count==0&&box.Rules.ContainsKey(port)&&kept.Count==1,"and when it cannot be removed, its record stays");
      box.RefuseAdd=false;box.RefuseDelete=false;RouterPort.Close(new List<MpRule>(kept),forget);Check(kept.Count==0&&box.Rules.Count==0,"until a later clean-up removes it");
      // a box that reports 0.0.0.0 has not told its address
      box.External="0.0.0.0";route=RouterPort.OpenAt(pc,"127.0.0.1",port,noSecond,record,forget);Check(route.Stop==""&&route.Public==""&&route.Rules.Count==1,"0.0.0.0 is not an internet address: the player types it");RouterPort.Close(route.Rules,forget);Check(kept.Count==0,"and closing still removes the rule");
      Check(route.Rules[0].MadeUtc.Ticks%TimeSpan.TicksPerSecond==0&&MpCore.ParseRule(MpCore.RuleLine(new MpRule{Box="192.168.1.1",Control="http://192.168.1.1/c",Service="urn:schemas-upnp-org:service:WANIPConnection:1",Port=port,Client="192.168.1.9",MadeUtc=route.Rules[0].MadeUtc,Lease=0})).MadeUtc==route.Rules[0].MadeUtc,"the time of a rule is in whole seconds, so the record names exactly this rule");
      // the provider shares the address
      box.External="100.70.1.2";route=RouterPort.OpenAt(pc,"127.0.0.1",port,noSecond,record,forget);Check(route.Stop==MpCore.CarrierText&&route.Public==""&&route.Rules.Count==1,"a shared provider address stops hosting");RouterPort.Close(route.Rules,forget);Check(box.Rules.Count==0&&kept.Count==0,"and the rule is taken away again");
      // the router does not tell its address
      box.External="";route=RouterPort.OpenAt(pc,"127.0.0.1",port,noSecond,record,forget);Check(route.Stop==""&&route.Steps==""&&route.Public==""&&route.Rules.Count==1,"a router that does not tell its address: the port is open and the player types the address");RouterPort.Close(route.Rules,forget);
      // a private outer address and no second box
      box.External="192.168.5.2";route=RouterPort.OpenAt(pc,"127.0.0.1",port,noSecond,record,forget);Check(route.Stop.Contains("no second box")&&route.Rules.Count==1,"a private outer address without a second box stops hosting");RouterPort.Close(route.Rules,forget);Check(kept.Count==0&&box.Rules.Count==0,"and nothing is left");

      // two boxes
      using(var outerBox=new FakeBox("127.0.0.2",RouterPort.SsdpPort)){
        outerBox.External="203.0.113.44";
        route=RouterPort.OpenAt(pc,"127.0.0.1",port,second2,record,forget);
        Check(route.Stop==""&&route.Steps==""&&route.Public=="203.0.113.44"&&route.RouterOuter=="192.168.5.2"&&route.SecondBox=="127.0.0.2"&&route.Rules.Count==2&&kept.Count==2,"two boxes: both ports are open and the public address comes from the outer box");
        Check(box.Rules[port]==pc&&outerBox.Rules[port]=="192.168.5.2","the outer box sends the port to the router's outer address, the router sends it to this PC");
        // closing leaves a rule alone that meanwhile points elsewhere
        outerBox.Rules[port]="192.168.5.77";
        Check(RouterPort.Close(route.Rules,forget).Count==0&&box.Rules.Count==0&&outerBox.Rules[port]=="192.168.5.77"&&kept.Count==0,"closing removes our rule and leaves a rule that now points elsewhere");
        outerBox.Rules.Clear();
        outerBox.RefuseAdd=true;route=RouterPort.OpenAt(pc,"127.0.0.1",port,second2,record,forget);Check(route.Rules.Count==1&&route.Steps.Contains("http://127.0.0.2")&&route.Steps.Contains("192.168.5.2")&&route.Public=="","an outer box that refuses gives the steps for that box");RouterPort.Close(route.Rules,forget);outerBox.RefuseAdd=false;
        outerBox.External="10.9.9.9";route=RouterPort.OpenAt(pc,"127.0.0.1",port,second2,record,forget);Check(route.Stop.Contains("also has a private address")&&route.Rules.Count==2,"two boxes that are both private stop hosting");RouterPort.Close(route.Rules,forget);Check(box.Rules.Count==0&&outerBox.Rules.Count==0&&kept.Count==0,"and both rules are taken away again");
        // looking changes nothing
        outerBox.External="203.0.113.44";box.Log.Clear();outerBox.Log.Clear();
        string look=RouterPort.LookAt(pc,"127.0.0.1",port,second2);
        Check(look.Contains("both ports can be opened automatically")&&look.Contains("Nothing was changed")&&box.Count("AddPortMapping")+box.Count("DeletePortMapping")+outerBox.Count("AddPortMapping")+outerBox.Count("DeletePortMapping")==0,"the check only looks: "+look);
      }
      box.External="203.0.113.9";box.Log.Clear();
      Check(RouterPort.LookAt(pc,"127.0.0.1",port,noSecond).Contains("public one")&&box.Count("AddPortMapping")==0,"the check on one router only looks");
      // a rule that cannot be removed is reported and its record stays
      route=RouterPort.OpenAt(pc,"127.0.0.1",port,noSecond,record,forget);
      box.Dispose();
      var said=RouterPort.Close(route.Rules,forget);Check(said.Count==1&&said[0].Contains("could not be removed")&&said[0].Contains("4 hours")&&kept.Count==1,"a box that is gone: the player is told, and the record stays for the next start");
      kept.Clear();
    }
    string steps=RouterPort.LookAt(pc,"127.0.0.3",port,noSecond);Check(steps.Contains("UPnP is off or missing")&&steps.Contains("192.168.77.23"),"no port service: the check shows the manual steps");
    var none=RouterPort.OpenAt(pc,"127.0.0.3",port,noSecond,record,forget);Check(none.Rules.Count==0&&none.Steps.Contains("http://127.0.0.3")&&none.Stop==""&&kept.Count==0,"no port service: hosting shows the manual steps");
  }
  public static int Main(string[] args){
    string folder=Path.GetFullPath(args.Length>0?args[0]:"mp-fixture");
    if(Directory.Exists(folder))Directory.Delete(folder,true);Directory.CreateDirectory(folder);
    Code();StatusAndAddresses();Records(folder);Router();
    if(failures>0){Console.WriteLine(failures+" multiplayer check(s) failed");return 1;}
    Console.WriteLine("PASS: "+checks+" multiplayer checks: the invite code (the contract's example, 300 round trips, every one-sign mistake and neighbour swap refused), the two settings, the status line, address kinds, the record of router rules, and the router questions against a stand-in box on this PC. No real router, network or game program was contacted.");return 0;
  }
}
