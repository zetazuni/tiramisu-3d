# Tiramisu 3D: start here

A cozy 3D house game made by Amir Ariffin (Zetazuni) for Athirah. It is a 3D remake of the 2D Tiramisu App, with a camera that spins a full 360 degrees around the house. Models are made in Blender, the game is built in Unity.

If you are a new conversation or a new account, read these in order before doing anything:

1. **`docs/RULES.md`**: Amir's rules. Always follow them. (Short version: keep these notes updated, public repo so no secrets, everything on S:\, human wording with no em dashes.)
2. **`docs/GAME_DESIGN.md`**: what the game is, what comes over from the 2D version and the phase plan.
3. **`docs/PIPELINE.md`**: how Blender and Unity connect through MCP, units, folders, naming and the asset workflow.
4. **`docs/devlog.md`**: what happened in each session and what is next. The last entry tells you where things stand.

## Quick facts

- Project folder: `S:\Tiramisu Corner by Zetazuni` (this Unity project root).
- Repo: https://github.com/zetazuni/tiramisu-3d (public, branch `main`, Git LFS for models, textures and audio).
- Unity 6000.6.3f1, Blender 5.2.2 LTS, both driven by Claude through MCP.
- The original 2D game is at `S:\Tiramisu App by Zetazuni`. Its `CLAUDE.md` is the detailed feature reference.
- Current version: **0.1.0** (greybox house with the 360 degree camera and wall cutaway, phase 1).
- Main scene: `Assets/Scenes/Main.unity`. It is generated, see "Greybox builder" below.

## Keeping these notes alive

- End of every session: add a devlog entry (what changed, how it was tested, what is next) and bump the version above if a release happened.
- If a rule, tool path, folder or decision changes, fix the doc that describes it in the same commit.
- Architecture notes for the Unity code go in a section below once there is code to describe.

## Architecture

One short paragraph per system. Keep these accurate as the code changes. All runtime code is in the `Tiramisu` namespace.

- **World coordinates:** 1 tile = 1 m. X runs along the house (0 living room end, 30 garage end), Z goes from the back wall (0) to the front glass (8) and on into the garden (up to about 22), so the garden is toward +Z. Ground floor top is y 0, the upper floor top is y 3.3 (`UPY`), walls are 3 m high, the slab under the house is 0.3 m and the garden ground sits at y -0.3. This matches the 2D game's grid (2D x = X, 2D y = Z) so furniture positions can be copied across.
- **Render setup:** URP 17.6 (`Assets/Settings/Tiramisu_URP.asset` + `Tiramisu_Renderer.asset`), linear colour, legacy Input Manager (no Input System package; mouse, touch and keys all go through `UnityEngine.Input`).
- **Greybox builder** (`Assets/Editor/GreyboxBuilder.cs`): menu **Tiramisu > Build greybox house** rebuilds `Main.unity` from scratch (house, garden, sun, ambient light, camera, game object) using materials in `Assets/Art/Materials/Greybox`. **Tiramisu > Set up render pipeline** makes and assigns the URP asset. Change the layout numbers in the builder and rerun it, do not hand-edit the greybox scene. `Wall()` builds a wall line with door gaps (and lintels above 2.3 m), `Room()` makes a floor plate plus a `RoomMarker`. Once Blender models replace the greybox, the builder will be retired or cut down to the parts still needed.
- **Camera** (`Scripts/CameraRig/OrbitCamera.cs`): orbit around `pivot` with `yaw` (free 360), `pitch` (12 to 85) and `distance` (3 to 70). Input writes to private targets (`tPivot/tYaw/tPitch/tDist`) and the real values ease toward them every frame. Left drag spins (after a 6 px threshold, otherwise it counts as a click in `ClickedThisFrame` for picking furniture later), right or middle drag pans, wheel zooms; touch has one finger spin, two finger pinch, pan and twist. `FocusOn`, `FitHouse`, `SetPivotHeight` are what other scripts call. `IsOverUi` is set by the HUD so drags on buttons do not move the camera.
- **Floors and walls** (`Scripts/House/HouseView.cs`, `WallCutaway.cs`): `HouseView` has the view (Ground, Upper with the ground still visible below, Whole house with the roof) and the wall mode (Auto, Up, Down). Keys 1/2/3 switch floors, Tab cycles wall mode, F fits the house. Every straight wall line is a `WallCutaway` with its pieces as children; in Auto mode a wall cuts down to a 0.25 m stub when the camera and the focus point are on opposite sides of it. The focus is the camera pivot clamped inside the house footprint (`houseMin/houseMax`) so the front glass still cuts when you look from the garden. Whole house view keeps every wall up.
- **Rooms** (`Scripts/House/RoomMarker.cs`): one per room with display name, floor, size and order; the HUD lists the rooms of the active floor and the camera jumps to them.
- **HUD** (`Scripts/UI/HouseHud.cs`): temporary IMGUI panel (floors, rooms, wall mode, fit, a hint line). Scales with screen height. Will be replaced by the real UI.
