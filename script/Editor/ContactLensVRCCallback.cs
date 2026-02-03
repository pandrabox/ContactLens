#if UNITY_EDITOR
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace com.github.pandrabox.contactlens
{

public class ContactLensVRCCallback : IVRCSDKPreprocessAvatarCallback, IVRCSDKPostprocessAvatarCallback
{
    public int callbackOrder => -10000; // 最初に実行
    
    public bool OnPreprocessAvatar(GameObject avatarGameObject)
    {
        ContactLens.IsVRCBuilding = true;
        Debug.Log("[ContactLens] VRC Build started");
        return true;
    }
    
    public void OnPostprocessAvatar()
    {
        ContactLens.IsVRCBuilding = false;
        Debug.Log("[ContactLens] VRC Build finished");
    }
}

}
#endif
