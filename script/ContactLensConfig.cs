#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace com.github.pandrabox.contactlens
{

[System.Serializable]
public class EyeParams
{
    public float cx;
    public float cy;
    public float width;
    public float height;
}

[System.Serializable]
public class IdentificationData
{
    public float[] attributePosX;
    public string[] animatorAvatarName;
    public string[] formalName;
    public int[] blendShapeCount;
}

[System.Serializable]
public class AvatarInfo
{
    public string displayName;
    public string readmeDisplayName;
    public int resolution;
    public string pupilType;
    public EyeParams leftEye;
    public EyeParams rightEye;
    public float scale = 1.0f;
    public IdentificationData identification;
    
    public bool IsIslandType => pupilType == "island";
    
    public string ReadmeDisplayName => !string.IsNullOrEmpty(readmeDisplayName) ? readmeDisplayName : displayName;
}

[System.Serializable]
public class AvatarsConfig
{
    public Dictionary<string, AvatarInfo> avatars;
    public string baseAvatar;
}

public static class ContactLensConfig
{
    static readonly string ConfigPath = "Assets/Pan/ContactLens/config/avatars.json";
    static readonly string MaskFolder = "Assets/Pan/ContactLens/texture/Mask";
    
    const int ATTRIBUTE_INDEX = 26;
    const int FLOOR_DIGITS = 7;
    
    static AvatarsConfig cachedConfig;
    static string[] cachedAvatarNames;
    
    public static AvatarsConfig Config
    {
        get
        {
            if (cachedConfig == null) LoadConfig();
            return cachedConfig;
        }
    }
    
    public static string[] AvatarNames
    {
        get
        {
            if (cachedAvatarNames == null) LoadConfig();
            return cachedAvatarNames;
        }
    }
    
    public static void LoadConfig()
    {
        if (!File.Exists(ConfigPath))
        {
            Debug.LogError($"[ContactLens] Config not found: {ConfigPath}");
            cachedConfig = new AvatarsConfig { avatars = new Dictionary<string, AvatarInfo>(), baseAvatar = "flat12" };
            cachedAvatarNames = new string[0];
            return;
        }
        
        string json = File.ReadAllText(ConfigPath);
        cachedConfig = JsonToConfig(json);
        cachedAvatarNames = new List<string>(cachedConfig.avatars.Keys).ToArray();
    }
    
    static AvatarsConfig JsonToConfig(string json)
    {
        var config = new AvatarsConfig();
        config.avatars = new Dictionary<string, AvatarInfo>();
        
        var wrapper = JsonUtility.FromJson<ConfigWrapper>(json);
        config.baseAvatar = wrapper.baseAvatar;
        
        json = json.Replace("\n", "").Replace("\r", "");
        
        string[] avatarNames = { "flat12", "flat3if", "comodo", "fel", "heon", "kewf" };
        foreach (var name in avatarNames)
        {
            int nameStart = json.IndexOf($"\"{name}\"");
            if (nameStart < 0) continue;
            
            int objStart = json.IndexOf("{", nameStart);
            int depth = 1;
            int objEnd = objStart + 1;
            while (depth > 0 && objEnd < json.Length)
            {
                if (json[objEnd] == '{') depth++;
                else if (json[objEnd] == '}') depth--;
                objEnd++;
            }
            
            string avatarJson = json.Substring(objStart, objEnd - objStart);
            var info = JsonUtility.FromJson<AvatarInfo>(avatarJson);
            if (info.scale == 0) info.scale = 1.0f;
            config.avatars[name] = info;
        }
        
        return config;
    }
    
    [System.Serializable]
    class ConfigWrapper
    {
        public string baseAvatar;
    }
    
    public static AvatarInfo GetAvatar(string name)
    {
        if (Config.avatars.TryGetValue(name, out var info))
            return info;
        return null;
    }
    
    public static string GetMaskPath(string avatarName, string maskType)
    {
        string folderName = avatarName.StartsWith("flat") ? "flat" : avatarName;
        return $"{MaskFolder}/{folderName}_{maskType}.png";
    }
    
    public static Texture2D LoadMask(string avatarName, string maskType)
    {
        string path = GetMaskPath(avatarName, maskType);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    
    public static int GetAvatarIndex(string name)
    {
        for (int i = 0; i < AvatarNames.Length; i++)
        {
            if (AvatarNames[i] == name) return i;
        }
        return 0;
    }
    
    public static void Reload()
    {
        cachedConfig = null;
        cachedAvatarNames = null;
        LoadConfig();
    }
    
    /// <summary>
    /// アバターを自動検出する
    /// </summary>
    public static string DetectAvatar(Transform avatarRoot)
    {
        if (avatarRoot == null) return null;
        
        // 1. AttributePoint判定
        var result = DetectByAttributePoint(avatarRoot);
        if (result != null) return result;
        
        // 2. AnimatorAvatarName判定
        result = DetectByAnimatorName(avatarRoot);
        if (result != null) return result;
        
        // 3. FormalName完全一致判定
        result = DetectByFormalNameExact(avatarRoot);
        if (result != null) return result;
        
        // 4. FormalName部分一致判定
        result = DetectByFormalNamePartial(avatarRoot);
        if (result != null) return result;
        
        return null;
    }
    
    static string DetectByAttributePoint(Transform root)
    {
        var body = root.Find("Body");
        if (body == null) return null;
        
        var smr = body.GetComponent<SkinnedMeshRenderer>();
        if (smr == null || smr.sharedMesh == null || !smr.sharedMesh.isReadable) return null;
        
        var mesh = smr.sharedMesh;
        if (mesh.vertexCount <= ATTRIBUTE_INDEX) return null;
        
        float attrX = FloorValue(mesh.vertices[ATTRIBUTE_INDEX].x);
        int blendShapeCount = mesh.blendShapeCount;
        
        foreach (var kvp in Config.avatars)
        {
            var info = kvp.Value;
            if (info.identification == null || info.identification.attributePosX == null) continue;
            
            for (int i = 0; i < info.identification.attributePosX.Length; i++)
            {
                if (attrX != FloorValue(info.identification.attributePosX[i])) continue;
                
                // BlendShapeCountチェックが必要な場合
                if (info.identification.blendShapeCount != null && 
                    i < info.identification.blendShapeCount.Length &&
                    info.identification.blendShapeCount[i] > 0)
                {
                    if (blendShapeCount == info.identification.blendShapeCount[i])
                        return kvp.Key;
                    continue;
                }
                
                return kvp.Key;
            }
        }
        
        return null;
    }
    
    static string DetectByAnimatorName(Transform root)
    {
        var animator = root.GetComponent<Animator>();
        if (animator == null || animator.avatar == null) return null;
        
        string avatarName = animator.avatar.name;
        
        foreach (var kvp in Config.avatars)
        {
            var info = kvp.Value;
            if (info.identification == null || info.identification.animatorAvatarName == null) continue;
            
            if (info.identification.animatorAvatarName.Contains(avatarName))
                return kvp.Key;
        }
        
        return null;
    }
    
    static string DetectByFormalNameExact(Transform root)
    {
        string objName = root.gameObject.name;
        
        foreach (var kvp in Config.avatars)
        {
            var info = kvp.Value;
            if (info.identification == null || info.identification.formalName == null) continue;
            
            if (info.identification.formalName.Contains(objName))
                return kvp.Key;
        }
        
        return null;
    }
    
    static string DetectByFormalNamePartial(Transform root)
    {
        string objName = root.gameObject.name;
        
        foreach (var kvp in Config.avatars)
        {
            var info = kvp.Value;
            if (info.identification == null || info.identification.formalName == null) continue;
            
            foreach (var formalName in info.identification.formalName)
            {
                if (objName.Contains(formalName))
                    return kvp.Key;
            }
        }
        
        return null;
    }
    
    static float FloorValue(float value)
    {
        return Mathf.Floor(value * Mathf.Pow(10, FLOOR_DIGITS));
    }
}

}
#endif
