GROWING UP - AUDIO SLOTS
========================

Put your real audio files in this folder (Assets/_Project/Audio/), then drag each clip onto the
"AudioClip" field of the AudioSource named below (Hierarchy path in the Forest scene).

Every slot except the mother's voice currently has a synthesised placeholder (ProceduralAudio
component, "Keep Existing Clip" = on). As soon as you assign a real clip, the placeholder is skipped.

MISSING - needs a real recording
  World/House/VO_Mother_HaveFun             "Go have fun, don't be late!"
                                            (until then the line is only shown as a subtitle;
                                             the same clip is reused, with echo, in the transformation)

PLACEHOLDERS - replace when you have better sounds
  World/House/DoorHinge/SFX_DoorClose                          front door shutting behind the child (intro)
  World/House/DoorHinge/DoorHandle/SFX_DoorLocked              adult tries the stuck/locked door
  World/Forest/Mushrooms (touch me)/SFX_MushroomBling          a touched mushroom lights up
  World/Interactions/FairyRingBerries/FairyRing (mushrooms)/SFX_MushroomBling
  World/Interactions/Log/SFX_RockScrape                        dragging a rock
  World/Interactions/DandelionField/Dandelion_N/SFX_DandelionWhoosh   blowing a dandelion (one per dandelion)
  World/Interactions/RiverCrossing/SFX_Splash                  child falls in the river
  World/Interactions/BushTunnel/SFX_BranchesRustle             adult pushes the branches aside
  World/Interactions/FairyRingBerries/SFX_BerriesAppear        glowing berries appear on the bush
  World/Interactions/FairyRingBerries/SFX_BerryCrunch          eating the berry

TRANSFORMATION (part of the player: [BuildingBlock] Camera Rig/PlayerAge/Perception/TransformFX)
  VO_Mother_Echo          echo of the mother's line (empty: uses VO_Mother_HaveFun's clip; echo + cave reverb filters)
  SFX_ChildrenLaughter    children laughing (placeholder: synthesised giggles)
  SFX_ChildrenVoices      children's voices playing (placeholder: synthesised babble, faded in/out)
  SFX_TransformShimmer    magical shimmer that sinks and darkens

MUSIC (part of the player) - or set them in Settings/AgeSettings.asset -> Child/Adult -> Music
  [BuildingBlock] Camera Rig/PlayerAge/Perception/Music_Child_Playful   playful child music
  [BuildingBlock] Camera Rig/PlayerAge/Perception/Music_Adult_Drone     adult (volume 0 = music off)

AMBIENCE (world)
  World/Ambience/Wind, World/Ambience/Birds_0..5, World/River/RiverSound_0..2
