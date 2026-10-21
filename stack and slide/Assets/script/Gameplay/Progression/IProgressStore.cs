/// <summary>
/// Persistence boundary for non-secret player progress. The domain never depends on
/// PlayerPrefs, files, scene objects, or a particular platform's storage implementation.
/// </summary>
public interface IProgressStore
{
    string Load();
    void Save(string serializedProgress);
}
