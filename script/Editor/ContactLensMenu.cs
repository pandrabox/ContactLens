#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace com.github.pandrabox.contactlens
{

public static class ContactLensMenu
{
    [MenuItem("Pan/ContactLens/RemoveAll")]
    static void RemoveAll()
    {
        // シーン内の全ContactLensを取得
        var lenses = Object.FindObjectsByType<ContactLens>(FindObjectsSortMode.None);
        
        int lensCount = lenses.Length;
        
        // 全て削除（OnDestroyでRestore発火）
        foreach (var lens in lenses)
        {
            Object.DestroyImmediate(lens.gameObject);
        }
        
        // 壊れたBodyをRevertで復旧
        int repaired = RepairBrokenBodies();
        
        // Generatedフォルダ内の残骸も掃除
        CleanupGenerated();
        
        AssetDatabase.Refresh();
        
        string msg = $"[ContactLens] {lensCount}件削除完了";
        if (repaired > 0)
        {
            msg += $", {repaired}件のBodyを復旧";
        }
        Debug.Log(msg);
    }
    
    static int RepairBrokenBodies()
    {
        int repaired = 0;
        
        var allSMR = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None);
        
        foreach (var smr in allSMR)
        {
            if (smr.gameObject.name != "Body") continue;
            if (smr.sharedMesh != null && smr.sharedMaterial != null) continue;
            
            // Prefabインスタンスならrevert
            if (PrefabUtility.IsPartOfPrefabInstance(smr.gameObject))
            {
                PrefabUtility.RevertObjectOverride(smr, InteractionMode.AutomatedAction);
                repaired++;
                Debug.Log($"[ContactLens] {smr.transform.parent?.name}/Body をRevertで復旧");
            }
        }
        
        return repaired;
    }
    
    static void CleanupGenerated()
    {
        string folder = "Assets/Pan/ContactLens/Generated";
        if (!AssetDatabase.IsValidFolder(folder)) return;
        
        var assets = AssetDatabase.FindAssets("", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .ToList();
        
        foreach (var path in assets)
        {
            AssetDatabase.DeleteAsset(path);
        }
        
        if (assets.Count > 0)
        {
            Debug.Log($"[ContactLens] Generated内 {assets.Count}件削除");
        }
    }
}
}
#endif
