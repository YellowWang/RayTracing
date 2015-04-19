using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class RayTracing {

	public static int width = 100;
	public static int height = 100;
	public const  int recursive_num = 5;

	public static List<Renderable> renderlist = new List<Renderable>();

	public static Color black = new Color(0,0,0);
	public static Color white = new Color (1,1,1);
	public static Color red   = new Color(1,0,0);
	public static Color green = new Color(0,1,0);     
	public static Color blue = new Color (0,0,1);

	public class Ray
	{
		public Vector3 pos;
		public Vector3 dir;
		public Ray()
		{}
		public Ray(Vector3 pos, Vector3 dir)
		{
			this.pos = pos;
			this.dir = dir.normalized;
		}
		public Vector3 GetPoint(float distance)
		{
			return pos + dir * distance;
		}
	}

	#region renderable
	public class Renderable
	{
		public virtual InterResult Intersect(Ray ray){
			return null;
		}
		public Material material;		
	}

	public class Plane : Renderable
	{
		public Vector3 n;
		public float   d;
		private Vector3 pos;

		public Plane(Vector3 n, float d, Material m)
		{
			this.n = n.normalized;
			this.d = d;
			this.material = m;

			this.pos = this.n * this.d;
		}

		public override InterResult Intersect(Ray ray)
		{
			var ddotn = Vector3.Dot (ray.dir, n);
			if (ddotn >= 0)
				return null;

			var dot = Vector3.Dot(this.n, (this.pos-ray.pos));
			InterResult result = new InterResult ();
			result.renderable = this;
			result.distance = - dot / ddotn;
			result.pos = ray.GetPoint (result.distance);
			result.normal = this.n;
			result.inRay = ray.dir;
			return result;
		}
	}

	public class Sphere : Renderable
	{
		public Vector3  pos;
		public float	radius;		
		public float 	albedo; // 0-1
		public Color  color;
		public Sphere(Vector3 p, float r, Material m)
		{
			pos = p;
			radius = r;
			material = m;
			this.albedo = albedo;
			this.color = color;
		}

		public override InterResult Intersect(Ray ray)
		{
			// early check
			Vector3 v = this.pos - ray.pos;		
			float ddotv = Vector3.Dot (ray.dir, v);
			if (ddotv >= 0) {
				float param1 = ddotv * ddotv;
				float param2 = v.magnitude * v.magnitude - this.radius * this.radius;
				if (param1 - param2 >= 0) {
					float distance = ddotv - Mathf.Sqrt (param1 - param2);

					InterResult result = new InterResult ();
					result.pos = ray.GetPoint (distance);
					result.normal = (result.pos - this.pos).normalized;
					result.inRay = ray.dir;
					result.renderable = this;
					result.distance = (ray.pos - result.pos).magnitude;
					return result;
				}
			}

			return null;
		}
	}		
	#endregion

	#region intersect
	public class InterResult
	{
		public Vector3 pos;
		public Vector3 normal;
		public Vector3 inRay;
		public float distance;
		public Renderable renderable;
	}		

	#endregion

	#region Material
	public class Material
	{
		public enum Type
		{
			Phong,
			reflectance,
		}
			
		public Type type;
		public float albedo;
		public Color color;
		public Material(Type t, float a, Color c)
		{
			this.type = t;
			this.albedo = a;
			this.color = c;
		}

		static public Color GetColor(InterResult result, Camera camera)
		{
			if (result.renderable.material.type == Type.Phong)
				return GetColorByPhong (result, camera);
			else if (result.renderable.material.type == Type.reflectance)
				return GetColorByReflectance (result, camera, recursive_num);
			return black;
		}

		static private Color GetColorByPhong (InterResult result, Camera camera)
		{
			float diffuse = Mathf.Max(0,  Vector3.Dot (result.normal, -result.inRay)) * 0.8f;
			Vector3 L = (camera.pos - result.pos).normalized;
			float specular = Mathf.Pow (Mathf.Max (0, Vector3.Dot (Vector3.Reflect (result.inRay, result.normal), L)), 10f) * 1.0f;			
			var final = (diffuse * result.renderable.material.color) + specular*RayTracing.white;

			return final;
		}

		static private Color GetColorByReflectance(InterResult result, Camera camera, int depth)
		{						
			float diffuse = Mathf.Max(0,  Vector3.Dot (result.normal, -result.inRay)) * (1-result.renderable.material.albedo);
			var final = diffuse * result.renderable.material.color;											
			if (depth == 0 || result.renderable.material.albedo - 0f < 0.000001f)
				return final;

			// reflect			
			Ray newRay = new Ray(result.pos, Vector3.Reflect(result.inRay, result.normal));		
			InterResult nearest=null;
			float min_distance=0f;
			for (int i = 0; i < RayTracing.renderlist.Count; i++) {
				//todo: if ignore myself
				if (RayTracing.renderlist [i] == result.renderable)
					continue;
				var newInterResult = RayTracing.renderlist [i].Intersect (newRay);
				if (newInterResult != null) {
					if (nearest == null) {
						nearest = newInterResult;
						min_distance = newInterResult.distance;
					} else if (newInterResult.distance < min_distance){
						nearest = newInterResult;
						min_distance = newInterResult.distance;
					}
				}				
			}

			if (nearest != null) {
				var reflect = GetColorByReflectance (nearest, camera, depth-1) * result.renderable.material.albedo;
				final += reflect;				
			}

			return final;
		}
	}
	#endregion

	// easy camera and fixed in the world pos zero
	public class Camera
	{
		public Vector3 pos = new Vector3(0,0,0);
		Vector3 tar = new Vector3(0,0,-1);
		float   fov = 60f;
		float   ratio = 1f;
		public float   near = 1f;

		Vector3 leftUp;
		float   width;
		float   height;

		float AngleToRadians(float angle)
		{
			return angle * 3.1514926f / 180f;
		}

		public Camera()
		{			
			float up = Mathf.Tan(AngleToRadians(fov/2)) * near;
			float left = -up * ratio;
			leftUp = new Vector3(left, up, near);
			width = Mathf.Abs(left)*2;
			height = Mathf.Abs(up)*2;
			// ("camera left:"+left + " / up:" + up + "/width:"+width + "/height:"+height);
		}

		public Ray ScreenToRay (int x, int y)
		{			
			return new Ray (pos, new Vector3(
				leftUp.x + (x*1f / RayTracing.width)*width,
				leftUp.y - (y*1f / RayTracing.height)*height,
				near
			));
		}
	}

	#region Test
	private void TestSavePNG()
	{
		Texture2D tex = new Texture2D (100, 100);
		var red = new Color (1.0f, 0.0f, 0.0f, 1.0f);
		for (int i = 0; i < 100; i++)
			for (int k = 0; k < 100; k++) {
				red.r = i * 1f / 100;
				red.g = k * 1f / 100;
				tex.SetPixel (i, k, red);
			}

		File.WriteAllBytes(Application.persistentDataPath + "/111.png", tex.EncodeToPNG ());
	}

	private void TestZPass()
	{		
		Texture2D tex = new Texture2D (RayTracing.width, RayTracing.height);
		var color = new Color (0.0f, 0.0f, 0.0f, 1.0f);

		Material m1 = new Material (Material.Type.Phong, 0f, color);
		Sphere sphere1 = new Sphere (new Vector3(0,0,10), 3,  m1);
		var camera = new Camera ();

		int count = 0;
		for (int i = 0; i < RayTracing.width; i++) {
			for (int k = 0; k < RayTracing.height; k++) {
				var ray = camera.ScreenToRay (i, k);				
				var result = sphere1.Intersect (ray);
				if (result != null) {
					//color.r = color.g = color.b = (result.renderable.pos.z-result.pos.z)/result.renderable.radius;										
					color.r = color.g = color.b = 1.0f;
				} else {
					color.r = color.g = color.b = 0.0f;
				}
				count++;
				tex.SetPixel (i, k, color);
			}
		}
		File.WriteAllBytes(Application.persistentDataPath + "/112.png", tex.EncodeToPNG ());		
	}

	private void TestNormal()
	{
		Texture2D tex = new Texture2D (RayTracing.width, RayTracing.height);
		var color = new Color (0.0f, 0.0f, 0.0f, 1.0f);

		Material m1 = new Material (Material.Type.Phong, 0f, color);
		Sphere sphere1 = new Sphere (new Vector3(0,0,10), 3, m1);
		var camera = new Camera ();

		int count = 0;
		for (int i = 0; i < RayTracing.width; i++) {
			for (int k = 0; k < RayTracing.height; k++) {
				var ray = camera.ScreenToRay (i, k);				
				var result = sphere1.Intersect (ray);
				if (result != null) {
					color.r = result.normal.x;
					color.g = result.normal.y;
					color.b = Mathf.Abs(result.normal.z);
				} else {
					color.r = color.g = color.b = 0.0f;
				}
				count++;
				tex.SetPixel (i, k, color);
			}
		}
		File.WriteAllBytes(Application.persistentDataPath + "/113.png", tex.EncodeToPNG ());
	}

	private void TestPhong()
	{
		Texture2D tex = new Texture2D (RayTracing.width, RayTracing.height);
		var color = new Color (0.0f, 0.0f, 0.0f, 1.0f);

		Material m1 = new Material (Material.Type.Phong, 0f, color);
		Sphere sphere1 = new Sphere (new Vector3(0,0,10), 3, m1);
		var camera = new Camera ();

		int count = 0;
		for (int i = 0; i < RayTracing.width; i++) {
			for (int k = 0; k < RayTracing.height; k++) {
				var ray = camera.ScreenToRay (i, k);				
				var result = sphere1.Intersect (ray);
				if (result != null) {
					color = Material.GetColor (result, camera);
				} else {
					color.r = color.g = color.b = 0.0f;
				}
				count++;
				tex.SetPixel (i, k, color);
			}
		}
		File.WriteAllBytes(Application.persistentDataPath + "/114.png", tex.EncodeToPNG ());
	}

	private void TestReflect()
	{
		Texture2D tex = new Texture2D (RayTracing.width, RayTracing.height);
		var color = new Color (0.0f, 0.0f, 0.0f, 1.0f);

		Material m1 = new Material (Material.Type.reflectance, 0.5f, RayTracing.red);		
		Sphere sphere1 = new Sphere (new Vector3(2f,0,5), 2f, m1);
		Material m2 = new Material (Material.Type.reflectance, 0.5f, RayTracing.green);
		Sphere sphere2 = new Sphere (new Vector3(-2f,0,5), 2f, m2);
		Material m3 = new Material (Material.Type.reflectance, 0f, RayTracing.blue);
		Plane plane1 = new Plane (new Vector3 (0f, -1f, 0f), 10f, m3);
		renderlist.Add (sphere1);
		renderlist.Add (sphere2);
		renderlist.Add (plane1);

		var camera = new Camera ();

		int count = 0;
		for (int i = 0; i < RayTracing.width; i++) {
			for (int k = 0; k < RayTracing.height; k++) {
				var ray = camera.ScreenToRay (i, k);	
				color.r = color.g = color.b = 0.0f;								
				foreach (var renderable in renderlist) {
					var result = renderable.Intersect (ray);
					if (result != null) {
						color = Material.GetColor (result, camera);						
						break;
					} 
				}								
				count++;
				color.a = 1f;
				tex.SetPixel (i, k, color);
			}
		}
		File.WriteAllBytes(Application.persistentDataPath + "/115.png", tex.EncodeToPNG ());
		Debug.Log ("path:" + Application.persistentDataPath);
	}
	#endregion

	public void Test()
	{
		//TestSavePNG ();
		//TestZPass ();
		//TestNormal();
		//TestPhong();

		Vector3 n = new Vector3 (0, 1, 0);
		Vector3 inray = new Vector3 (1, 1, 0).normalized;
		Vector3 reflect = Vector3.Reflect (inray, n);
		Debug.Log ("reflect:" + reflect.ToString ());

		TestReflect();
	}


}
