using UnityEditor;
using UnityEngine;
using FlameAndBlame.VFX;

namespace FlameAndBlame.EditorTools
{
	public static class VfxPrefabBuilder
	{
		static Material Mat(string name, Color c, int shape, bool additive, float softness = 0.6f)
		{
			string path = "Assets/Materials/" + name + ".mat";
			var m = AssetDatabase.LoadAssetAtPath<Material>(path);
			if (m == null) { m = new Material(Shader.Find("FlameAndBlame/FXParticle")); AssetDatabase.CreateAsset(m, path); }
			m.shader = Shader.Find("FlameAndBlame/FXParticle");
			m.SetColor("_BaseColor", c);
			m.SetFloat("_Shape", shape);
			m.SetFloat("_Softness", softness);
			m.SetFloat("_SrcBlend", 5); m.SetFloat("_DstBlend", additive ? 1 : 10);
			m.DisableKeyword("_SHAPE_SOFT"); m.DisableKeyword("_SHAPE_STAR"); m.DisableKeyword("_SHAPE_HARD");
			m.EnableKeyword(shape == 1 ? "_SHAPE_STAR" : shape == 2 ? "_SHAPE_HARD" : "_SHAPE_SOFT");
			EditorUtility.SetDirty(m);
			return m;
		}

		static Gradient Grad(Color a, Color b, float aMid = 0.7f)
		{
			var g = new Gradient();
			g.SetKeys(new[] { new GradientColorKey(a, 0), new GradientColorKey(b, 1) },
				new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(aMid, 0.6f), new GradientAlphaKey(0, 1) });
			return g;
		}

		static GameObject Make<T>(string name, Material mat, out ParticleSystem ps) where T : EffectController
		{
			var go = new GameObject(name);
			go.AddComponent<T>();
			var child = new GameObject("Particles");
			child.transform.SetParent(go.transform, false);
			ps = child.AddComponent<ParticleSystem>();
			ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
			var r = child.GetComponent<ParticleSystemRenderer>();
			r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
			var main = ps.main; main.playOnAwake = false;
			var so = new SerializedObject(go.GetComponent<T>());
			so.FindProperty("systems").arraySize = 1;
			so.FindProperty("systems").GetArrayElementAtIndex(0).objectReferenceValue = ps;
			so.ApplyModifiedPropertiesWithoutUndo();
			return go;
		}

		static void Save(GameObject go) { PrefabUtility.SaveAsPrefabAsset(go, "Assets/Prefabs/VFX/" + go.name + ".prefab"); Object.DestroyImmediate(go); }

		static AnimationCurve Curve(float a, float b) => AnimationCurve.Linear(0, a, 1, b);

		[MenuItem("Tools/VFX/Build Effect Prefabs")]
		public static void Build()
		{
			var fireMat = Mat("M_FX_Fire", Color.white, 0, true);
			var smokeMat = Mat("M_FX_Smoke", new Color(0.25f, 0.25f, 0.27f, 0.6f), 0, false, 0.5f);
			var waterMat = Mat("M_FX_Water", new Color(0.6f, 0.8f, 1f, 0.9f), 2, false);
			var starMat = Mat("M_FX_Star", new Color(1f, 0.9f, 0.2f, 1f), 1, true);

			var go = Make<FireEffect>("Fire", fireMat, out var ps);
			{
				var m = ps.main; m.loop = true; m.duration = 1; m.startLifetime = 0.9f; m.startSpeed = 1.6f; m.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
				m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.5f, 0.1f), new Color(1f, 0.8f, 0.2f)); m.maxParticles = 40; m.gravityModifier = -0.2f;
				m.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f);
				var e = ps.emission; e.rateOverTime = 14;
				var s = ps.shape; s.shapeType = ParticleSystemShapeType.Box; s.scale = new Vector3(2f, 0.1f, 0.3f); s.rotation = new Vector3(-90, 0, 0);
				var c = ps.colorOverLifetime; c.enabled = true; c.color = Grad(new Color(1, 0.9f, 0.4f), new Color(0.8f, 0.1f, 0f), 0.8f);
				var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, Curve(1f, 0.2f));
			}
			Save(go);

			go = Make<GroundFireEffect>("GroundFire", fireMat, out ps);
			{
				var m = ps.main; m.loop = true; m.duration = 1; m.startLifetime = 1.1f; m.startSpeed = 1.2f; m.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
				m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.45f, 0.1f), new Color(1f, 0.75f, 0.2f)); m.maxParticles = 60; m.gravityModifier = -0.15f;
				var e = ps.emission; e.rateOverTime = 22;
				var s = ps.shape; s.shapeType = ParticleSystemShapeType.Circle; s.radius = 1.5f; s.radiusThickness = 1f; s.rotation = new Vector3(-90, 0, 0);
				var c = ps.colorOverLifetime; c.enabled = true; c.color = Grad(new Color(1, 0.9f, 0.4f), new Color(0.8f, 0.1f, 0f), 0.8f);
				var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, Curve(1f, 0.2f));
			}
			Save(go);

			go = Make<WaterDropEffect>("WaterDrop", waterMat, out ps);
			{
				var m = ps.main; m.loop = true; m.duration = 1; m.startLifetime = 0.45f; m.startSpeed = 0.3f; m.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
				m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 40; m.gravityModifier = 0.5f;
				var e = ps.emission; e.rateOverTime = 40;
				var s = ps.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 0.35f;
				var c = ps.colorOverLifetime; c.enabled = true; c.color = Grad(new Color(0.8f, 0.92f, 1f), new Color(0.4f, 0.65f, 1f), 0.8f);
				var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, Curve(1f, 0.3f));
			}
			Save(go);

			go = Make<SplashEffect>("Splash", waterMat, out ps);
			{
				var m = ps.main; m.loop = false; m.duration = 0.8f; m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f); m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
				m.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.45f); m.gravityModifier = 1.5f; m.maxParticles = 40; m.simulationSpace = ParticleSystemSimulationSpace.World;
				var e = ps.emission; e.rateOverTime = 0; e.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });
				var s = ps.shape; s.shapeType = ParticleSystemShapeType.Cone; s.angle = 45; s.radius = 0.3f; s.rotation = new Vector3(-90, 0, 0);
				var c = ps.colorOverLifetime; c.enabled = true; c.color = Grad(new Color(0.85f, 0.95f, 1f), new Color(0.5f, 0.7f, 1f), 0.9f);
				var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, Curve(1f, 0.3f));
			}
			Save(go);

			go = Make<StunStarsEffect>("StunStars", starMat, out ps);
			{
				var m = ps.main; m.loop = true; m.duration = 1; m.startLifetime = 1.6f; m.startSpeed = 0f; m.startSize = 0.35f; m.maxParticles = 6;
				m.simulationSpace = ParticleSystemSimulationSpace.Local;
				var e = ps.emission; e.rateOverTime = 3;
				var s = ps.shape; s.shapeType = ParticleSystemShapeType.Circle; s.radius = 0.45f; s.radiusThickness = 0f; s.rotation = new Vector3(-90, 0, 0);
				var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.Local;
				v.x = 0; v.y = 0; v.z = 0; v.orbitalY = 3.5f;
				var c = ps.colorOverLifetime; c.enabled = true; c.color = Grad(Color.white, Color.white, 1f);
				var r = ps.rotationOverLifetime; r.enabled = true; r.z = 3f;
			}
			Save(go);

			go = Make<SmokeEffect>("Smoke", smokeMat, out ps);
			{
				var m = ps.main; m.loop = true; m.duration = 1; m.startLifetime = 2.5f; m.startSpeed = 1.5f; m.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.4f); m.maxParticles = 25;
				m.startColor = new Color(0.3f, 0.3f, 0.32f, 0.5f); m.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f);
				var e = ps.emission; e.rateOverTime = 6;
				var s = ps.shape; s.shapeType = ParticleSystemShapeType.Box; s.scale = new Vector3(2f, 0.1f, 0.5f); s.rotation = new Vector3(-90, 0, 0);
				var c = ps.colorOverLifetime; c.enabled = true; c.color = Grad(Color.white, new Color(0.7f, 0.7f, 0.7f), 0.5f);
				var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, Curve(0.6f, 2f));
			}
			Save(go);
			AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
			Debug.Log("[VFX] prefabs built");
		}
	}
}
