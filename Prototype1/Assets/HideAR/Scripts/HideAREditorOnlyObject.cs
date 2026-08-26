using UnityEngine;

namespace HideAR
{
    public class HideAREditorOnlyObject : MonoBehaviour
    {
        void Awake()
        {
#if !UNITY_EDITOR
            Destroy(gameObject);
#endif
        }
    }
}
