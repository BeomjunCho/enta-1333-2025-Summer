using UnityEngine;

/// <summary>
/// Generic class for Singleton
/// </summary>
/// <typeparam name="T"></typeparam>
public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T instance;
    private static bool _isQuitting;
    public static T Instance
    {
        get
        {
            if (_isQuitting) return null;
            if (instance == null)
            {
                instance = (T)FindAnyObjectByType(typeof(T));

                if (instance == null)
                {
                    GameObject obj = new GameObject(typeof(T).Name, typeof(T));
                    instance = obj.GetComponent<T>();
                }
            }
            return instance;
        }
    }
    private void Awake()
    {
        if (transform.parent != null && transform.root != null)
        {
            DontDestroyOnLoad(this.transform.root.gameObject);
        }
        else
        {
            DontDestroyOnLoad(this.gameObject);
        }
    }
    private void OnApplicationQuit() => _isQuitting = true;
}


