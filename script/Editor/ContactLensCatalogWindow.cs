#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace com.github.pandrabox.contactlens
{

public class ContactLensCatalogWindow : EditorWindow
{
    private List<CatalogItem> items = new List<CatalogItem>();
    private Vector2 scrollPos;
    private int selectedIndex = -1;
    private int gridSize = 80;
    
    // フィルタ
    private int authorFilterIndex = 0;
    private int avatarFilterIndex = 0;
    private List<string> authorList = new List<string> { "全て" };
    private List<string> avatarFilterList = new List<string> { "全て" };
    
    // スタイル
    private GUIStyle largeLabel;
    
    private class CatalogItem
    {
        public string prefabPath;
        public string name;
        public Texture2D thumbnail;
        public string targetAvatar;
        public string sourceAvatar;
        public string author;
    }
    
    [MenuItem("Pan/ContactLens/カタログ")]
    public static void ShowWindow()
    {
        var window = GetWindow<ContactLensCatalogWindow>("ContactLens カタログ");
        window.minSize = new Vector2(900, 600);
        window.RefreshCatalog();
    }
    
    private void OnEnable()
    {
        RefreshCatalog();
    }
    
    private void InitStyles()
    {
        if (largeLabel == null)
        {
            largeLabel = new GUIStyle(EditorStyles.label);
            largeLabel.fontSize = 14;
        }
    }
    
    private void OnGUI()
    {
        InitStyles();
        
        // ツールバー
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        if (GUILayout.Button("更新", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            RefreshCatalog();
        }
        
        GUILayout.Space(10);
        
        // 作者フィルタ
        EditorGUILayout.LabelField("作者:", GUILayout.Width(35));
        authorFilterIndex = EditorGUILayout.Popup(authorFilterIndex, authorList.ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(100));
        
        GUILayout.Space(10);
        
        // アバターフィルタ
        EditorGUILayout.LabelField("アバター:", GUILayout.Width(50));
        avatarFilterIndex = EditorGUILayout.Popup(avatarFilterIndex, avatarFilterList.ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(100));
        
        GUILayout.FlexibleSpace();
        
        EditorGUILayout.LabelField($"{GetFilteredItems().Count} 件", GUILayout.Width(50));
        
        EditorGUILayout.EndHorizontal();
        
        // メインエリア
        EditorGUILayout.BeginHorizontal();
        
        // グリッド表示
        DrawGrid();
        
        // プレビューパネル
        DrawPreview();
        
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawGrid()
    {
        var filteredItems = GetFilteredItems();
        
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width - 540));
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        
        int columns = Mathf.Max(1, (int)((position.width - 560) / (gridSize + 10)));
        int col = 0;
        
        EditorGUILayout.BeginHorizontal();
        
        for (int i = 0; i < filteredItems.Count; i++)
        {
            var item = filteredItems[i];
            
            Rect rect = EditorGUILayout.GetControlRect(GUILayout.Width(gridSize), GUILayout.Height(gridSize + 20));
            
            if (selectedIndex == i)
            {
                EditorGUI.DrawRect(rect, new Color(0.3f, 0.5f, 0.8f, 0.5f));
            }
            
            Rect thumbRect = new Rect(rect.x + 5, rect.y + 2, gridSize - 10, gridSize - 10);
            if (item.thumbnail != null)
            {
                GUI.DrawTexture(thumbRect, item.thumbnail, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUI.DrawRect(thumbRect, Color.gray);
            }
            
            Rect labelRect = new Rect(rect.x, rect.y + gridSize - 8, gridSize, 20);
            GUI.Label(labelRect, item.name, EditorStyles.centeredGreyMiniLabel);
            
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                selectedIndex = i;
                var obj = AssetDatabase.LoadAssetAtPath<GameObject>(item.prefabPath);
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
                Repaint();
            }
            
            col++;
            if (col >= columns)
            {
                col = 0;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
            }
        }
        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }
    
    private void DrawPreview()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(520));
        
        var filteredItems = GetFilteredItems();
        
        if (selectedIndex >= 0 && selectedIndex < filteredItems.Count)
        {
            var item = filteredItems[selectedIndex];
            
            Rect previewRect = EditorGUILayout.GetControlRect(GUILayout.Width(512), GUILayout.Height(512));
            if (item.thumbnail != null)
            {
                GUI.DrawTexture(previewRect, item.thumbnail, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUI.DrawRect(previewRect, Color.gray);
                GUI.Label(previewRect, "No Thumbnail", EditorStyles.centeredGreyMiniLabel);
            }
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField(item.name, largeLabel);
            EditorGUILayout.LabelField($"作者: {item.author}", largeLabel);
            
            var targetInfo = ContactLensConfig.GetAvatar(item.targetAvatar);
            var sourceInfo = ContactLensConfig.GetAvatar(item.sourceAvatar);
            string targetDisplay = targetInfo?.displayName ?? item.targetAvatar;
            string sourceDisplay = sourceInfo?.displayName ?? item.sourceAvatar;
            
            EditorGUILayout.LabelField($"適用先: {targetDisplay}", largeLabel);
            if (item.sourceAvatar != item.targetAvatar)
            {
                EditorGUILayout.LabelField($"作成元: {sourceDisplay}", largeLabel);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("レンズを選択してください", MessageType.Info);
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private List<CatalogItem> GetFilteredItems()
    {
        var filtered = items.AsEnumerable();
        
        // 作者フィルタ
        if (authorFilterIndex > 0 && authorFilterIndex < authorList.Count)
        {
            string author = authorList[authorFilterIndex];
            filtered = filtered.Where(i => i.author == author);
        }
        
        // アバターフィルタ
        if (avatarFilterIndex > 0 && avatarFilterIndex < avatarFilterList.Count)
        {
            string avatar = avatarFilterList[avatarFilterIndex];
            filtered = filtered.Where(i => i.targetAvatar == avatar || i.sourceAvatar == avatar);
        }
        
        return filtered.ToList();
    }
    
    private void RefreshCatalog()
    {
        items.Clear();
        selectedIndex = -1;
        authorList = new List<string> { "全て" };
        var authors = new HashSet<string>();
        
        // アバターフィルタリスト構築
        avatarFilterList = new List<string> { "全て" };
        var avatarNames = ContactLensConfig.AvatarNames;
        if (avatarNames != null)
        {
            foreach (var name in avatarNames)
            {
                var info = ContactLensConfig.GetAvatar(name);
                avatarFilterList.Add(info?.displayName ?? name);
            }
        }
        
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            
            var lens = prefab.GetComponent<ContactLens>();
            if (lens == null) continue;
            
            string author = ExtractAuthor(path);
            authors.Add(author);
            
            var item = new CatalogItem
            {
                prefabPath = path,
                name = prefab.name,
                targetAvatar = lens.targetAvatar ?? "flat12",
                sourceAvatar = lens.sourceAvatar ?? "flat12",
                thumbnail = FindThumbnail(path),
                author = author
            };
            
            items.Add(item);
        }
        
        authorList.AddRange(authors.OrderBy(a => a));
        
        Debug.Log($"[ContactLens] カタログ更新: {items.Count} 件");
    }
    
    private string ExtractAuthor(string path)
    {
        var parts = path.Split('/');
        if (parts.Length >= 2 && parts[0] == "Assets")
        {
            return parts[1];
        }
        return "Unknown";
    }
    
    private Texture2D FindThumbnail(string prefabPath)
    {
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
                return tex;
            }
        }
        
        return null;
    }
}

}
#endif
