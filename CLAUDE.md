# VR project: "Growing Up" — one forest, two ages

University assignment (Interaction Models). HMD VR experience where the SAME virtual environment is experienced through two Virtual Subjectivenesses (VS): a CHILD and an ADULT. The environment (geometry, objects, house, forest, river, path) must NEVER change between the two. Only the player changes: body size, mappings, behaviours, and how the world is perceived (camera post-processing + audio filter are part of the player, not the world).

Key idea: "The forest didn't change. Growing up did." The house was always old and moldy; the child's perception made it magical.

## Tech rules
- Unity 6.6, URP (Universal 3D), target Meta Quest. Built the way the course teaches: **Meta XR All-in-One SDK + Meta Building Blocks**. Do NOT use the XR Interaction Toolkit or XR Origin.
- Player rig = **Camera Rig building block (OVRCameraRig)** + Controller Tracking. Use Building Blocks / Interaction SDK first (Grab Interaction, Locomotion/Teleport). Write custom scripts only where no block exists (age change, perception filters, task logic).
- Grabbable objects: Mesh Collider (Convex) + Rigidbody + Grab Interaction block (or right-click → Interaction SDK → Add Grab Interaction).
- No Passthrough — full VR.
- Low-poly style. Use the imported nature pack(s) in Assets/. Build missing simple pieces from primitives with flat-colour materials.
- Quest-friendly: few real-time lights, simple materials, low poly counts.
- To change age, scale the player rig — NEVER scale or swap the environment.
- Put every age-dependent value in ONE ScriptableObject with a "Child" and an "Adult" profile: rig scale, eye height, walk speed, step/jump, reach, URP Volume profile, audio filter settings, music.
- Work in small steps. After each step: check the Console, fix errors, summarise what changed, then STOP so I can test (Meta XR Simulator or headset) and commit.

## The story (full plan — only Phase 1 is being built now)
1. The player starts as a CHILD stepping out of the front door of their house into a forest.
2. A linear path leads through the forest and across a river. As a child everything feels magical: warm saturated light, sparkles/fireflies, playful music, louder birds. The child explores and does a few small tasks (to be defined).
3. At the end of the path, after the child's experiences, the player GROWS UP into an adult (perspective change).
4. The adult walks back along the SAME path, same forest, same river. Everything now feels depressing: desaturated, cold, foggy, quiet, no sparkles. Tasks along the way (to be defined, possibly mirrored/different).
5. The adult reaches the house again but cannot go in. Now they see it as it is: old and moldy. End.

## The two Virtual Subjectivenesses
CHILD (VS1)
- Rig scale ≈ 0.65 (eye height ≈ 1.05 m).
- Walk speed ≈ 1.6 m/s (lively).
- Perception: URP Volume "Magic" — warm colour grading, higher saturation, bloom; sparkle/firefly particles attached to the player's view; bright playful music; nature sounds louder.
- Can fit through low gaps (e.g. under a bush or fallen log) that the adult cannot.

ADULT (VS2)
- Rig scale = 1.0 (eye height ≈ 1.65 m).
- Walk speed ≈ 1.1 m/s (heavier, slower).
- Perception: URP Volume "Grey" — desaturated, cold tint, light fog/vignette, no bloom; no sparkles; music off or low drone; sounds muted/low-passed.
- Cannot fit through the child's gaps; cannot enter the house (door won't open).

## Phase 1 — build this now
Goal: the walkable world, the house, the path, and a working child → adult switch with both perception profiles.

Step 1 — World layout
- Ground (terrain or large plane) with one clear linear path (a loop isn't needed: out and back along the same path).
- The HOUSE at the start of the path. Use an imported house model if available in Assets; otherwise build a simple cottage (walls, roof, door, windows). Give it a weathered/moldy look already (dark stains, cracked/greyish materials) — it must look the same in both ages; the child's filter will just make it read as warm and cosy.
- Player spawns just outside the front door, facing the path (as if they just walked out).
- Forest along the path: trees, rocks, bushes, logs, mushrooms, flowers from the imported packs. Dense enough to feel like a forest; keep the path clear.
- A river crossing the path about midway, with a way across (stepping stones or a log bridge).
- An end-of-path clearing (the "growing up" spot), e.g. a big old tree.
- Soft daylight. Ambient sound (wind, birds, river).

Step 2 — Child player
- Camera Rig + locomotion blocks, set to the CHILD profile at start.
- Collides with trees, rocks, house.

Step 3 — Perception profiles
- Two URP Volume profiles ("Magic" and "Grey") and two audio setups, applied by the age profile.
- Child sparkles/fireflies particles that only exist for the child.

Step 4 — Growing up
- A trigger in the end-of-path clearing starts the transition (later it will require finishing the child tasks).
- Transition (~3 s): fade or slow blend, rig scales 0.65 → 1.0 with feet kept on the ground, Volume blends Magic → Grey, music fades out, sounds become muted.
- For testing: a debug key/button that toggles Child ↔ Adult.

Step 5 — The house door
- Child: the door is open at the start (they just came out of it).
- Adult: the door is closed and won't open — trying it plays a stuck/locked sound. Hook for the ending (fade to black / end text).

## Later phases (do NOT build yet)
- Child tasks along the path (to be defined)
- Adult tasks on the way back (to be defined)
- Requiring the child tasks before growing up
- Ending sequence at the house
