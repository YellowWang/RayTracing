using UnityEngine;
using System.Collections.Generic;
using PT;

public class PathTracingTest : MonoBehaviour {

	void Start () {

		//TestSavePic ();
		//TestIntersect ();
		//TestCamera ();

		/*Vec dir = new Vec(-0.0536, -0.1600, -0.9857);
		Vec n = new Vec(0.6160, -0.1701, 0.7692);
		// 0.8875, -0.4199, 0.1896
		Vec rrr = dir-n*2*n.dot(dir);

		Vector3 dir1 = new Vector3(-0.0536f, -0.16f, -0.9857f);
		Vector3 n1 = new Vector3(0.616f, -0.1701f, 0.7692f);
		Vector3 r1 = Vector3.Reflect(dir1, n1);

		int a = 0;*/

		//TestMain1();
		TestMain2();
		//TestMain3Ref();

	}

	void Update()
	{
		PathTracing.Update ();
	}

	void TestMain1()
	{
		PathTracing.Sphere s1 = new PathTracing.Sphere(new Vec(-2,0,10), 2, new Vec(1,0,0), new Vec(0,0,0), PT.PathTracing.Relf.DIFF);	
		PathTracing.Sphere s2 = new PathTracing.Sphere(new Vec(2,0,10), 2, new Vec(0,1,0), new Vec(0,0,0), PT.PathTracing.Relf.DIFF);
		PathTracing.Sphere se = new PathTracing.Sphere(new Vec(0,4,10), 2, new Vec(0,0,0), new Vec(10,10,10), PT.PathTracing.Relf.DIFF);
		List<PathTracing.Renderable> list = new List<PathTracing.Renderable>();
		list.Add(s1);
		list.Add(s2);
		list.Add(se);
		//PathTracing.Main(100,100, 50, "pt1.png", list);
	}

	void TestMain3Ref()
	{
		List<PathTracing.Renderable> list = new List<PathTracing.Renderable>();		
		list.Add(new PathTracing.Sphere(new Vec(0,0,10), 2, new Vec(0.75,0.25,0.25), new Vec(1,1,1), PT.PathTracing.Relf.SPEC));
		
		int w = 100;
		int h = 100;
		PT.Camera camera = new PT.Camera (new Vec (0, 0, 0), new Vec (0, 0, -1).norm(), 45, w * 1f / h);
		PathTracing.Main(w,h, 1, "pt3.png", list, camera);
	}

	void TestMain2()
	{						
		List<PathTracing.Renderable> list = new List<PathTracing.Renderable>();		
		list.Add(new PathTracing.Sphere(new Vec(1e5+1,40.8,81.6), 1e5, new Vec(0.75,0.25,0.25), new Vec(), PT.PathTracing.Relf.DIFF));//left
		list.Add(new PathTracing.Sphere(new Vec(-1e5+99,40.8,81.6), 1e5, new Vec(0.25,0.25,0.75), new Vec(), PT.PathTracing.Relf.DIFF));//right
		list.Add(new PathTracing.Sphere(new Vec(50,40.8,1e5), 1e5, new Vec(0.75,0.75,0.75), new Vec(), PT.PathTracing.Relf.DIFF));//back
		list.Add(new PathTracing.Sphere(new Vec(50,40.8,-1e5+170), 1e5, new Vec(), new Vec(), PT.PathTracing.Relf.DIFF));//front
		list.Add(new PathTracing.Sphere(new Vec(50,1e5,81.6), 1e5, new Vec(0.75,0.75,0.75), new Vec(), PT.PathTracing.Relf.DIFF));//bottom
		list.Add(new PathTracing.Sphere(new Vec(50,-1e5+81.6,81.6), 1e5, new Vec(0.75,0.75,0.75), new Vec(), PT.PathTracing.Relf.DIFF));//top
		list.Add(new PathTracing.Sphere(new Vec(27,16.5,47), 16.5, new Vec(1,1,1)*0.999, new Vec(), PT.PathTracing.Relf.SPEC));//mirr
		list.Add(new PathTracing.Sphere(new Vec(73,16.5,47), 16.5, new Vec(1,1,1)*0.999, new Vec(), PT.PathTracing.Relf.REFR));//glass
		list.Add(new PathTracing.Sphere(new Vec(50,681.6-0.27,81.6), 600, new Vec(), new Vec(12,12,12), PT.PathTracing.Relf.DIFF));//lite

		int w = 100;
		int h = 100;
		PT.Camera camera = new PT.Camera (new Vec (50, 52, 295.6), new Vec (0, -0.042612, -1).norm(), 45, w * 1f / h);				
		PathTracing.Main(w,h, 10, "pt2_temp.png", list, camera);		
	}

	void TestSavePic() 
	{		
		SavePic pic = new SavePic (Application.persistentDataPath + "/1.png", 100, 100);
		for (int i = 0; i < 100; i++) {
			for (int k = 0; k < 100; k++) {
				pic.SetPixel (k, i, new Color (i/100f, k/100f, 0));
			}
		}
		pic.Save ();
	}

	void TestIntersect()
	{
		PathTracing.Sphere s1 = new PathTracing.Sphere(new Vec(0,0,10), 1, new Vec(1,0,0), new Vec(0,0,0), PT.PathTracing.Relf.DIFF);
		PathTracing.Ray ray = new PathTracing.Ray (new Vec (0,0,0), new Vec (0,0,1).norm());
		var res = s1.Intersect (ray);
		UnityEngine.Debug.Log ("res:" + res.distance+"/"+res.pos.x+","+res.pos.y+","+res.pos.z+"/"+res.normal.x+","+res.normal.y+","+res.normal.z);
	}

	void TestCamera()
	{
		PT.Camera camera = new PT.Camera (new Vec (0, 0, 0), new Vec (0, -1, 1).norm(), 60, 1.0f);
		UnityEngine.Debug.Log ("cx:"+PT.Debug.ToString (camera.cx));
		UnityEngine.Debug.Log ("cy:" + PT.Debug.ToString (camera.cy));
	}
}
