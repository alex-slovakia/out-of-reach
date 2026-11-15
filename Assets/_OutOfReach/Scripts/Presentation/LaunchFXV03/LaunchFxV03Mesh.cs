using UnityEngine;

namespace OutOfReach.Presentation.LaunchFXV03
{
    internal static class LaunchFxV03Mesh
    {
        internal static Mesh Create()
        {
            const int rings=32,sides=16;
            var v=new Vector3[(rings+1)*(sides+1)]; var uv=new Vector2[v.Length];
            var triangles=new int[rings*sides*6];
            for(int r=0;r<=rings;r++) for(int s=0;s<=sides;s++)
            {
                float z=(float)r/rings, angle=s*2f*Mathf.PI/sides;
                float radius=Mathf.Lerp(.72f,1.48f,Mathf.Sqrt(z))*(1f-Mathf.Pow(z,3.5f)*.82f);
                int index=r*(sides+1)+s;
                v[index]=new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,z);
                uv[index]=new Vector2((float)s/sides,z);
            }
            int k=0;
            for(int r=0;r<rings;r++) for(int s=0;s<sides;s++)
            {
                int a=r*(sides+1)+s,b=a+sides+1;
                triangles[k++]=a; triangles[k++]=a+1; triangles[k++]=b;
                triangles[k++]=a+1; triangles[k++]=b+1; triangles[k++]=b;
            }
            var mesh=new Mesh { name="Original V03 exhaust volume",hideFlags=HideFlags.DontSave };
            mesh.vertices=v;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();
            mesh.bounds=new Bounds(new Vector3(0,0,.5f),new Vector3(4,4,1.1f));
            return mesh;
        }
    }
}
