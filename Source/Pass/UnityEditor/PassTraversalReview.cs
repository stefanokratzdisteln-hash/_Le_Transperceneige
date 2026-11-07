// Run in an isolated Unity project using the Input System and the baked Pass scene.
// -executeMethod Winterbourne.Editor.PassTraversalReview.Run (omit -quit).
#if ENABLE_INPUT_SYSTEM
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Winterbourne.Demo;
using Winterbourne.Pass;
using Object = UnityEngine.Object;

namespace Winterbourne.Editor
{
    [InitializeOnLoad]
    public static class PassTraversalReview
    {
        const string Flag = "Winterbourne.TraversalReview";
        const float Dt = 1f / 30;
        static readonly Stack<IEnumerator> tasks = new Stack<IEnumerator>();
        static PassJourney route;
        static Car07Walker walker;
        static CharacterController body;
        static Keyboard keyboard;
        static Mouse mouse;
        static int lastFrame = -1;
        static bool rolling;
        static Transform[] cars;
        static PassDoor[] doors;
        static PassDoorTarget[] grips;

        static PassTraversalReview() => EditorApplication.update += Update;

        public static void Run()
        {
            EditorSceneManager.OpenScene(PassBuild.ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(Flag, true);
            SessionState.SetString(Flag + ".start", DateTime.UtcNow.ToString("O"));
            FocusGame();
            EditorApplication.EnterPlaymode();
        }

        static void FocusGame() => EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        static void Check(bool ok, string message) => PassBuild.Check(ok, "TRAVERSAL: " + message);

        static void Update()
        {
            int pendingExit = SessionState.GetInt(Flag + ".exit", -1);
            if (pendingExit >= 0)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    SessionState.EraseInt(Flag + ".exit");
                    EditorApplication.Exit(pendingExit);
                }
                return;
            }
            if (!SessionState.GetBool(Flag, false)) return;
            try
            {
                Check((DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Flag + ".start", "")).ToUniversalTime()).TotalMinutes < 20,
                    "Input walkthrough timed out");
                if (EditorApplication.isPaused) EditorApplication.isPaused = false;
                EditorApplication.QueuePlayerLoopUpdate();
                if (!EditorApplication.isPlaying || Time.frameCount < 4 || lastFrame == Time.frameCount) return;
                lastFrame = Time.frameCount;
                if (tasks.Count == 0) tasks.Push(RunCases());
                if (rolling) route.Advance(Dt);
                while (tasks.Count > 0)
                {
                    var current = tasks.Peek();
                    if (!current.MoveNext()) { tasks.Pop(); continue; }
                    if (current.Current is IEnumerator nested) { tasks.Push(nested); continue; }
                    return;
                }
                SessionState.EraseBool(Flag);
                Debug.Log("TRAVERSAL PASS: full keyboard walkthrough finished");
                Stop(0);
            }
            catch (Exception e)
            {
                SessionState.EraseBool(Flag);
                Debug.LogException(e);
                Stop(1);
            }
        }

        static void Stop(int code)
        {
            SessionState.SetInt(Flag + ".exit",code);
            EditorApplication.ExitPlaymode();
        }

        static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
        static IEnumerator Tap(Key key)
        {
            Keys(key); yield return Frames(2);
            Keys(); yield return Frames(2);
        }

        static void Pose(Vector3 feet, Vector3 facing, bool onTrain)
        {
            var support = onTrain ? route.consist : null;
            float pitch = -Mathf.Asin(facing.normalized.y) * Mathf.Rad2Deg;
            var yaw = Quaternion.Euler(0, Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg, 0);
            walker.Place(support, support ? support.InverseTransformPoint(feet) : feet, yaw, pitch);
            Physics.SyncTransforms();
        }

        static Vector3 Feet => walker.transform.position;
        static void ClockToStation()
        {
            route.Retry();
            for (int i = 0; i < 500 && route.Phase != JourneyPhase.Dwell; i++) route.Advance(.43f);
            Check(route.Phase == JourneyPhase.Dwell && route.Aboard, "Station fixture did not reach the stop");
        }

        static IEnumerator Capture()
        {
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return Frames(2);
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return Frames(3);
            InputSystem.QueueStateEvent(mouse, new MouseState());
            Check(walker.HasMouse, "Native Game view did not capture the injected mouse click (focus=" +
                Application.isFocused + ", lock=" + Cursor.lockState + ")");
        }

        static IEnumerator RunCases()
        {
            FocusGame();
            Time.captureDeltaTime = Dt;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 90;
            route = Object.FindFirstObjectByType<PassJourney>();
            walker = route.walker;
            // Input, camera rays, character collision and game state still run. The
            // rendering walkthrough is a separate test; do not shade the valley 5,000 times.
            walker.eyeCamera.enabled = false;
            body = walker.GetComponent<CharacterController>();
            route.ManualClock = true;
            Debug.Log("TRAVERSAL INPUT: native Game view " + Application.unityVersion + " focused=" + Application.isFocused);
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            cars = Enumerable.Range(1,9).Select(n => route.consist.Find($"Car{n:00}")).ToArray();
            Check(cars.All(car => car), "Scene lacks a named coach under the moving consist");
            doors = route.consist.GetComponentsInChildren<PassDoor>(true);
            grips = route.consist.GetComponentsInChildren<PassDoorTarget>(true);
            yield return Capture();
            PassReview.EnterFromRoom(route);
            yield return Couplings();
            ClockToStation();
            yield return DoorSweep();
            foreach (int number in new[] { 5, 7, 9 }) yield return Chase(number);
            ClockToStation();
            yield return Recovery();
            yield return RearEscape();
        }

        static IEnumerator Couplings()
        {
            // Use the same motor as Update at four different simulation rates. Keep the
            // live camera and the real capsule, and move the consist for every motor step.
            bool enabled = walker.enabled;
            walker.enabled = false;
            var runner = walker.transform;
            for (int i = 0; i < 8; i++)
            {
                var joint = cars[i].Find($"Gangway {i+1:00}-{i+2:00}");
                Check(joint && joint.Find("Joint tread") && joint.Find("Left joint guard") && joint.Find("Right joint guard"),
                    "Missing connecting plate or guard after " + cars[i].name);
                foreach (int fps in new[] { 15, 30, 60, 120 })
                foreach (int sign in new[] { -1, 1 })
                {
                    float dt = 1f / fps;
                    // Each start is on the existing leaf. Test the longitudinal seam at
                    // speed, including moving diagonally into the adjacent flexible wall.
                    Pose(joint.position + new Vector3(sign * .28f, .025f, -sign * .92f), Vector3.forward * sign, true);
                    float highest = Feet.y;
                    for (int frame = 0; frame < fps * 1.3f; frame++)
                    {
                        route.Advance(dt);
                        walker.MoveCharacter(new Vector2(.32f, 1), true, dt, frame == fps / 4 && fps == 60);
                        route.ReconcileBoarding();
                        highest = Mathf.Max(Feet.y, highest);
                        Check(runner.position.y > 1.30f && route.Aboard,
                            $"Fell from {joint.name} at {fps} fps facing {sign}: {runner.position:F3}");
                        if (joint.InverseTransformPoint(Feet).z * sign > .86f) break;
                    }
                    Check(joint.InverseTransformPoint(Feet).z * sign > .86f, $"Blocked crossing {joint.name} at {fps} fps ({sign}): close to {joint.InverseTransformPoint(Feet):F3}");
                    if (fps == 60) Check(highest > 1.49f, "Space did not lift at " + joint.name);
                }
                yield return null;
            }
            walker.enabled = enabled;
            yield return Capture();
            Debug.Log("TRAVERSAL MOTOR: 8 moving connectors, 64 directional and edge runs across 15/30/60/120 Hz");
        }

        static bool SameGrip(PassDoorTarget target, PassDoor h, Car07SlidingDoor s)
            => target && (target.hinge ? target.hinge == h : target.slider && target.slider == s);

        static bool StandAndAim(PassDoorTarget target, int face)
        {
            Vector3 aim = target.GetComponent<Collider>().bounds.center;
            var eye = walker.eyeCamera;
            // A real standing operator needs floor, free capsule space and a visible
            // target. Reject points that would put the actor on the roof or in woodwork.
            foreach (float radius in new[] { .86f, 1.14f, .63f, 1.43f, .46f })
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI / 12;
                Vector3 above = aim + new Vector3(Mathf.Sin(angle) * radius, 0, Mathf.Cos(angle) * radius);
                if (face * Vector3.Dot(above - aim, target.transform.right) < .24f) continue;
                above.y = 1.60f;
                if (!Physics.Raycast(above, Vector3.down, out var floor, 2.65f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                    floor.collider == body || floor.normal.y < .8f) continue;
                Vector3 p = floor.point + Vector3.up * .025f;
                var solids = Physics.OverlapCapsule(p + Vector3.up * .25f, p + Vector3.up * (body.height - .22f),
                    body.radius - .014f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (solids.Any(col => col != body)) continue;
                if (target.hinge && target.hinge.TargetOpen && Vector3.Distance(p, target.hinge.threshold.position - Vector3.up) < .65f) continue;
                Pose(p, aim - (p + Vector3.up * eye.transform.localPosition.y), floor.collider.transform.IsChildOf(route.consist));
                if (target.hinge && target.hinge.Occupied(body)) continue;
                if (PassDoorTarget.Resolve(eye, walker.interactionDistance, out var hinge, out var slider) && SameGrip(target,hinge,slider))
                    return true;
            }
            return false;
        }

        static IEnumerator DoorSweep()
        {
            var interactive = doors.Where(d => d.kind != PassDoorKind.Escape).Cast<Component>()
                .Concat(route.consist.GetComponentsInChildren<Car07SlidingDoor>(true)).ToArray();
            bool State(Component d) => d is PassDoor h ? h.TargetOpen : ((Car07SlidingDoor)d).TargetClosed;
            var initial = interactive.ToDictionary(d => d, State);
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var d in interactive)
                {
                    var pulls = grips.Where(g => (Component)g.hinge == d || (Component)g.slider == d);
                    Check(pulls.Any(grip => StandAndAim(grip, pass == 0 ? -1 : 1)), "No standing interaction sightline on face " +
                        (pass == 0 ? -1 : 1) + " of " + d.transform.parent.name + "/" + d.name);
                    bool before = State(d);
                    yield return Tap(Key.E);
                    Check(State(d) != before, "Native E did not toggle " + d.transform.parent.name + "/" + d.name);
                }
                Pose(cars[4].TransformPoint(new Vector3(0,1.41f,0)),Vector3.forward,true);
                yield return Frames(45);
                foreach (var d in interactive)
                {
                    float pose = d is PassDoor h ? h.Openness : ((Car07SlidingDoor)d).ClosedFraction;
                    Check(Mathf.Abs(pose - (State(d) ? 1 : 0)) < .025f, "Leaf blocked before reaching its target: " + d.name);
                    Check(State(d) == (pass == 1 ? initial[d] : !initial[d]), "Wrong final pose for " + d.name);
                }
            }
            Debug.Log($"TRAVERSAL INPUT: {interactive.Length} passenger, staff, luggage, cabin and gangway leaves physically operated both directions by E");
        }

        static IEnumerator WalkToward(Vector3 target, bool sprint = false)
        {
            Vector3 start = Feet;
            for (int frame = 0; frame < 360; frame++)
            {
                Vector3 delta = target - Feet; delta.y = 0;
                if (delta.magnitude < .13f) { Keys(); yield return Frames(2); yield break; }
                var local = walker.transform.InverseTransformDirection(delta);
                var keys = new List<Key>(3);
                if (Mathf.Abs(local.z) > .42f * delta.magnitude) keys.Add(local.z > 0 ? Key.W : Key.S);
                if (Mathf.Abs(local.x) > .42f * delta.magnitude) keys.Add(local.x > 0 ? Key.D : Key.A);
                if (sprint && delta.magnitude > 1.0f) keys.Add(Key.LeftShift);
                Keys(keys.ToArray());
                yield return null;
            }
            throw new InvalidOperationException($"Native WASD path from {start:F2} to {target:F2} stopped at {Feet:F2}");
        }

        static IEnumerator Chase(int number)
        {
            ClockToStation();
            var car = cars[number-1];
            int end = number == 7 ? -1 : 1;
            var gate = doors.Single(d => d.car == car && d.ExteriorSide == -1 &&
                Mathf.Abs(car.InverseTransformPoint(d.threshold.position).z - end * 8.04f) < .1f);
            var waypoints = number == 5 ? new[] {new Vector2(-.60f,5.42f),new Vector2(0,5.42f),new Vector2(0,8.04f)} :
                number == 7 ? new[] {new Vector2(-.13f,-5.21f),new Vector2(.90f,-5.21f),new Vector2(.90f,-7.65f),new Vector2(0,-7.65f),new Vector2(0,-8.04f)} :
                new[] {new Vector2(0,5.37f),new Vector2(0,8.04f)};
            Vector3 Spot(Vector2 v) => car.TransformPoint(new Vector3(v.x,1.405f,v.y));
            Pose(Spot(waypoints[0]), Vector3.back, true);
            yield return Frames(3);
            foreach (var p in waypoints.Skip(1)) yield return WalkToward(Spot(p));
            bool AimAtPull(bool aboard) => grips.Where(t => t.hinge == gate).OrderBy(t => Vector3.Distance(t.transform.position, Feet))
                .Any(t => {
                    Vector3 f = t.GetComponent<Collider>().bounds.center - walker.eyeCamera.transform.position;
                    Pose(Feet,f,aboard);
                    return PassDoorTarget.Resolve(walker.eyeCamera,walker.interactionDistance,out var h,out _) && h==gate;
                });
            Check(AimAtPull(true),"No handle sightline from the vestibule of " + car.name);
            yield return Tap(Key.E);
            Check(gate.TargetOpen, "E did not open the selected door of " + car.name);
            yield return Frames(32);
            Check(gate.IsOpen, "Door stalled on exit of " + car.name);
            // Turn in place with the same yaw as a passenger following the train. No
            // fixture movement after this point: get off, run past the stone and jump.
            Pose(Feet, Vector3.back, true);
            yield return WalkToward(new Vector3(-2.95f,1.405f,gate.threshold.position.z));
            Check(!route.Aboard, "Walked off the train but remained aboard " + car.name);
            Check(AimAtPull(false), "No platform-side grip on " + car.name);
            yield return Tap(Key.E);
            yield return Frames(28);
            Check(!gate.TargetOpen && gate.Openness < .01f, "Native E from the platform failed to close " + car.name);
            // Face the broadside of the coach while strafing in the direction of travel.
            // The soon-to-be-moving door starts shut and can only open to a real E press.
            Pose(Feet, new Vector3(1,-.34f,0), false);
            route.Advance(route.StationSeconds + Dt);
            rolling = true;
            while (gate.threshold.position.z > Feet.z - 10f) yield return null;
            float start = Feet.z, peak = Feet.y;
            bool opened = false, jumped = false;
            float margin = 0;
            for (int frame = 0; frame < 420 && !route.Aboard; frame++)
            {
                float gap = Feet.z - gate.threshold.position.z;
                if (!opened && PassDoorTarget.Resolve(walker.eyeCamera, walker.interactionDistance, out var looked, out _) && looked == gate)
                {
                    Keys(Key.D, Key.E);
                    opened = true;
                }
                else if (!opened) Keys(Key.D, Key.LeftShift);
                else if (jumped) Keys(Key.W, Key.D, Key.LeftShift);
                else if (!gate.IsOpen || gap < -.36f) Keys(Key.D);
                else if (gap < .80f)
                {
                    Keys(Key.W, Key.D, Key.LeftShift, Key.Space);
                    jumped = true; margin = gap;
                }
                else Keys(Key.D,Key.LeftShift);
                peak = Mathf.Max(peak, Feet.y);
                yield return null;
            }
            Keys();
            yield return Frames(24);
            rolling = false;
            Check(opened && jumped && route.Aboard && !walker.externalMotion && walker.transform.parent == route.consist &&
                car.InverseTransformPoint(Feet).x > -1.52f && Feet.z < start - 8f && peak > 1.47f,
                $"Failed moving {number:00} catch. Station offset={Feet.z-start:F2}, jump approach={margin:F2}, peak={peak:F2}, landed={car.InverseTransformPoint(Feet):F2}, phase={route.Phase}");
            Debug.Log($"TRAVERSAL INPUT: {number:00} seat/bed/guard room to station, Shift sprint {start-Feet.z:F1} m, reopened moving door with E, Space at +{margin:F2} m, landed inside");
        }

        static IEnumerator Recovery()
        {
            var joint = cars[6].Find("Gangway 07-08");
            foreach (bool trapped in new[] { false, true })
            {
                // The fixture recreates a prior fall. Only a keypress can return it to the platform.
                Vector3 p = new Vector3(trapped ? -.83f : -1.40f, .20f, trapped ? joint.position.z : -1107f);
                Pose(p, Vector3.left, false);
                route.ReconcileBoarding();
                Check(!route.Aboard, "Feet below a carriage still marked aboard");
                rolling = true;
                Keys(Key.W, Key.Space); yield return Frames(2);
                Keys(); yield return Frames(30);
                rolling = false;
                Check(Feet.y >= 1.34f && Feet.x < -1.97f && Mathf.Abs(Feet.z-p.z)<.15f && !route.Aboard,
                    $"Space could not recover {(trapped?"under the coupling":"at the platform face")}: {Feet:F3}");
            }
            Debug.Log("TRAVERSAL INPUT: ordinary platform mantle and screenshot-style coupling trap both recovered with Space");
        }

        static IEnumerator RearEscape()
        {
            ClockToStation();
            route.Advance(121);
            while (!route.EscapeReady && !route.Terminal) route.Advance(.5f);
            Check(route.EscapeReady, "Tail did not reach the playable escape location");
            var rear = doors.Single(d => d.kind == PassDoorKind.Escape);
            var grip = grips.First(g => g.hinge == rear);
            Vector3 feet = rear.car.TransformPoint(new Vector3(.18f,1.405f,7.42f));
            Vector3 aim = grip.GetComponent<Collider>().bounds.center;
            Pose(feet,aim - (feet+Vector3.up*walker.eyeCamera.transform.localPosition.y),true);
            Check(route.DoorAvailable(rear) && PassDoorTarget.Resolve(walker.eyeCamera, walker.interactionDistance,
                out var looked, out _) && looked == rear,"No usable E sightline to unlocked tail door from guard's room");
            yield return Tap(Key.E);
            yield return Frames(35);
            Check(rear.IsOpen, $"09 did not open at the bridge with native E: target={rear.TargetOpen}, pose={rear.Openness:F2}, aboard={route.Aboard}");
            Pose(Feet, Vector3.forward,true);
            yield return WalkToward(route.escapeLaunch.position);
            Keys(Key.W,Key.Space);
            yield return Frames(3);
            Keys();
            Check(route.Phase == JourneyPhase.Jumping && !route.Aboard, "Native Space did not leap from 09");
            for (int i = 0; i < 90 && !route.Terminal; i++) route.Advance(.25f);
            Check(route.Phase == JourneyPhase.Fell,"Unprotected rear leap survived the drop");
            Debug.Log("TRAVERSAL INPUT: separate 09 bridge door opened with E, crossed to jump platform, Space launched into canyon");
        }
    }
}
#endif
