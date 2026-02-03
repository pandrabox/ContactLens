#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

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
        
        if (GUI.changed)
        {
            EditorUtility.SetDirty(lens);
        }
    }
}

}
#endif
