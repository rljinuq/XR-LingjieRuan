using UnityEngine;

namespace HideAR.Quest
{
    public static class QuestDinosaur
    {
        public static GameObject Create(GameObject prefab, Transform parent, float height, bool selectable)
        {
            var root = new GameObject("HideAR Dinosaur");
            root.transform.SetParent(parent, false);
            var model = Object.Instantiate(prefab, root.transform);
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Object.Destroy(root); return null; }
            var bounds = BoundsInLocalSpace(root.transform, renderers);
            model.transform.localScale *= height / Mathf.Max(bounds.size.y, 0.001f);
            bounds = BoundsInLocalSpace(root.transform, renderers);
            model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y - 0.01f, bounds.center.z);
            if (selectable)
            {
                bounds = BoundsInLocalSpace(root.transform, renderers);
                var collider = root.AddComponent<BoxCollider>();
                collider.center = bounds.center;
                collider.size = bounds.size + Vector3.one * 0.07f;
            }
            return root;
        }

        static Bounds BoundsInLocalSpace(Transform root, Renderer[] renderers)
        {
            var result = new Bounds();
            bool first = true;
            foreach (var renderer in renderers)
            {
                var bounds = renderer.bounds;
                for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                for (int z = 0; z < 2; z++)
                {
                    var corner = root.InverseTransformPoint(new Vector3(
                        x == 0 ? bounds.min.x : bounds.max.x,
                        y == 0 ? bounds.min.y : bounds.max.y,
                        z == 0 ? bounds.min.z : bounds.max.z));
                    if (first) { result = new Bounds(corner, Vector3.zero); first = false; }
                    else result.Encapsulate(corner);
                }
            }
            return result;
        }
    }
}
