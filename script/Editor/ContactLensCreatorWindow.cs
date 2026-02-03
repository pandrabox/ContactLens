#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace com.github.pandrabox.contactlens
{

public class ContactLensCreatorWindow : EditorWindow
{
    private string authorName = "";
    private string productName = "";
    private ContactLens.AvatarMode avatarMode = ContactLens.AvatarMode.Ver12;
    
    private const string TmpPath = "Assets/Pan/ContactLens/tmp";
    private const string StateFileName = "creator_state.json";
    
    [System.Serializable]
    private class CreatorState
    {
        public string authorName;
        public string productName;
        public int avatarMode;
        public bool isCreating;
    }
    
    private static CreatorState currentState;
    
    [MenuItem("Pan/ContactLens/製作者モード")]
    public static void ShowWindow()
    {
        var window = GetWindow<ContactLensCreatorWindow>("ContactLens 製作者モード");
        window.minSize = new Vector2(400, 300);
        window.LoadState();
    }
    
    public static bool IsCreating => currentState?.isCreating ?? false;
    public static string ProjectPath => currentState != null && currentState.isCreating 
        ? $"Assets/{currentState.authorName}/{currentState.productName}" 
        : null;
    
    private void OnEnable()
    {
        LoadState();
    }
    
    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("製作者モード", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("コンタクトレンズの配布用unitypackageを作成します。\n\n" +
            "フォルダ構造:\n" +
            "  {作者名}/{製品名}/        - プレハブ\n" +
            "  {作者名}/{製品名}/res/tex - レンズテクスチャ\n" +
            "  {作者名}/{製品名}/res/tmb - サムネイル", MessageType.Info);
        
        EditorGUILayout.Space(10);
        
        bool isCreating = currentState?.isCreating ?? false;
        
        using (new EditorGUI.DisabledGroupScope(isCreating))
        {
            EditorGUILayout.LabelField("作者名", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Assetsフォルダ直下のフォルダ名です", EditorStyles.miniLabel);
            authorName = EditorGUILayout.TextField(authorName);
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.LabelField("製品名", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("作者フォルダ直下のフォルダ名です", EditorStyles.miniLabel);
            productName = EditorGUILayout.TextField(productName);
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.LabelField("対象アバター", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("作成時に使ったアバターを選んでください", EditorStyles.miniLabel);
            avatarMode = (ContactLens.AvatarMode)EditorGUILayout.EnumPopup(avatarMode);
        }
        
        EditorGUILayout.Space(10);
        
        if (isCreating)
        {
            EditorGUILayout.HelpBox($"製作中: Assets/{currentState.authorName}/{currentState.productName}", MessageType.None);
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("unitypackage出力", GUILayout.Height(30)))
            {
                ExportUnityPackage();
            }
            if (GUILayout.Button("製作終了", GUILayout.Height(30)))
            {
                EndCreation();
            }
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            bool canStart = !string.IsNullOrEmpty(authorName) && !string.IsNullOrEmpty(productName);
            
            using (new EditorGUI.DisabledGroupScope(!canStart))
            {
                if (GUILayout.Button("製作開始", GUILayout.Height(30)))
                {
                    StartCreation();
                }
            }
            
            if (!canStart)
            {
                EditorGUILayout.HelpBox("作者名と製品名を入力してください。", MessageType.Warning);
            }
        }
    }
    
    private void StartCreation()
    {
        string projectPath = $"Assets/{authorName}/{productName}";
        
        // フォルダ作成
        if (!AssetDatabase.IsValidFolder($"Assets/{authorName}"))
        {
            AssetDatabase.CreateFolder("Assets", authorName);
        }
        if (!AssetDatabase.IsValidFolder(projectPath))
        {
            AssetDatabase.CreateFolder($"Assets/{authorName}", productName);
        }
        
        // res フォルダ作成
        string resPath = $"{projectPath}/res";
        if (!AssetDatabase.IsValidFolder(resPath))
        {
            AssetDatabase.CreateFolder(projectPath, "res");
        }
        
        // tex フォルダ作成
        string texPath = $"{resPath}/tex";
        if (!AssetDatabase.IsValidFolder(texPath))
        {
            AssetDatabase.CreateFolder(resPath, "tex");
        }
        
        // tmb フォルダ作成
        string tmbPath = $"{resPath}/tmb";
        if (!AssetDatabase.IsValidFolder(tmbPath))
        {
            AssetDatabase.CreateFolder(resPath, "tmb");
        }
        
        // tmp作成
        if (!AssetDatabase.IsValidFolder(TmpPath))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Pan/ContactLens"))
            {
                AssetDatabase.CreateFolder("Assets/Pan", "ContactLens");
            }
            AssetDatabase.CreateFolder("Assets/Pan/ContactLens", "tmp");
        }
        
        // 状態保存
        currentState = new CreatorState
        {
            authorName = authorName,
            productName = productName,
            avatarMode = (int)avatarMode,
            isCreating = true
        };
        SaveState();
        
        AssetDatabase.Refresh();
        Debug.Log($"[ContactLens] 製作開始: {projectPath}");
    }
    
    private void EndCreation()
    {
        currentState.isCreating = false;
        SaveState();
        Debug.Log("[ContactLens] 製作終了");
    }
    
    private void ExportUnityPackage()
    {
        string projectPath = $"Assets/{currentState.authorName}/{currentState.productName}";
        
        if (!AssetDatabase.IsValidFolder(projectPath))
        {
            EditorUtility.DisplayDialog("エラー", $"プロジェクトフォルダが存在しません: {projectPath}", "OK");
            return;
        }
        
        string exportPath = EditorUtility.SaveFilePanel(
            "unitypackage出力",
            "",
            $"{currentState.productName}.unitypackage",
            "unitypackage"
        );
        
        if (string.IsNullOrEmpty(exportPath)) return;
        
        AssetDatabase.ExportPackage(projectPath, exportPath, ExportPackageOptions.Recurse);
        Debug.Log($"[ContactLens] unitypackage出力完了: {exportPath}");
        EditorUtility.DisplayDialog("完了", $"unitypackageを出力しました。\n{exportPath}", "OK");
    }
    
    private void SaveState()
    {
        if (!Directory.Exists(TmpPath))
        {
            Directory.CreateDirectory(TmpPath);
        }
        string json = JsonUtility.ToJson(currentState, true);
        File.WriteAllText(Path.Combine(TmpPath, StateFileName), json);
    }
    
    private void LoadState()
    {
        string path = Path.Combine(TmpPath, StateFileName);
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            currentState = JsonUtility.FromJson<CreatorState>(json);
            authorName = currentState.authorName ?? "";
            productName = currentState.productName ?? "";
            avatarMode = (ContactLens.AvatarMode)currentState.avatarMode;
        }
        else
        {
            currentState = null;
        }
    }
}

}
#endif
