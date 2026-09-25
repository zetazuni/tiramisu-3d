# Pipeline: Blender to Unity

How the tools connect and how an asset gets from Blender into the game.

## Tools

| Tool | Version | Where |
| --- | --- | --- |
| Unity Editor | 6000.6.3f1 (Unity 6) | `C:\Program Files\Unity\Hub\Editor\6000.6.3f1` (Windows and WebGL build support installed) |
| Unity Hub | | `S:\Unity\Unity Hub` |
| Blender | 5.2.2 LTS | Blender MCP add-on v1.7 |
| uv / uvx | | `S:\Tools\uv` (runs both MCP servers) |
| Git + Git LFS | LFS 3.7.1 | GitHub CLI at `C:\Program Files\GitHub CLI`, account `zetazuni` |

## MCP connections (how Claude drives the tools)

Both are registered in Claude Code at **user scope**, so they work from any folder.

- **Blender** (`blender`): `S:\Tools\uv\uvx.exe blender-mcp`. Add-on source is at `S:\Tools\blender-mcp`. To connect: open Blender, press N in the 3D viewport, open the BlenderMCP tab and start the server. Claude checks with `get_addon_status`.
- **Unity** (`unity`): `S:\Tools\uv\uvx.exe --from mcpforunityserver mcp-for-unity --transport stdio` (Coplay's MCP for Unity). The bridge package `com.coplaydev.unity-mcp` is already in `Packages/manifest.json`. To connect: open this project in Unity, then go to Window > MCP for Unity and click Start Session. Claude Code has to be restarted once after the server is first registered so the tools load.
- **Unity transport must be Stdio.** In the MCP for Unity window, set Transport to `Stdio`, then Start Session. Do not use the setup wizard's "Configure" button for Claude Code: it switches Unity to HTTP mode and adds a `UnityMCP` HTTP entry (http://127.0.0.1:8080/mcp) that needs a separate server nobody starts, so it fails with "[WebSocket] Connection failed". If that entry shows up again, remove it with `claude mcp remove UnityMCP -s local` from this folder.
- If a tool says it cannot connect, the fix is almost always to open the app and start its session, not to reinstall. Unity's own log is `Logs/Editor.log` in this folder.

## Units and axes

- 1 Blender unit = 1 metre = 1 Unity unit. **1 floor tile = 1 m.** Ground floor wall height is 3 m.
- Blender is Z up, Unity is Y up. Export with Forward `-Z`, Up `Y`, "Apply Transform" on, scale 1.0 with "FBX All" so objects come in at scale 1 with no rotation.
- Model furniture with the pivot at the back-left bottom corner of its footprint, facing +Y in Blender (which becomes the front in Unity). That keeps grid placement and rotation simple.

## Folders

```
Blender/                 source .blend files (Git LFS), one per set: house.blend, furniture_living.blend ...
Assets/Art/Models/       exported .fbx, one file per piece, named by item id
Assets/Art/Materials/    shared palette materials
Assets/Art/Textures/     palette atlas and any real textures
Assets/Scenes/           Main.unity and test scenes
Assets/Scripts/          C# (Camera, Build, Economy, People, Pets, Time, UI, Cloud)
docs/                    these notes
```

## Naming

- Item ids match the 2D game where the piece exists there (`sofa`, `platformbed`, `marbletable`, `mazda3` ...). The id is the Blender object name, the FBX file name and the id in the item catalogue.
- New pieces use short lowercase ids with no spaces.

## Materials and colour options

- Furniture uses a small shared set of palette materials (or a palette atlas) instead of one texture per piece. That keeps draw calls low on the iPad and makes the 13 colour options a simple material or tint swap.
- Mark the recolourable part of each piece with its own material slot named `main`, so the colour picker knows what to tint.

## Workflow for one asset

1. Model it in Blender (via MCP or by hand), check it with a viewport screenshot.
2. Save the `.blend` in `Blender/`, export the FBX into `Assets/Art/Models/`.
3. In Unity (via MCP), set import settings, make a prefab, add it to the item catalogue, place it in the scene and look at it from a few camera angles.
4. Commit the `.blend`, the FBX and the prefab together.
