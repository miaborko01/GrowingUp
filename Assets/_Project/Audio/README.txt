GROWING UP - AUDIO SLOTS
========================

Put your real audio files in this folder (Assets/_Project/Audio/), then drag each clip onto the
"AudioClip" field of the AudioSource named below (Hierarchy path in the Forest scene).

Every slot except the mother's voice currently has a synthesised placeholder (ProceduralAudio
component, "Keep Existing Clip" = on). As soon as you assign a real clip, the placeholder is skipped.

MISSING - needs a real recording
  World/House/VO_Mother_GoExplore           "Go explore the forest, but come back before dark, and be careful!"
                                            (until then the line is only shown as a subtitle)

PLACEHOLDERS - replace when you have better sounds
  World/House/DoorHinge/SFX_DoorClose                     front door slamming behind the child (intro)
  World/House/DoorHinge/DoorHandle/SFX_DoorLocked         adult tries the stuck/locked door
  World/Interactions/RiverCrossing/SFX_Splash             child falls in / rock lands in the gap
  World/Interactions/Bird/Bird (robin, primitives)/SFX_BirdChirp   bird chirps / happy after eating
  World/Interactions/Dandelion/SFX_DandelionWhoosh        blowing the dandelion seeds (magical)
  World/Interactions/FairyRing/SFX_MushroomSnap           mushroom snaps into the ring
  World/Interactions/FairyRing/SFX_FairyChime             fairy ring complete
  World/Interactions/Berries/SFX_BerriesAppear            glowing berries appear on the bush
  World/Interactions/Berries/SFX_BerryCrunch              child eats a berry

MUSIC (part of the player) - or set them in Settings/AgeSettings.asset -> Child/Adult -> Music
  [BuildingBlock] Camera Rig/PlayerAge/Perception/Music_Child_Playful   playful child music
  [BuildingBlock] Camera Rig/PlayerAge/Perception/Music_Adult_Drone     adult (volume 0 = music off)

AMBIENCE (world)
  World/Ambience/Wind, World/Ambience/Birds_0..5, World/River/RiverSound_0..2
