using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Runtime.CompilerServices;
using OutOfReach.Composition;
using OutOfReach.Player;
using UnityEngine;

static class Program
{
    static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve+=(sender,eventArgs)=>
        {
            string file=new AssemblyName(eventArgs.Name).Name+".dll";
            string path=Path.Combine(args[0],"Library/ScriptAssemblies",file);
            if(!File.Exists(path))path=Directory.GetFiles(@"C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Data\Managed",file,SearchOption.AllDirectories).FirstOrDefault();
            return path!=null&&File.Exists(path)?Assembly.LoadFrom(path):null;
        };
        return Run();
    }
    [MethodImpl(MethodImplOptions.NoInlining)] static int Run()=>Tests.Run();
}
static class Tests
{
    delegate bool Law(Vector3 normal,Vector3 intent,float minimumAway,out Vector3 direction);
    static Law actual;
    static int count;
    static void Check(bool pass,string why){if(!pass)throw new Exception(why);count++;}
    static void Near(Vector3 a,Vector3 b,float epsilon,string why)=>Check((a-b).magnitude<=epsilon,why+" distance="+(a-b).magnitude);
    static Vector3 Resolve(Vector3 n,Vector3 v,float minimum=.2f)
    {Check(actual(n,v,minimum,out Vector3 d),"Finite nonzero vector accepted by candidate math");return d;}
    static Vector3 Inward(float tangent)=>new Vector3(-(float)Math.Sqrt(1-tangent*tangent),tangent,0);
    static Vector3 Rotate(Vector3 v,Vector3 axis,double angle)
    {axis=axis.normalized;float c=(float)Math.Cos(angle),s=(float)Math.Sin(angle);return v*c+Vector3.Cross(axis,v)*s+axis*Vector3.Dot(axis,v)*(1-c);}
    static void Bounds(Vector3 normal,Vector3 d,float away)
    {
        Check(!float.IsNaN(d.x)&&!float.IsInfinity(d.x)&&!float.IsNaN(d.y)&&!float.IsInfinity(d.y)&&!float.IsNaN(d.z)&&!float.IsInfinity(d.z),"Finite output");
        Check(Math.Abs(d.magnitude-1)<3e-6f,"Unit direction preserves Player force magnitude");
        Check(Vector3.Dot(normal.normalized,d)>=away-3e-6f,"Direction respects exact existing outward cone");
    }
    public static int Run()
    {
        actual=(Law)typeof(I06MicrogravityBridge).GetMethod("TryDirectedPushDirection",BindingFlags.NonPublic|BindingFlags.Static).CreateDelegate(typeof(Law));
        var tuning=new MicrogravityLocomotion();Vector3 n=Vector3.right;
        Vector3 desired=new Vector3(1,1,0).normalized,d=Resolve(n,desired);
        Near(d,desired,1e-6f,"45-degree outward intent is retained, not flattened to14.48degrees above plane");
        Vector3 tangential=Resolve(n,Vector3.forward);
        Near(tangential,n*.25f+Vector3.forward*(float)Math.Sqrt(1-.25*.25),1e-6f,"Original tangential W cone remains exactly unchanged");
        Near(Resolve(n,-n),n,1e-6f,"Exactly inward intent gives normal push");
        Near(Resolve(n,n),n,1e-6f,"Exactly outward intent gives normal push");
        foreach(float tiny in new[]{0f,1e-10f,1e-8f,1e-6f,1e-4f,.001f})
        {
            Vector3 positive=Resolve(n,Inward(tiny)),negative=Resolve(n,Inward(-tiny));
            Near(positive,n,.000064f,"Nearly inward positive tangent tends to normal");
            Near(negative,n,.000064f,"Nearly inward negative tangent tends to normal");
            Bounds(n,positive,.25f);Bounds(n,negative,.25f);
        }
        Vector3 cutoffBelow=Resolve(n,Inward(.25f-1e-5f)),cutoffAt=Resolve(n,Inward(.25f)),cutoffAbove=Resolve(n,Inward(.25f+1e-5f));
        Near(cutoffBelow,cutoffAt,2e-6f,"Continuous lower side of25percent tangent join");
        Near(cutoffAt,cutoffAbove,2e-6f,"Continuous upper side of25percent tangent join");
        Vector3 coneBelow=new Vector3(.25f-1e-5f,(float)Math.Sqrt(1-Math.Pow(.25f-1e-5f,2)),0);
        Vector3 coneAbove=new Vector3(.25f+1e-5f,(float)Math.Sqrt(1-Math.Pow(.25f+1e-5f,2)),0);
        Near(Resolve(n,coneBelow),Resolve(n,coneAbove),.00003f,"Continuous away-dot join; derivative may change");
        foreach(Vector3 invalid in new[]{Vector3.zero,new Vector3(.00001f,0,0),new Vector3(float.NaN,0,0),new Vector3(0,float.PositiveInfinity,0),new Vector3(float.MaxValue,0,0)})
        {
            Check(!actual(n,invalid,.2f,out Vector3 bad)&&bad==Vector3.zero,"Invalid intent fails closed without a usable direction");
            Check(!actual(invalid,Vector3.up,.2f,out bad)&&bad==Vector3.zero,"Invalid normal fails closed");
        }
        Check(!actual(n,Vector3.up,float.NaN,out _),"NaN tuning fails closed");
        Check(!actual(n,Vector3.up,float.PositiveInfinity,out _),"Infinite tuning fails closed");
        var random=new System.Random(688658);
        for(int i=0;i<1200;i++)
        {
            Vector3 intent=new Vector3((float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1).normalized;
            float minimum=i%5==0?.7f:.2f,away=Math.Max(.25f,minimum+.025f);
            d=Resolve(n,intent,minimum);Bounds(n,d,away);
            if(Vector3.Dot(n,intent)>=away)Near(d,intent,2e-6f,"Already-away random intent retained");
            float scale=(float)Math.Pow(10,random.NextDouble()*5-1);
            Near(Resolve(n,intent*scale,minimum),d,2e-6f,"Direction independent of intent magnitude above original deadzone");
            Vector3 axis=new Vector3((float)random.NextDouble(),(float)random.NextDouble(),(float)random.NextDouble());double angle=random.NextDouble()*Math.PI*2;
            Vector3 rn=Rotate(n,axis,angle),ri=Rotate(intent,axis,angle),rd=Resolve(rn,ri,minimum);
            Near(rd,Rotate(d,axis,angle),4e-6f,"Rotation covariance including oblique walls/ceilings");Bounds(rn,rd,away);
            Near(tuning.Push(Vector3.zero,d,.35f),d*.35f,2e-6f,"Actual Player pure law retains original strength and magnitude");
        }
        // Dense inward arc: continuity/monotonicity through both signs and the former hard cutoff.
        Vector3 prior=Resolve(n,Inward(-.26f));float largestStep=0;
        for(int i=1;i<=5200;i++)
        {
            float t=-.26f+i*.0001f;Vector3 current=Resolve(n,Inward(t));
            float step=(current-prior).magnitude;largestStep=Math.Max(largestStep,step);
            Check(step<.00081f,"Dense inward sweep has no hard normal/cone jump");Bounds(n,current,.25f);prior=current;
        }
        Console.WriteLine("PASS "+count+" checks against private method in full actual candidate source; maximum0.0001 tangent-step vector delta="+largestStep.ToString("R"));
        Console.WriteLine("Original cone/strength preserved; no native physics, contacts, input, Unity process or route execution.");
        return 0;
    }
}
