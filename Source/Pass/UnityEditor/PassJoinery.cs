using UnityEditor;
using UnityEngine;

namespace Winterbourne.Editor
{
    public static partial class PassPortalBuild
    {
        static void ApplyTimber(Material material, float shade=1f)
        {
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Package+"/Station/WeatheredWalnut.png");
            if(!texture) throw new System.InvalidOperationException("Missing cabinet walnut surface");
            material.SetTexture("_BaseMap",texture);
            material.SetColor("_BaseColor",new Color(.92f*shade,.93f*shade,.945f*shade,1));
            material.SetFloat("_Metallic",0);
            material.SetFloat("_Smoothness",.29f);
            EditorUtility.SetDirty(material);
        }
    }
}
