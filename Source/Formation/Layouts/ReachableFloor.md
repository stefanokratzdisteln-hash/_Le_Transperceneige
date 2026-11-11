# Reachable interior floor and chest access

The **Current** column gives the walkable interior floor in the delivered `WinterbournePass` scene. It was measured in Unity **6000.6.5f1** using the scene passenger and PhysX. The **Original** column is the pre-expansion fleet from `720baba`. The **Previous** column is the first playable antique collection from `5a9d7c5`, measured in Unity 6000.2.10f1. The difference between editions includes the floor occupied by the new cabinet fittings.

Delivered scene SHA-256: `d163be5f58411b4f575f8925f981aea888c5b3abe871996e8861f960fb69f709`.

| Car | Coupling pitch (m) | Original (m²) | Previous (m²) | Current (m²) | Change vs previous (m²) | Current / original |
|---|---:|---:|---:|---:|---:|---:|
| 01 | 18.00 | 3.98 | 11.35 | 11.35 | 0.00 | 2.852× |
| 02 | 12.00 | 5.94 | 12.34 | 12.34 | 0.00 | 2.077× |
| 03 | 29.40 | 31.74 | 66.65 | 66.11 | -0.54 | 2.083× |
| 04 | 29.40 | 23.86 | 53.44 | 53.04 | -0.40 | 2.223× |
| 05 | 29.40 | 27.31 | 60.12 | 59.62 | -0.50 | 2.183× |
| 06 | 29.40 | 21.78 | 57.10 | 57.10 | 0.00 | 2.622× |
| 07 | 29.40 | 22.91 | 55.87 | 55.87 | 0.00 | 2.439× |
| 08 | 29.40 | 22.46 | 54.40 | 54.40 | 0.00 | 2.422× |
| 09 | 29.40 | 29.91 | 64.86 | 64.17 | -0.69 | 2.145× |

## Survey method

The survey samples the interior deck on a 0.10 m grid after opening the internal passage doors. Each eligible cell needs a physical floor within 0.059 m of the nominal deck height and enough space for the standing passenger capsule (1.72 m tall, 0.22 m radius). A four-neighbour flood from the gangway counts 0.01 m² per reachable cell. The locomotive is surveyed from the tender end. The measurements exclude roof decks, exterior steps, furniture tops, the boiler and the coal bunker. All nine vehicles had zero disconnected eligible cells in the current run.

## Access to the collection

In the same saved scene, 66 authored item positions cover all 21 valuables, including the five grand prizes authored inside each of three locked cabinets. The check includes the disabled alternatives, verifies all 15 prize/cabinet combinations and checks the three possible starting watch sightlines. The six new chests were also tested by finding a standing-height approach from the vehicle gangway or the Fellbridge platform entrance to the **front** of the mechanism. Each successful floor position gives a standing line of sight to initiate the interaction and a crouched line of sight at the lock. This separate check covers both station cases as well as cars 03, 04, 05 and 09.

## Reproduce

Copy the Formation and Pass editor sources into an Editor folder in the authoring project. Run the following Unity batch methods against the saved scene.

- `Winterbourne.Editor.FleetFloorSurvey.Measure` reports each connected area and disconnected cell count. It fails if any car falls below twice its original area. The optional `-surveyScene` argument selects the free-roam Express scene.
- `Winterbourne.Editor.PassInventoryReview.CheckAuthoredSites` checks standing and crouched access to the authored loot, the six front-facing mechanisms and the three bedside arrivals.

To repeat the original measurement, run the floor survey against the scene and vehicle assets from `720baba` in a separate project and pass `-baselineFloor`.
