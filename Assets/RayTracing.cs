using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class RayTracing {

	public static int width = 100;
	public static int height = 100;
	public static List<Sphere> renderlist = new List<Sphere>();

	public static Color black = new Color(0,0,0);
	public static Color white = new Color (1,1,1);
	public static Color red   = new Color(1,0,0);
	public static Color green = new Color(0,1,0);     

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

	public class Sphere
	{
		public Vector3  pos;
		public float	radius;
		public Material.Type material;
		public float 	albedo; // 0-1
		public Color  color;
		public Sphere(Vector3 p, float r, Material.Type m, float albedo = 0f, Color color = default(Color))
		{
			pos = p;
			radius = r;
			material = m;
			this.albedo = albedo;
			this.color = color;
		}
	}		

	#region intersect
	public class InterResult
	{
		public Vector3 pos;
		public Vector3 normal;
		public Vector3 inRay;
		public Sphere sphere;
	}

	public static InterResult Intersect(Ray ray, Sphere sphere)
	{
		// early check
		Vector3 v = sphere.pos - ray.pos;		
		float ddotv = Vector3.Dot (ray.dir, v);
		if (ddotv >= 0) {
			float param1 = ddotv * ddotv;
			float param2 = v.magnitude * v.magnitude - sphere.radius * sphere.radius;
			if (param1 - param2 >= 0) {
				float distance = ddotv - Mathf.Sqrt (param1 - param2);

				InterResult result = new InterResult ();
				result.pos = ray.GetPoint (distance);
				result.normal = (result.pos - sphere.pos).normalized;
				result.inRay = ray.dir;
				result.sphere = sphere;
				return result;
			}
		}
			
		return null;
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



		static public Color GetColor(InterResult result, Camera camera)
		{
			if (result.sphere.material == Type.Phong)
				return GetColorByPhong (result, camera);
			else if (result.sphere.material == Type.reflectance)
				return GetColorByReflectance (result, camera, 10);
			return black;
		}

		static private Color GetColorByPhong (InterResult result, Camera camera)
		{
			float diffuse = Mathf.Max(0,  Vector3.Dot (result.normal, -result.inRay)) * 0.8f;
			Vector3 L = (camera.pos - result.pos).normalized;
			float specular = Mathf.Pow (Mathf.Max (0, Vector3.Dot (Vector3.Reflect (result.inRay, result.normal), L)), 10f) * 1.0f;
			float final = diffuse + specular;

			return new Color(final, final, final, 1.0f);
		}

		static private Color GetColorByReflectance(InterResult result, Camera camera, int depth = 10)
		{						
			float diffuse = Mathf.Max(0,  Vector3.Dot (result.normal, -result.inRay)) * (1-result.sphere.albedo);
			var final = diffuse * result.sphere.color;								

			if (depth == 0 || result.sphere.albedo - 0f < 0.000001f)
				return final;

			// reflect			
			Ray newRay = new Ray(result.pos, Vector3.Reflect(result.inRay, result.normal));		
			for (int i = 0; i < RayTracing.renderlist.Count; i++) {
				if (RayTracing.renderlist [i] == result.sphere)
					continue;
				var newInterResult = RayTracing.Intersect (newRay, RayTracing.renderlist[i]);
				if (newInterResult != null) {
					var reflect = GetColorByReflectance (newInterResult, camera, depth-1) * result.sphere.albedo;
					final += reflect;
					Debug.Log ("reflect:"+reflect.ToString()+ "/ albedo:"+newInterResult.sphere.albedo + "/final:"+final.ToString());
					break;
				}
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

		Sphere sphere1 = new Sphere (new Vector3(0,0,10), 3,  Material.Type.Phong);
		var camera = new Camera ();

		int count = 0;
		for (int i = 0; i < RayTracing.width; i++) {
			for (int k = 0; k < RayTracing.height; k++) {
				var ray = camera.ScreenToRay (i, k);				
				var result = Intersect (ray, sphere1);
				if (result != null) {
					color.r = color.g = color.b = (result.sphere.pos.z-result.pos.z)/result.sphere.radius;					
					//Debug.Log (count + ":" + ray.dir.ToString() + "/" + result.pos.z);
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

		Sphere sphere1 = new Sphere (new Vector3(0,0,10), 3, Material.Type.Phong);
		var camera = new Camera ();

		int count = 0;
		for (int i = 0; i < RayTracing.width; i++) {
			for (int k = 0; k < RayTracing.height; k++) {
				var ray = camera.ScreenToRay (i, k);				
				var result = Intersect (ray, sphere1);
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

		Sphere sphere1 = new Sphere (new Vector3(0,0,10), 3, Material.Type.Phong);
		var camera = new Camera ();

		int count = 0;
		for (int i = 0; i < RayTracing.width; i++) {
			for (int k = 0; k < RayTracing.height; k++) {
				var ray = camera.ScreenToRay (i, k);				
				var result = Intersect (ray, sphere1);
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

		Sphere sphere1 = new Sphere (new Vector3(3.5f,0,10), 2f, Material.Type.reflectance, 0.5f, RayTracing.red);
		Sphere sphere2 = new Sphere (new Vector3(-3.5f,0,8), 2.5f, Material.Type.reflectance, 0.0f, RayTracing.green);
		renderlist.Add (sphere1);
		renderlist.Add (sphere2);

		var camera = new Camera ();

		int count = 0;
		for (int i = 0; i < RayTracing.width; i++) {
			for (int k = 0; k < RayTracing.height; k++) {
				var ray = camera.ScreenToRay (i, k);	
				color.r = color.g = color.b = 0.0f;								
				foreach (var renderable in renderlist) {
					var result = Intersect (ray, renderable);
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
