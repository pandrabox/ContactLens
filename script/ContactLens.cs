#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace com.github.pandrabox.contactlens
{

[ExecuteInEditMode]
public class ContactLens : MonoBehaviour, VRC.SDKBase.IEditorOnly
{
    [Header("レンズ設定")]
    public Texture2D lensTexture;
    
    [Header("アバター設定")]
    public string targetAvatar = "flat12";
    public string sourceAvatar = "flat12";
    
    [Header("瞳孔設定")]
    public bool enablePupil = true;
    public Color pupilColor = Color.white;
    [Range(0f, 1f)]
    public float pupilAlpha = 1f;
    
    [Header("上級者設定")]
    public bool showAdvancedSettings = false;
    [Range(0.65f, 1.35f)]
    public float scaleAdjust = 1.0f;
    
    [Range(0f, 1f)]
    public float hueShift = 0f;
    
    static readonly Vector3 CollapsePosition = new Vector3(0f, 0f, 1.5f);
    
    [HideInInspector] public string originalMaterialGUID;
    [HideInInspector] public string generatedMaterialPath;
    [HideInInspector] public string generatedMainTexPath;
    [HideInInspector] public string generatedEmissionTexPath;
    [HideInInspector] public string appliedAvatarPath;
    [HideInInspector] public Mesh originalMesh;
    [HideInInspector] public Mesh modifiedMesh;
    [HideInInspector] public string generatedMeshPath;
    
    static string GeneratedFolder => "Assets/Pan/ContactLens/Generated";
    static List<string> pendingDeletes = new List<string>();
    
    public static bool IsVRCBuilding { get; set; } = false;
    
    bool NeedsPupilHide
    {
        get
        {
            var avatarInfo = ContactLensConfig.GetAvatar(targetAvatar);
            if (avatarInfo == null) return false;
            return avatarInfo.IsIslandType && (!enablePupil || pupilAlpha < 1f);
        }
    }
    
    void OnEnable()
    {
        if (!Application.isPlaying)
        {
            CleanupInvalidState();
            EditorApplication.update += WaitAndApply;
        }
    }
    
    void CleanupInvalidState()
    {
        if (!string.IsNullOrEmpty(generatedMaterialPath))
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(generatedMaterialPath);
            if (mat == null)
            {
                ClearState();
            }
        }
    }
    
    int waitFrames = 3;
    void WaitAndApply()
    {
        waitFrames--;
        if (waitFrames > 0) return;
        
        EditorApplication.update -= WaitAndApply;
        waitFrames = 3;
        
        if (this == null) return;
        if (lensTexture == null) return;
        if (!string.IsNullOrEmpty(generatedMaterialPath)) return;
        
        Apply();
    }
    
    void OnDestroy()
    {
        if (!Application.isPlaying)
        {
            if (BuildPipeline.isBuildingPlayer || IsVRCBuilding)
            {
                Debug.Log("[ContactLens] Skipping restore - building");
                return;
            }
            
            EditorApplication.update -= WaitAndApply;
            RestoreAndScheduleDelete();
        }
    }
    
    void OnDisable()
    {
        EditorApplication.update -= WaitAndApply;
    }
    
    void OnTransformParentChanged()
    {
        if (!Application.isPlaying)
        {
            RestoreOriginalAvatar();
            
            EditorApplication.delayCall += () => {
                if (this != null && lensTexture != null)
                {
                    Apply();
                }
            };
        }
    }
    
    void RestoreRenderer(SkinnedMeshRenderer smr)
    {
        if (smr == null) return;
        
        if (!string.IsNullOrEmpty(originalMaterialGUID))
        {
            var path = AssetDatabase.GUIDToAssetPath(originalMaterialGUID);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                smr.sharedMaterial = mat;
            }
        }
        if (originalMesh != null)
        {
            smr.sharedMesh = originalMesh;
        }
    }
    
    void ScheduleDeleteGeneratedAssets()
    {
        ScheduleDelete(generatedMaterialPath);
        ScheduleDelete(generatedMainTexPath);
        ScheduleDelete(generatedEmissionTexPath);
        ScheduleDelete(generatedMeshPath);
    }
    
    void ClearState()
    {
        originalMaterialGUID = "";
        generatedMaterialPath = "";
        generatedMainTexPath = "";
        generatedEmissionTexPath = "";
        generatedMeshPath = "";
        appliedAvatarPath = "";
        originalMesh = null;
        modifiedMesh = null;
    }
    
    void RestoreOriginalAvatar()
    {
        if (!string.IsNullOrEmpty(appliedAvatarPath))
        {
            var avatar = GameObject.Find(appliedAvatarPath);
            if (avatar != null)
            {
                var body = avatar.transform.Find("Body");
                if (body != null)
                {
                    RestoreRenderer(body.GetComponent<SkinnedMeshRenderer>());
                }
            }
        }
        ScheduleDeleteGeneratedAssets();
        ClearState();
    }
    
    void RestoreAndScheduleDelete()
    {
        RestoreRenderer(GetBodyRenderer());
        ScheduleDeleteGeneratedAssets();
        ClearState();
        Debug.Log("[ContactLens] 除去完了");
    }
    
    static void ScheduleDelete(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        
        pendingDeletes.Add(path);
        EditorApplication.delayCall -= ProcessPendingDeletes;
        EditorApplication.delayCall += ProcessPendingDeletes;
    }
    
    static void ProcessPendingDeletes()
    {
        foreach (var path in pendingDeletes)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
        pendingDeletes.Clear();
    }
    
    SkinnedMeshRenderer GetBodyRenderer()
    {
        var avatar = transform.parent;
        if (avatar == null) return null;
        
        var body = avatar.Find("Body");
        if (body == null) return null;
        
        return body.GetComponent<SkinnedMeshRenderer>();
    }
    
    string GetAvatarPath()
    {
        if (transform.parent == null) return "";
        return transform.parent.name;
    }
    
    public void Apply()
    {
        var smr = GetBodyRenderer();
        if (smr == null || lensTexture == null) return;
        
        if (!string.IsNullOrEmpty(generatedMaterialPath))
        {
            Restore();
        }
        
        var originalMat = smr.sharedMaterial;
        if (originalMat == null) return;
        
        EnsureFolder(GeneratedFolder);
        
        originalMaterialGUID = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(originalMat));
        appliedAvatarPath = GetAvatarPath();
        originalMesh = smr.sharedMesh;
        
        var newMat = new Material(originalMat);
        string avatarName = transform.parent.name;
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        generatedMaterialPath = $"{GeneratedFolder}/{avatarName}_lens_{timestamp}.mat";
        AssetDatabase.CreateAsset(newMat, generatedMaterialPath);
        
        var mainTex = originalMat.GetTexture("_MainTex") as Texture2D;
        if (mainTex != null)
        {
            var combined = CombineAll(mainTex);
            generatedMainTexPath = SaveTexture(combined, $"{avatarName}_main_{timestamp}");
            newMat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(generatedMainTexPath));
        }
        
        var emissionTex = originalMat.GetTexture("_EmissionMap") as Texture2D;
        if (emissionTex != null)
        {
            var combined = CombineAll(emissionTex);
            generatedEmissionTexPath = SaveTexture(combined, $"{avatarName}_emission_{timestamp}");
            newMat.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(generatedEmissionTexPath));
        }
        
        smr.sharedMaterial = newMat;
        
        if (NeedsPupilHide)
        {
            ApplyPupilHide(smr);
        }
        
        EditorUtility.SetDirty(newMat);
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        
        Debug.Log($"[ContactLens] 適用完了: {transform.parent.name} ({sourceAvatar} -> {targetAvatar})");
    }
    
    void ApplyPupilHide(SkinnedMeshRenderer smr)
    {
        if (originalMesh == null) return;
        
        modifiedMesh = Instantiate(originalMesh);
        modifiedMesh.name = originalMesh.name + "_pupilHidden";
        
        var pupilVertices = FindPupilVertices(modifiedMesh);
        
        if (pupilVertices.Count == 0)
        {
            Debug.LogWarning("[ContactLens] 瞳孔アイランドが見つかりませんでした");
            DestroyImmediate(modifiedMesh);
            modifiedMesh = null;
            return;
        }
        
        Vector3[] verts = modifiedMesh.vertices;
        foreach (int idx in pupilVertices)
        {
            verts[idx] = CollapsePosition;
        }
        modifiedMesh.vertices = verts;
        modifiedMesh.RecalculateBounds();
        
        string avatarName = transform.parent.name;
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        generatedMeshPath = $"{GeneratedFolder}/{avatarName}_mesh_{timestamp}.asset";
        AssetDatabase.CreateAsset(modifiedMesh, generatedMeshPath);
        
        smr.sharedMesh = modifiedMesh;
        
        Debug.Log($"[ContactLens] 瞳孔Hide適用: {pupilVertices.Count}頂点を移動");
    }
    
    List<int> FindPupilVertices(Mesh mesh)
    {
        var pupilIslandMask = ContactLensConfig.LoadMask(targetAvatar, "pupilIsland");
        if (pupilIslandMask == null)
        {
            Debug.LogWarning($"[ContactLens] pupilIslandマスクが見つかりません: {targetAvatar}");
            return new List<int>();
        }
        
        var maskReadable = GetReadable(pupilIslandMask);
        var maskPixels = maskReadable.GetPixels();
        int maskWidth = maskReadable.width;
        int maskHeight = maskReadable.height;
        
        int n = mesh.vertexCount;
        int[] p = new int[n];
        for (int i = 0; i < n; i++) p[i] = i;
        
        int[] tris = mesh.triangles;
        for (int i = 0; i < tris.Length; i += 3)
        {
            int a = tris[i], b = tris[i + 1], c = tris[i + 2];
            int ra = a; while (p[ra] != ra) ra = p[ra];
            int rb = b; while (p[rb] != rb) rb = p[rb];
            int rc = c; while (p[rc] != rc) rc = p[rc];
            p[ra] = rc;
            p[rb] = rc;
        }
        
        for (int i = 0; i < n; i++)
        {
            int r = i;
            while (p[r] != r) r = p[r];
            p[i] = r;
        }
        
        var dict = new Dictionary<int, List<int>>();
        for (int i = 0; i < n; i++)
        {
            if (!dict.ContainsKey(p[i])) dict[p[i]] = new List<int>();
            dict[p[i]].Add(i);
        }
        
        Vector2[] uvs = mesh.uv;
        var result = new List<int>();
        
        foreach (var kv in dict)
        {
            float totalMask = 0f;
            int sampleCount = 0;
            
            foreach (int idx in kv.Value)
            {
                if (idx >= uvs.Length) continue;
                Vector2 uv = uvs[idx];
                
                int px = Mathf.Clamp((int)(uv.x * maskWidth), 0, maskWidth - 1);
                int py = Mathf.Clamp((int)(uv.y * maskHeight), 0, maskHeight - 1);
                
                totalMask += maskPixels[py * maskWidth + px].grayscale;
                sampleCount++;
            }
            
            if (sampleCount > 0 && totalMask / sampleCount > 0.5f)
            {
                result.AddRange(kv.Value);
            }
        }
        
        return result;
    }
    
    Texture2D TransformLensTexture(Texture2D lens, int targetResolution)
    {
        var srcInfo = ContactLensConfig.GetAvatar(sourceAvatar);
        var dstInfo = ContactLensConfig.GetAvatar(targetAvatar);
        
        if (srcInfo == null || dstInfo == null)
        {
            Debug.LogWarning("[ContactLens] アバター情報が見つかりません");
            return lens;
        }
        
        // 同じアバターなら変換不要（scaleAdjustのみ適用）
        bool sameAvatar = sourceAvatar == targetAvatar || 
            (sourceAvatar.StartsWith("flat") && targetAvatar.StartsWith("flat"));
        
        if (sameAvatar)
        {
            if (lens.width != targetResolution || lens.height != targetResolution)
            {
                return Resize(lens, targetResolution, targetResolution);
            }
            return lens;
        }
        
        var srcReadable = GetReadable(lens);
        int srcRes = srcReadable.width;
        
        var result = new Texture2D(targetResolution, targetResolution, TextureFormat.RGBA32, false);
        var resultPixels = new Color[targetResolution * targetResolution];
        
        for (int i = 0; i < resultPixels.Length; i++)
        {
            resultPixels[i] = new Color(0, 0, 0, 0);
        }
        
        // 相対スケール: dst / src * ユーザー調整
        float totalScale = (dstInfo.scale / srcInfo.scale) * scaleAdjust;
        
        TransformEye(srcReadable, srcInfo.leftEye, dstInfo.leftEye, srcRes, targetResolution, resultPixels, totalScale);
        TransformEye(srcReadable, srcInfo.rightEye, dstInfo.rightEye, srcRes, targetResolution, resultPixels, totalScale);
        
        result.SetPixels(resultPixels);
        result.Apply();
        return result;
    }
    
    void TransformEye(Texture2D src, EyeParams srcEye, EyeParams dstEye, int srcRes, int dstRes, Color[] resultPixels, float scale)
    {
        // UV座標系: (0,0)が左下、(1,1)が右上
        // ピクセル座標系（GetPixels/SetPixels）: (0,0)が左下、(width,height)が右上
        
        // ソース領域（ピクセル座標、左下原点）
        int srcCx = (int)(srcEye.cx * srcRes);
        int srcCy = (int)(srcEye.cy * srcRes);  // UV.yをそのままピクセルYに
        int srcW = (int)(srcEye.width * srcRes);
        int srcH = (int)(srcEye.height * srcRes);
        
        int srcX1 = Mathf.Max(0, srcCx - srcW / 2);
        int srcY1 = Mathf.Max(0, srcCy - srcH / 2);
        int srcX2 = Mathf.Min(srcRes, srcX1 + srcW);
        int srcY2 = Mathf.Min(srcRes, srcY1 + srcH);
        
        // デスト領域（scaleを適用）
        int dstCx = (int)(dstEye.cx * dstRes);
        int dstCy = (int)(dstEye.cy * dstRes);
        int dstW = (int)(dstEye.width * dstRes * scale);
        int dstH = (int)(dstEye.height * dstRes * scale);
        
        int dstX1 = Mathf.Max(0, dstCx - dstW / 2);
        int dstY1 = Mathf.Max(0, dstCy - dstH / 2);
        
        // 切り出し
        int cropW = srcX2 - srcX1;
        int cropH = srcY2 - srcY1;
        if (cropW <= 0 || cropH <= 0) return;
        
        // GetPixelsは左下原点
        var srcPixels = src.GetPixels(srcX1, srcY1, cropW, cropH);
        
        var eyeTex = new Texture2D(cropW, cropH, TextureFormat.RGBA32, false);
        eyeTex.SetPixels(srcPixels);
        eyeTex.Apply();
        
        var resized = Resize(eyeTex, dstW, dstH);
        var resizedPixels = resized.GetPixels();
        
        // 書き込み（左下原点）
        for (int y = 0; y < dstH; y++)
        {
            for (int x = 0; x < dstW; x++)
            {
                int dx = dstX1 + x;
                int dy = dstY1 + y;
                if (dx < 0 || dx >= dstRes || dy < 0 || dy >= dstRes) continue;
                
                int srcIdx = y * dstW + x;
                int dstIdx = dy * dstRes + dx;
                
                if (srcIdx < resizedPixels.Length && dstIdx < resultPixels.Length)
                {
                    resultPixels[dstIdx] = resizedPixels[srcIdx];
                }
            }
        }
        
        DestroyImmediate(eyeTex);
        DestroyImmediate(resized);
    }
    
    Texture2D CombineAll(Texture2D baseTex)
    {
        var bgReadable = GetReadable(baseTex);
        int targetRes = bgReadable.width;
        
        var lensTransformed = TransformLensTexture(GetReadable(lensTexture), targetRes);
        
        var eyeAreaMask = ContactLensConfig.LoadMask(targetAvatar, "eyeArea");
        var pupilTextureMask = ContactLensConfig.LoadMask(targetAvatar, "pupilTexture");
        var pupilIslandMask = ContactLensConfig.LoadMask(targetAvatar, "pupilIsland");
        
        Texture2D eyeAreaReadable = null;
        if (eyeAreaMask != null)
        {
            eyeAreaReadable = GetReadable(eyeAreaMask);
            if (eyeAreaReadable.width != targetRes)
                eyeAreaReadable = Resize(eyeAreaReadable, targetRes, targetRes);
        }
        
        Texture2D pupilTextureReadable = null;
        if (pupilTextureMask != null)
        {
            pupilTextureReadable = GetReadable(pupilTextureMask);
            if (pupilTextureReadable.width != targetRes)
                pupilTextureReadable = Resize(pupilTextureReadable, targetRes, targetRes);
        }
        
        Texture2D pupilIslandReadable = null;
        if (pupilIslandMask != null)
        {
            pupilIslandReadable = GetReadable(pupilIslandMask);
            if (pupilIslandReadable.width != targetRes)
                pupilIslandReadable = Resize(pupilIslandReadable, targetRes, targetRes);
        }
        
        var avatarInfo = ContactLensConfig.GetAvatar(targetAvatar);
        bool isIslandType = avatarInfo?.IsIslandType ?? false;
        
        var result = new Texture2D(targetRes, targetRes, TextureFormat.RGBA32, false);
        
        var bgPixels = bgReadable.GetPixels();
        var lensPixels = lensTransformed.GetPixels();
        
        // 色相シフト適用
        if (hueShift > 0.001f)
        {
            for (int i = 0; i < lensPixels.Length; i++)
            {
                if (lensPixels[i].a > 0.001f)
                {
                    float h, s, v;
                    Color.RGBToHSV(lensPixels[i], out h, out s, out v);
                    h = (h + hueShift) % 1f;
                    Color shifted = Color.HSVToRGB(h, s, v);
                    shifted.a = lensPixels[i].a;
                    lensPixels[i] = shifted;
                }
            }
        }
        var eyeAreaPixels = eyeAreaReadable?.GetPixels();
        var pupilTexturePixels = pupilTextureReadable?.GetPixels();
        var pupilIslandPixels = pupilIslandReadable?.GetPixels();
        var resultPixels = new Color[bgPixels.Length];
        
        for (int i = 0; i < bgPixels.Length; i++)
        {
            Color bgC = bgPixels[i];
            Color lensC = lensPixels[i];
            Color current = bgC;
            
            float eyeAreaValue = eyeAreaPixels != null ? eyeAreaPixels[i].grayscale : 1f;
            float pupilTextureValue = pupilTexturePixels != null ? pupilTexturePixels[i].grayscale : 0f;
            float pupilIslandValue = pupilIslandPixels != null ? pupilIslandPixels[i].grayscale : 0f;
            
            float lensBlend = lensC.a * eyeAreaValue;
            current = Color.Lerp(current, lensC, lensBlend);
            
            if (!isIslandType)
            {
                if (enablePupil && pupilTexturePixels != null)
                {
                    float pupilBlend = pupilTextureValue * pupilAlpha;
                    current = Color.Lerp(current, pupilColor, pupilBlend);
                }
            }
            else
            {
                if (enablePupil)
                {
                    if (pupilAlpha < 1f)
                    {
                        if (pupilTexturePixels != null)
                        {
                            float pupilBlend = pupilTextureValue * pupilAlpha;
                            current = Color.Lerp(current, pupilColor, pupilBlend);
                        }
                    }
                    else
                    {
                        if (pupilIslandPixels != null)
                        {
                            float pupilBlend = pupilIslandValue * pupilAlpha;
                            current = Color.Lerp(current, pupilColor, pupilBlend);
                        }
                    }
                }
            }
            
            current.a = bgC.a;
            resultPixels[i] = current;
        }
        
        result.SetPixels(resultPixels);
        result.Apply();
        return result;
    }
    
    public void Restore()
    {
        RestoreRenderer(GetBodyRenderer());
        ScheduleDeleteGeneratedAssets();
        ClearState();
        Debug.Log("[ContactLens] 除去完了");
    }
    
    Texture2D GetReadable(Texture2D source)
    {
        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height);
        Graphics.Blit(source, rt);
        RenderTexture.active = rt;
        
        var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        readable.Apply();
        
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        return readable;
    }
    
    Texture2D Resize(Texture2D source, int width, int height)
    {
        RenderTexture rt = RenderTexture.GetTemporary(width, height);
        Graphics.Blit(source, rt);
        RenderTexture.active = rt;
        
        var resized = new Texture2D(width, height, TextureFormat.RGBA32, false);
        resized.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        resized.Apply();
        
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        return resized;
    }
    
    string SaveTexture(Texture2D tex, string name)
    {
        var path = $"{GeneratedFolder}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.Refresh();
        
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.streamingMipmaps = true;
            importer.SaveAndReimport();
        }
        
        return path;
    }
    
    void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}

}
#endif
