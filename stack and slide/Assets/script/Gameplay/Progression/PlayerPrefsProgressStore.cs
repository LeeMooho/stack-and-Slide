using UnityEngine;

/// <summary>
/// Keeps local progress behind the domain's storage boundary. The domain validates
/// the versioned payload; this adapter only handles Unity's device-local persistence.
/// It is not a platform achievement service or a cloud save implementation.
/// </summary>
public sealed class PlayerPrefsProgressStore : IProgressStore
{
    public const string StorageKey = "StackAndSlide.Progress.v1";

    public string Load()
    {
        return PlayerPrefs.GetString(StorageKey, "");
    }

    public void Save(string payload)
    {
        PlayerPrefs.SetString(StorageKey, payload);
        // Flush on milestones rather than depending on OnApplicationQuit on mobile.
        PlayerPrefs.Save();
    }
}
