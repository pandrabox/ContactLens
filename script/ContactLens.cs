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
    
    [Header("アバターモード")]
    public AvatarMode avatarMode = AvatarMode.Ver12;
    
    [Header("瞳孔設定")]
    public bool enablePupil = true;
    public Color pupilColor = Color.white;
    [Range(0f, 1f)]
    public float pupilAlpha = 1f;
    
    // 瞳孔Hide設定 (Ver3&If用) - 内部固定値
    const int PupilVertexCount = 100;
    static readonly Vector2 PupilYRange = new Vector2(-0.19f, -0.175f);
    static readonly Vector3 CollapsePosition = new Vector3(0f, 0f, 1.5f);
    
    [Header("マスク設定")]
    public bool showMaskSettings = false;
    public Texture2D pupilMask12;
    public Texture2D pupilMask3If;
    public Texture2D eyeMask;
    
    public enum AvatarMode
    {
        Ver12,
        Ver3If
    }
    
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
    
    // VRChatビルド中フラグ
    public static bool IsVRCBuilding { get; set; } = false;
    
    // Ver3&Ifで瞳孔Hideが必要かどうか
    bool NeedsPupilHide => avatarMode == AvatarMode.Ver3If && (!enablePupil || pupilAlpha < 1f);
    
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
                originalMaterialGUID = "";
                generatedMaterialPath = "";
                generatedMainTexPath = "";
                generatedEmissionTexPath = "";
                appliedAvatarPath = "";
                originalMesh = null;
                modifiedMesh = null;
                generatedMeshPath = "";
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
            // ビルド中は何もしない
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
        
        // メッシュ保存（瞳孔Hide用）
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
        
        // 瞳孔Hide処理
        if (NeedsPupilHide)
        {
            ApplyPupilHide(smr);
        }
        
        EditorUtility.SetDirty(newMat);
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        
        Debug.Log($"[ContactLens] 適用完了: {transform.parent.name}");
    }
    
    void ApplyPupilHide(SkinnedMeshRenderer smr)
    {
        if (originalMesh == null) return;
        
        // メッシュ複製
        modifiedMesh = Instantiate(originalMesh);
        modifiedMesh.name = originalMesh.name + "_pupilHidden";
        
        // アイランド検出
        var pupilVertices = FindPupilVertices(modifiedMesh);
        
        if (pupilVertices.Count == 0)
        {
            Debug.LogWarning("[ContactLens] 瞳孔アイランドが見つかりませんでした");
            DestroyImmediate(modifiedMesh);
            modifiedMesh = null;
            return;
        }
        
        // 頂点を潰す
        Vector3[] verts = modifiedMesh.vertices;
        foreach (int idx in pupilVertices)
        {
            verts[idx] = CollapsePosition;
        }
        modifiedMesh.vertices = verts;
        modifiedMesh.RecalculateBounds();
        
        // アセットとして保存
        string avatarName = transform.parent.name;
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        generatedMeshPath = $"{GeneratedFolder}/{avatarName}_mesh_{timestamp}.asset";
        AssetDatabase.CreateAsset(modifiedMesh, generatedMeshPath);
        
        smr.sharedMesh = modifiedMesh;
        
        Debug.Log($"[ContactLens] 瞳孔Hide適用: {pupilVertices.Count}頂点を移動");
    }
    
    List<int> FindPupilVertices(Mesh mesh)
    {
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
        
        Vector3[] verts = mesh.vertices;
        var result = new List<int>();
        
        foreach (var kv in dict)
        {
            if (kv.Value.Count != PupilVertexCount) continue;
            
            // 中心座標計算
            Vector3 center = Vector3.zero;
            foreach (int idx in kv.Value)
            {
                center += verts[idx];
            }
            center /= PupilVertexCount;
            
            // Y座標でフィルタ
            if (center.y >= PupilYRange.x && center.y <= PupilYRange.y)
            {
                result.AddRange(kv.Value);
            }
        }
        
        return result;
    }
    
    Texture2D CombineAll(Texture2D baseTex)
    {
        var bgReadable = GetReadable(baseTex);
        var lensReadable = GetReadable(lensTexture);
        
        if (lensReadable.width != bgReadable.width || lensReadable.height != bgReadable.height)
        {
            lensReadable = Resize(lensReadable, bgReadable.width, bgReadable.height);
        }
        
        Texture2D eyeMaskReadable = null;
        if (eyeMask != null)
        {
            eyeMaskReadable = GetReadable(eyeMask);
            if (eyeMaskReadable.width != bgReadable.width || eyeMaskReadable.height != bgReadable.height)
            {
                eyeMaskReadable = Resize(eyeMaskReadable, bgReadable.width, bgReadable.height);
            }
        }
        
        Texture2D pupilMask12Readable = null;
        if (pupilMask12 != null)
        {
            pupilMask12Readable = GetReadable(pupilMask12);
            if (pupilMask12Readable.width != bgReadable.width || pupilMask12Readable.height != bgReadable.height)
            {
                pupilMask12Readable = Resize(pupilMask12Readable, bgReadable.width, bgReadable.height);
            }
        }
        
        Texture2D pupilMask3IfReadable = null;
        if (pupilMask3If != null)
        {
            pupilMask3IfReadable = GetReadable(pupilMask3If);
            if (pupilMask3IfReadable.width != bgReadable.width || pupilMask3IfReadable.height != bgReadable.height)
            {
                pupilMask3IfReadable = Resize(pupilMask3IfReadable, bgReadable.width, bgReadable.height);
            }
        }
        
        var result = new Texture2D(bgReadable.width, bgReadable.height, TextureFormat.RGBA32, false);
        
        var bgPixels = bgReadable.GetPixels();
        var lensPixels = lensReadable.GetPixels();
        var eyeMaskPixels = eyeMaskReadable?.GetPixels();
        var pupilMask12Pixels = pupilMask12Readable?.GetPixels();
        var pupilMask3IfPixels = pupilMask3IfReadable?.GetPixels();
        var resultPixels = new Color[bgPixels.Length];
        
        for (int i = 0; i < bgPixels.Length; i++)
        {
            Color bgC = bgPixels[i];
            Color lensC = lensPixels[i];
            Color current = bgC;
            
            float eyeMaskValue = eyeMaskPixels != null ? eyeMaskPixels[i].grayscale : 1f;
            float pupilMask12Value = pupilMask12Pixels != null ? pupilMask12Pixels[i].grayscale : 0f;
            float pupilMask3IfValue = pupilMask3IfPixels != null ? pupilMask3IfPixels[i].grayscale : 0f;
            
            // レンズ合成（EyeMaskで制限）
            float lensBlend = lensC.a * eyeMaskValue;
            current = Color.Lerp(current, lensC, lensBlend);
            
            // 瞳孔処理（テクスチャベース）
            if (avatarMode == AvatarMode.Ver12)
            {
                // Ver1&2: pupilMask12で描画
                if (enablePupil && pupilMask12Pixels != null)
                {
                    float pupilBlend = pupilMask12Value * pupilAlpha;
                    current = Color.Lerp(current, pupilColor, pupilBlend);
                }
            }
            else if (avatarMode == AvatarMode.Ver3If)
            {
                if (enablePupil)
                {
                    if (pupilAlpha < 1f)
                    {
                        // Ver3&If + α<1: 元メッシュHide + pupilMask12で描画（Ver1&2方式）
                        if (pupilMask12Pixels != null)
                        {
                            float pupilBlend = pupilMask12Value * pupilAlpha;
                            current = Color.Lerp(current, pupilColor, pupilBlend);
                        }
                    }
                    else
                    {
                        // Ver3&If + α=1: 元メッシュ色替え（pupilMask3If）
                        if (pupilMask3IfPixels != null)
                        {
                            float pupilBlend = pupilMask3IfValue * pupilAlpha;
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
        var smr = GetBodyRenderer();
        
        if (smr != null)
        {
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
        
        ScheduleDelete(generatedMaterialPath);
        ScheduleDelete(generatedMainTexPath);
        ScheduleDelete(generatedEmissionTexPath);
        CleanupModifiedMesh();
        
        originalMaterialGUID = "";
        generatedMeshPath = "";
        generatedMaterialPath = "";
        generatedMainTexPath = "";
        generatedEmissionTexPath = "";
        appliedAvatarPath = "";
        originalMesh = null;
        
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
        
        // Streaming Mip Maps を有効化
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
