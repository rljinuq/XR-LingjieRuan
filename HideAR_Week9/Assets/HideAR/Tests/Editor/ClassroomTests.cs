using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit.UI;
using HideAR.Classroom.Editor;

namespace HideAR.Classroom.Tests
{
    // 职责：不连接头显即可检查纯回合规则；这些测试代码不进入 Quest 游戏。
    // Role: test pure round rules without a headset; these tests are not part of the Quest game.
    public class RoundTests
    {
        static RoundState Review()
        {
            var game = new RoundState(); game.Start(); game.UpdateInput(false, 0.36f);
            game.BeginPlacement(); game.PlacementFinished(); return game;
        }

        static RoundState Countdown()
        {
            var game = Review(); game.Confirm();
            game.UpdateInput(false, 0.36f); game.Ready(); return game;
        }

        [Test] public void WelcomeNeverPlaces() { Assert.IsFalse(new RoundState().BeginPlacement()); }
        [Test] public void StartPressDoesNotPlace()
        {
            var game = new RoundState(); game.Start(); game.UpdateInput(true, 2);
            Assert.IsFalse(game.BeginPlacement()); game.UpdateInput(false, 0.1f);
            Assert.IsFalse(game.BeginPlacement()); game.UpdateInput(false, 0.26f);
            Assert.IsTrue(game.BeginPlacement());
        }
        [Test] public void InputStaysArmedForNextPress()
        {
            var game = new RoundState(); game.Start(); game.UpdateInput(false, 1); game.UpdateInput(true, 0.02f);
            Assert.IsTrue(game.BeginPlacement());
        }
        [Test] public void OnlyOnePendingPlacement()
        {
            var game = new RoundState(); game.Start(); game.UpdateInput(false, 1);
            Assert.IsTrue(game.BeginPlacement()); game.UpdateInput(false, 1);
            Assert.IsFalse(game.BeginPlacement());
        }
        [Test] public void PlacementWaitsForConfirmation()
        {
            var game = Review(); game.Tick(100, true); Assert.AreEqual(Stage.Review, game.Stage);
            Assert.IsFalse(game.Find()); Assert.IsFalse(game.BeginPlacement());
        }
        [Test] public void RepositionRequiresRelease()
        {
            var game = Review(); game.Move(); Assert.AreEqual(Stage.Placing, game.Stage);
            Assert.IsFalse(game.BeginPlacement()); game.UpdateInput(false, 1); Assert.IsTrue(game.BeginPlacement());
        }
        [Test] public void HandoffWaitsForSeeker()
        {
            var game = Review(); game.Confirm(); game.Tick(100, true);
            Assert.AreEqual(Stage.Handoff, game.Stage); Assert.AreEqual(0, game.Seconds);
        }
        [Test] public void CanStartCountdownWithoutRemovingHeadset()
        {
            var game = Review(); game.Confirm();
            game.UpdateInput(false, 1); game.Ready();
            Assert.AreEqual(Stage.Countdown, game.Stage); Assert.AreEqual(3, game.Countdown);
        }
        [Test] public void StartSearchNeedsFreshReleaseAndExplicitClick()
        {
            var game = Review(); game.Confirm(); game.UpdateInput(true, 1);
            game.Ready(); Assert.AreEqual(Stage.Handoff, game.Stage);
            game.UpdateInput(false, 0.36f); game.Tick(100, true);
            Assert.AreEqual(Stage.Handoff, game.Stage); Assert.IsTrue(game.CanReady);
            game.Ready(); Assert.AreEqual(Stage.Countdown, game.Stage); Assert.AreEqual(3, game.Countdown);
        }
        [Test] public void TrackingRecoveryDoesNotStartSearchAutomatically()
        {
            var game = Review(); game.Confirm();
            game.Tick(100, false); game.Tick(100, true);
            Assert.AreEqual(Stage.Handoff, game.Stage);
            game.UpdateInput(false, 1); game.Ready(); Assert.AreEqual(3, game.Countdown);
        }
        [Test] public void CountdownPausesWithoutTracking()
        {
            var game = Countdown(); game.Tick(100, false);
            Assert.AreEqual(3, game.Countdown); game.Tick(3, true); Assert.AreEqual(Stage.Seeking, game.Stage);
        }
        [Test] public void SearchTimePausesAndStopsOnFound()
        {
            var game = Countdown(); game.Tick(3, true); game.UpdateInput(false, 1);
            game.Tick(5, true); game.Tick(100, false); Assert.AreEqual(5, game.Seconds);
            Assert.IsFalse(game.Find()); game.UpdateInput(false, 1); Assert.IsTrue(game.Find());
            game.Tick(100, true); Assert.AreEqual(5, game.Seconds);
        }
        [Test] public void ReplayClearsTimer()
        {
            var game = Countdown(); game.Tick(3, true); game.UpdateInput(false, 1);
            game.Tick(5, true); game.Find(); Assert.IsTrue(game.Start()); Assert.AreEqual(0, game.Seconds);
        }
        [Test] public void TimedOutResultCannotEnterReview()
        {
            var game = new RoundState(); game.Start(); game.UpdateInput(false, 1); game.BeginPlacement();
            game.Fail(); game.PlacementFinished(); Assert.AreEqual(Stage.Error, game.Stage);
        }
    }

    // 检查已保存场景的引用、按钮、尺寸、颜色与菜单逻辑，不验证真实空间追踪质量。
    // Check saved references, controls, size, color and menu logic, not real-world tracking quality.
    public class SceneTests
    {
        GameFlow game;
        static void UpdateMenu(GameUI ui) { typeof(GameUI).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null); }
        [SetUp] public void OpenScene()
        {
            EditorSceneManager.OpenScene(ClassroomSetup.ScenePath);
            game = Object.FindAnyObjectByType<GameFlow>();
        }
        [Test] public void NoMissingReferencesOrScripts()
        {
            Assert.NotNull(game);
            foreach (var component in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Assert.NotNull(component);
                if (component.GetType().Namespace != "HideAR.Classroom") continue;
                var fields = new SerializedObject(component).GetIterator();
                while (fields.NextVisible(true))
                    if (fields.propertyType == SerializedPropertyType.ObjectReference) Assert.NotNull(fields.objectReferenceValue, component.name + ": " + fields.name);
            }
        }
        [Test] public void OneSessionAndPermissionGatedManagers()
        {
            Assert.AreEqual(1, Object.FindObjectsByType<ARSession>(FindObjectsSortMode.None).Length);
            Assert.IsFalse(game.room.planes.enabled); Assert.IsFalse(game.room.raycasts.enabled);
            Assert.NotNull(game.ui.head.GetComponent<TrackedPoseDriver>());
            Assert.NotNull(Object.FindAnyObjectByType<XRUIInputModule>());
        }
        [Test] public void MenuBlocksPlacementAndButtonsAreWired()
        {
            Assert.AreEqual(5, game.ui.gameObject.layer);
            var box = game.ui.GetComponent<BoxCollider>(); Assert.NotNull(box);
            Assert.AreEqual(((RectTransform)game.ui.transform).rect.width, box.size.x);
            foreach (var button in new[] { game.ui.primary, game.ui.secondary, game.ui.reset, game.ui.roomSetup }.Concat(game.ui.colorButtons))
            {
                Assert.AreEqual(1, button.onClick.GetPersistentEventCount());
                Assert.AreEqual(game, button.onClick.GetPersistentTarget(0));
            }
        }
        [Test] public void MenuKeepsItsHeadingWhenLookingAlmostVertically()
        {
            var ui = game.ui;
            ui.head.transform.rotation = Quaternion.Euler(0, 73, 0);
            ui.Recenter();
            var heading = ui.transform.forward;
            foreach (float pitch in new[] { 80f, 89.9f, 90f, -89.9f, -90f })
            {
                ui.head.transform.rotation = Quaternion.Euler(pitch, 210, 0);
                ui.Recenter();
                Assert.Less(Vector3.Angle(heading, ui.transform.forward), 0.01f);
                Assert.Less(Vector3.Angle(Vector3.up, ui.transform.up), 0.01f);
                Assert.That(Vector3.Distance(ui.transform.position, ui.head.transform.position + heading * 1.5f + Vector3.down * 0.1f), Is.LessThan(0.0001f));
            }
        }
        [Test] public void FirstMenuPlacementWhileLookingDownUsesHeadYaw()
        {
            var ui = game.ui;
            Assert.IsFalse(ui.HasPosition);
            ui.head.transform.rotation = Quaternion.Euler(90, 125, 0);
            ui.Recenter();
            var expected = Quaternion.Euler(0, 125, 0) * Vector3.forward;
            Assert.Less(Vector3.Angle(expected, ui.transform.forward), 0.01f);
            Assert.Less(Vector3.Angle(Vector3.up, ui.transform.up), 0.01f);
        }
        [Test] public void MenuHeightAdaptsToSeatedAndStandingPlayers()
        {
            var ui = game.ui;
            ui.head.transform.rotation = Quaternion.Euler(20, 40, 30);
            var heading = Vector3.ProjectOnPlane(ui.head.transform.forward, Vector3.up).normalized;
            foreach (float height in new[] { 1.1f, 1.8f })
            {
                ui.head.transform.position = new Vector3(3, height, -4);
                ui.Recenter();
                Assert.That(ui.transform.position.y, Is.EqualTo(height - 0.1f).Within(0.0001f));
                Assert.That(Vector3.Distance(ui.transform.position, ui.head.transform.position + heading * 1.5f + Vector3.down * 0.1f), Is.LessThan(0.0001f));
                Assert.Less(Vector3.Angle(Vector3.up, ui.transform.up), 0.01f);
            }
        }
        [Test] public void MenuRequestUsesLatestPoseAndThenStaysPinned()
        {
            var ui = game.ui;
            ui.RequestRecenter();
            Assert.IsFalse(ui.HasPosition);
            ui.head.transform.SetPositionAndRotation(new Vector3(2, 1.2f, 3), Quaternion.Euler(0, 90, 0));
            UpdateMenu(ui);
            Assert.IsTrue(ui.HasPosition);
            Assert.That(Vector3.Distance(ui.transform.position, new Vector3(3.5f, 1.1f, 3)), Is.LessThan(0.0001f));
            var position = ui.transform.position;
            var rotation = ui.transform.rotation;
            ui.head.transform.SetPositionAndRotation(new Vector3(-3, 1.8f, 1), Quaternion.Euler(0, 220, 0));
            UpdateMenu(ui);
            Assert.AreEqual(position, ui.transform.position);
            Assert.AreEqual(rotation, ui.transform.rotation);
        }
        // 用测试对象比较世界坐标；没有运行真实锚点定位，不能当作真机稳定性测试。
        // Compare world poses using test objects, not live anchor tracking or device stability data.
        [Test] public void StageChangesDoNotMovePinnedMenuOrPlacedObject()
        {
            var anchorObject = new GameObject("Test Anchor");
            anchorObject.SetActive(false);
            var anchor = anchorObject.AddComponent<ARAnchor>();
            var placed = Object.Instantiate(game.placement.SelectedPrefab, anchor.transform);
            anchor.transform.position = new Vector3(-2, 0.8f, 4);
            typeof(PlaceOnPlane).GetProperty(nameof(PlaceOnPlane.Anchor)).SetValue(game.placement, anchor);
            typeof(PlaceOnPlane).GetProperty(nameof(PlaceOnPlane.Dinosaur)).SetValue(game.placement, placed);
            try
            {
                game.ui.Recenter();
                var position = game.ui.transform.position;
                var rotation = game.ui.transform.rotation;
                var objectPosition = placed.transform.position;
                game.ui.head.transform.SetPositionAndRotation(new Vector3(4, 1.2f, -3), Quaternion.Euler(85, 160, 0));
                void Check(Stage expected)
                {
                    Assert.AreEqual(expected, game.Round.Stage);
                    typeof(GameFlow).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
                    UpdateMenu(game.ui);
                    Assert.AreEqual(expected, game.Round.Stage);
                    Assert.AreEqual(position, game.ui.transform.position, expected.ToString());
                    Assert.AreEqual(rotation, game.ui.transform.rotation, expected.ToString());
                    Assert.AreEqual(objectPosition, placed.transform.position, expected.ToString());
                }
                Check(Stage.Welcome);
                game.Round.Start(); Check(Stage.Placing);
                game.Round.UpdateInput(false, 1); game.Round.BeginPlacement(); Check(Stage.Saving);
                game.Round.PlacementFinished(); Check(Stage.Review);
                game.Round.Confirm(); Check(Stage.Handoff);
                game.Round.UpdateInput(false, 1); game.Round.Ready(); Check(Stage.Countdown);
                game.Round.Tick(3, true); Check(Stage.Seeking);
                game.Round.UpdateInput(false, 1); game.Round.Find(); Check(Stage.Found);
                game.Round.Fail(); Check(Stage.Error);
            }
            finally { Object.DestroyImmediate(anchorObject); }
        }
        [Test] public void DinosaurFitsTableAndIsSelectable()
        {
            var dinosaur = Object.Instantiate(game.placement.prefabToPlace);
            var renderers = dinosaur.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Assert.AreEqual(0.182f, bounds.size.y, 0.002f); Assert.GreaterOrEqual(bounds.min.y, 0);
            Assert.NotNull(dinosaur.GetComponent<BoxCollider>());
            Assert.IsFalse(dinosaur.transform.IsChildOf(game.ui.head.transform));
            foreach (var renderer in renderers) Assert.IsTrue(renderer.sharedMaterials.All(m => m.shader.name == "Universal Render Pipeline/Simple Lit"));
            Object.DestroyImmediate(dinosaur);
        }
        [Test] public void RoomSetupAndModelChoiceRemainIndependent()
        {
            typeof(GameFlow).GetProperty(nameof(GameFlow.Tracked)).SetValue(game, true);
            typeof(RoomAccess).GetProperty(nameof(RoomAccess.Granted)).SetValue(game.room, true);
            foreach (bool placing in new[] { false, true })
            {
                if (placing) game.Round.Start();
                game.ui.Refresh(game);
                Assert.IsTrue(game.ui.roomSetup.gameObject.activeSelf);
                Assert.IsTrue(game.ui.roomSetup.interactable);
                Assert.AreEqual("SetUpRoom", game.ui.roomSetup.onClick.GetPersistentMethodName(0));
                Assert.AreEqual("Change object", game.ui.secondary.GetComponentInChildren<Text>().text);
                int selected = game.placement.SelectedIndex;
                game.Secondary();
                Assert.AreEqual((selected + 1) % 4, game.placement.SelectedIndex);
            }
        }
        [Test] public void ObjectChoicesAreSmallAndNamed()
        {
            Assert.AreEqual(4, game.placement.placeablePrefabs.Length);
            CollectionAssert.AreEqual(new[] { "Dinosaur", "Dog", "Cat", "Bunny" }, game.placement.placeableNames);
            foreach (var prefab in game.placement.placeablePrefabs)
            {
                var obj = Object.Instantiate(prefab);
                var renderers = obj.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                Assert.LessOrEqual(bounds.size.y, 0.19f, prefab.name);
                Assert.NotNull(obj.GetComponent<BoxCollider>(), prefab.name);
                if (prefab != game.placement.prefabToPlace)
                    foreach (var renderer in renderers) Assert.NotNull(renderer.sharedMaterial.GetTexture("_BaseMap"), prefab.name + " texture");
                Object.DestroyImmediate(obj);
            }
        }
        [Test] public void OnlyClassroomSceneBuilds()
        {
            Assert.AreEqual(1, EditorBuildSettings.scenes.Length);
            Assert.AreEqual(ClassroomSetup.ScenePath, EditorBuildSettings.scenes[0].path);
            Assert.AreEqual("com.lingjieruan.hidear.classroom", PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android));
        }
        [Test] public void ColorPreviewDoesNotChangePrefabOrBlockPlacement()
        {
            var placement = game.placement;
            var material = placement.SelectedPrefab.GetComponentInChildren<Renderer>().sharedMaterial;
            var original = material.GetColor("_BaseColor");
            placement.SelectColor(2);
            var preview = placement.CreatePreview();
            try
            {
                Assert.IsTrue(preview.GetComponentsInChildren<Collider>().All(c => !c.enabled));
                var renderer = preview.GetComponentInChildren<Renderer>();
                var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties, 0);
                var applied = properties.GetColor("_BaseColor");
                Assert.That(applied.r, Is.EqualTo(PlaceOnPlane.Colors[2].r).Within(0.00001f));
                Assert.That(applied.g, Is.EqualTo(PlaceOnPlane.Colors[2].g).Within(0.00001f));
                Assert.That(applied.b, Is.EqualTo(PlaceOnPlane.Colors[2].b).Within(0.00001f));
                Assert.AreEqual(original, material.GetColor("_BaseColor"));
                placement.SelectColor(0); placement.ApplyColor(preview);
                renderer.GetPropertyBlock(properties, 0); Assert.IsTrue(properties.isEmpty);
                Assert.AreEqual(PlaceOnPlane.Colors.Length, game.ui.colorButtons.Length);
                Assert.NotNull(game.ui.previewRoot);
                placement.SelectNext(); placement.SelectColor(3);
                var petPreview = placement.CreatePreview();
                try
                {
                    petPreview.GetComponentInChildren<Renderer>().GetPropertyBlock(properties, 0);
                    Assert.AreEqual(placement.colorTextures[3], properties.GetTexture("_BaseMap"));
                }
                finally { Object.DestroyImmediate(petPreview); }
            }
            finally { Object.DestroyImmediate(preview); }
        }
    }
}
