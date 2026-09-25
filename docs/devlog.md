# Devlog: Tiramisu 3D

## Session 1: setup (2026-09-26) · v0.0.1

- Connected Claude Code to Unity and Blender through MCP. Unity uses Coplay's MCP for Unity (server `unity`, bridge package in `Packages/manifest.json`). Blender uses the Blender MCP add-on v1.7 on Blender 5.2.2 LTS, confirmed live.
- Created this Unity 6 project (6000.6.3f1) in `S:\Tiramisu Corner by Zetazuni`. For now it is an empty project on the built-in render pipeline.
- Read through the 2D Tiramisu App notes to plan the remake. Wrote `CLAUDE.md`, `docs/RULES.md`, `docs/GAME_DESIGN.md`, `docs/PIPELINE.md` and this devlog.
- Set up git with LFS for Blender files, models, textures and audio, and pushed to the new public repo `zetazuni/tiramisu-3d`.

**Next**
- Open the project in Unity and start the MCP session, then restart Claude Code so the Unity tools load.
- Add URP, the Input System and Cinemachine (through the Unity MCP so the pipeline asset gets set up properly).
- Phase 1: greybox the house at 1 m per tile and build the 360 degree orbit camera with automatic wall fading.
- Confirm the target platform with Amir (assumed WebGL on Netlify).

## Session 2: greybox house and the 360 degree camera (2026-09-26) · v0.1.0

- Unity MCP connected (after switching the bridge to Stdio). Installed URP 17.6, made the project linear colour, and kept the legacy Input Manager so no extra input package is needed.
- **Greybox house built from the 2D floor plan** by an editor menu (Tiramisu > Build greybox house). Ground floor: living room, kitchen, stair hall with floating oak stairs, bathroom, garage. Upper floor: Teacher's Room, Office & Library, landing with a glass rail over the stairwell, Engineer's Room, Gym. Flat roof for now. Garden: deck, pool, stepping stones, mailbox spot, a few trees.
- **Orbit camera:** spin all the way around, tilt within limits, zoom, pan, with smooth easing. Mouse, touch (pinch, twist, two finger pan) and keys all work.
- **Walls cut away on their own:** any wall between the camera and the room you are looking at drops to a short stub, like The Sims. There is also a manual Auto, Always up, Always down switch. Whole house view keeps every wall up and shows the roof.
- **Floor views:** Ground floor, Upper floor (ground still visible under it), Whole house. Room buttons jump the camera to each room.
- Tested in play mode through the MCP: views from the garden side and from behind (the right walls cut each time), upper floor view, whole house view, no console errors or warnings. Found and fixed two things along the way: the ground filled the pool hole, and the front glass would not cut when the camera looked at the deck (the check point is now kept inside the house).
- Not tested yet: real touch on the iPad (needs a WebGL build), and how it feels by hand. Please have a spin in the editor.

**Next**
- Amir tries the camera in the editor (press Play) and says how it feels.
- Start the Blender side: house shell and the first furniture set in the soft low poly style.
- Decide the target platform (still assuming WebGL on Netlify).

## Session 3: film look, first furniture, Unity guide (2026-09-26) · v0.2.0

- **New rule 5:** lighting and reflections must look exclusive and movie like. Added to `docs/RULES.md` and the design doc. Two decisions came with it: models use realistic materials instead of blocky low poly, and the house shell stays generated in Unity while Blender makes everything inside it.
- **Film look:** HDRI sky from Poly Haven (CC0) for sky, ambient light and reflections. Soft 4096 shadows with 4 cascades, ambient occlusion, Forward+ lighting, a warm light and a reflection probe in every room, filmic ACES tonemapping, bloom, warm grading with cool shadows, vignette, light film grain and depth of field that follows what you look at. Floors, glass and water got shinier so the reflections show. First attempt was badly overexposed. Tuned by eye in play mode (sun 1.3, ambient 0.55, exposure -0.3).
- **First Blender furniture:** modern three-seat sofa with separate cushions and a walnut plinth, marble coffee table with a walnut shelf and black steel legs, and a two-tone rug. They're in `Blender/furniture_living.blend` and exported as FBX. Unity links their materials by name to proper furniture materials and places them in the living room from a small layout table. Found and fixed two things: the sofa faced the wall (Blender -Y arrives as Unity +Z, now documented), and the rug was hidden inside the 2 cm floor plate.
- **`docs/UNITY_GUIDE.md`:** a beginner's guide to the Unity window, moving around the Scene view, testing in Play mode, the game controls, a test checklist, how the project is built and a troubleshooting table.
- Tested in play mode through the MCP (overview, low cinematic angle, living room close up), no console errors or warnings.

**Next**
- Amir runs through the test checklist in `docs/UNITY_GUIDE.md` and says how the camera and the look feel.
- Realistic textures for the architecture (oak planks, concrete, grass, tiles) from Poly Haven, since flat colours are now the weakest part of the picture.
- More living room and kitchen furniture in Blender.
- Night lighting (sun sets, room lights and LED strips glow) to really sell the film look.

## Session 4: PC first, HDRP, textures, high poly, physics (2026-09-26) · v0.3.0

- **New rule 6 (PC first):** Tiramisu 3D is now a Windows PC game, not a web game. High poly models, detailed PBR textures, realistic physics, HDRP on DirectX 12, 60 FPS at 1080p on the dev PC (RTX 4050 laptop, 6 GB) as the target. Web, WebGL, iPad and Netlify dropped. Rules, design doc, pipeline notes and the Unity guide all updated.
- **URP replaced by HDRP 17.6.** Physically based sky with a cloud layer, volumetric fog, automatic exposure (EV 9 to 13.8), screen space reflections (also on glass), screen space global illumination, ambient occlusion, contact shadows, 4096 soft PCSS sun shadows, lights in real units (sun 100000 lux, 900 lumen room lights), ACES and a warm grade, TAA, ray tracing support switched on for a future Ultra mode. URP and its leftover assets were removed. Unity needed a restart after the switch (HDRP threw NullReferenceExceptions until then).
- **Textures:** 20 CC0 Poly Haven sets at 2K (`tools/fetch_textures.py`), tiled at true size with Planar (floors), Triplanar (walls) or UV0 (furniture) mapping. Several sets did not look like their names (leafy grass is autumn yellow, linen is blue, the boucle is plaid, the roof metal is rusty), so the script also makes a neutral greyscale copy and those surfaces take their colour from a tint. That also sets up the furniture colour options. Plaster gets reduced contrast so walls read as smooth paint.
- **High poly furniture remake in Blender:** sofa (15 parts, about 13k faces, puffy subdivided cushions, rounded arms, steel feet), marble table with rounded corners and 48 sided legs, rug with 128 tassels, two new linen throw cushions. Real UVs in metres. One child mesh per part.
- **Physics:** 90 Hz, 12/4 solver iterations, 8 surface physics materials matched by material name, fitted box collider per furniture part (PhysX convex hulls are capped at 256 polygons, too few for high poly parts), real masses. Cushions drop onto the sofa at start and settle. Click something to shove it (`PhysicsPoke`). Tested: a shoved cushion flew 2 m and came to rest on the floor, the 70 kg sofa only slid 5 cm and rocked back.
- **Tuning found by testing:** auto exposure pinned at EV 15 looked gloomy, capped at 13.8. Physical camera depth of field kept reading a 10 m camera focus and blurred the house, replaced by manual focus ranges that follow the orbit distance. Motion blur removed (smeared every camera spin).
- About 150 FPS in the small editor Game view (880x377). Not yet measured at 1080p.

**Next**
- Measure FPS at 1920x1080 (Game view set to 1080p, or a Windows build) and add graphics modes (Ultra with ray tracing, Quality, Performance) plus DLSS if the NVIDIA package works.
- One leftover "missing script" warning comes from an old URP asset, not the scene. Track it down.
- Real trees and plants (the lollipop trees are the weakest thing on screen now), a proper roof, window frames.
- More furniture for the living room and kitchen.
- Night lighting.

## Session 5: real trees, photoscanned props, graphics modes and DLSS (2026-09-26) · v0.4.0

- **Measured performance at 1920x1080** in the editor (Game view set to Full HD): the v0.3 scene ran at 65 FPS, then 54 once the new trees and props went in.
- **Graphics modes with DLSS** (NVIDIA module + dynamic resolution in HDRP): Ultra (DLSS Quality, ray traced reflections, full res SSGI), Quality (default, DLSS Quality, half res low SSGI), Performance (DLSS Balanced, no SSGI, no volumetric fog, lighter shadows). G key or the HUD button. DLSS is detected on the RTX 4050.
- **Benchmark tool** (F9): measures every mode and Quality with each effect off. It showed SSGI was the big cost (6.4 ms), so Quality now runs it at half resolution on the low preset. Final numbers at 1920x1080, DLSS on, RTX 4050 laptop, in the editor: **Performance 112 FPS, Quality 78 FPS, Ultra 44 FPS** (Ultra went from 20 to 44 by leaving static foliage out of ray tracing).
- **Photoscanned CC0 models from Poly Haven** through glTFast (`tools/fetch_models.py`, `Assets/Editor/PropPlacer.cs`): two kinds of olive-like trees, shrubs, a potted plant and a money tree, a mid-century lounge chair, side table, arm lamp, 20 encyclopedias (each its own physics body), a vase and a picture frame. Trees are huge (island_tree_02 is 46 MB and a million triangles even at 1K), so one tree file is reused.
- **Architecture:** black steel window mullions and rails on every glass wall (they cut away with the walls), a flat roof with a deep front overhang, oak soffit and black steel fascia. The lollipop trees are gone.
- The armchair first faced away from the rug: Poly Haven furniture faces -Z, the opposite of our Blender pieces. Documented in the pipeline notes.
- Project art is about 425 MB (Git LFS stores a bit more because of older versions). Worth keeping an eye on the GitHub LFS quota before adding many more big models.

**Next**
- Grass that looks like grass up close (the lawn is a flat texture; HDRP terrain details or scattered grass clumps), and something on the horizon (hills or distant trees).
- Art for the empty picture frame (something personal, original).
- More rooms: kitchen next.
- Night lighting and a day and night cycle.
- One old "missing script" warning from a URP leftover asset is still to be found.

## Session 6: stair hall fix (2026-09-26)

- **Fixed the black partition in the stair hall.** It was the "Stair spine", a full height steel slab (0.2 m wide, floor to upper floor) running down the middle of the stairs. It is now a slim steel support under each step, so the stairs really float. Rebuilt the scene and checked from the bottom of the stairs.

**Next**
- Kitchen furniture and props, then the rest of the "Next" list from session 5.
