# Interior floor survey

Both measurements use the passenger from the saved `WinterbournePass` scene. The original layout is from commit `720baba`. The expanded scene includes the shelving, search containers, side access fittings and rescue equipment. This survey ran in Unity 6000.2.10f1 against the delivered scene with SHA-256 `4345811189ab50fd150416a3aaf89c9f5d7f4d0cfc6f4781411337f9ee49f15c`.

| Car | Coupling pitch (m) | Original reachable floor (m²) | Expanded reachable floor (m²) | Ratio |
|---|---:|---:|---:|---:|
| 01 | 18.00 | 3.98 | 11.35 | 2.852× |
| 02 | 12.00 | 5.94 | 12.34 | 2.077× |
| 03 | 29.40 | 31.74 | 66.65 | 2.100× |
| 04 | 29.40 | 23.86 | 53.44 | 2.240× |
| 05 | 29.40 | 27.31 | 60.12 | 2.201× |
| 06 | 29.40 | 21.78 | 57.10 | 2.622× |
| 07 | 29.40 | 22.91 | 55.87 | 2.439× |
| 08 | 29.40 | 22.46 | 54.40 | 2.422× |
| 09 | 29.40 | 29.91 | 64.86 | 2.169× |

## Method

Open the saved scene in Unity and sample the interior deck on a 0.10 m square grid. At each cell, raycast down to an interior deck collider and test the standing player's 1.72 m tall, 0.22 m radius capsule against the actual solid geometry. Flood the resulting cells from the car's gangway, using four orthogonal neighbours. A valid cell contributes 0.01 m². The locomotive is measured from its tender end. Internal doors are open. The coal bunker, boiler, furniture surfaces, external boarding steps and roof decks are excluded. All nine expanded vehicles had zero disconnected eligible floor cells.

## Reproduce

Copy the Formation and Pass editor sources to an Editor folder in the authoring project. Run `Winterbourne.Editor.FleetFloorSurvey.Measure` with Unity in batch mode. It opens `WinterbournePass`, writes a `FLEET FLOOR` line for each vehicle and fails when any vehicle has less than twice its original connected cell count. The optional `-surveyScene` argument selects the free-roam Express scene, whose end fittings differ. The table reports the Pass scene.

To measure the original formation independently, open a separate copy of the original assets from `720baba` with the current survey script and pass `-baselineFloor` to record the original areas.
