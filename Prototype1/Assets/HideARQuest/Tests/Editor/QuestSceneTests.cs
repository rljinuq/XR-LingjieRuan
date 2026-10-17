using System.Linq;
using HideAR.Quest.Editor;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARFoundation.Samples;
using Unity.XR.CoreUtils;

namespace HideAR.Quest.Tests
{
    public class QuestSceneTests
    {
        [SetUp]
        public void OpenQuest() => EditorSceneManager.OpenScene(QuestProjectSetup.ScenePath);

        [TearDown]
        public void CloseQuest() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);

        [Test]
        public void SceneReferencesAreComplete() => QuestProjectSetup.ValidateScene();

        [Test]
        public void RawHitsCannotBypassTheGameGate()
        {
            var game = new SerializedObject(Object.FindAnyObjectByType<QuestGameController>());
            var raycaster = Object.FindAnyObjectByType<RaycastEventController>();
            var placement = Object.FindAnyObjectByType<ARPlaceAnchor>();
            var approved = game.FindProperty("approvedHit").objectReferenceValue;
            Assert.AreNotSame(raycaster.raycastHitEventAsset, approved);
            Assert.AreSame(game.FindProperty("rawHit").objectReferenceValue, raycaster.raycastHitEventAsset);
            Assert.AreSame(approved, new SerializedObject(placement).FindProperty("m_RaycastHitEvent").objectReferenceValue);
            Assert.IsFalse(raycaster.enabled);
            Assert.IsFalse(placement.enabled);
            Assert.AreEqual(UnityEngine.XR.ARSubsystems.TrackableType.PlaneWithinPolygon, raycaster.trackableType);
        }

        [Test]
        public void PanelPhysicallyBlocksPlacementRays()
        {
            var panel = Object.FindAnyObjectByType<QuestPanel>();
            var camera = Object.FindAnyObjectByType<XROrigin>().Camera;
            panel.Recenter(camera);
            Physics.SyncTransforms();
            var ray = new Ray(camera.transform.position, panel.transform.position - camera.transform.position);
            Assert.IsTrue(Physics.Raycast(ray, out var hit, 20, 1 << LayerMask.NameToLayer("UI"), QueryTriggerInteraction.Collide));
            Assert.AreSame(panel.gameObject, hit.collider.gameObject);
            Assert.AreEqual(RenderMode.WorldSpace, panel.GetComponent<Canvas>().renderMode);
        }

        [Test]
        public void WorldContentUsesPrivatePrefabsAndTheOriginalRig()
        {
            var anchors = Object.FindAnyObjectByType<ARAnchorManager>();
            var planes = Object.FindAnyObjectByType<ARPlaneManager>();
            Assert.That(AssetDatabase.GetAssetPath(anchors.anchorPrefab), Does.StartWith(QuestProjectSetup.Root));
            Assert.That(AssetDatabase.GetAssetPath(planes.planePrefab), Does.StartWith(QuestProjectSetup.Root));
            Assert.IsNotNull(planes.planePrefab.GetComponent<ARPlaneMeshVisualizer>());
            Assert.IsNull(anchors.anchorPrefab.GetComponent<ARAnchorDebugVisualizer>());
            Assert.IsFalse(planes.enabled);
            var origin = Object.FindAnyObjectByType<XROrigin>();
            Assert.IsNotNull(PrefabUtility.GetCorrespondingObjectFromSource(origin));
        }

        [Test]
        public void DinosaurIsSelectableAndGroundedUnderItsAnchor()
        {
            var game = new SerializedObject(Object.FindAnyObjectByType<QuestGameController>());
            var parent = new GameObject("Test Anchor");
            parent.transform.position = new Vector3(2, 1, 3);
            var dinosaur = QuestDinosaur.Create(game.FindProperty("dinosaurPrefab").objectReferenceValue as GameObject,
                parent.transform, 0.28f, true);
            Assert.AreSame(parent.transform, dinosaur.transform.parent);
            var renderers = dinosaur.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            Assert.That(bounds.size.y, Is.EqualTo(0.28f).Within(0.002f));
            Assert.That(bounds.min.y, Is.EqualTo(parent.transform.position.y + 0.01f).Within(0.002f));
            Assert.IsNotNull(dinosaur.GetComponent<BoxCollider>());
            var before = dinosaur.transform.position;
            Object.FindAnyObjectByType<XROrigin>().Camera.transform.position += Vector3.right;
            Assert.AreEqual(before, dinosaur.transform.position);
            Object.DestroyImmediate(parent);
        }

        [Test]
        public void LongestPanelMessagesFit()
        {
            var panel = Object.FindAnyObjectByType<QuestPanel>();
            panel.Show("Happy with this spot?", "Pass this headset and the controllers to the seeker.\nWhen they are ready, begin the countdown.",
                "Room access was declined. Allow Spatial Data in this app's headset permissions, then retry.",
                "Confirm hiding", "Move dinosaur", true, true);
            Canvas.ForceUpdateCanvases();
            foreach (var text in panel.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                Assert.IsFalse(text.isTextOverflowing, text.name + " overflows");
            }
        }
    }
}
