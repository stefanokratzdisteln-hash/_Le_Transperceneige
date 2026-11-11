using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Winterbourne.Editor
{
    public static class DetectiveBuild
    {
        public const string Folder = "Assets/Transperceneige/Pass/Detective";
        public const string PrefabPath = Folder + "/Detective.prefab";

        public static GameObject BuildPrefab()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Detective.fbx");
            if (!source) throw new InvalidOperationException("The detective's rigged FBX has not been imported.");
            var root = new GameObject("Detective");
            try
            {
                var rig = (GameObject)PrefabUtility.InstantiatePrefab(source,root.transform);
                var bones = rig.GetComponentsInChildren<Transform>(true);
                Transform Bone(string id) => bones.Single(b => b.name == id);
                var nose = (Bone("LeftEye").position + Bone("RightEye").position) * .5f - Bone("Head").position;
                nose.y = 0;
                rig.transform.localRotation = Quaternion.FromToRotation(nose.normalized,Vector3.forward) * rig.transform.localRotation;
                var skin = rig.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (skin.Length==0 || skin.Any(r => r.sharedMaterials.Any(m => !m || m.shader.name!="Universal Render Pipeline/Lit")))
                    throw new InvalidOperationException("Expected skinned anatomy and garments with native URP materials.");
                foreach (var renderer in skin)
                {
                    renderer.updateWhenOffscreen = true;
                    renderer.localBounds = new Bounds(renderer.localBounds.center,renderer.localBounds.size + .18f * Vector3.one);
                }
                foreach(var name in new[]{"Hips","Chest","Neck","Head","LeftHand","RightHand","LeftUpperArm","RightLowerLeg"})
                    Bone(name);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if(!prefab) throw new InvalidOperationException("Could not save "+PrefabPath);
                return prefab;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        public static void Build()
        {
            var prefab=BuildPrefab();
            Debug.Log("DETECTIVE PREFAB VERIFIED "+AssetDatabase.GetAssetPath(prefab));
        }
    }
}
