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
    private string sourceAvatar = "flat12";  // レンズ作成元アバター
    
    private const string TmpPath = "Assets/Pan/ContactLens/tmp";
    private const string StateFileName = "creator_state.json";
    private const string ReadMeTemplatePath = "Assets/Pan/ContactLens/config/ReadMeTemplate.txt";
    
    [System.Serializable]
    private class CreatorState
    {
        public string authorName;
        public string productName;
        public string sourceAvatar;
        public bool isCreating;
    }
    
    private static CreatorState currentState;
    
    [MenuItem("Pan/ContactLens/製作者モード")]
    public static void ShowWindow()
    {
        var window = GetWindow<ContactLensCreatorWindow>("ContactLens 製作者モード");
        window.minSize = new Vector2(400, 320);
        window.LoadState();
    }
    
    public static bool IsCreating => currentState?.isCreating ?? false;
    public static string ProjectPath => currentState != null && currentState.isCreating 
        ? $"Assets/{currentState.authorName}/{currentState.productName}" 
        : null;
    public static string SourceAvatar => currentState?.sourceAvatar ?? "flat12";
    public static string AuthorName => currentState?.authorName ?? "";
    public static string ProductName => currentState?.productName ?? "";
    
    private void OnEnable()
    {
        LoadState();
    }
    
    private void OnGUI()
    {
        var avatarNames = ContactLensConfig.AvatarNames;
        
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
            
            EditorGUILayout.LabelField("レンズ作成元アバター", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("どのアバター用にレンズを作成しますか？", EditorStyles.miniLabel);
            
            if (avatarNames != null && avatarNames.Length > 0)
            {
                int sourceIndex = ContactLensConfig.GetAvatarIndex(sourceAvatar);
                string[] displayNames = GetDisplayNames(avatarNames);
                int newIndex = EditorGUILayout.Popup(sourceIndex, displayNames);
                if (newIndex >= 0 && newIndex < avatarNames.Length)
                {
                    sourceAvatar = avatarNames[newIndex];
                }
            }
        }
        
        EditorGUILayout.Space(10);
        
        if (isCreating)
        {
            EditorGUILayout.HelpBox($"製作中: Assets/{currentState.authorName}/{currentState.productName}\n" +
                $"作成元: {GetDisplayName(currentState.sourceAvatar)}", MessageType.None);
            
            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("アクティブなアバターにひな形を作成", GUILayout.Height(30)))
            {
                CreateTemplateOnActiveAvatar();
            }
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("配布物出力", GUILayout.Height(30)))
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
    
    private void CreateTemplateOnActiveAvatar()
    {
        // アクティブなGameObjectを取得
        var activeObj = Selection.activeGameObject;
        if (activeObj == null)
        {
            EditorUtility.DisplayDialog("エラー", "アバターを選択してください。", "OK");
            return;
        }
        
        // アバターのルートを探す（Bodyがある階層）
        Transform avatarRoot = null;
        if (activeObj.transform.Find("Body") != null)
        {
            avatarRoot = activeObj.transform;
        }
        else if (activeObj.transform.parent != null && activeObj.transform.parent.Find("Body") != null)
        {
            avatarRoot = activeObj.transform.parent;
        }
        
        if (avatarRoot == null)
        {
            EditorUtility.DisplayDialog("エラー", "アバター（Bodyを持つオブジェクト）を選択してください。", "OK");
            return;
        }
        
        // 既存のContactLensがあるか確認
        var existingLens = avatarRoot.GetComponentInChildren<ContactLens>();
        if (existingLens != null)
        {
            EditorUtility.DisplayDialog("エラー", "このアバターには既にContactLensがあります。", "OK");
            Selection.activeGameObject = existingLens.gameObject;
            return;
        }
        
        // ひな形を作成
        var lensObj = new GameObject("ContactLens");
        lensObj.transform.SetParent(avatarRoot);
        lensObj.transform.localPosition = Vector3.zero;
        lensObj.transform.localRotation = Quaternion.identity;
        lensObj.transform.localScale = Vector3.one;
        
        var lens = lensObj.AddComponent<ContactLens>();
        lens.sourceAvatar = sourceAvatar;
        lens.targetAvatar = sourceAvatar;
        
        Selection.activeGameObject = lensObj;
        EditorGUIUtility.PingObject(lensObj);
        
        Debug.Log($"[ContactLens] ひな形を作成: {avatarRoot.name}/ContactLens (アバター: {sourceAvatar})");
    }
    
    string[] GetDisplayNames(string[] avatarNames)
    {
        var names = new string[avatarNames.Length];
        for (int i = 0; i < avatarNames.Length; i++)
        {
            var info = ContactLensConfig.GetAvatar(avatarNames[i]);
            names[i] = info?.displayName ?? avatarNames[i];
        }
        return names;
    }
    
    string GetDisplayName(string avatarName)
    {
        var info = ContactLensConfig.GetAvatar(avatarName);
        return info?.displayName ?? avatarName;
    }
    
    private void StartCreation()
    {
        string projectPath = $"Assets/{authorName}/{productName}";
        
        if (!AssetDatabase.IsValidFolder($"Assets/{authorName}"))
        {
            AssetDatabase.CreateFolder("Assets", authorName);
        }
        if (!AssetDatabase.IsValidFolder(projectPath))
        {
            AssetDatabase.CreateFolder($"Assets/{authorName}", productName);
        }
        
        string resPath = $"{projectPath}/res";
        if (!AssetDatabase.IsValidFolder(resPath))
        {
            AssetDatabase.CreateFolder(projectPath, "res");
        }
        
        string texPath = $"{resPath}/tex";
        if (!AssetDatabase.IsValidFolder(texPath))
        {
            AssetDatabase.CreateFolder(resPath, "tex");
        }
        
        string tmbPath = $"{resPath}/tmb";
        if (!AssetDatabase.IsValidFolder(tmbPath))
        {
            AssetDatabase.CreateFolder(resPath, "tmb");
        }
        
        if (!AssetDatabase.IsValidFolder(TmpPath))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Pan/ContactLens"))
            {
                AssetDatabase.CreateFolder("Assets/Pan", "ContactLens");
            }
            AssetDatabase.CreateFolder("Assets/Pan/ContactLens", "tmp");
        }
        
        currentState = new CreatorState
        {
            authorName = authorName,
            productName = productName,
            sourceAvatar = sourceAvatar,
            isCreating = true
        };
        SaveState();
        
        AssetDatabase.Refresh();
        Debug.Log($"[ContactLens] 製作開始: {projectPath} (作成元: {sourceAvatar})");
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
        
        string exportFolder = EditorUtility.SaveFolderPanel(
            "配布物出力先を選択",
            "",
            ""
        );
        
        if (string.IsNullOrEmpty(exportFolder)) return;
        
        // 製品名フォルダを作成
        string productFolder = Path.Combine(exportFolder, currentState.productName);
        if (!Directory.Exists(productFolder))
        {
            Directory.CreateDirectory(productFolder);
        }
        
        string exportPath = Path.Combine(productFolder, $"{currentState.productName}.unitypackage");
        
        // ReadMe生成（製品名フォルダ内に、上書きしない）
        GenerateReadMe(exportPath);
        
        AssetDatabase.ExportPackage(projectPath, exportPath, ExportPackageOptions.Recurse);
        Debug.Log($"[ContactLens] 配布物出力完了: {productFolder}");
        EditorUtility.DisplayDialog("完了", $"配布物を出力しました。\n{productFolder}\n\nReadMe.txtはサンプルです。自由に書き換えてご利用ください。\n\nBoothなどで公開する際は、元アバターのライセンスを確認し、著作者から適切な許可を取得してください。", "OK");
    }
    
    private void GenerateReadMe(string unityPackagePath)
    {
        string exportDir = Path.GetDirectoryName(unityPackagePath);
        string readMePath = Path.Combine(exportDir, "ReadMe.txt");
        
        // 既に存在する場合は上書きしない
        if (File.Exists(readMePath))
        {
            Debug.Log($"[ContactLens] ReadMe.txt は既に存在するためスキップ: {readMePath}");
            return;
        }
        
        // テンプレート読み込み
        string templateFullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), ReadMeTemplatePath);
        if (!File.Exists(templateFullPath))
        {
            Debug.LogWarning($"[ContactLens] ReadMeテンプレートが見つかりません: {templateFullPath}");
            return;
        }
        
        string template = File.ReadAllText(templateFullPath);
        
        // プレースホルダー置換
        var sourceInfo = ContactLensConfig.GetAvatar(currentState.sourceAvatar);
        string sourceDisplay = sourceInfo?.ReadmeDisplayName ?? currentState.sourceAvatar;
        string nowDate = System.DateTime.Now.ToString("yyyy-MM-dd");
        string unityVersion = Application.unityVersion;
        
        string content = template
            .Replace("{SourceAvatar}", sourceDisplay)
            .Replace("{製品名}", currentState.productName)
            .Replace("{NowDate}", nowDate)
            .Replace("{Unity Version}", unityVersion);
        
        File.WriteAllText(readMePath, content);
        Debug.Log($"[ContactLens] ReadMe.txt を生成: {readMePath}");
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
            sourceAvatar = currentState.sourceAvatar ?? "flat12";
        }
        else
        {
            currentState = null;
            sourceAvatar = "flat12";
        }
    }
}

}
#endif
