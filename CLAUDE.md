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
- Current version: **0.0.1** (setup only, nothing playable yet).

## Keeping these notes alive

- End of every session: add a devlog entry (what changed, how it was tested, what is next) and bump the version above if a release happened.
- If a rule, tool path, folder or decision changes, fix the doc that describes it in the same commit.
- Architecture notes for the Unity code go in a section below once there is code to describe.

## Architecture

Nothing yet. Fill this in as the scripts get written (one short paragraph per system: camera, build grid, economy, people, pets, time, UI, cloud).
