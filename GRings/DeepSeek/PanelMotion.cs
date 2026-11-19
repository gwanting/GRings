using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace DeepSeekBalance {
sealed class PanelMotion:IDisposable {
 [DllImport("dwmapi.dll")]static extern int DwmFlush();
 [DllImport("winmm.dll")]static extern uint timeBeginPeriod(uint period);
 [DllImport("winmm.dll")]static extern uint timeEndPeriod(uint period);
 readonly Form owner;
 readonly Func<Bitmap> capture;
 readonly Action restore;
 readonly Stopwatch clock=new Stopwatch();
 MotionSurface surface;
 Rectangle from,to,fullBounds,currentBounds;
 Point anchor;
 bool opening,disposed,highResTimer;
 volatile bool running;
 int generation;
 double duration;
 byte startAlpha;
 public bool Running{get{return running;}}
 public bool TargetVisible{get{return running?opening:owner.Visible;}}
 public PanelMotion(Form form,Func<Bitmap> snapshot,Action refresh){owner=form;capture=snapshot;restore=refresh;}
 static Rectangle Small(Rectangle full,Point center){int w=Math.Max(1,(int)Math.Round(full.Width*0.20)),h=Math.Max(1,(int)Math.Round(full.Height*0.20));return new Rectangle(center.X-w/2,center.Y-h/2,w,h);}
 void EnableHighRes(bool enable){if(enable==highResTimer)return;highResTimer=enable;try{if(enable)timeBeginPeriod(1);else timeEndPeriod(1);}catch{}}
 public void Show(Point origin){
  if(disposed)return;if(running&&opening)return;if(owner.Visible&&!running)return;
  anchor=origin;fullBounds=owner.Bounds;
  Rectangle small=Small(fullBounds,anchor);
  Rectangle start=surface==null?small:currentBounds;
  if(surface==null)surface=new MotionSurface(capture(),Rectangle.Union(fullBounds,small));
  owner.Hide();Start(start,fullBounds,true);
 }
 public void Hide(Point origin){
  if(disposed||running&&!opening)return;if(!owner.Visible&&!running)return;
  anchor=origin;fullBounds=owner.Bounds;
  Rectangle small=Small(fullBounds,anchor);
  Rectangle start=surface==null?fullBounds:currentBounds;
  if(surface==null)surface=new MotionSurface(capture(),Rectangle.Union(fullBounds,small));
  if(!surface.HasFrame){currentBounds=start;surface.Render(start,255);}
  surface.Show();owner.Hide();Start(start,small,false);
 }
 void Start(Rectangle start,Rectangle end,bool show){
  from=start;to=end;currentBounds=start;opening=show;
  startAlpha=surface.HasFrame?surface.CurrentAlpha:show?(byte)30:(byte)255;
  duration=show?200:160;
  running=true;
  int myGen=++generation;
  EnableHighRes(true);
  clock.Restart();
  surface.Render(from,startAlpha);
  if(!surface.Visible)surface.Show();
  var thread=new Thread(()=>RunLoop(myGen)){IsBackground=true,Priority=ThreadPriority.AboveNormal,Name="DeepSeek VSync Motion"};
  thread.Start();
 }
 void RunLoop(int myGen){
  long lastTicks=0;
  double minFrameMs=1000.0/120.0;
  while(running&&!disposed&&generation==myGen){
   try{
    if(DwmFlush()!=0)Thread.Sleep(8);
   }catch{
    Thread.Sleep(8);
   }
   if(!running||disposed||generation!=myGen)break;
   long nowTicks=clock.ElapsedTicks;
   double sinceLast=(nowTicks-lastTicks)*1000.0/Stopwatch.Frequency;
   if(lastTicks!=0&&sinceLast<minFrameMs)continue;
   lastTicks=nowTicks;
   try{
    var s=surface;
    if(s==null||s.IsDisposed||!s.IsHandleCreated)break;
    s.Invoke(new Action(()=>Step(myGen)));
   }catch{break;}
  }
 }
 void Step(int myGen){
  if(disposed||!running||generation!=myGen||owner.IsDisposed||surface==null){running=false;EnableHighRes(false);return;}
  double fraction=Math.Min(1.0,clock.Elapsed.TotalMilliseconds/duration);
  double ease=opening?1.0-Math.Pow(1.0-fraction,3):1.0-Math.Pow(1.0-fraction,2);
  currentBounds=new Rectangle(Mix(from.X,to.X,ease),Mix(from.Y,to.Y,ease),Math.Max(1,Mix(from.Width,to.Width,ease)),Math.Max(1,Mix(from.Height,to.Height,ease)));
  surface.Render(currentBounds,(byte)Math.Max(0,Math.Min(255,startAlpha+((opening?255:0)-startAlpha)*ease)));
  if(fraction<1.0)return;
  running=false;clock.Stop();EnableHighRes(false);
  if(opening){owner.Bounds=fullBounds;restore();owner.Show();owner.Update();}
  surface.Dispose();surface=null;
 }
 static int Mix(int a,int b,double amount){return (int)Math.Round(a+(b-a)*amount);}
 public void Dispose(){if(disposed)return;disposed=true;running=false;generation++;EnableHighRes(false);if(surface!=null){surface.Dispose();surface=null;}}
}

sealed class MotionSurface:Form {
 readonly Bitmap snapshot,canvas;
 readonly Graphics canvasGraphics;
 readonly Rectangle surfaceBounds;
 IntPtr screenDc,memoryDc,dibHandle,oldBitmap;
 public bool HasFrame{get;private set;}
 public byte CurrentAlpha{get;private set;}
 [StructLayout(LayoutKind.Sequential)]struct NativePoint{public int X,Y;public NativePoint(int x,int y){X=x;Y=y;}}
 [StructLayout(LayoutKind.Sequential)]struct NativeSize{public int Width,Height;public NativeSize(int width,int height){Width=width;Height=height;}}
 [StructLayout(LayoutKind.Sequential,Pack=1)]struct Blend{public byte Operation,Flags,Alpha,Format;}
 [StructLayout(LayoutKind.Sequential)]struct BITMAPINFOHEADER{public uint biSize;public int biWidth,biHeight;public ushort biPlanes,biBitCount;public uint biCompression,biSizeImage;public int biXPelsPerMeter,biYPelsPerMeter;public uint biClrUsed,biClrImportant;}
 [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr window);
 [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr window,IntPtr dc);
 [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr dc);
 [DllImport("gdi32.dll")]static extern IntPtr CreateDIBSection(IntPtr hdc,ref BITMAPINFOHEADER pbmi,uint iUsage,out IntPtr ppvBits,IntPtr hSection,uint dwOffset);
 [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr item);
 [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr item);
 [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
 [DllImport("user32.dll")]static extern bool UpdateLayeredWindow(IntPtr window,IntPtr screen,ref NativePoint destination,ref NativeSize size,IntPtr source,ref NativePoint origin,uint key,ref Blend blend,uint flags);
 protected override bool ShowWithoutActivation{get{return true;}}
 protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x80|0x80000|0x20;return p;}}
 public MotionSurface(Bitmap image,Rectangle unionBounds){
  snapshot=image;
  surfaceBounds=unionBounds;
  int w=Math.Max(1,unionBounds.Width),h=Math.Max(1,unionBounds.Height);
  screenDc=GetDC(IntPtr.Zero);
  memoryDc=CreateCompatibleDC(screenDc);
  var bmi=new BITMAPINFOHEADER{biSize=(uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER)),biWidth=w,biHeight=-h,biPlanes=1,biBitCount=32,biCompression=0};
  IntPtr bits;
  dibHandle=CreateDIBSection(screenDc,ref bmi,0,out bits,IntPtr.Zero,0);
  oldBitmap=SelectObject(memoryDc,dibHandle);
  canvas=new Bitmap(w,h,w*4,PixelFormat.Format32bppPArgb,bits);
  canvasGraphics=Graphics.FromImage(canvas);
  canvasGraphics.CompositingMode=CompositingMode.SourceCopy;
  canvasGraphics.CompositingQuality=CompositingQuality.HighSpeed;
  canvasGraphics.InterpolationMode=InterpolationMode.Bilinear;
  canvasGraphics.PixelOffsetMode=PixelOffsetMode.Half;
  Text="DeepSeek 面板动画";FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;
  Bounds=unionBounds;
 }
 public void Render(Rectangle bounds,byte alpha){
  HasFrame=true;CurrentAlpha=alpha;
  canvasGraphics.Clear(Color.Transparent);
  var dest=new Rectangle(bounds.X-surfaceBounds.X,bounds.Y-surfaceBounds.Y,bounds.Width,bounds.Height);
  canvasGraphics.DrawImage(snapshot,dest,0,0,snapshot.Width,snapshot.Height,GraphicsUnit.Pixel);
  var destination=new NativePoint(surfaceBounds.X,surfaceBounds.Y);
  var origin=new NativePoint(0,0);
  var size=new NativeSize(surfaceBounds.Width,surfaceBounds.Height);
  var blend=new Blend{Alpha=alpha,Format=1};
  UpdateLayeredWindow(Handle,screenDc,ref destination,ref size,memoryDc,ref origin,0,ref blend,2);
 }
 protected override void Dispose(bool disposing){
  if(disposing){
   canvasGraphics.Dispose();
   canvas.Dispose();
   snapshot.Dispose();
  }
  if(memoryDc!=IntPtr.Zero){
   if(oldBitmap!=IntPtr.Zero){SelectObject(memoryDc,oldBitmap);oldBitmap=IntPtr.Zero;}
   DeleteDC(memoryDc);memoryDc=IntPtr.Zero;
  }
  if(dibHandle!=IntPtr.Zero){DeleteObject(dibHandle);dibHandle=IntPtr.Zero;}
  if(screenDc!=IntPtr.Zero){ReleaseDC(IntPtr.Zero,screenDc);screenDc=IntPtr.Zero;}
  base.Dispose(disposing);
 }
}
}
