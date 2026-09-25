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
