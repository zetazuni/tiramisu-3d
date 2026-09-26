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

## Session 6: stair hall fix and the kitchen (2026-09-26)

- **Fixed the black partition in the stair hall.** It was the "Stair spine", a full height steel slab (0.2 m wide, floor to upper floor) running down the middle of the stairs. It is now a slim steel support under each step, so the stairs really float. Rebuilt the scene and checked from the bottom of the stairs.

- **Kitchen furnished** (v0.5.0). New script `tools/blender_kitchen.py` builds and exports seven pieces in one go (run it inside Blender): a 4.4 m counter run (charcoal cabinets, marble worktop and splashback, sink with tap, glass cooktop with hood, wall units, oven tower with two ovens, brass handles), a stainless fridge, a waterfall marble island with a walnut slat front, three bar stools, a walnut dining table, four dining chairs and three brass pendant lamps with glowing bulbs and a warm light each. Between them about 75k triangles. Placed by the builder's `Layout` table; fixed pieces (run, fridge, island, pendants) are static, stools, table and chairs are real rigid bodies.
- Tested: rebuilt the scene, no errors, looked from the garden and from inside the kitchen. Not yet played by hand, so please try shoving a stool.

- **Dining chairs redone.** The first ones had posts poking above a floating back. Now a padded seat, walnut legs and rear posts flush with a curved back pad.
- **Bathroom furnished** (v0.6.0): walk-in shower with glass and rain head, floating walnut vanity with a vessel basin, mirror, wall hung toilet, freestanding tub, towel ladder, bath mat, washer, dryer and laundry basket, plus a plant.
- **Garage furnished:** a red sedan and a silver MPV (built in Blender by lofting cross sections, with subdivision, boolean wheel arches, glass, alloy wheels, lights), EV charger, workbench with pegboard, metal shelving with bins, tool chest and a bicycle. `tools/blender_bathgarage.py` builds and exports all 17 pieces.
- Found by looking: bike wheels lay flat (the torus helper only made flat rings, now has an `axis` option).
- Tested: rebuilt the scene, checked the bathroom, garage and dining corner from inside. The car window edges are a bit jagged up close, worth a proper remodel later.

- **Shower fixed:** now a fully glazed cubicle (fixed front panel, hinged door with handle, side panel and a glass ceiling, black frames), the left and back sides are the room walls. The steel bar from the wall to the glass is gone. Moved 23 cm left so it sits against the wall.
- **Cars:** much brighter tail lights (emission x5, wrap-around light bar) and a red point light behind each light cluster.
- **Props pass** (v0.7.0): `tools/blender_props.py` builds 11 small props (fruit bowl, bread on a board, mugs, espresso machine, utensil crock, herb pot, towel stack, candles, cardboard boxes, paint cans, spare tyres). The builder got a `Tabletop` table so things can stand on surfaces at a given height. All are real rigid bodies, so they can be knocked about.

- **Car windows fixed:** the cabin now has real edge loops at the window sill and the top of the glass, and glass or paint is picked per face before subdivision, so the window edges are clean. Also 4 pillar stretches (A, B, C) stay paint.
- **Upper floor furnished** (v0.8.0), 27 new models from two scripts: Teacher's Room (bed with pillows and throw, nightstands and lamps, wardrobe, bookcase full of books, desk and chair, globe, chalkboard, world map), Office & Library (two bookcases, desk with monitor, two chairs, file cabinet, uplight, whiteboard, Poly Haven armchair and plants), Engineer's Room (charcoal bed, electronics bench with monitors and an oscilloscope, robot arm, 3D printer with filament, beanbag), Gym (two treadmills, dumbbell rack, bench with barbell, two spin bikes, punching bag on chains, yoga mats, water dispenser, three wall mirrors). New materials: bedding, leather, glowing screens, chalk and whiteboard, rubber, plastics, map colours, book colours and more.
- **Tested in Play mode:** 50 rigid bodies, none moving or fallen after settling, no console errors. Looked at all four upper rooms and the gym from the garden.
- Unity note: `Tiramisu > Build greybox house` refuses to run while Play mode is on, so stop Play before rebuilding.

- **Feedback round from Amir (v0.9.0).** Taller plants: the 27 cm succulent pots are gone, replaced by money trees (1.9 m) and the 1.8 m plant. The shower still had an open side: Blender +X arrives as Unity -X, so the glass was on the wall side. Mirrored the shower in Blender (documented in CLAUDE.md), it is now closed on every side. The tail lights are now proper blocks with a dark bezel, sticking out of the body, emission x12.
- **Kitchen and bathroom props moved:** for the same reason the counter run is mirrored (oven tower next to the fridge, cooktop on the left), so the espresso machine, utensils and herbs moved, and the towel stack went on the dryer.
- **Pool ripples** (new `PoolRipples`), tested by simulating 6 seconds of physics: a coffee table dropped in floats half submerged, a cushion floats on the surface, rings spread and bounce off the walls.
- **Decorate mode** (new `DecorateMode` and `Furniture`): tested the rules through the API (wall and furniture overlaps are rejected, free spots accepted, the sofa carried its two cushions to a new place and turned 90 degrees with them, the saved layout survived a restart and reset cleanly). Not tested with a real mouse, please try it.
- **Night lighting and day and night cycle** (new `DayNightCycle`, `SwitchableLight`). Warm interiors, glowing pendants and lamps, garden bollards, roof downlights, cyan pool lamps, moon and stars. First attempt was blown out (exposure floor too low), tuned to EV 4.2. Exposure adapts in about 2 seconds now.
- Unity gotcha found on the way: a MonoBehaviour must live in its own file named after the class, or scene references turn into "missing script".

## Session 7: night look fixes (2026-09-26) · v0.9.1

Amir's notes on the first night: too glowy and reflective, lights too strong and pointy, plants need a vase, cars too shiny, kitchen wall cabinet flickering.

- **The glow was the reflection probes.** They were baked in full daylight (thousands of nits). At 10 percent they still lit every rough surface like a sunny day. At night they now go to 0.02 percent, so the plants stopped glowing white and the chair went back to its real colour. Bloom and exposure compensation also drop a little at night, and emissive lamps, bulbs and screens are about half as strong.
- **Softer lights.** Room lights are now big rectangle panels in the ceiling (no more hot spots), pendants and downlights and lamps and pool lamps are small soft area lights that face where the light really goes (down, up into the ceiling, into the pool). Fewer lumens, more even.
- **Less shine:** floors, wood, marble, ceramic, steel and brass have lower smoothness. Car paint went from a mirror to satin (smoothness 0.55, metallic 0.3 to 0.35).
- **Planters:** a new `planter` model (ceramic, soil and pebbles) under all five money trees, the trees stand on it.
- **Flicker found and fixed:** the kitchen wall doors sat inside their cabinet with front faces exactly level, so the two surfaces fought (diagonal stripes, flickering when the camera moved). The doors now stand 1 cm in front. I ruled out shadows, screen space reflections, global illumination and contact shadows by switching each off in Play mode first. Also gave the sun and moon more shadow bias.
- Tested in Play mode with the time at 21:30 and at 13:00: living room, kitchen, whole house and the cars from behind.

## Session 8: doors, windows, steadier furniture (2026-09-26) · v0.10.0

- **Furniture cannot be tipped any more.** Every movable body is frozen upright (it can still turn on the spot), damped hard, and the nudge is sideways only and small. Test: 12 shoves each on the table, sofa, cushion, armchair, side table, stool and chair, all shoved at the height that tips things worst: no tilt, at most 2 cm of movement. A **Reset furniture and windows** button in the HUD is always there and puts everything back upright.
- **Room lights: several soft panels per room** (2 to 4, 64 area lights in the house) in a cooler colour, 4300 K, and 5200 K in the garage, gym, bathroom and office. The light is even, with no bright centre.
- **The money tree in the engineer's room** is centred on its trunk now (the placer used the middle of the leaves, which hang to one side). All plants use the trunk.
- **Stairwell glass removed.**
- **Sliding doors** in all eight doorways between rooms, glass in glass walls and walnut in concrete walls, black rail, long bar handle. Open on hover and for anything carrying `DoorOpener` (hooks for the people and pets to come). Tested through the API: a door slid its full 2.1 m open on hover, a test pet stepping into another door's sensor opened it.
- **Movable windows:** 11 windows in the concrete outer walls (living room, stair hall, bathroom high window, garage, teacher's room, landing, gym). Drag along the wall in decorate mode, R for the width. Tested through the API: it refuses to overlap a neighbour or the wall end, accepts a nudge, cycles the width, and a reset restores everything.
- Not tested with a real mouse: hovering over doors, dragging windows. Please try both.

## Session 9: curtains, front doors, garage shutter, paint (2026-09-26) · v0.11.0

- **Small things stick to their spot.** Everything under 3 kg (cushions, books, towels, mug, fruit bowl, vases, lamps and so on, 40 pieces) is kinematic and anchored. A click shifts it 3 cm and tilts it 5 degrees and it eases back in 200 ms. Carried by the piece it sits on in decorate mode (the cushions go with the sofa). Tested: a nudged cushion moved 0.030 m and stayed kinematic.
- **Upper floor sliding doors hid properly:** they were outside the upper floor group. Now inside it.
- **Curtains** on all 11 windows, click to draw or open. Found by testing that toggling did nothing after Play started (the editor built ones were not known to the script), fixed, then tested again.
- **Gym light** 3.6x and a lighter floor, garage 1.3x. Compared before and after at night from the same spot.
- **Front doors:** pairs of walnut hinged doors in a black frame in the garden facade (living room, kitchen, bathroom, no door for the garage), they swing out. Opening tested through the API (both leaves swing 100 degrees).
- **Garage shutter door** in the end wall opening onto a new driveway (apron, ramp) and a road with kerbs and lane lines along the east side. Rolls up on hover or when something with a `DoorOpener` comes near. Tested through the API: it rolled up to 6 percent of its height.
- **Room paints:** every room has its own colour, both faces of the interior walls, both the plaster on the outer walls. Looks right from the upper floor view (coral gym, slate engineer, blue office, blush teacher).
- Not tested with a real mouse: clicking curtains, hovering the new doors. Please try.

## Session 10: front yard, LED strips, marble kitchen (2026-09-26) · v0.12.0

- **Floating cushions fixed.** Found by simulating them in Play mode: they settle with their bottom at 0.578 m on a seat that tops out at 0.58 m, but the layout placed them at 0.71 and 0.75 m. Placed at 0.612 (their origin) now.
- **Kitchen floor:** large format polished marble (Poly Haven `marble_01`). I looked at the Grey Cartago sets first but they are rusty brown and grey, not something for a kitchen, so those downloads were removed again.
- **Garage front:** a zig-zag glass door that folds to both sides. Cars parked in a row nose to the shutter. Tested through the API: the panels fold, the doorway clears.
- **Front yard populated** like the 2D game's yard (20 new models, about 110k triangles): see CLAUDE.md. The beach ball floats in the pool and rings spread around it.
- **Light strips and string lights**, both only glow at night (146 glowing surfaces, 71 lights). The LED strips on the roof were first left floating in the sky in the ground floor view, they now belong to the roof.
- **Curtains fluffier,** two rounds of thickness and depth tuning. Please judge them by eye and tell me if you want them even fuller.
- Not tested with a real mouse: hovering the garage folding door, dragging the new yard furniture.

## Session 11: pool, fountains, skylights, night fixes (2026-09-26) · v0.13.0

Amir's notes: night too dark outside, skylights, lounger backrest upside down, ball should float submerged and roll with the ripples, flower bed soil, floating cushions, floating weeds, black marble pool with a jacuzzi and a wall fountain, brighter LED strips, a real moving round fountain.

- All done, see CLAUDE.md for how. Checked by looking (day, night, close ups) and by scripts: the ball drifted and turned, five ripple surfaces run, cushions settle within 2.4 cm after the first pass (threshold tightened to 4 mm after).
- **Cushions:** my earlier fix was right for a fresh build, but an old saved layout on Amir's machine could still hold the old floating heights. Small things now save relative to their host and always settle on what is under them.
- **Weeds:** the scanned shrub has stem tips above its lowest leaf, so it hung in the air. Sunk into the lawn.
- Not tested by hand: dragging the yard furniture, hovering the folding door, clicking the water.

## Session 12: fence, gates, attached small things (2026-09-26) · v0.14.0

- Weeds removed, hammock away from the pavement, bathtub moved to the side of the garage door.
- **Small things are now one piece with what they sit on.** First attempt attached them at the start of Play and left a 1.5 cm gap under the cushions, because the sofa pushes itself out of the floor in the first moments. Now it waits 1.2 s, settles on the highest surface, then attaches. Also found on the way: a host with a small thing already attached to it was refused as a host for the next one (destroying a component only takes effect at the end of the frame), so only 7 of 20 attached at first.
- **Turn buttons** in the HUD.
- **Lawn under and behind the house, fence with wall lights, front gate on the pavement axis, back gate, open garage lane.** Checked from above by day, the gates' swing direction by script (both inward).
- **Testing note:** when the Unity window is in the background it renders only when asked, so game time barely advances. Timed things (the 1.2 s wait, door opening) were checked by calling the code directly.
- Not tested by hand: gates opening by hover, the turn buttons with a mouse.

**Next**
- Amir walks round the fence by day and night and says what feels off (height, light spacing, gate style).
- People and pets (they carry `DoorOpener`), wall pieces movable along their wall, a car that drives out of the shutter, streetlights along the road, weather.
