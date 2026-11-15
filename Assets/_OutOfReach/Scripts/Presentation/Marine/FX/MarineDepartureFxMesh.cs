using UnityEngine;

namespace OutOfReach.Presentation.MarineOcean.FX
{
    /// <summary>Original bounded 3D effect geometry. No billboards, colliders or imported models.</summary>
    internal static class MarineDepartureFxMesh
    {
        internal static Mesh CreatePlume()
        {
            const int radial = 32, longitudinal = 32;
            var vertices = new Vector3[(radial + 1) * (longitudinal + 1) + 2];
            var uv = new Vector2[vertices.Length]; var normals = new Vector3[vertices.Length];
            var indices = new int[(radial * longitudinal * 2 + radial * 2) * 3];
            for (int row = 0; row <= longitudinal; row++) for (int col = 0; col <= radial; col++)
            {
                float angle = col / (float)radial * Mathf.PI * 2, t = row / (float)longitudinal;
                int i = row * (radial + 1) + col;
                vertices[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), t);
                normals[i] = new Vector3(vertices[i].x, vertices[i].y, 0); uv[i] = new Vector2(col / (float)radial, t);
            }
            int p = 0;
            for (int row = 0; row < longitudinal; row++) for (int col = 0; col < radial; col++)
            {
                int a = row * (radial + 1) + col, b = a + radial + 1;
                indices[p++] = a; indices[p++] = a + 1; indices[p++] = b + 1;
                indices[p++] = a; indices[p++] = b + 1; indices[p++] = b;
            }
            int start = vertices.Length - 2, end = start + 1;
            vertices[end] = Vector3.forward; normals[start] = Vector3.back; normals[end] = Vector3.forward; uv[end] = new Vector2(.5f, 1);
            for (int col = 0; col < radial; col++)
            {
                indices[p++] = start; indices[p++] = col + 1; indices[p++] = col;
                int a = longitudinal * (radial + 1) + col;
                indices[p++] = end; indices[p++] = a; indices[p++] = a + 1;
            }
            var mesh = new Mesh { name = "Original 2112-triangle departure plume", hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.triangles = indices;
            mesh.bounds = new Bounds(new Vector3(0, 0, .5f), new Vector3(6f, 6f, 1.04f)); mesh.UploadMeshData(true); return mesh;
        }

        internal static Mesh CreatePressureFront()
        {
            const int major=64,minor=8;var v=new Vector3[(major+1)*(minor+1)];var n=new Vector3[v.Length];var uv=new Vector2[v.Length];
            var triangles=new int[major*minor*6];int cursor=0;
            for(int i=0;i<=major;i++)for(int j=0;j<=minor;j++)
            {
                float a=i/(float)major*Mathf.PI*2,b=j/(float)minor*Mathf.PI*2;int k=i*(minor+1)+j;
                var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                v[k]=radial*(1+.055f*Mathf.Cos(b))+Vector3.up*Mathf.Sin(b);n[k]=(radial*Mathf.Cos(b)+Vector3.up*Mathf.Sin(b)).normalized;
                uv[k]=new Vector2(i/(float)major,j/(float)minor);
                if(i==major||j==minor)continue;int next=k+minor+1;
                triangles[cursor++]=k;triangles[cursor++]=next+1;triangles[cursor++]=next;
                triangles[cursor++]=k;triangles[cursor++]=k+1;triangles[cursor++]=next+1;
            }
            var mesh=new Mesh{name="Original pressure-driven dust torus",hideFlags=HideFlags.DontSave};
            mesh.vertices=v;mesh.normals=n;mesh.uv=uv;mesh.triangles=triangles;mesh.bounds=new Bounds(Vector3.zero,new Vector3(2.5f,2.4f,2.5f));mesh.UploadMeshData(true);return mesh;
        }

        internal static Mesh CreateSteam()
        {
            const int radial = 24, rings = 12;
            var vertices = new Vector3[(rings + 1) * (radial + 1)]; var uv = new Vector2[vertices.Length];
            var indices = new int[radial * (rings - 1) * 6]; int p = 0;
            for (int row = 0; row <= rings; row++) for (int col = 0; col <= radial; col++)
            {
                float phi = row / (float)rings * Mathf.PI, angle = col / (float)radial * Mathf.PI * 2;
                int i = row * (radial + 1) + col;
                Vector3 direction = new Vector3(Mathf.Sin(phi) * Mathf.Cos(angle), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(angle));
                // Continuous 3D harmonics preserve the seam and poles while breaking the spherical outline.
                float bulge = 1f + .18f * Mathf.Sin(direction.x * 7f + direction.y * 3f)
                    * Mathf.Sin(direction.z * 6f - direction.y * 4f)
                    + .075f * Mathf.Cos(direction.y * 11f + direction.z * 4f);
                vertices[i] = direction * bulge;
                uv[i] = new Vector2(col / (float)radial, row / (float)rings);
                if (row == rings || col == radial) continue;
                int next = i + radial + 1;
                if (row > 0) { indices[p++] = i; indices[p++] = i + 1; indices[p++] = next; }
                if (row < rings - 1) { indices[p++] = i + 1; indices[p++] = next + 1; indices[p++] = next; }
            }
            var mesh = new Mesh { name = "Original 528-triangle rolling steam volume", hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = indices; mesh.RecalculateNormals();
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 3f); mesh.UploadMeshData(true); return mesh;
        }
    }
}
