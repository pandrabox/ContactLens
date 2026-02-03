#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace com.github.pandrabox.contactlens
{

[CustomEditor(typeof(ContactLens))]
public class ContactLensEditor : Editor
{
    string prevTargetAvatar;
    string prevSourceAvatar;
    bool prevEnablePupil;
    
    void OnEnable()
    {
        var lens = (ContactLens)target;
        prevTargetAvatar = lens.targetAvatar;
        prevSourceAvatar = lens.sourceAvatar;
        prevEnablePupil = lens.enablePupil;
    }
    
    public override void OnInspectorGUI()
    {
        var lens = (ContactLens)target;
        var avatarNames = ContactLensConfig.AvatarNames;
        
        if (avatarNames == null || avatarNames.Length == 0)
        {
            EditorGUILayout.HelpBox("avatars.json が読み込めません", MessageType.Error);
            if (GUILayout.Button("設定を再読み込み"))
            {
                ContactLensConfig.Reload();
            }
            return;
        }
        
        // レンズ設定
        EditorGUILayout.LabelField("レンズ設定", EditorStyles.boldLabel);
        lens.lensTexture = (Texture2D)EditorGUILayout.ObjectField("レンズテクスチャ", lens.lensTexture, typeof(Texture2D), false);
        
        EditorGUILayout.Space();
        
        // アバター選択 (to)
        EditorGUILayout.LabelField("適用先アバター", EditorStyles.boldLabel);
        int targetIndex = ContactLensConfig.GetAvatarIndex(lens.targetAvatar);
        string[] displayNames = GetDisplayNames(avatarNames);
        int newTargetIndex = EditorGUILayout.Popup(targetIndex, displayNames);
        if (newTargetIndex >= 0 && newTargetIndex < avatarNames.Length)
        {
            lens.targetAvatar = avatarNames[newTargetIndex];
        }
        
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
        
        // 瞳孔Hide発動時のヒント
        var avatarInfo = ContactLensConfig.GetAvatar(lens.targetAvatar);
        if (avatarInfo != null && avatarInfo.IsIslandType && (!lens.enablePupil || lens.pupilAlpha < 1f))
        {
            EditorGUILayout.HelpBox("瞳孔メッシュは自動的に非表示になります", MessageType.Info);
        }
        
        EditorGUILayout.Space();
        
        // 上級者設定（折りたたみ）
        lens.showAdvancedSettings = EditorGUILayout.Foldout(lens.showAdvancedSettings, "上級者設定");
        if (lens.showAdvancedSettings)
        {
            EditorGUI.indentLevel++;
            
            // レンズ作成元アバター (from)
            EditorGUILayout.LabelField("レンズ作成元", EditorStyles.miniLabel);
            int sourceIndex = ContactLensConfig.GetAvatarIndex(lens.sourceAvatar);
            int newSourceIndex = EditorGUILayout.Popup(sourceIndex, displayNames);
            if (newSourceIndex >= 0 && newSourceIndex < avatarNames.Length)
            {
                lens.sourceAvatar = avatarNames[newSourceIndex];
            }
            
            if (lens.sourceAvatar != lens.targetAvatar)
            {
                EditorGUILayout.HelpBox($"テクスチャ変換: {lens.sourceAvatar} → {lens.targetAvatar}", MessageType.Info);
            }
            

            EditorGUI.indentLevel--;
        }
        
        // 設定変更時に自動更新（scaleAdjustは除外）
        bool settingsChanged = lens.targetAvatar != prevTargetAvatar ||
                               lens.sourceAvatar != prevSourceAvatar ||
                               lens.enablePupil != prevEnablePupil;
        
        if (settingsChanged)
        {
            prevTargetAvatar = lens.targetAvatar;
            prevSourceAvatar = lens.sourceAvatar;
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
        
        EnsureFolder(texFolderPath);
        EnsureFolder(tmbFolderPath);
        
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
        
        if (string.IsNullOrEmpty(lens.generatedMaterialPath))
        {
            lens.Apply();
        }
        
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
        
        GameObject prefabObj = new GameObject(prefabName);
        var newLens = prefabObj.AddComponent<ContactLens>();
        
        newLens.lensTexture = textureToUse;
        newLens.targetAvatar = lens.targetAvatar;
        newLens.sourceAvatar = lens.sourceAvatar;
        newLens.enablePupil = lens.enablePupil;
        newLens.pupilColor = lens.pupilColor;
        newLens.pupilAlpha = lens.pupilAlpha;
        
        PrefabUtility.SaveAsPrefabAsset(prefabObj, prefabPath);
        DestroyImmediate(prefabObj);
        
        AssetDatabase.Refresh();
        Debug.Log($"[ContactLens] リリース完了: {prefabPath}");
        EditorUtility.DisplayDialog("完了", $"リリースしました。\n{prefabPath}", "OK");
    }
}

}
#endif
