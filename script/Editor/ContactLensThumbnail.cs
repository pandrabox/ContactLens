#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace com.github.pandrabox.contactlens
{

[InitializeOnLoad]
public static class ContactLensThumbnail
{
    static Dictionary<string, Texture2D> thumbnailCache = new Dictionary<string, Texture2D>();
    
    static ContactLensThumbnail()
    {
        EditorApplication.projectWindowItemOnGUI -= OnProjectWindowItemGUI;
        EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
    }
    
    static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        
        if (!path.EndsWith(".prefab")) return;
        
        // ContactLensコンポーネントを持つprefabのみ対象
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return;
        if (prefab.GetComponent<ContactLens>() == null) return;
        
        Texture2D thumb = GetThumbnail(path);
        if (thumb == null) return;
        
        if (selectionRect.height > 20)
        {
            float iconSize = selectionRect.height - 14;
            Rect iconRect = new Rect(
                selectionRect.x + (selectionRect.width - iconSize) / 2,
                selectionRect.y,
                iconSize,
                iconSize
            );
            GUI.DrawTexture(iconRect, thumb, ScaleMode.ScaleToFit);
        }
        else
        {
            Rect iconRect = new Rect(selectionRect.x, selectionRect.y, selectionRect.height, selectionRect.height);
            GUI.DrawTexture(iconRect, thumb, ScaleMode.ScaleToFit);
        }
    }
    
    static Texture2D GetThumbnail(string prefabPath)
    {
        if (thumbnailCache.TryGetValue(prefabPath, out var cached))
        {
            return cached;
        }
        
        string dir = Path.GetDirectoryName(prefabPath);
        string name = Path.GetFileNameWithoutExtension(prefabPath);
        
        string[] searchPaths = new string[]
        {
            Path.Combine(dir, name + "_thumb.png"),
            Path.Combine(dir, "res", "tmb", name + "_thumb.png"),
            Path.Combine(dir, "texture", name + "_thumb.png"),
            Path.Combine(dir, "texture", "thumb", name + "_thumb.png"),
        };
        
        foreach (var thumbPath in searchPaths)
        {
            string unityPath = thumbPath.Replace("\\", "/");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(unityPath);
            if (tex != null)
            {
                thumbnailCache[prefabPath] = tex;
                return tex;
            }
        }
        
        thumbnailCache[prefabPath] = null;
        return null;
    }
    
    public static void ClearCache()
    {
        thumbnailCache.Clear();
        EditorApplication.RepaintProjectWindow();
        Debug.Log("[ContactLens] サムネイルキャッシュをクリアしました");
    }
}

}
#endif
