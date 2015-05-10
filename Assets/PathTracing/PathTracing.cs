using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace PT {

	public struct Vec {
		public double x,y,z;                 // position,also color (r,g,b)
		public Vec(double x_=0,double y_=0,double z_=0) {x=x_;y=y_;z=z_;}
		public static Vec operator +(Vec a,Vec b) {return new Vec(a.x+b.x,a.y+b.y,a.z+b.z);}
		public static Vec operator -(Vec a,Vec b) {return new Vec(a.x-b.x,a.y-b.y,a.z-b.z);}
		public static Vec operator *(Vec a,double b) {return new Vec(a.x*b,a.y*b,a.z*b);}
		public Vec mult(Vec b) { return new Vec(x*b.x,y*b.y,z*b.z);}
		public Vec norm() { 
			var t = this * (1 / Math.Sqrt (x * x + y * y + z * z));
			this.x = t.x;
			this.y = t.y;
			this.z = t.z;
			return this;
		}
		public double dot(Vec b) { return x*b.x+y*b.y+z*b.z;}
		//cross:
		public static Vec operator %(Vec a,Vec b) { return new Vec(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);}
		//magnitude
		public double magnitude
		{
			get { 
				return Math.Sqrt (x * x + y * y + z * z);
			}
		}
	}

	public class Camera{

		public Vec postion;
		private Vec target;
		public Vec cx;
		public Vec cy;
		public Vec dir;
		private double near = 1;

		public double AngleToRadians(double angle)
		{
			return angle * Math.PI / 180;
		}

		public Camera(Vec pos, Vec dir, double fov, double ratio)
		{
			postion = pos;		
			this.dir = dir;
			cx=new Vec(ratio*0.5135);
			cy=(cx%dir).norm()*0.5135;
			/*cy = new Vec(0,1,0);
			cx = (cy % dir).norm ();
			cy = (dir % cx).norm ();
			double height = Math.Tan (AngleToRadians (fov/2))*near*2;
			UnityEngine.Debug.Log ("cx:" + Debug.ToString (cx) + "/cy:"+Debug.ToString(cy)+"/height:"+height);
			cy = cy * height;
			cx = cx * height * ratio;*/
		}
	}

	public class SavePic
	{
		private string path;
		private Texture2D tex;
		private int width;
		private int height;
		public SavePic(string path, int width, int height)
		{
			this.path = path;
			tex = new Texture2D (width, height);
			this.width = width;
			this.height = height;
		}

		public void SetPixel(int u, int v, Color c)
		{
			c.a = 1f;
			tex.SetPixel (u, height - v, c);
		}

		public void Save()
		{
			File.WriteAllBytes(path, tex.EncodeToPNG ());
			UnityEngine.Debug.Log(path);
		}
	}

	public class PathTracing {

		public static List<Renderable> renderList = new List<Renderable>();

		// rand double from 0.0 to 1.0
		private static System.Random[] rand = new System.Random[4];
		public static double erand48(int index)
		{
			if (rand[index] == null)
				rand[index] = new System.Random();			
			return rand[index].NextDouble();
		}

		public static double clamp(double x)
		{
			return (x<0?0:(x>1?1:x));		
		}

		// fixed value = 2.2
		public static float Gamma(double x)
		{		
			return (float)Math.Pow (clamp (x), 1 / 2.2);			
		}

		public class Ray
		{
			public Vec pos;
			public Vec dir;
			public Ray()
			{}
			public Ray(Vec pos, Vec dir)
			{
				this.pos = pos;
				this.dir = dir.norm();
			}
			public Vec GetPoint(double distance)
			{
				return pos + dir * distance;
			}
		}

		public class InterResult
		{
			public Vec pos;
			public Vec normal;
			public double distance;
			public Renderable renderable;
		}	

		public enum Relf 
		{
			DIFF,
			SPEC,
			REFR,
		}

		public class Renderable
		{
			// object color
			public Vec c;
			// emission
			public Vec e;
			// material
			public Relf refl;
			public virtual InterResult Intersect(Ray ray){
				return null;
			}		
		}

		public class Sphere : Renderable
		{
			public Vec  pos;
			public double	radius;						
			public Sphere(Vec p, double r, Vec c, Vec e, Relf refl)
			{
				pos = p;
				radius = r;			
				this.refl = refl;
				this.c = c;
				this.e = e;
			}

			public override InterResult Intersect(Ray ray)
			{			
				Vec op = pos-ray.pos;
				double t, eps = 1e-4;
				double b = op.dot(ray.dir);
				double det = b*b-op.dot(op)+radius*radius;
				if (det < 0)
					return null;
				else
					det = Math.Sqrt(det);
				InterResult result = new InterResult ();
				result.distance = (t=b-det)>eps?t: ((t=b+det)>eps ? t:0);
				if (result.distance == 0)
					return null;
				result.pos = ray.GetPoint (result.distance);
				result.normal = (result.pos - this.pos).norm();					
				result.renderable = this;					 
				return result;

				/*Vec v = ray.pos-this.pos;		
				double ddotv = ray.dir.dot(v);
				if (ddotv <= 0) {
					double param1 = ddotv * ddotv;
					double param2 = v.magnitude * v.magnitude - this.radius * this.radius;				
					if (param1 - param2 >= 0) {					
						InterResult result = new InterResult ();
						result.distance = -ddotv - Math.Sqrt (param1 - param2);
						result.pos = ray.GetPoint (result.distance);
						result.normal = (result.pos - this.pos).norm();					
						result.renderable = this;					 
						return result;
					}
				}

				return null;*/
			}
		}

		public static InterResult Intersect(Ray inray)
		{
			InterResult nearest = null;
			double min_dis = 0;
			foreach (var renderable in renderList) {
				var res = renderable.Intersect (inray);
				if (res != null && (nearest == null || res.distance < min_dis)) {
					nearest = res;
					min_dis = res.distance;
				}
			}
			return nearest;
		}

		public static Vec Radiance(Ray r, int depth, int thread, int E = 1)
		{
			// distance to intersection
			double t;
			
			InterResult res = Intersect(r);
			// if non-intersect or recursive num > 10
			if (res == null)
				return new Vec();							
			var obj = res.renderable;
			
			// todo:
			if (depth > 2)
				return obj.e;
			
			// ray intersect point
			Vec x = res.pos;
			// normal
			Vec n = res.normal;
			// oriented surface normal: determine if is entering or exiting glass
			Vec n1 = r.dir.dot(n)<0?n:n*-1;
			// object color (BRDF modulator)
			Vec f = obj.c;


			// Use maximum reflectivity amount for Russian Roulette			
			double p = f.x>f.y && f.x>f.z ? f.x : f.y>f.z?f.y:f.z; // max refl
			if (++depth>5) {
				if (erand48(thread)<p)
					f = f*(1/p); // todo: why?
				else
					return obj.e; //todo:E?
			}

			// ideal diffuse reflection
			if (obj.refl == Relf.DIFF)
			{
				double r1 = 2*Math.PI*erand48(thread);
				double r2 = erand48(thread);
				double r2s = Math.Sqrt(r2);
				// todo: how to determine the coordinates
				Vec w = n1;
				Vec u = ((Math.Abs(w.x)>0.1?new Vec(0,1):new Vec(1))%w).norm();
				Vec v = w%u;
				// sampling unit hemisphere
				Vec d = (u*Math.Cos(r1)*r2s + v*Math.Sin(r1)*r2s + w*Math.Sqrt(1-r2)).norm();
				// todo: sample lights for shadow

				return obj.e + f.mult(Radiance(new Ray(x,d),depth, thread));
			}
			// ideal specualr reflection
			else if (obj.refl == Relf.SPEC)
			{
				Vec rrr = r.dir-n*2*n.dot(r.dir);
				return obj.e + f.mult(Radiance(new Ray(x, rrr), depth, thread));
			}
			// ideal dielectric(glass) reflection
			var reflRay = new Ray(x, r.dir-n*2*n.dot(r.dir));
			// Ray from outside going in?
			bool into = n.dot(n1)>0;
			double nc=1;
			double nt=1.5; // IOR for glass is 1.5
			double nnt=into?nc/nt:nt/nc;
			double ddn=r.dir.dot(n1);
			double cos2t;
			// if total internal reflection, reflect
			// todo: why?
			if ((cos2t=1-nnt*nnt*(1-ddn*ddn))<0)
				return obj.e + f.mult(Radiance(reflRay,depth,thread));
			// otherwise, choose reflection or refraction
			Vec tdir = (r.dir*nnt - n*((into?1:-1)*(ddn*nnt+Math.Sqrt(cos2t)))).norm();
			double a=nt-nc;
			double b=nt+nc;
			double R0=a*a/(b*b);
			double c=1-(into?-ddn:tdir.dot(n));
			double Re=R0+(1-R0)*c*c*c*c*c;
			double Tr=1-Re;
			double P=0.25+0.5*Re;
			double RP=Re/P;
			double TP=Tr/(1-p);
			return obj.e + f.mult(depth>2?(erand48(thread)<P?Radiance(reflRay,depth,thread)*RP:Radiance(new Ray(x,tdir),depth,thread)*TP):
			                      Radiance(reflRay,depth,thread)*Re+Radiance(new Ray(x,tdir),depth,thread)*Tr);
			                            
		}

		public static void Update()
		{
			if (start) {
				/*if (steps / (_width * _height / 100) != rate) {
					rate = steps / (_width * _height / 100);
					UnityEngine.Debug.Log ("steps:"+rate+"%");
				}

				int x = steps % _width;
				int y = steps / _width;
				Pixel (x, y);
				steps++;*/

				//if (steps >= _width * _height) {					
				//_semaphone.WaitOne();
				//_semaphone.WaitOne();
				/*_semaphone.WaitOne();
				_semaphone.WaitOne();*/

				int completed = 0;
				foreach (var b in _threadCompelte) {
					completed += b ? 1 : 0;
				}

				if (completed == 4)
				{
					Debug.EndTime ();
					start = false;					
					SavePic pic = new SavePic (Application.persistentDataPath + "/" + _fileName , _width, _height);

					for (int yy = 0; yy <_height; yy++){
						for (int xx = 0; xx < _width; xx++) {
							int i = (_height-yy-1)*_width+xx;
							pic.SetPixel(xx, _height-yy, new Color(Gamma(_c[i].x), Gamma(_c[i].y), Gamma(_c[i].z), 1.0f));
						}
					}
					pic.Save();
				}
			}
		}
		public static int steps = 0;
		public static bool start = false;					
		public static int rate = -1;

		public static void Pixel(int x, int y, int thread)
		{
			int width = _width;
			int height = _height;
			int samples = _samples;
			Camera camera = _camera;

			Vec r = new Vec ();
			// sy,sy are subpixels 2x2
			for (int sy = 0, i=(height-y-1)*width+x; sy<2; sy++){
				for (int sx = 0; sx < 2; sx++, r = new Vec ()) {
					
					// all samples
					for (int s = 0; s < samples; s++) {
						
						// tent filter: http://www.realitypixels.com/turk/computergraphics/ResamplingFilters.pdf
						double r1 = 2 * erand48(thread);												
						double dx = r1 < 1 ? Math.Sqrt (r1) - 1 : 1 - Math.Sqrt (2 - r1);												
						double r2 = 2 * erand48 (thread);						
						double dy = r2 < 1 ? Math.Sqrt (r2) - 1 : 1 - Math.Sqrt (2 - r2);
						
						Vec d = camera.cx * (((sx + 0.5 + dx) / 2 + x) / width - 0.5) +
							camera.cy * (((sy + 0.5 + dy) / 2 + y) / height - 0.5) +
							camera.dir;
						r = r + Radiance(new Ray(camera.postion+d*140, d.norm()), 0, thread)*(1f/samples);
						//UnityEngine.Debug.Log("ray /pos:"+camera.postion.ToString()+"/dir:"+d.ToString());
					}
					_c [i] = _c [i] + (new Vec (clamp (r.x), clamp (r.y), clamp (r.z))) * 0.25;
				}			
			}
		}

		static Vec []_c;
		public static int _width;
		static int _height;
		static int _samples;
		static Camera _camera;
		static string _fileName;
		public static int _block;
		public static int _maxCount;
		public static Semaphore _semaphone = new Semaphore (0, 4);
		public static bool[] _threadCompelte = new bool[4];

		public static void Main(int width, int height, int samples, string name, List<Renderable> list, Camera camera)
		{						
			Debug.StartTime ();
			renderList.Clear();
			renderList.AddRange(list);

			_fileName = name;
			_c = new Vec[width*height];
			_width = width;
			_height = height;
			_samples = samples;
			_camera = camera;
			start = true;
			steps = 0;

			// threading
			_block = width*height/4;
			_maxCount = width * height;
			for (int i = 0; i < 4; i++) {
				var worker = new Worker (i);
				Thread t= new Thread(new ParameterizedThreadStart(worker.Do));
				t.Start();
			}							
		}


	}

	class Worker
	{
		int index;
		public  Worker(int i)
		{
			index = i;
		}

		public void Do(object num)
		{						
			UnityEngine.Debug.Log ("thread " + index + " start " + (index*PathTracing._block) + "/" + PathTracing._block);

			if (index == 3) {
				int steps = PathTracing._block / 10;
				for (int i = index*PathTracing._block; i < PathTracing._maxCount; i++) {
					if (steps-- <= 0)					
					{
						steps = PathTracing._block / 10;						
						UnityEngine.Debug.Log("Thread " + index + ": " + (i-index*PathTracing._block)*100/PathTracing._block + "%");
					}
					int x = i % PathTracing._width;
					int y = i / PathTracing._width;
					PathTracing.Pixel (x, y, index);
				}
			}
			else {
				int steps = PathTracing._block / 10;
				for (int i = index*PathTracing._block; i < index*PathTracing._block+PathTracing._block; i++) {						
					if (steps-- <= 0)					
					{
						steps = PathTracing._block / 10;						
						UnityEngine.Debug.Log("Thread " + index + ": " + (i-index*PathTracing._block)*100/PathTracing._block + "%");
					}

					int x = i % PathTracing._width;
					int y = i / PathTracing._width;
					PathTracing.Pixel (x, y, index);
				}
			}

			PathTracing._threadCompelte [index] = true;
			UnityEngine.Debug.Log ("thread " + index + " complete");
			PathTracing._semaphone.Release ();
		}
	}		

	public static class Debug {
		public static string ToString(Vec v)
		{
			return "[" + v.x + "," + v.y + "," + v.z + "]";
		}

		public static string ToString(this Camera c)
		{
			return "(Camera:cx:" + c.cx.ToString() + "/cy:"+ c.cy.ToString()+")";
		}

		private static float time;
		public static void StartTime()
		{
			time = Time.realtimeSinceStartup;
		}
		public static void EndTime()
		{
			UnityEngine.Debug.Log ("time:" + (Time.realtimeSinceStartup-time));
		}
	}
}

