using System;
using FlameAndBlame.Fire;
using FlameAndBlame.GameRules;
using FlameAndBlame.Heli;
using FlameAndBlame.Rescue;
using UnityEditor;
using UnityEngine;

namespace FlameAndBlame.EditorTools
{
	/// <summary>Idempotent project setup: tags, layers, collision matrix, config assets.</summary>
	public static class ProjectBootstrap
	{
		private const string SettingsFolder = "Assets/Settings";

		private static readonly string[] Tags =
		{
			"Heli", "Rotor", "Cup", "CupGrabZone", "Human", "Pool", "Roof", "GroundFire", "BuildingSide", "WaterDrop"
		};

		private static readonly (int index, string name)[] Layers =
		{
			(4, "Water"), (8, "Heli"), (9, "Rotors"), (10, "Cup"), (11, "Human"),
			(12, "Building"), (13, "FireZone"), (14, "Ground"), (15, "Obstacle")
		};

		// Pairs that collide or trigger; every other pair among the layers above is ignored.
		private static readonly (string a, string b)[] Allowed =
		{
			("Rotors", "Building"), ("Rotors", "Ground"), ("Rotors", "Obstacle"),
			("Heli", "Building"), ("Heli", "Ground"), ("Heli", "Obstacle"),
			("Cup", "Building"), ("Cup", "Ground"), ("Cup", "Obstacle"), ("Cup", "Human"), ("Cup", "Water"),
			("Human", "Building"), ("Human", "Ground"), ("Human", "Obstacle"), ("Human", "FireZone"), ("Human", "Water"),
			("Water", "FireZone"), ("Water", "Building"), ("Water", "Ground"), ("Water", "Obstacle"), ("Water", "Water")
		};

		private static readonly string[] StrictLayers = { "Rotors", "Cup", "Human" };
		private static readonly string[] BuiltInLayers = { "Default", "TransparentFX", "Ignore Raycast", "UI" };

		[InitializeOnLoadMethod]
		private static void OnLoad() => EditorApplication.delayCall += Run;

		private static void Run()
		{
			if (EditorApplication.isCompiling || EditorApplication.isUpdating)
			{
				EditorApplication.delayCall += Run;
				return;
			}

			int tags = AddTags();
			int layers = AddLayers();
			int matrix = SetCollisionMatrix();
			int assets = CreateAssets();
			AssetDatabase.SaveAssets();
			Debug.Log($"[Bootstrap] tags={tags} layers={layers} matrix={matrix} assets={assets} (counts of changes; 0 means already set)");
			Debug.Log("[Bootstrap] done");
		}

		private static int AddTags()
		{
			var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
			var tagsProp = tagManager.FindProperty("tags");
			int added = 0;
			foreach (var tag in Tags)
			{
				bool found = false;
				for (int i = 0; i < tagsProp.arraySize && !found; i++)
					found = tagsProp.GetArrayElementAtIndex(i).stringValue == tag;
				if (found) continue;
				tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
				tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
				added++;
			}
			if (added > 0) tagManager.ApplyModifiedPropertiesWithoutUndo();
			return added;
		}

		private static int AddLayers()
		{
			var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
			var layersProp = tagManager.FindProperty("layers");
			int added = 0;
			foreach (var (index, name) in Layers)
			{
				var element = layersProp.GetArrayElementAtIndex(index);
				if (element.stringValue == name) continue;
				if (!string.IsNullOrEmpty(element.stringValue))
				{
					Debug.LogWarning($"[Bootstrap] layer {index} is '{element.stringValue}', expected '{name}'; left unchanged");
					continue;
				}
				element.stringValue = name;
				added++;
			}
			if (added > 0) tagManager.ApplyModifiedPropertiesWithoutUndo();
			return added;
		}

		private static int SetCollisionMatrix()
		{
			// Layer names are resolved from TagManager; this only works after AddLayers has been applied.
			int changed = 0;
			for (int i = 0; i < Layers.Length; i++)
			{
				for (int j = i; j < Layers.Length; j++)
				{
					int a = LayerMask.NameToLayer(Layers[i].name);
					int b = LayerMask.NameToLayer(Layers[j].name);
					if (a < 0 || b < 0) continue;
					bool collide = IsAllowed(Layers[i].name, Layers[j].name);
					if (Physics.GetIgnoreLayerCollision(a, b) != collide) continue; // already correct
					Physics.IgnoreLayerCollision(a, b, !collide);
					changed++;
				}
			}
			// Rotors, Cup and Human must not collide with the built-in layers.
			foreach (var project in StrictLayers)
				foreach (var builtIn in BuiltInLayers)
				{
					int a = LayerMask.NameToLayer(project), b = LayerMask.NameToLayer(builtIn);
					if (a < 0 || b < 0 || Physics.GetIgnoreLayerCollision(a, b)) continue;
					Physics.IgnoreLayerCollision(a, b, true);
					changed++;
				}
			if (changed > 0)
			{
				var dynamics = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset");
				if (dynamics.Length > 0) EditorUtility.SetDirty(dynamics[0]);
			}
			return changed;
		}

		private static bool IsAllowed(string x, string y)
		{
			foreach (var (a, b) in Allowed)
				if ((a == x && b == y) || (a == y && b == x)) return true;
			return false;
		}

		private static int CreateAssets()
		{
			if (!AssetDatabase.IsValidFolder(SettingsFolder)) AssetDatabase.CreateFolder("Assets", "Settings");
			int created = 0;
			created += Create<HeliConfig>();
			created += Create<CupConfig>();
			created += Create<HumanConfig>();
			created += Create<FireConfig>();
			created += Create<BuildingConfig>();
			created += Create<GameRulesConfig>();
			return created;
		}

		private static int Create<T>() where T : ScriptableObject
		{
			string path = $"{SettingsFolder}/{typeof(T).Name}.asset";
			if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return 0;
			AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(), path); // field initializers hold the defaults
			return 1;
		}
	}
}
