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
    static string contactLensFolder = "Assets/Pan/ContactLens";
    
    static ContactLensThumbnail()
    {
        EditorApplication.projectWindowItemOnGUI -= OnProjectWindowItemGUI;
        EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
    }
    
    static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        
        // ContactLensフォルダ内のプレハブのみ対象
        if (!path.StartsWith(contactLensFolder)) return;
        if (!path.EndsWith(".prefab")) return;
        
        // サムネイル画像を探す
        Texture2D thumb = GetThumbnail(path);
        if (thumb == null) return;
        
        // アイコンサイズの判定（リスト表示 vs グリッド表示）
        if (selectionRect.height > 20)
        {
            // グリッド表示（大きいアイコン）
            float iconSize = selectionRect.height - 14; // ラベル分を引く
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
            // リスト表示（小さいアイコン）
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
        
        // プレハブと同名の_thumb.pngを探す
        string dir = Path.GetDirectoryName(prefabPath);
        string name = Path.GetFileNameWithoutExtension(prefabPath);
        
        // 検索パターン: 同じフォルダ、textureフォルダ、texture/thumbフォルダ
        string[] searchPaths = new string[]
        {
            Path.Combine(dir, name + "_thumb.png"),
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
    
    // キャッシュクリア用（サムネイル更新時に呼ぶ）
    [MenuItem("Pan/ContactLens/ClearThumbnailCache")]
    public static void ClearCache()
    {
        thumbnailCache.Clear();
        EditorApplication.RepaintProjectWindow();
        Debug.Log("[ContactLens] サムネイルキャッシュをクリアしました");
    }
}

}
#endif
