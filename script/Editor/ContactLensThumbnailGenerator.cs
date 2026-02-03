#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace com.github.pandrabox.contactlens
{

public static class ContactLensThumbnailGenerator
{
    const int ThumbnailSize = 512;
    static readonly Vector3 CameraLocalPosition = new Vector3(0f, -1.04f, 1.52f);
    static readonly Vector3 CameraLocalRotation = new Vector3(270f, 180f, 0f);
    const float CameraOrthographicSize = 0.13f;
    
    public static string Generate(Transform bodyTransform, string savePath)
    {
        if (bodyTransform == null) return null;
        
        // カメラ生成
        var camObj = new GameObject("_TmbCamera_Temp");
        camObj.transform.SetParent(bodyTransform);
        camObj.transform.localPosition = CameraLocalPosition;
        camObj.transform.localRotation = Quaternion.Euler(CameraLocalRotation);
        camObj.transform.localScale = Vector3.one;
        
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
