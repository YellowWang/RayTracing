using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class RayTracing {

	public static int width = 512;
	public static int height = 512;
	public const  int recursive_num = 5;

	public static List<Renderable> renderlist = new List<Renderable>();

	public static Color black = new Color(0,0,0);
	public static Color white = new Color(1,1,1);
	public static Color red   = new Color(1,0,0);
	public static Color green = new Color(0,1,0);     
	public static Color blue  = new Color(0,0,1);
	public static Color error = new Color(0.1f, 0.5f, 0.8f);

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

	#region lighting	
	public class Light 
	{
		public enum Type
		{
			dir,
			point,
			spot
		}
		public Type  type;// "dir", "point", "spot"
		public Vector3 pos; // position
		public Vector3 dir; // direction
		public Vector3 irradiance; // irradiance
		public Vector3 spot; // spot parameters: 0:theta  1:phi 2:falloff
		public Light(Type type)
		{
			this.type = type;
		}
	}
	public static List<Light> lights = new List<Light>();
	private static Color Multiply(Color c, Vector3 irr)
	{
		return new Color (c.r * irr.x, c.g * irr.y, c.b * irr.z, c.a);
	}
	#endregion

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
			//Debug.Log (ray.dir.x + "," + ray.dir.y + "," + ray.dir.z + " /" + n.x + "," + n.y + "," + n.z);
			var dot = Vector3.Dot(this.n, (this.pos-ray.pos));
			if (dot >= 0)
				return null;

			InterResult result = new InterResult ();
			result.renderable = this;
			result.distance = dot / ddotn;
			result.pos = ray.GetPoint (result.distance);
			result.normal = this.n;
			result.inRay = ray.dir;
			//Debug.Log ("ddotn:" + ddotn + "/dot:" + dot+"/normal:"+this.n);
			//Debug.Log ("plane: distance:"+result.distance + " pos:" + result.pos.ToString()+"/ray:"+ray.pos+"/"+ray.dir);
			return result;
		}
	}

	public class Sphere : Renderable
	{
		public Vector3  pos;
		public float	radius;						
		public Sphere(Vector3 p, float r, Material m)
		{
			pos = p;
			radius = r;
			material = m;						
		}

		public override InterResult Intersect(Ray ray)
		{			
			Vector3 v = ray.pos-this.pos;		
			float ddotv = Vector3.Dot (ray.dir, v);
			//Debug.Log ("s ddotv:" + ddotv + "ray.pos:"+ray.pos+"/shpere pos:"+this.pos+"/radius:"+this.radius);
			if (ddotv <= 0) {
				float param1 = ddotv * ddotv;
				float param2 = v.magnitude * v.magnitude - this.radius * this.radius;
				//Debug.Log ("s p1:" + param1 + "/p2:" + param2);
				if (param1 - param2 >= 0) {					
					InterResult result = new InterResult ();
					result.distance = -ddotv - Mathf.Sqrt (param1 - param2);;
					result.pos = ray.GetPoint (result.distance);
					result.normal = (result.pos - this.pos).normalized;
					result.inRay = ray.dir;
					result.renderable = this;					 
					return result;
				}
			}

			return null;
		}
	}		

	public static InterResult Intersect(Ray ray)
	{
		InterResult nearest=null;
		float min_distance=0f;
		foreach (var renderable in renderlist) {			
			var result = renderable.Intersect (ray);
			if (result != null) {
				if (nearest == null) {
					nearest = result;
					min_distance = result.distance;					
				} else if (result.distance < min_distance) {
					nearest = result;
					min_distance = result.distance;
				}																	
			} 
		}	
		if (nearest != null)
			return nearest;
		return null;
	}

	#endregion

	#region intersect
	public class InterResult
	{
		public Vector3 pos;
		public Vector3 normal;
		//todo: if need?
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
			lambert,
			checkboard,
			phong,
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
			return GetColorByReflectance (result, camera, RayTracing.recursive_num);
		}

		static public Color GetColorByType(InterResult result, Camera camera, Light light = null)
		{
			// shadow ray
			// todo:
			if (true && light!=null)//shadow
			{
				if (light.type == Light.Type.dir) {
					Vector3 shadowRay = -light.dir; 
					var shadowResult = RayTracing.Intersect (new Ray (result.pos, shadowRay.normalized));					
					if (shadowResult != null)
						return RayTracing.black;
				} else if (light.type == Light.Type.point || light.type == Light.Type.spot) {
					Vector3 shadowRay = light.pos - result.pos;
					var shadowResult = RayTracing.Intersect (new Ray (result.pos, shadowRay.normalized));					
					if (shadowResult != null && shadowResult.distance < shadowRay.magnitude)
						return RayTracing.black;
				}				
			}

			if (result.renderable.material.type == Type.checkboard)
				return GetColorByCheckboard (result, camera);
			else if (result.renderable.material.type == Type.phong)
				return GetColorByPhong (result, camera, light);
			else if (result.renderable.material.type == Type.lambert)
				return GetColorByLambert (result, camera, light);
			return black;
		}

		static private Color GetColorByLambert (InterResult result, Camera camera, Light light)
		{
			Vector3 inray = Vector3.zero;
			float attenuation = 1f;
			if (light.type == Light.Type.dir)
				inray = -light.dir;
			else if (light.type == Light.Type.point) {
				inray = (light.pos - result.pos);
				attenuation = 1f / inray.sqrMagnitude;					
			} else if (light.type == Light.Type.spot) {
				inray = (light.pos - result.pos);
				attenuation = 1f / inray.sqrMagnitude;				
				inray.Normalize ();
				var alp = Vector3.Dot(inray, -light.dir);			
				//todo: should pre calculate the and phi
				var the = Mathf.Cos (Camera.AngleToRadians (light.spot.x / 2));
				var phi = Mathf.Cos (Camera.AngleToRadians (light.spot.y / 2));
				if (alp >= the)
					attenuation *= 1f;				
				else if (alp < the && alp > phi) 
					attenuation *= Mathf.Pow((alp - phi)/(the - phi), light.spot.z);				
				else
					attenuation *= 0f;
			}
			inray.Normalize ();
			float diffuse = Mathf.Max(0,  Vector3.Dot (result.normal, inray));			
			var final = (diffuse * Multiply(result.renderable.material.color, light.irradiance));			
			return final*attenuation;
		}

		static private Color GetColorByCheckboard (InterResult result, Camera camera)
		{			
			return ((Mathf.Floor(result.pos.x) + Mathf.Floor(result.pos.z)) % 2) < 1 ? RayTracing.white : RayTracing.black;
		}

		static private Color GetColorByPhong (InterResult result, Camera camera, Light light)
		{			
			Vector3 inray = Vector3.zero;
			float attenuation = 1f;
			if (light.type == Light.Type.dir)
				inray = -light.dir;
			else if (light.type == Light.Type.point) {				
				inray = (light.pos - result.pos);
				attenuation = 1f / inray.sqrMagnitude;						
			}
			else if (light.type == Light.Type.spot) {
				inray = (light.pos - result.pos);
				attenuation = 1f / inray.sqrMagnitude;				
				inray.Normalize ();
				var alp = Vector3.Dot(inray, -light.dir);				
				var the = Mathf.Cos (Camera.AngleToRadians (light.spot.x / 2));
				var phi = Mathf.Cos (Camera.AngleToRadians (light.spot.y / 2));
				if (alp >= the)
					attenuation *= 1f;				
				else if (alp < the && alp > phi) 
					attenuation *= Mathf.Pow((alp - phi)/(the - phi), light.spot.z);				
				else
					attenuation *= 0f;
			}
			inray.Normalize ();

			float diffuse = Mathf.Max(0,  Vector3.Dot (result.normal, inray));
			Vector3 L = (camera.pos - result.pos).normalized;
			float specular = Mathf.Pow (Mathf.Max (0, Vector3.Dot (-Vector3.Reflect (inray, result.normal), L)), 10f) * 1.0f;			
			var final = (diffuse * Multiply(result.renderable.material.color, light.irradiance)) + specular*RayTracing.white;			
			return final*attenuation;
		}

		static private Color GetColorByReflectance(InterResult result, Camera camera, int depth)
		{						
			// todo:emissvie?
			Color final = RayTracing.black;	
			foreach (var l in lights) {
				final += GetColorByType (result, camera, l)*(1-result.renderable.material.albedo);
			}

			if (depth == 0 || result.renderable.material.albedo - 0f < 0.000001f)
				return final;

			//todo: if black, then should not reflect?

			// reflect			
			Ray newRay = new Ray(result.pos, Vector3.Reflect(result.inRay, result.normal));		
			//Debug.Log ("newRay:" + newRay.pos + "/" + newRay.dir);
			InterResult nearest=null;
			float min_distance=0f;
			//Debug.Log ("renderlist:" + RayTracing.renderlist.Count);
			for (int i = 0; i < RayTracing.renderlist.Count; i++) {
				//todo: if ignore myself
				//if (RayTracing.renderlist [i] == result.renderable)
				//	continue;
				
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
				//Debug.Log ("nearest: final:" + final);
			}

			return final;
		}
	}
	#endregion

	// easy camera and fixed in the world pos zero
	public class Camera
	{
		public Vector3 pos = new Vector3(0,0,0);
		Vector3 tar = new Vector3(0,0,1);
		float   fov = 60f;
		float   ratio = 1f;
		public float   near = 1f;

		Vector3 leftUp;
		float   width;
		float   height;

		public static float AngleToRadians(float angle)
		{
			return angle * Mathf.PI / 180f;
		}

		public Camera()
		{			
			float up = Mathf.Tan(AngleToRadians(fov/2)) * near;
			float left = -up * ratio;
			leftUp = new Vector3(left, up, near);
			width = Mathf.Abs(left)*2;
			height = Mathf.Abs(up)*2;
			//Debug.Log ("camera left:"+left + " / up:" + up + "/width:"+width + "/height:"+height);
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

		Material m1 = new Material (Material.Type.phong, 0f, color);
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

		Material m1 = new Material (Material.Type.phong, 0f, color);
		Sphere sphere1 = new Sphere (new Vector3(0,0,10), 3, m1);
		var camera = new Camera ();
					
		int count = 0;
		for (int i = 0; i < RayTracing.height; i++) {
			for (int k = 0; k < RayTracing.width; k++) {
				var ray = camera.ScreenToRay (k, i);				
				var result = sphere1.Intersect (ray);
				if (result != null) {
					count++;

					color.r = (result.normal.x+1f)*0.5f;
					color.g = (result.normal.y+1f)*0.5f;
					color.b = (result.normal.z+1f)*0.5f;
					//Debug.Log (k+","+i+"/ray:"+ray.dir+"/normal:" + result.normal.ToString ()+ "/color:"+color.ToString());
				} else {
					color.r = color.g = color.b = 0.0f;
				}				
				color.a = 1.0f;		
				tex.SetPixel (k, RayTracing.height-i, color);
			}
		}

		File.WriteAllBytes(Application.persistentDataPath + "/113.png", tex.EncodeToPNG ());
	}

	private void TestPhong()
	{
		Texture2D tex = new Texture2D (RayTracing.width, RayTracing.height);
		var color = new Color (0.0f, 0.0f, 0.0f, 1.0f);

		Material m1 = new Material (Material.Type.phong, 0f, RayTracing.red);
		Sphere sphere1 = new Sphere (new Vector3(0,0,10), 3, m1);
		var camera = new Camera ();

		int count = 0;
		for (int i = 0; i < RayTracing.height; i++) {
			for (int k = 0; k < RayTracing.width; k++) {		
				var ray = camera.ScreenToRay (k, i);				
				var result = sphere1.Intersect (ray);
				if (result != null) {
					color = Material.GetColor (result, camera);
				} else {
					color.r = color.g = color.b = 0.0f;
				}
				count++;
				color.a = 1.0f;
				tex.SetPixel (k, RayTracing.height-i, color);
			}
		}
		File.WriteAllBytes(Application.persistentDataPath + "/114.png", tex.EncodeToPNG ());
	}

	private void TestReflect()
	{
		Texture2D tex = new Texture2D (RayTracing.width, RayTracing.height);
		var color = new Color (0.0f, 0.0f, 0.0f, 1.0f);

		Material m1 = new Material (Material.Type.phong, 0.3f, RayTracing.red);		
		Sphere sphere1 = new Sphere (new Vector3(2f,1f,10), 2f, m1);
		Material m2 = new Material (Material.Type.phong, 0.3f, RayTracing.blue);
		Sphere sphere2 = new Sphere (new Vector3(-2f,1f,10), 2f, m2);
		Material m3 = new Material (Material.Type.checkboard, 0.5f, new Color(0.0f, 0.5f, 0.5f));
		Plane plane1 = new Plane (new Vector3 (0f, 1f, 0f), -1.0f, m3);
		renderlist.Add (sphere1);
		renderlist.Add (sphere2);
		renderlist.Add (plane1);

		lights.Clear();
		var l1 = new Light (Light.Type.point);
		l1.pos = new Vector3 (10, 10, 0);		
		l1.irradiance = new Vector3 (1,1,1)*500f;
		lights.Add (l1);

		var camera = new Camera ();
		
		int count = 0;
		for (int i = RayTracing.height; i >= 0 ; i--) {
			for (int k = 0; k < RayTracing.width; k++) {
				var ray = camera.ScreenToRay (k, i);	
				color.r = color.g = color.b = 0.0f;		
				InterResult nearest=null;
				float min_distance=0f;
				foreach (var renderable in renderlist) {
					var result = renderable.Intersect (ray);
					if (result != null) {
						if (nearest == null) {
							nearest = result;
							min_distance = result.distance;
							//Debug.Log (k + "," + i);
						} else if (result.distance < min_distance) {
							nearest = result;
							min_distance = result.distance;
						}																	
					} 
				}								
								
				if (nearest != null)
					color = Material.GetColor (nearest, camera);	
				color.a = 1f;
				tex.SetPixel (k, RayTracing.height-i, color);
			}
		}
		File.WriteAllBytes(Application.persistentDataPath + "/115.png", tex.EncodeToPNG ());
		//Debug.Log ("path:" + Application.persistentDataPath);
	}

	private void TestSpot()
	{
		Texture2D tex = new Texture2D (RayTracing.width, RayTracing.height);
		var color = new Color (0.0f, 0.0f, 0.0f, 1.0f);

		Material m1 = new Material (Material.Type.lambert, 0.0f, RayTracing.white);		
		Sphere sphere1 = new Sphere (new Vector3(-1f,-1f,8f), 2f, m1);
		Material m2 = new Material (Material.Type.lambert, 0.0f, new Color(1.0f, 1.0f, 1.0f));
		Plane plane1 = new Plane (new Vector3 (0f, 1f, 0f), -3.0f, m2);								
		renderlist.Clear ();
		renderlist.Add (sphere1);
		renderlist.Add (plane1);		

		// light
		var l1 = new Light (Light.Type.dir);
		l1.dir = new Vector3 (-1.75f, -2f, 1.5f).normalized;	
		l1.irradiance = new Vector3 (1,1,1)*0.7f;
		//lights.Add (l1);
		var l2 = new Light (Light.Type.spot);
		l2.pos = new Vector3 (10, 10, 8f);
		l2.dir = new Vector3 (-1, -1, 0).normalized;
		l2.spot = new Vector3 (20, 30, 0.5f);
		l2.irradiance = new Vector3 (1,1,1)*120f;
		lights.Add (l2);

		var camera = new Camera ();

		int count = 0;
		for (int i = RayTracing.height; i >= 0 ; i--) {
			for (int k = 0; k < RayTracing.width; k++) {
				var ray = camera.ScreenToRay (k, i);	
				color.r = color.g = color.b = 0.0f;		
				InterResult nearest=null;
				float min_distance=0f;
				foreach (var renderable in renderlist) {
					var result = renderable.Intersect (ray);
					if (result != null) {
						if (nearest == null) {
							nearest = result;
							min_distance = result.distance;							
						} else if (result.distance < min_distance) {
							nearest = result;
							min_distance = result.distance;
						}																	
					} 
				}								

				if (nearest != null)
					color = Material.GetColor (nearest, camera);	
				color.a = 1f;
				tex.SetPixel (k, RayTracing.height-i, color);
			}
		}
		File.WriteAllBytes(Application.persistentDataPath + "/117.png", tex.EncodeToPNG ());
	}

	private void TestLight()
	{
		Texture2D tex = new Texture2D (RayTracing.width, RayTracing.height);
		var color = new Color (0.0f, 0.0f, 0.0f, 1.0f);

		Material m1 = new Material (Material.Type.lambert, 0.0f, RayTracing.white);		
		Sphere sphere1 = new Sphere (new Vector3(1f,1f,7f), 2f, m1);
		Material m2 = new Material (Material.Type.lambert, 0.0f, new Color(1.0f, 1.0f, 1.0f));
		Plane plane1 = new Plane (new Vector3 (0f, 1f, 0f), -2.0f, m2);		
		Material m3 = new Material (Material.Type.lambert, 0.0f, new Color(1.0f, 1.0f, 1.0f));
		Plane plane2 = new Plane (new Vector3 (1f, 0f, 0f), -2.0f, m3);		
		Plane plane3 = new Plane (new Vector3 (0f, 0f, -1f), -13.0f, m3);
		renderlist.Clear ();
		renderlist.Add (sphere1);
		renderlist.Add (plane1);
		renderlist.Add (plane2);
		renderlist.Add (plane3);

		// light
		lights.Clear();
		for (int i = 0; i < 6; i++)
			for (int k = 0; k < 6; k++) {
				var l = new Light (Light.Type.point);
				l.pos = new Vector3 (10+i*1.1f, 20, -5+k*1.1f);
				l.irradiance = new Vector3 (1,1,1)*12f;
				lights.Add (l);
			}
		var l1 = new Light (Light.Type.dir);
		l1.dir = new Vector3 (-1.75f, -2f, 1.5f).normalized;	
		l1.irradiance = new Vector3 (1,1,1)*0.4f;
		//lights.Add (l1);
		var l2 = new Light (Light.Type.point);
		l2.pos = new Vector3 (10, 20, -5);
		l2.irradiance = new Vector3 (1,1,1)*500f;
		//lights.Add (l2);

		var camera = new Camera ();

		int count = 0;
		for (int i = RayTracing.height; i >= 0 ; i--) {
			for (int k = 0; k < RayTracing.width; k++) {
				var ray = camera.ScreenToRay (k, i);	
				color.r = color.g = color.b = 0.0f;		
				InterResult nearest=null;
				float min_distance=0f;
				foreach (var renderable in renderlist) {
					var result = renderable.Intersect (ray);
					if (result != null) {
						if (nearest == null) {
							nearest = result;
							min_distance = result.distance;							
						} else if (result.distance < min_distance) {
							nearest = result;
							min_distance = result.distance;
						}																	
					} 
				}								

				if (nearest != null)
					color = Material.GetColor (nearest, camera);	
				color.a = 1f;
				tex.SetPixel (k, RayTracing.height-i, color);
			}
		}
		File.WriteAllBytes(Application.persistentDataPath + "/116.png", tex.EncodeToPNG ());
	}
	#endregion

	public void Test()
	{
		//TestSavePNG ();
		//TestZPass ();
		//TestNormal();
		//TestPhong();

		TestReflect();
		TestSpot();
		TestLight();

	}


}
