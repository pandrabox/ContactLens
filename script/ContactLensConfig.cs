#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

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
public class AvatarInfo
{
    public string displayName;
    public int resolution;
    public string pupilType;
    public EyeParams leftEye;
    public EyeParams rightEye;
    public float scale = 1.0f;
    
    public bool IsIslandType => pupilType == "island";
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
}

}
#endif
