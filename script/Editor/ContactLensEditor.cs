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
    bool prevEnablePupil;
    Color prevPupilColor;
    float prevPupilAlpha;
    float prevScaleAdjust;
    float prevHueShift;
    Texture2D prevLensTexture;
    
    bool hasUnappliedChanges = false;
    
    void OnEnable()
    {
        var lens = (ContactLens)target;
        prevTargetAvatar = lens.targetAvatar;
        prevEnablePupil = lens.enablePupil;
        prevPupilColor = lens.pupilColor;
        prevPupilAlpha = lens.pupilAlpha;
        prevScaleAdjust = lens.scaleAdjust;
        prevHueShift = lens.hueShift;
        prevLensTexture = lens.lensTexture;
        hasUnappliedChanges = false;
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
        
        // 作成時アバター（ラベル表示のみ）
        var sourceInfo = ContactLensConfig.GetAvatar(lens.sourceAvatar);
        string sourceDisplay = sourceInfo?.displayName ?? lens.sourceAvatar;
        EditorGUILayout.LabelField($"作成時アバター: {sourceDisplay}", EditorStyles.miniLabel);
        
        EditorGUILayout.Space(5);
        
        // レンズ設定
        EditorGUILayout.LabelField("レンズ設定", EditorStyles.boldLabel);
        lens.lensTexture = (Texture2D)EditorGUILayout.ObjectField("レンズテクスチャ", lens.lensTexture, typeof(Texture2D), false);
        
        // レンズテクスチャ変更時に即時更新
        if (lens.lensTexture != prevLensTexture)
        {
            prevLensTexture = lens.lensTexture;
            if (lens.lensTexture != null)
            {
                lens.Restore();
                lens.Apply();
                prevEnablePupil = lens.enablePupil;
                prevPupilColor = lens.pupilColor;
                prevPupilAlpha = lens.pupilAlpha;
                prevScaleAdjust = lens.scaleAdjust;
                prevHueShift = lens.hueShift;
                hasUnappliedChanges = false;
            }
        }
        
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
        
        EditorGUILayout.Space();
        
        // スケール調整と色相シフト（製作者モード中は非表示）
        if (!ContactLensCreatorWindow.IsCreating)
        {
            lens.scaleAdjust = EditorGUILayout.Slider("目のスケール", lens.scaleAdjust, 0.65f, 1.35f);
            lens.hueShift = EditorGUILayout.Slider("色相シフト", lens.hueShift, 0f, 1f);
            
            EditorGUILayout.Space();
        }
        
        // 設定変更時にアバター変更のみ自動更新
        if (lens.targetAvatar != prevTargetAvatar)
        {
            prevTargetAvatar = lens.targetAvatar;
            
            if (!string.IsNullOrEmpty(lens.generatedMaterialPath))
            {
                lens.Restore();
                lens.Apply();
                // 適用したので前回値を更新
                prevEnablePupil = lens.enablePupil;
                prevPupilColor = lens.pupilColor;
                prevPupilAlpha = lens.pupilAlpha;
                prevScaleAdjust = lens.scaleAdjust;
                prevHueShift = lens.hueShift;
                hasUnappliedChanges = false;
            }
        }
        
        // 他の設定変更をチェック
        bool settingsChanged = lens.enablePupil != prevEnablePupil ||
                               lens.pupilColor != prevPupilColor ||
                               !Mathf.Approximately(lens.pupilAlpha, prevPupilAlpha) ||
                               !Mathf.Approximately(lens.scaleAdjust, prevScaleAdjust) ||
                               !Mathf.Approximately(lens.hueShift, prevHueShift);
        
        if (settingsChanged && !string.IsNullOrEmpty(lens.generatedMaterialPath))
        {
            hasUnappliedChanges = true;
        }
        
        // 未適用の設定がある場合の警告
        if (hasUnappliedChanges)
        {
            EditorGUILayout.HelpBox("適用されていない設定があります。更新を押して下さい", MessageType.Warning);
        }
        
        // 更新ボタン
        if (GUILayout.Button("更新", GUILayout.Height(30)))
        {
            lens.Restore();
            lens.Apply();
            
            // 前回値を更新
            prevEnablePupil = lens.enablePupil;
            prevPupilColor = lens.pupilColor;
            prevPupilAlpha = lens.pupilAlpha;
            prevScaleAdjust = lens.scaleAdjust;
            prevHueShift = lens.hueShift;
            hasUnappliedChanges = false;
        }
        
        // 製作者モード中のみリリースボタン表示
        if (ContactLensCreatorWindow.IsCreating)
        {
            EditorGUILayout.Space(10);
            
            // 目立つボックスで囲む
            var boxStyle = new GUIStyle(EditorStyles.helpBox);
            boxStyle.padding = new RectOffset(10, 10, 10, 10);
            
            EditorGUILayout.BeginVertical(boxStyle);
            
            // 大きめのタイトル
            var titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.fontSize = 14;
            titleStyle.normal.textColor = new Color(0.2f, 0.8f, 0.4f);
            EditorGUILayout.LabelField("★ 製作者モード ★", titleStyle);
            
            EditorGUILayout.Space(5);
            
            // 作者名/作品名/アバター
            string authorName = ContactLensCreatorWindow.AuthorName ?? "";
            string productName = ContactLensCreatorWindow.ProductName ?? "";
            string srcAvatar = ContactLensCreatorWindow.SourceAvatar ?? "";
            var srcAvatarInfo = ContactLensConfig.GetAvatar(srcAvatar);
            string avatarDisplay = srcAvatarInfo?.displayName ?? srcAvatar;
            
            EditorGUILayout.LabelField($"作者: {authorName} / 作品: {productName} / アバター: {avatarDisplay}");
            EditorGUILayout.LabelField($"出力先: {ContactLensCreatorWindow.ProjectPath}", EditorStyles.miniLabel);
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("リリース", GUILayout.Height(30)))
            {
                Release(lens);
            }
            
            if (GUILayout.Button("管理画面", GUILayout.Height(30)))
            {
                ContactLensCreatorWindow.ShowWindow();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
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
