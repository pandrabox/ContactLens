#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace com.github.pandrabox.contactlens
{

public static class ContactLensThumbnailGenerator
{
    const int ThumbnailSize = 512;
    const float CameraOrthographicSize = 0.13f;
    const float CameraDistance = 0.5f;
    
    public static string Generate(Transform bodyTransform, string savePath)
    {
        if (bodyTransform == null) return null;
        
        // Headボーンを探す
        Transform head = FindBone(bodyTransform, "Head");
        if (head == null)
        {
            // Headが見つからない場合はArmatureから探す
            var armature = bodyTransform.parent?.Find("Armature");
            if (armature != null)
            {
                head = FindBoneRecursive(armature, "Head");
            }
        }
        
        Vector3 targetPosition;
        if (head != null)
        {
            // Headの位置を基準に、目の高さに合わせる
            targetPosition = head.position + Vector3.up * 0.13f;
        }
        else
        {
            // フォールバック：Bodyの位置から推定
            targetPosition = bodyTransform.position + Vector3.up * 1.5f;
        }
        
        // カメラ生成
        var camObj = new GameObject("_TmbCamera_Temp");
        camObj.transform.position = targetPosition + Vector3.forward * CameraDistance;
        camObj.transform.LookAt(targetPosition);
        
        var cam = camObj.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = CameraOrthographicSize;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 1000f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.cullingMask = -1;
        
        // RenderTexture
        var rt = new RenderTexture(ThumbnailSize, ThumbnailSize, 24, RenderTextureFormat.ARGB32);
        rt.Create();
        
        cam.targetTexture = rt;
        cam.Render();
        
        // Texture2D
        RenderTexture.active = rt;
        var tex = new Texture2D(ThumbnailSize, ThumbnailSize, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, ThumbnailSize, ThumbnailSize), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        
        // 保存
        var dir = Path.GetDirectoryName(savePath);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllBytes(savePath, tex.EncodeToPNG());
        
        // クリーンアップ
        cam.targetTexture = null;
        rt.Release();
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(camObj);
        
        AssetDatabase.Refresh();
        ContactLensThumbnail.ClearCache();
        
        Debug.Log($"[ContactLens] サムネイル生成: {savePath}");
        return savePath;
    }
    
    static Transform FindBone(Transform body, string boneName)
    {
        // 直接の子から探す
        foreach (Transform child in body)
        {
            if (child.name.Contains(boneName))
                return child;
        }
        return null;
    }
    
    static Transform FindBoneRecursive(Transform parent, string boneName)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Contains(boneName))
                return child;
            
            var found = FindBoneRecursive(child, boneName);
            if (found != null)
                return found;
        }
        return null;
    }
    
    public static string GenerateForLens(ContactLens lens)
    {
        if (lens == null || lens.transform.parent == null) return null;
        
        var body = lens.transform.parent.Find("Body");
        if (body == null) return null;
        
        var lensName = lens.gameObject.name;
        var savePath = $"Assets/Pan/ContactLens/Generated/{lensName}_thumb.png";
        
        return Generate(body, savePath);
    }
}

}
#endif
