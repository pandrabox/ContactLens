#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace com.github.pandrabox.contactlens
{

[CustomEditor(typeof(ContactLens))]
public class ContactLensEditor : Editor
{
    ContactLens.AvatarMode prevAvatarMode;
    bool prevEnablePupil;
    
    void OnEnable()
    {
        var lens = (ContactLens)target;
        prevAvatarMode = lens.avatarMode;
        prevEnablePupil = lens.enablePupil;
    }
    
    public override void OnInspectorGUI()
    {
        var lens = (ContactLens)target;
        
        // レンズ設定
        EditorGUILayout.LabelField("レンズ設定", EditorStyles.boldLabel);
        lens.lensTexture = (Texture2D)EditorGUILayout.ObjectField("レンズテクスチャ", lens.lensTexture, typeof(Texture2D), false);
        
        EditorGUILayout.Space();
        
        // アバターモード選択
        EditorGUILayout.LabelField("アバターモード", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Toggle(lens.avatarMode == ContactLens.AvatarMode.Ver12, "Ver1&2", EditorStyles.radioButton))
            lens.avatarMode = ContactLens.AvatarMode.Ver12;
        if (GUILayout.Toggle(lens.avatarMode == ContactLens.AvatarMode.Ver3If, "Ver3&If", EditorStyles.radioButton))
            lens.avatarMode = ContactLens.AvatarMode.Ver3If;
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        // 瞳孔設定
        EditorGUILayout.LabelField("瞳孔設定", EditorStyles.boldLabel);
        lens.enablePupil = EditorGUILayout.Toggle("瞳孔を有効化", lens.enablePupil);
        
        if (lens.enablePupil)
        {
            EditorGUI.indentLevel++;
            lens.pupilColor = EditorGUILayout.ColorField("瞳孔の色", lens.pupilColor);
            lens.pupilAlpha = EditorGUILayout.Slider("瞳孔の透過度", lens.pupilAlpha, 0f, 1f);
            EditorGUI.indentLevel--;
        }
        
        // Ver3&Ifで瞳孔Hide発動時のヒント
        if (lens.avatarMode == ContactLens.AvatarMode.Ver3If && (!lens.enablePupil || lens.pupilAlpha < 1f))
        {
            EditorGUILayout.HelpBox("瞳孔メッシュは自動的に非表示になります", MessageType.Info);
        }
        
        EditorGUILayout.Space();
        
        // マスク設定（折りたたみ）
        lens.showMaskSettings = EditorGUILayout.Foldout(lens.showMaskSettings, "マスク設定");
        if (lens.showMaskSettings)
        {
            EditorGUI.indentLevel++;
            lens.pupilMask12 = (Texture2D)EditorGUILayout.ObjectField("瞳孔 Ver1&2", lens.pupilMask12, typeof(Texture2D), false);
            lens.pupilMask3If = (Texture2D)EditorGUILayout.ObjectField("瞳孔 Ver3&If", lens.pupilMask3If, typeof(Texture2D), false);
            lens.eyeMask = (Texture2D)EditorGUILayout.ObjectField("EyeMask", lens.eyeMask, typeof(Texture2D), false);
            EditorGUI.indentLevel--;
        }
        
        // 設定変更時に自動更新
        bool settingsChanged = lens.avatarMode != prevAvatarMode ||
                               lens.enablePupil != prevEnablePupil;
        
        if (settingsChanged)
        {
            prevAvatarMode = lens.avatarMode;
            prevEnablePupil = lens.enablePupil;
            
            if (!string.IsNullOrEmpty(lens.generatedMaterialPath))
            {
                lens.Restore();
                lens.Apply();
            }
        }
        
        EditorGUILayout.Space();
        
        // 更新ボタン
        if (GUILayout.Button("更新", GUILayout.Height(30)))
        {
            lens.Restore();
            lens.Apply();
        }
        
        // 製作者モード中のみリリースボタン表示
        if (ContactLensCreatorWindow.IsCreating)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("製作者モード", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox($"出力先: {ContactLensCreatorWindow.ProjectPath}", MessageType.None);
            
            if (GUILayout.Button("リリース", GUILayout.Height(30)))
            {
                Release(lens);
            }
        }
        
        if (GUI.changed)
        {
            EditorUtility.SetDirty(lens);
        }
    }
    
    private void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        
        var parent = Path.GetDirectoryName(path).Replace("\\", "/");
        var folderName = Path.GetFileName(path);
        
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }
    
    private void Release(ContactLens lens)
    {
        string projectPath = ContactLensCreatorWindow.ProjectPath;
        if (string.IsNullOrEmpty(projectPath))
        {
            EditorUtility.DisplayDialog("エラー", "製作者モードで製作を開始してください。", "OK");
            return;
        }
        
        if (lens.lensTexture == null)
        {
            EditorUtility.DisplayDialog("エラー", "レンズテクスチャを設定してください。", "OK");
            return;
        }
        
        string prefabName = lens.lensTexture.name;
        string texFolderPath = $"{projectPath}/res/tex";
        string tmbFolderPath = $"{projectPath}/res/tmb";
        string prefabPath = $"{projectPath}/{prefabName}.prefab";
        string thumbPath = $"{tmbFolderPath}/{prefabName}_thumb.png";
        
        // フォルダ確保
        EnsureFolder(texFolderPath);
        EnsureFolder(tmbFolderPath);
        
        // テクスチャがres/texにない場合はコピー
        string texturePath = AssetDatabase.GetAssetPath(lens.lensTexture);
        Texture2D textureToUse = lens.lensTexture;
        
        if (!texturePath.StartsWith(texFolderPath + "/"))
        {
            string ext = Path.GetExtension(texturePath);
            string destTexPath = $"{texFolderPath}/{lens.lensTexture.name}{ext}";
            AssetDatabase.CopyAsset(texturePath, destTexPath);
            AssetDatabase.Refresh();
            textureToUse = AssetDatabase.LoadAssetAtPath<Texture2D>(destTexPath);
            Debug.Log($"[ContactLens] テクスチャをコピー: {destTexPath}");
        }
        
        // 適用状態を確保
        if (string.IsNullOrEmpty(lens.generatedMaterialPath))
        {
            lens.Apply();
        }
        
        // サムネイル生成
        var avatar = lens.transform.parent;
        if (avatar != null)
        {
            var body = avatar.Find("Body");
            if (body != null)
            {
                string fullThumbPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), thumbPath);
                ContactLensThumbnailGenerator.Generate(body, fullThumbPath);
                AssetDatabase.Refresh();
                Debug.Log($"[ContactLens] サムネイル生成: {thumbPath}");
            }
        }
        
        // prefab作成
        GameObject prefabObj = new GameObject(prefabName);
        var newLens = prefabObj.AddComponent<ContactLens>();
        
        // 設定コピー（テクスチャはコピー先を参照）
        newLens.lensTexture = textureToUse;
        newLens.avatarMode = lens.avatarMode;
        newLens.enablePupil = lens.enablePupil;
        newLens.pupilColor = lens.pupilColor;
        newLens.pupilAlpha = lens.pupilAlpha;
        newLens.pupilMask12 = lens.pupilMask12;
        newLens.pupilMask3If = lens.pupilMask3If;
        newLens.eyeMask = lens.eyeMask;
        
        // prefab保存
        PrefabUtility.SaveAsPrefabAsset(prefabObj, prefabPath);
        DestroyImmediate(prefabObj);
        
        AssetDatabase.Refresh();
        Debug.Log($"[ContactLens] リリース完了: {prefabPath}");
        EditorUtility.DisplayDialog("完了", $"リリースしました。\n{prefabPath}", "OK");
    }
}

}
#endif
