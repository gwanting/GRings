using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using QuotaTray;
using System.Windows.Automation;
using QuotaData;

namespace DualQuotaBar {
class Bar:Form {
 readonly RingClicks ringClicks;
 QuotaCard quotaCard;
 bool blackDigits;
 FullscreenVisibility fullscreen;volatile bool fullscreenHidden;
 Snapshot snapshot,lastLocalApplied;LocalQuotaFeed localFeed;bool busy,closing;string error;ToolTip tip=new ToolTip();System.Windows.Forms.Timer refresh,position,panelWait;DateTime fetched,panelDeadline;string statusText;IntPtr panelHandle,mouseHook;MouseHookCallback mouseCallback;bool ringPressed,ringRightPressed,trayRequested;
 [StructLayout(LayoutKind.Sequential)]public struct Rect{public int Left,Top,Right,Bottom;}
 delegate bool EnumCallback(IntPtr handle,IntPtr param);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindow(string cls,string title);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr handle,out Rect rect);
 [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr handle,IntPtr after,int x,int y,int width,int height,uint flags);
 [DllImport("user32.dll")]static extern bool EnumWindows(EnumCallback callback,IntPtr param);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr handle,out uint processId);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr handle,StringBuilder text,int count);
 [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr handle,int command);
 [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr handle);
 [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr handle);
 delegate IntPtr MouseHookCallback(int code,IntPtr message,IntPtr data);
 [StructLayout(LayoutKind.Sequential)]struct MouseEvent{public NativePoint Point;public uint MouseData,Flags,Time;public IntPtr ExtraInfo;}
 [DllImport("user32.dll")]static extern IntPtr SetWindowsHookEx(int id,MouseHookCallback callback,IntPtr module,uint thread);
 [DllImport("user32.dll")]static extern bool UnhookWindowsHookEx(IntPtr hook);
 [DllImport("user32.dll")]static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr GetModuleHandle(string name);
 [DllImport("user32.dll")]static extern IntPtr WindowFromPoint(NativePoint point);
 [StructLayout(LayoutKind.Sequential)]struct NativePoint{public int X,Y;public NativePoint(int x,int y){X=x;Y=y;}}
 [StructLayout(LayoutKind.Sequential)]struct NativeSize{public int Width,Height;public NativeSize(int w,int h){Width=w;Height=h;}}
 [StructLayout(LayoutKind.Sequential,Pack=1)]struct Blend{public byte Op,Flags,Alpha,Format;}
 [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr handle);
 [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr handle,IntPtr dc);
 [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr dc);
 [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
 [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr obj);
 [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
 [DllImport("user32.dll")]static extern bool UpdateLayeredWindow(IntPtr window,IntPtr screen,ref NativePoint destination,ref NativeSize size,IntPtr source,ref NativePoint origin,uint key,ref Blend blend,uint flags);
 protected override bool ShowWithoutActivation{get{return true;}}
 protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x80|0x80000;return p;}}
 public Bar(){Text="Codex 双额度任务栏";AutoScaleMode=AutoScaleMode.None;StartPosition=FormStartPosition.Manual;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;DoubleBuffered=true;ClientSize=new Size(60,60);BackColor=Color.FromArgb(25,32,40);Cursor=Cursors.Hand;using(var outline=new GraphicsPath()){outline.AddEllipse(0,0,60,60);Region=new Region(outline);}tip.SetToolTip(this,"外环：5h 剩余额度；内环：周剩余额度\n正在读取额度…");
  try{localFeed=new LocalQuotaFeed();}catch(IOException){}catch(UnauthorizedAccessException){}
  var menu=new ContextMenuStrip{Font=new Font("Microsoft YaHei UI",14,FontStyle.Regular,GraphicsUnit.Pixel)};menu.Items.Add("打开 Win-CodexBar 用量面板",null,(s,e)=>OpenPanel());menu.Items.Add("刷新两个额度",null,(s,e)=>Fetch());menu.Items.Add("关于",null,(s,e)=>MessageBox.Show(this,"Codex 用量圆环\n\n圆环设计与制作：Wanting\nCopyright © 2026 Wanting\n\nMIT License\n\nhttps://github.com/gwanting/GRings","关于 Codex 用量圆环",MessageBoxButtons.OK,MessageBoxIcon.Information));menu.Items.Add("关闭此圆环",null,(s,e)=>Close());menu.Items.Add("退出全部三环",null,(s,e)=>QuotaRings.Program.ExitAll());ContextMenuStrip=menu;
  panelWait=new System.Windows.Forms.Timer{Interval=150};panelWait.Tick+=(s,e)=>{panelHandle=FindPanel();if(panelHandle!=IntPtr.Zero){panelWait.Stop();ShowPanel();}else if(DateTime.Now>panelDeadline)panelWait.Stop();else if(!trayRequested){try{trayRequested=InvokeTray();}catch{}}};
  ringClicks=new RingClicks(TogglePanel,()=>{blackDigits=!blackDigits;Render();});
  fullscreen=new FullscreenVisibility(this,FullscreenChanged);
  refresh=new System.Windows.Forms.Timer{Interval=30000};refresh.Tick+=(s,e)=>Fetch();refresh.Start();position=new System.Windows.Forms.Timer{Interval=1000};position.Tick+=(s,e)=>{Place();ApplyLocalQuota();UpdateStatus();Render();};position.Start();Shown+=(s,e)=>{fullscreen.Start();Place();Render();Fetch();mouseCallback=OnMouse;mouseHook=SetWindowsHookEx(14,mouseCallback,GetModuleHandle(null),0);};MouseEnter+=(s,e)=>{if(snapshot==null||DateTime.Now-fetched>TimeSpan.FromSeconds(5))Fetch();};FormClosed+=(s,e)=>{closing=true;ringClicks.Dispose();fullscreen.Dispose();if(quotaCard!=null)quotaCard.Dispose();if(localFeed!=null)localFeed.Dispose();if(mouseHook!=IntPtr.Zero)UnhookWindowsHookEx(mouseHook);panelWait.Dispose();refresh.Dispose();position.Dispose();tip.Dispose();};
 }
 protected override void WndProc(ref Message m){if(m.Msg==0x21){m.Result=new IntPtr(3);return;}if(m.Msg==0x202){long bits=m.LParam.ToInt64();var p=new Point((short)(bits&65535),(short)((bits>>16)&65535));if(ClientRectangle.Contains(p))BeginInvoke(new Action(()=>HandleRingClick(PointToScreen(p))));m.Result=IntPtr.Zero;return;}base.WndProc(ref m);}
 void FullscreenChanged(bool hidden){fullscreenHidden=hidden;if(hidden){ringClicks.Cancel();ringPressed=ringRightPressed=false;ContextMenuStrip.Close();if(quotaCard!=null)quotaCard.Hide();panelWait.Stop();if(panelHandle!=IntPtr.Zero)ShowWindow(panelHandle,0);ShowWindow(Handle,0);}else if(!closing&&IsHandleCreated)BeginInvoke(new Action(()=>{if(!closing&&!fullscreenHidden){Place();Render();}}));}
 void Place(){if(fullscreen!=null&&fullscreen.Refresh())return;var task=FindWindow("Shell_TrayWnd",null);Rect t,n;int x,y;if(task!=IntPtr.Zero&&GetWindowRect(task,out t)){var tray=FindWindowEx(task,IntPtr.Zero,"TrayNotifyWnd",null);int right=GetWindowRect(tray,out n)?n.Left-10:t.Right-300;x=Math.Max(t.Left+10,right-Width);y=t.Top+Math.Max(0,(t.Bottom-t.Top-Height)/2);}else{var r=Screen.PrimaryScreen.WorkingArea;x=r.Right-Width-12;y=r.Bottom-Height-8;}SetWindowPos(Handle,new IntPtr(-1),x,y,0,0,(uint)(Left==x&&Top==y?0x53:0x51));}
 Window Quota(int minutes){return snapshot==null?null:snapshot.Windows.Find(w=>w.Minutes==minutes);}
 void ApplyLocalQuota(){if(localFeed==null)return;var candidate=localFeed.Poll();if(candidate!=null&&!Object.ReferenceEquals(candidate,lastLocalApplied)){lastLocalApplied=candidate;snapshot=Snapshot.Merge(snapshot,candidate);fetched=snapshot.Fetched.LocalDateTime;error=null;}}
 bool Current(Window window){return snapshot!=null&&snapshot.IsCurrent(window,DateTimeOffset.Now);}
 void UpdateStatus(){var a=Quota(300);var b=Quota(10080);bool validA=Current(a),validB=Current(b);string state=error!=null?"刷新失败，正在重试":busy?"正在刷新":snapshot==null?"正在读取":!validA||!validB?"数据已过期，等待刷新":"已更新";AccessibleName="Codex 5小时剩余 "+(validA?a.Percent:"未知")+"；每周剩余 "+(validB?b.Percent:"未知")+"；"+state+(snapshot==null?"":"；"+snapshot.Source+"；更新 "+fetched.ToString("HH:mm:ss"));string text="外环（青色）：5h 剩余 "+(validA?a.Percent:"未知")+"\n内环（紫色）：周剩余 "+(validB?b.Percent:"未知")+"\n"+(a==null?"":a.Countdown(DateTimeOffset.Now))+"\n"+(b==null?"":b.Countdown(DateTimeOffset.Now))+"\n"+state+(snapshot==null?"":"；上次成功 "+fetched.ToString("HH:mm:ss"))+(snapshot==null?"":"\n来源："+snapshot.Source)+"\n中心数字为剩余额度；应用事件实时更新，接口每30秒补充\n点击打开用量面板并刷新；右键手动刷新";if(text!=statusText){statusText=text;tip.SetToolTip(this,text);}if(quotaCard!=null&&quotaCard.Visible)quotaCard.UpdateQuota(snapshot,busy,error!=null);}
 async void Fetch(){if(busy||closing)return;ApplyLocalQuota();busy=true;refresh.Stop();UpdateStatus();try{var result=await Task.Run(()=>Read());if(closing)return;ApplyLocalQuota();snapshot=Snapshot.Merge(snapshot,result);fetched=snapshot.Fetched.LocalDateTime;error=null;}catch(Exception ex){if(!closing){if(snapshot==null||snapshot.Source!="Codex 应用额度事件"||DateTimeOffset.Now-snapshot.Fetched>TimeSpan.FromSeconds(30))error=ex.Message;try{File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"dual-error.txt"),ex.ToString());}catch{}}}finally{busy=false;if(!closing){UpdateStatus();Render();refresh.Interval=error==null?30000:15000;refresh.Start();}}}
 Snapshot Read(){
  try{var direct=ReadAppServer();if(direct!=null)return direct;}catch{}
  var path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"backend","CodexMeter.exe");
  if(!File.Exists(path))path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","CodexMeter","CodexMeter.exe");
  var output=Path.Combine(Path.GetTempPath(),"dual-quota-"+Guid.NewGuid().ToString("N")+".json");try{using(var p=new Process()){p.StartInfo=new ProcessStartInfo(path,"--check \""+output+"\""){UseShellExecute=false,CreateNoWindow=true};p.Start();if(!p.WaitForExit(45000)){try{p.Kill();}catch{}throw new Exception("额度读取超时，显示上次结果");}if(p.ExitCode!=0||!File.Exists(output))throw new Exception("刷新失败，显示上次结果"+(File.Exists(output)?": "+File.ReadAllText(output):" (no output; exit "+p.ExitCode+")"));return Snapshot.Parse(File.ReadAllText(output));}}finally{try{File.Delete(output);}catch{}}
 }
 static string FindCodexExe(){
  var envPath=Environment.GetEnvironmentVariable("PATH");
  if(!String.IsNullOrEmpty(envPath)){foreach(var dir in envPath.Split(Path.PathSeparator)){if(String.IsNullOrWhiteSpace(dir))continue;try{var candidate=Path.Combine(dir.Trim(),"codex.exe");if(File.Exists(candidate))return candidate;}catch{}}}
  var alias=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Microsoft","WindowsApps","codex.exe");
  if(File.Exists(alias))return alias;
  foreach(var proc in Process.GetProcessesByName("codex")){try{var file=proc.MainModule!=null?proc.MainModule.FileName:null;if(!String.IsNullOrEmpty(file)&&File.Exists(file))return file;}catch{}finally{proc.Dispose();}}
  return null;
 }
 static Snapshot ReadAppServer(){
  var codex=FindCodexExe();if(String.IsNullOrEmpty(codex))return null;
  var psi=new ProcessStartInfo(codex,"app-server"){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true,StandardOutputEncoding=Encoding.UTF8};
  using(var p=Process.Start(psi)){
   try{
    p.StandardInput.WriteLine("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"clientInfo\":{\"name\":\"DualQuotaBar\",\"version\":\"1.0\"}}}");
    p.StandardInput.WriteLine("{\"jsonrpc\":\"2.0\",\"method\":\"initialized\"}");
    p.StandardInput.WriteLine("{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"account/rateLimits/read\",\"params\":{}}");
    p.StandardInput.Flush();
    var deadline=DateTime.UtcNow.AddSeconds(15);
    while(DateTime.UtcNow<deadline){
     int waitMs=(int)Math.Max(100,(deadline-DateTime.UtcNow).TotalMilliseconds);
     var readTask=p.StandardOutput.ReadLineAsync();
     if(!readTask.Wait(waitMs))break;
     var line=readTask.Result;
     if(line==null)break;
     if(line.IndexOf("\"id\":2",StringComparison.Ordinal)<0&&line.IndexOf("\"id\": 2",StringComparison.Ordinal)<0)continue;
     return Snapshot.ParseAppServer(line);
    }
    return null;
   }finally{try{if(!p.HasExited)p.Kill();}catch{}}
  }
 }
 protected override void OnPaint(PaintEventArgs e){Render();}
 void Render(){if(!IsHandleCreated||closing||fullscreenHidden)return;using(var bitmap=new Bitmap(Width,Height,System.Drawing.Imaging.PixelFormat.Format32bppPArgb)){using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;using(var hitSurface=new SolidBrush(Color.FromArgb(1,0,0,0)))g.FillEllipse(hitSurface,0,0,Width,Height);g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;DrawRing(g,new RectangleF(5,5,50,50),5,Quota(300),Color.FromArgb(58,205,215));DrawRing(g,new RectangleF(14,14,32,32),4.5f,Quota(10080),Color.FromArgb(174,153,250));DrawPercentage(g);if(error!=null||(snapshot!=null&&(!Current(Quota(300))||!Current(Quota(10080)))))using(var warning=new SolidBrush(Color.FromArgb(244,183,94)))g.FillEllipse(warning,47,5,6,6);}var screen=GetDC(IntPtr.Zero);var memory=CreateCompatibleDC(screen);var image=bitmap.GetHbitmap(Color.FromArgb(0));var old=SelectObject(memory,image);try{var destination=new NativePoint(Left,Top);var origin=new NativePoint(0,0);var size=new NativeSize(Width,Height);var blend=new Blend{Alpha=255,Format=1};UpdateLayeredWindow(Handle,screen,ref destination,ref size,memory,ref origin,0,ref blend,2);}finally{SelectObject(memory,old);DeleteObject(image);DeleteDC(memory);ReleaseDC(IntPtr.Zero,screen);}}}
 void DrawPercentage(Graphics g){var quota=Quota(300);string value=Current(quota)?quota.Percent.Replace("%",""):"--";bool stale=error!=null||!Current(quota);using(var format=new StringFormat(StringFormat.GenericTypographic)){format.Alignment=StringAlignment.Center;format.LineAlignment=StringAlignment.Center;using(var font=new Font("Segoe UI",value.Length>2?15:19,FontStyle.Bold,GraphicsUnit.Pixel))using(var brush=new SolidBrush(blackDigits?Color.Black:stale?Color.FromArgb(151,163,174):Color.FromArgb(240,247,253)))g.DrawString(value,font,brush,new RectangleF(16,18,28,24),format);}}
 void DrawRing(Graphics g,RectangleF bounds,float thickness,Window window,Color color){using(var track=new Pen(Color.FromArgb(100,color),thickness))g.DrawEllipse(track,bounds);if(!Current(window))return;float sweep=(float)(Math.Max(0,Math.Min(100,window.Remaining.Value))*3.6);RingArc.Draw(g,bounds,thickness,sweep,color);}
 bool InvokeTray(){var condition=new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button);var buttons=AutomationElement.RootElement.FindAll(TreeScope.Descendants,condition);foreach(AutomationElement button in buttons){try{if(button.Current.Name.StartsWith("CodexBar Desktop CodexBar",StringComparison.Ordinal)){object pattern;if(button.TryGetCurrentPattern(InvokePattern.Pattern,out pattern)){((InvokePattern)pattern).Invoke();return true;}}}catch(ElementNotAvailableException){}}return false;}
 bool InsideCircle(NativePoint point){if(fullscreenHidden)return false;double x=point.X-Left-Width/2.0,y=point.Y-Top-Height/2.0;return x*x+y*y<=Width*Width/4.0;}
 IntPtr OnMouse(int code,IntPtr message,IntPtr data){if(code>=0&&!closing&&!fullscreenHidden){int kind=message.ToInt32();var point=((MouseEvent)Marshal.PtrToStructure(data,typeof(MouseEvent))).Point;if(kind==0x204&&InsideCircle(point)){ringRightPressed=true;return new IntPtr(1);}if(kind==0x205&&ringRightPressed){ringRightPressed=false;if(InsideCircle(point))try{BeginInvoke(new Action(()=>{ringClicks.Cancel();if(!closing&&!fullscreenHidden){ContextMenuStrip.Show(new Point(point.X,point.Y));SetForegroundWindow(ContextMenuStrip.Handle);}}));}catch(InvalidOperationException){}return new IntPtr(1);}if(kind==0x201&&InsideCircle(point)){ringPressed=true;return new IntPtr(1);}if(kind==0x202&&ringPressed){ringPressed=false;if(InsideCircle(point))try{BeginInvoke(new Action(()=>HandleRingClick(new Point(point.X,point.Y))));}catch(InvalidOperationException){}return new IntPtr(1);}if((panelHandle!=IntPtr.Zero||ContextMenuStrip.Visible||(quotaCard!=null&&quotaCard.Visible))&&(kind==0x201||kind==0x204||kind==0x207))try{BeginInvoke(new Action(()=>DismissOutside(point)));}catch(InvalidOperationException){}}return CallNextHookEx(mouseHook,code,message,data);}
 static bool PointInWindow(IntPtr handle,NativePoint point){Rect r;return handle!=IntPtr.Zero&&GetWindowRect(handle,out r)&&point.X>=r.Left&&point.X<r.Right&&point.Y>=r.Top&&point.Y<r.Bottom;}
 void DismissOutside(NativePoint point){if(closing)return;if(quotaCard!=null&&quotaCard.Visible){if(PointInWindow(quotaCard.Handle,point))return;quotaCard.Hide();}if(ContextMenuStrip.Visible){if(PointInWindow(ContextMenuStrip.Handle,point))return;ContextMenuStrip.Close();}if(panelHandle==IntPtr.Zero||!IsWindowVisible(panelHandle))return;if(Region!=null&&Region.IsVisible(point.X-Left,point.Y-Top))return;Rect panelBounds;if(GetWindowRect(panelHandle,out panelBounds)&&point.X>=panelBounds.Left&&point.X<panelBounds.Right&&point.Y>=panelBounds.Top&&point.Y<panelBounds.Bottom)return;uint panelPid,targetPid;GetWindowThreadProcessId(panelHandle,out panelPid);GetWindowThreadProcessId(WindowFromPoint(point),out targetPid);if(targetPid==panelPid)return;ShowWindow(panelHandle,0);}
 IntPtr FindPanel(){IntPtr found=IntPtr.Zero;EnumWindows((h,p)=>{var name=new StringBuilder(256);GetWindowText(h,name,256);if(name.ToString()!="CodexBar")return true;uint pid;GetWindowThreadProcessId(h,out pid);try{using(var process=Process.GetProcessById((int)pid)){if(process.ProcessName=="CodexBar")found=h;}}catch(ArgumentException){}return true;},IntPtr.Zero);return found;}
 void ShowPanel(){if(fullscreenHidden)return;if(panelHandle==IntPtr.Zero)return;Rect r;if(!GetWindowRect(panelHandle,out r))return;int width=r.Right-r.Left,height=r.Bottom-r.Top;var area=Screen.FromRectangle(Bounds).WorkingArea;int x=Math.Max(area.Left+8,Math.Min(Left+(Width-width)/2,area.Right-width-8));int y=Math.Max(area.Top+8,Math.Min(Top-height-8,area.Bottom-height-8));SetWindowPos(panelHandle,new IntPtr(-1),x,y,0,0,0x11);ShowWindow(panelHandle,4);}
 void HandleRingClick(Point screenPoint){
  if(closing||fullscreenHidden)return;
  if(ContextMenuStrip.Visible){ringClicks.Cancel();ContextMenuStrip.Close();return;}
  ringClicks.Click(screenPoint);
 }
 void TogglePanel() {
  if (fullscreenHidden || closing) return;
  if (ContextMenuStrip.Visible) { ContextMenuStrip.Close(); return; }
  if (quotaCard != null && quotaCard.Visible) { quotaCard.Hide(); return; }

  panelWait.Stop();
  if (panelHandle != IntPtr.Zero && panelHandle == FindPanel()) ShowWindow(panelHandle, 0);
  if (quotaCard == null) quotaCard = new QuotaCard();
  quotaCard.UpdateQuota(snapshot, busy, error != null);
  Rectangle area = Screen.FromRectangle(Bounds).WorkingArea;
  int x = Math.Max(area.Left + 8, Math.Min(Left + (Width - quotaCard.Width) / 2, area.Right - quotaCard.Width - 8));
  int y = Math.Max(area.Top + 8, Math.Min(Top - quotaCard.Height - 8, area.Bottom - quotaCard.Height - 8));
  quotaCard.Location = new Point(x, y);
  quotaCard.Show();
  Fetch();
 }
 void OpenPanel(){if(fullscreenHidden)return;if(quotaCard!=null)quotaCard.Hide();Fetch();try{panelHandle=FindPanel();if(panelHandle!=IntPtr.Zero){ShowPanel();return;}trayRequested=false;var processes=Process.GetProcessesByName("CodexBar");try{if(processes.Length==0){var exe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Win-CodexBar","CodexBar.exe");Process.Start(new ProcessStartInfo(exe){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(exe)});}else trayRequested=InvokeTray();}finally{foreach(var process in processes)process.Dispose();}panelDeadline=DateTime.Now.AddSeconds(5);panelWait.Start();}catch{}}
}
}
