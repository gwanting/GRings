using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DeepSeekBalance {
sealed class PanelMotion:IDisposable {
 [DllImport("winmm.dll")]static extern uint timeBeginPeriod(uint period);
 [DllImport("winmm.dll")]static extern uint timeEndPeriod(uint period);
 readonly Form owner;
 readonly Func<Bitmap> capture;
 readonly Action restore;
 readonly Timer timer=new Timer{Interval=8};
 readonly Stopwatch clock=new Stopwatch();
 MotionSurface surface;
 Rectangle from,to,fullBounds,currentBounds;
 Point anchor;
 bool opening,disposed,highResTimer;
 double duration;
 byte startAlpha;
 public bool Running{get{return timer.Enabled;}}
 public bool TargetVisible{get{return Running?opening:owner.Visible;}}
 public PanelMotion(Form form,Func<Bitmap> snapshot,Action refresh){owner=form;capture=snapshot;restore=refresh;timer.Tick+=(s,e)=>Tick();}
 static Rectangle Small(Rectangle full,Point center){int w=Math.Max(1,(int)Math.Round(full.Width*0.18)),h=Math.Max(1,(int)Math.Round(full.Height*0.18));return new Rectangle(center.X-w/2,center.Y-h/2,w,h);}
 void EnableHighRes(bool enable){if(enable==highResTimer)return;highResTimer=enable;try{if(enable)timeBeginPeriod(1);else timeEndPeriod(1);}catch{}}
 public void Show(Point origin){
  if(disposed)return;if(Running&&opening)return;if(owner.Visible&&!Running)return;
  anchor=origin;fullBounds=owner.Bounds;
  Rectangle small=Small(fullBounds,anchor);
  Rectangle start=surface==null?small:currentBounds;
  if(surface==null)surface=new MotionSurface(capture(),Rectangle.Union(fullBounds,small));
  owner.Hide();Start(start,fullBounds,true);
 }
 public void Hide(Point origin){
  if(disposed||Running&&!opening)return;if(!owner.Visible&&!Running)return;
  anchor=origin;fullBounds=owner.Bounds;
  Rectangle small=Small(fullBounds,anchor);
  Rectangle start=surface==null?fullBounds:currentBounds;
  if(surface==null)surface=new MotionSurface(capture(),Rectangle.Union(fullBounds,small));
  if(!surface.HasFrame){currentBounds=start;surface.Render(start,255);}
  surface.Show();owner.Hide();Start(start,small,false);
 }
 void Start(Rectangle start,Rectangle end,bool show){
  from=start;to=end;currentBounds=start;opening=show;
  startAlpha=surface.HasFrame?surface.CurrentAlpha:show?(byte)40:(byte)255;
  duration=show?180:140;
  EnableHighRes(true);
  clock.Restart();
  surface.Render(from,startAlpha);
  if(!surface.Visible)surface.Show();
  timer.Start();
 }
 void Tick(){
  if(disposed||owner.IsDisposed){Dispose();return;}
  double fraction=Math.Min(1,clock.Elapsed.TotalMilliseconds/duration);
  double ease=opening?1-Math.Pow(1-fraction,3):fraction*fraction;
  currentBounds=new Rectangle(Mix(from.X,to.X,ease),Mix(from.Y,to.Y,ease),Math.Max(1,Mix(from.Width,to.Width,ease)),Math.Max(1,Mix(from.Height,to.Height,ease)));
  surface.Render(currentBounds,(byte)Math.Max(0,Math.Min(255,startAlpha+((opening?255:0)-startAlpha)*ease)));
  if(fraction<1)return;
  timer.Stop();clock.Stop();EnableHighRes(false);
  if(opening){owner.Bounds=fullBounds;restore();owner.Show();owner.Update();}
  surface.Dispose();surface=null;
 }
 static int Mix(int a,int b,double amount){return (int)Math.Round(a+(b-a)*amount);}
 public void Dispose(){if(disposed)return;disposed=true;timer.Stop();timer.Dispose();EnableHighRes(false);if(surface!=null){surface.Dispose();surface=null;}}
}

sealed class MotionSurface:Form {
 readonly Bitmap snapshot,canvas;
 readonly Graphics canvasGraphics;
 readonly Rectangle surfaceBounds;
 IntPtr screenDc,memoryDc;
 public bool HasFrame{get;private set;}
 public byte CurrentAlpha{get;private set;}
 [StructLayout(LayoutKind.Sequential)]struct NativePoint{public int X,Y;public NativePoint(int x,int y){X=x;Y=y;}}
 [StructLayout(LayoutKind.Sequential)]struct NativeSize{public int Width,Height;public NativeSize(int width,int height){Width=width;Height=height;}}
 [StructLayout(LayoutKind.Sequential,Pack=1)]struct Blend{public byte Operation,Flags,Alpha,Format;}
 [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr window);
 [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr window,IntPtr dc);
 [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr dc);
 [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr item);
 [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr item);
 [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
 [DllImport("user32.dll")]static extern bool UpdateLayeredWindow(IntPtr window,IntPtr screen,ref NativePoint destination,ref NativeSize size,IntPtr source,ref NativePoint origin,uint key,ref Blend blend,uint flags);
 protected override bool ShowWithoutActivation{get{return true;}}
 protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x80|0x80000|0x20;return p;}}
 public MotionSurface(Bitmap image,Rectangle unionBounds){
  snapshot=image;
  surfaceBounds=unionBounds;
  canvas=new Bitmap(Math.Max(1,unionBounds.Width),Math.Max(1,unionBounds.Height),PixelFormat.Format32bppPArgb);
  canvasGraphics=Graphics.FromImage(canvas);
  canvasGraphics.CompositingMode=CompositingMode.SourceCopy;
  canvasGraphics.CompositingQuality=CompositingQuality.HighSpeed;
  canvasGraphics.InterpolationMode=InterpolationMode.Bilinear;
  canvasGraphics.PixelOffsetMode=PixelOffsetMode.Half;
  screenDc=GetDC(IntPtr.Zero);
  memoryDc=CreateCompatibleDC(screenDc);
  Text="DeepSeek 面板动画";FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;
  Bounds=unionBounds;
 }
 public void Render(Rectangle bounds,byte alpha){
  HasFrame=true;CurrentAlpha=alpha;
  canvasGraphics.Clear(Color.Transparent);
  var dest=new Rectangle(bounds.X-surfaceBounds.X,bounds.Y-surfaceBounds.Y,bounds.Width,bounds.Height);
  canvasGraphics.DrawImage(snapshot,dest,0,0,snapshot.Width,snapshot.Height,GraphicsUnit.Pixel);
  IntPtr bitmap=canvas.GetHbitmap(Color.FromArgb(0));
  IntPtr old=SelectObject(memoryDc,bitmap);
  try{
   var destination=new NativePoint(surfaceBounds.X,surfaceBounds.Y);
   var origin=new NativePoint(0,0);
   var size=new NativeSize(surfaceBounds.Width,surfaceBounds.Height);
   var blend=new Blend{Alpha=alpha,Format=1};
   UpdateLayeredWindow(Handle,screenDc,ref destination,ref size,memoryDc,ref origin,0,ref blend,2);
  }finally{
   SelectObject(memoryDc,old);
   DeleteObject(bitmap);
  }
 }
 protected override void Dispose(bool disposing){
  if(disposing){
   canvasGraphics.Dispose();
   canvas.Dispose();
   snapshot.Dispose();
  }
  if(memoryDc!=IntPtr.Zero){DeleteDC(memoryDc);memoryDc=IntPtr.Zero;}
  if(screenDc!=IntPtr.Zero){ReleaseDC(IntPtr.Zero,screenDc);screenDc=IntPtr.Zero;}
  base.Dispose(disposing);
 }
}
}
