// Play-mode regression against the baked Pass scene and its actual table colliders.
// -batchmode -executeMethod Winterbourne.Editor.PassMotorReview.Run (omit -quit).
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using System.Reflection;
#endif
using Winterbourne.Demo;
using Winterbourne.Pass;
using Object = UnityEngine.Object;

namespace Winterbourne.Editor
{
    [InitializeOnLoad]
    public static class PassMotorReview
    {
        const string Flag = "Winterbourne.MotorReview";
        const float Dt = 1f / 60f;
        static Car07Walker walker;
        static CharacterController body;

        static PassMotorReview() => EditorApplication.update += Tick;

        public static void Run()
        {
            EditorSceneManager.OpenScene(PassBuild.ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(Flag, true);
            SessionState.SetString(Flag + ".Start", DateTime.UtcNow.ToString("O"));
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            try
            {
                if (DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Flag + ".Start", "")).ToUniversalTime() > TimeSpan.FromMinutes(3))
                    throw new TimeoutException("Pass motor did not reach Play Mode");
                if (EditorApplication.isPaused) EditorApplication.isPaused = false;
                EditorApplication.QueuePlayerLoopUpdate();
                if (!EditorApplication.isPlaying || Time.frameCount < 5) return;
                SessionState.EraseBool(Flag);
                var route = Object.FindFirstObjectByType<PassJourney>();
                route.ManualClock = true;
                walker = route.walker;
                walker.enabled = false;
                body = route.walker.GetComponent<CharacterController>();
                var desk = GameObject.Find("Keeper's writing desk").GetComponent<BoxCollider>();
                TestTable(desk, null, false);
                TestStance();
#if ENABLE_INPUT_SYSTEM
                TestCtrlKeys();
#endif
                PassReview.EnterFromRoom(route);
                foreach (var tableSpec in new[] { (6, "Dining_table_1_1"), (9, "Guard duty room desk"), (4, "Drawing_compartment_1_Writing_table"), (7, "Writing bureau") })
                {
                    var car = route.consist.Find($"Car{tableSpec.Item1:00}");
                    var table = car.GetComponentsInChildren<BoxCollider>().Single(c => c.name == tableSpec.Item2);
                    TestTable(table, route.consist, true);
                }
                var shelves = route.consist.Find("Car07").GetComponentsInChildren<BoxCollider>()
                    .Where(c => c.name.StartsWith("Window writing shelf")).ToArray();
                Check(shelves.Length == 4, "07 should have four supported window writing shelves");
                foreach (var shelf in shelves)
                    TestWindowShelf(shelf, route.consist);
                Check(route.Health == 100, "Ordinary table jumps cost HP");
                Debug.Log("PASS MOTOR: normal jump height, actual desks and shelves, automatic clearance, anchored Ctrl capsule and camera");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static void Check(bool value, string description)
        {
            if (!value) throw new InvalidOperationException("Pass motor: " + description);
        }

        static void Pose(Transform support, Vector3 position, Vector3 facing)
        {
            Quaternion rotation = Quaternion.LookRotation(facing);
            walker.Place(support, support ? support.InverseTransformPoint(position) : position,
                support ? Quaternion.Inverse(support.rotation) * rotation : rotation, 0f);
            Physics.SyncTransforms();
            Check(walker.SetCrouched(false), "Stance could not reset at fixture feet " + position);
            for (int i = 0; i < 10; i++) walker.MoveCharacter(Vector2.zero, false, Dt);
            Check(body.isGrounded && !walker.IsCrouched, "Fixture must start upright and grounded at " + position);
        }

        static void TestTable(BoxCollider table, Transform support, bool lowRoof)
        {
            Bounds bounds = table.bounds;
            Vector3 from = new Vector3(bounds.max.x + .62f, bounds.max.y + .09f, bounds.center.z);
            if (table.name == "Writing bureau") from.z += .54f; // clear the clerk's chair
            Check(Physics.Raycast(from, Vector3.down, out var ground, 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore),
                "No floor alongside " + table.name);
            Pose(support, ground.point + Vector3.up * .035f, Vector3.left);
            var floorFeet = walker.transform.position;
            float high = floorFeet.y;
            for (int i = 0; i < 95; i++)
            {
                bool advance = walker.transform.position.x > bounds.center.x + .10f;
                walker.MoveCharacter(advance ? Vector2.up : Vector2.zero, false, Dt, i == 0);
                high = Mathf.Max(high, walker.transform.position.y);
            }
            var feet = walker.transform.position;
            Check(body.isGrounded && Physics.Raycast(feet + Vector3.up * .08f, Vector3.down, out var hit, .18f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.collider == table,
                $"Jump missed {table.name} at {feet:F3}, expected {bounds.center:F3} top {bounds.max.y:F3}, start {floorFeet:F3}, apex {high:F3}");
            Check(walker.IsCrouched == lowRoof && !walker.CrouchSelected, $"Unexpected stance on {table.name}: {body.height}");
            if (lowRoof) Check(!walker.SetCrouched(false), "Standing would clip the ceiling on " + table.name);
            Debug.Log($"PASS MOTOR TABLE: {table.name}, tabletop {bounds.max.y-floorFeet.y:F2}m, apex {high-floorFeet.y:F2}m, capsule {body.height:F2}m");
            for (int i = 0; i < 120; i++) walker.MoveCharacter(walker.transform.position.x < floorFeet.x-.05f ? Vector2.down : Vector2.zero, false, Dt);
            Check(body.isGrounded && !walker.IsCrouched, "Did not stand naturally after stepping off " + table.name);
        }

        static void TestWindowShelf(BoxCollider shelf, Transform train)
        {
            Bounds bounds = shelf.bounds;
            Vector3 target = bounds.center;
            // Take the near half of the window desk toward the mattress and away from the table lamp.
            target.z += .18f * -Mathf.Sign(train.InverseTransformPoint(target).z - train.Find("Car07").localPosition.z);
            Vector3 from = new Vector3(bounds.max.x + .72f, bounds.max.y + .10f, target.z);
            Check(Physics.Raycast(from, Vector3.down, out var floor, 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), "No cabin floor at " + from);
            Pose(train, floor.point + Vector3.up * .03f, Vector3.left);
            for (int jump = 0; jump < 2; jump++)
            {
                float stop = jump == 0 ? bounds.max.x + .47f : bounds.center.x + .045f;
                for (int i = 0; i < 90; i++)
                    walker.MoveCharacter(walker.transform.position.x > stop ? Vector2.up : Vector2.zero, false, Dt, i == 0);
                if (jump == 0)
                {
                    bool hitGround = Physics.Raycast(walker.transform.position + Vector3.up * .06f,
                        Vector3.down, out var mattress, 1.35f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    Check(body.isGrounded && hitGround && mattress.distance < .16f && mattress.collider.name.EndsWith("_Bed"),
                        $"First jump beside {shelf.name}, feet {walker.transform.position:F3}, ground {mattress.collider?.name}, distance {mattress.distance:F3}");
                }
            }
            Vector3 feet = walker.transform.position;
            Check(body.isGrounded && Physics.Raycast(feet + Vector3.up * .06f, Vector3.down, out var hit, .15f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.collider == shelf,
                $"Two steps via bed failed for {shelf.name}, actual {feet:F3}, expected {target:F3} top {bounds.max.y:F3}");
            Debug.Log($"PASS MOTOR SHELF: mattress to {shelf.name}, feet {feet:F3}, capsule {body.height:F2}m");
        }

#if ENABLE_INPUT_SYSTEM
        static void TestCtrlKeys()
        {
            // An isolated device sends real Input System down/held/up samples, even in a
            // headless Editor where there is no focused OS Game view to capture a mouse.
            var previousMode = InputSystem.settings.updateMode;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var readInput = typeof(Car07Walker).GetMethod("ReadInput", BindingFlags.Static | BindingFlags.NonPublic);
            object[] values = new object[readInput.GetParameters().Length];
            void Sample(Key? key, bool expectedEdge, bool expectedStance)
            {
                InputSystem.QueueStateEvent(keyboard, key.HasValue ? new KeyboardState(key.Value) : new KeyboardState());
                InputSystem.Update();
                readInput.Invoke(null, values);
                bool edge = (bool)values[7];
                walker.MoveCharacter((Vector2)values[0], (bool)values[5], Dt, (bool)values[6], edge);
                Check(edge == expectedEdge && walker.IsCrouched == expectedStance,
                    $"Keyboard {key} returned edge={edge}, crouched={walker.IsCrouched}");
            }
            try
            {
                InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
                Sample(null, false, false);
                foreach (var key in new[] { Key.LeftCtrl, Key.RightCtrl })
                {
                    Sample(key, true, true);
                    for (int i = 0; i < 6; i++) Sample(key, false, true);
                    Sample(null, false, true);
                    Sample(key, true, false);
                    Sample(null, false, false);
                }
                Debug.Log("PASS MOTOR KEYBOARD: LeftCtrl and RightCtrl both toggle on down, keep stance while held and on release, stand on second down");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.updateMode = previousMode;
            }
        }
#endif

        static void TestStance()
        {
            float feet = body.bounds.min.y;
            float initialEye = walker.eyeCamera.transform.localPosition.y;
            walker.MoveCharacter(Vector2.zero, false, Dt, false, true);
            Check(walker.CrouchSelected && walker.IsCrouched && Mathf.Abs(body.height-1f) < .001f, "Ctrl failed to crouch");
            for (int i = 0; i < 20; i++) walker.MoveCharacter(Vector2.zero, false, Dt);
            Check(Mathf.Abs(body.bounds.min.y-feet)<.025f && Mathf.Abs(walker.eyeCamera.transform.localPosition.y-(initialEye-.72f))<.02f,
                "Crouch moved feet or failed to lower camera");
            walker.MoveCharacter(Vector2.zero, false, Dt, false, true);
            for (int i = 0; i < 20; i++) walker.MoveCharacter(Vector2.zero, false, Dt);
            Check(!walker.IsCrouched && Mathf.Abs(body.bounds.min.y-feet)<.025f && Mathf.Abs(walker.eyeCamera.transform.localPosition.y-initialEye)<.015f,
                "Second Ctrl failed to restore capsule and camera");
            float takeoff = walker.transform.position.y;
            float high = takeoff;
            for (int i = 0; i < 90; i++)
            {
                walker.MoveCharacter(Vector2.zero, false, Dt, i == 0);
                high = Mathf.Max(high, walker.transform.position.y);
            }
            Check(Mathf.Abs(high-takeoff-1.08f)<.04f && body.isGrounded, "Standing jump apex: " + (high-takeoff));
        }
    }
}
