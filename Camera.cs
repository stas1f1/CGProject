using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.DoubleNumerics;
using System.Windows.Forms;
using static RTTest1.Objects;
using static RTTest1.Utils;
using System.Diagnostics;

namespace RTTest1
{
    public class Camera
    {
        public FormUpdater updateDelegate = null;
        public Point3D pos;
        public Point[] Screencoords;
        public Stopwatch stopwatch = new Stopwatch();
        Color AmbientColor = Color.DarkGray;
        bool scatteredRays = false;

        public Camera(Point3D pos, Point lu, Point br, bool scatteredRays = false)
        {
            this.pos = pos;
            Screencoords = new Point[]{ lu, br};
            this.scatteredRays = scatteredRays;
        }

        public Point3D SetCamVector(int _x, int _y, int w, int h)
        {
            int x = _x - w / 2;
            int y = _y - h / 2;
            int z = (int)pos.Z;
            Point3D cameraVector = new Point3D(x, y, -z);
            double distance = Distance(cameraVector, new Point3D(0, 0, 0));
            cameraVector.X = cameraVector.X / distance;
            cameraVector.Y = cameraVector.Y / distance;
            cameraVector.Z = cameraVector.Z / distance;
            return cameraVector;
        }

        public void Render(Bitmap bm, PictureBox pb)
        {
            Graphics g = Graphics.FromImage(bm);
            g.Clear(Color.Transparent);

            Point3D cameraLocation = new Point3D(pos);

            int l = Math.Max(0, Screencoords[0].X);
            int r = Math.Min(bm.Width, Screencoords[1].X);

            int u = Math.Max(0, Screencoords[0].Y);
            int d = Math.Min(bm.Height, Screencoords[1].Y);

            int w = r - l;
            int h = d - u;
            int total = w * h;
            stopwatch.Reset();
            stopwatch.Start();
            int pDone = 0;

            for (int i = l; i < r; i++)
                for (int j = u; j < d; j++)
                {
                    bm.SetPixel(i, j, RayCast(cameraLocation, SetCamVector(i, j, bm.Width, bm.Height), 3));
                    pDone++;
                    updateDelegate(((double)pDone) / total, stopwatch.Elapsed);
                }
            stopwatch.Stop();
            pb.Refresh();
        }

        public Color RayCast(Point3D pos, Point3D dir, int depth)
        {
            if (depth == 0) return Color.Black;
            Point3D hit = new Point3D();
            Point3D normal = new Point3D();
            Material hitMaterial = new Material();
            double minDist = double.MaxValue;
            bool found = false;
            double eps = 0.001;


            foreach (Mesh m in meshes)
            {
                foreach (Polygon pol in m.faces)
                {
                    double t = 0;
                    Point3D P = new Point3D();
                    if (!pol.Ray_intersection(pos, dir, ref P, ref t))
                        continue;


                    if (t < minDist)
                    {
                        found = true;
                        minDist = t;
                        hit = P;
                        normal = pol.normal;
                        hitMaterial = m.material;
                    }
                }
            }
            foreach (Sphere sphere in spheres)
            {
                double t = 0;
                if (sphere.ray_intersection(pos, dir, ref t))
                {
                    if (t < minDist)
                    {
                        found = true;
                        minDist = t;
                        hit = pos + dir * t;
                        normal = Normalize(hit - sphere.pos);
                        hitMaterial = sphere.mat;
                    }
                }
            }

            if (found)
            {
                double difIntensitySumR = 0;
                double difIntensitySumG = 0;
                double difIntensitySumB = 0;
                double specIntensitySum = 0;

                double ReflectionR = 0;
                double ReflectionG = 0;
                double ReflectionB = 0;

                double RefractionR = 0;
                double RefractionG = 0;
                double RefractionB = 0;

                Color DiffuseReflectedCol = AmbientColor;

                if (hitMaterial.parameters[3] > 0)
                {
                    Point3D RefractRay = Normalize(RayRefract(dir, normal, hitMaterial.refractionIndex));
                    Point3D RefractLoc = RefractRay * normal < 0 ? hit - normal * eps : hit + normal * eps;
                    Color RefractCol = RayCast(RefractLoc, RefractRay, depth - 1);

                    RefractionR = RefractCol.R * hitMaterial.parameters[3];
                    RefractionG = RefractCol.G * hitMaterial.parameters[3];
                    RefractionB = RefractCol.B * hitMaterial.parameters[3];
                }

                if (hitMaterial.parameters[2] > 0)
                {
                    Point3D ReflectRay = RayReflect(dir, normal);
                    Point3D ReflectLoc = ReflectRay * normal < 0 ? hit - normal * eps : hit + normal * eps;
                    Color ReflectCol = RayCast(ReflectLoc, ReflectRay, depth - 1);

                    ReflectionR = ReflectCol.R * hitMaterial.parameters[2] * (1 - hitMaterial.parameters[3]);
                    ReflectionG = ReflectCol.G * hitMaterial.parameters[2] * (1 - hitMaterial.parameters[3]);
                    ReflectionB = ReflectCol.B * hitMaterial.parameters[2] * (1 - hitMaterial.parameters[3]);
                }

                if (hitMaterial.parameters[0] > 0)
                    if (scatteredRays)
                    {
                        List<Point3D> DiffuseRays = MakeDiffuseRays(hit, normal, 4, 8); // 4 8
                        Point3D DiffuseLoc = DiffuseRays[0] * normal < 0 ? hit - normal * eps : hit + normal * eps;
                        int[] colSum = new int[3] { 0, 0, 0 };

                        foreach (var DiffuseRay in DiffuseRays)
                        {
                            Color curDifCol = RayCast(DiffuseLoc, DiffuseRay, depth - 1);
                            colSum[0] += curDifCol.R;
                            colSum[1] += curDifCol.G;
                            colSum[2] += curDifCol.B;
                        }
                        colSum[0] /= DiffuseRays.Count;
                        colSum[1] /= DiffuseRays.Count;
                        colSum[2] /= DiffuseRays.Count;
                        DiffuseReflectedCol = Color.FromArgb(colSum[0], colSum[1], colSum[2]);
                    }
                    else
                    { }

                foreach (Light l in lights)
                {
                    Point3D lightVecFull = l.pos - hit;
                    double light_distance = lightVecFull.Length();
                    Point3D lightVec = Normalize(lightVecFull);


                    Point3D shadow_origin = lightVec * normal < 0 ? hit - normal * eps : hit + normal * eps;
                    Point3D shadow_destination = new Point3D();
                    Point3D shadow_N = new Point3D();
                    Material tmat = new Material();
                    if (FindIntersection(shadow_origin, lightVec, ref shadow_destination, ref shadow_N, ref tmat) && (shadow_destination - shadow_origin).Length() < light_distance)
                        continue;
                    
                    double intensity = LightFalloff(light_distance, 800) * l.intensity * Math.Max(0.0, (lightVec * normal));
                    difIntensitySumR += (l.color.R / 255.0) * intensity;
                    difIntensitySumG += (l.color.G / 255.0) * intensity;
                    difIntensitySumB += (l.color.B / 255.0) * intensity;
                    specIntensitySum += Math.Pow(Math.Max(0.0, (RayReflect(lightVec, normal) * dir)), hitMaterial.specularHighlight) * l.intensity;
                }
                double lightnessR = difIntensitySumR * (1 - hitMaterial.parameters[2]) * (1 - hitMaterial.parameters[3]);
                double lightnessG = difIntensitySumG * (1 - hitMaterial.parameters[2]) * (1 - hitMaterial.parameters[3]);
                double lightnessB = difIntensitySumB * (1 - hitMaterial.parameters[2]) * (1 - hitMaterial.parameters[3]);

                double addspec = 255.0 * specIntensitySum * hitMaterial.parameters[1];

                double colR = hitMaterial.color.R * (1 - hitMaterial.parameters[0]) * lightnessR + DiffuseReflectedCol.R * hitMaterial.parameters[0];
                double colG = hitMaterial.color.G * (1 - hitMaterial.parameters[0]) * lightnessG + DiffuseReflectedCol.G * hitMaterial.parameters[0];
                double colB = hitMaterial.color.B * (1 - hitMaterial.parameters[0]) * lightnessB + DiffuseReflectedCol.B * hitMaterial.parameters[0];

                int r = (int)(colR + addspec + ReflectionR + RefractionR);
                r = r > 255 ? 255 : r;
                int g = (int)(colG + addspec + ReflectionG + RefractionG);
                g = g > 255 ? 255 : g;
                int b = (int)(colB + addspec + ReflectionB + RefractionB);
                b = b > 255 ? 255 : b;
                return Color.FromArgb(r, g, b);
            }
            return AmbientColor;
        }
    }
}
