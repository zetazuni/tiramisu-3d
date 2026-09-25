# Game design: Tiramisu 3D

A 3D remake of the Tiramisu App (the 2D isometric browser game, also called Cozy Corner). Same heart, new dimension: a cozy modern two-storey house you can spin all the way around.

## Where the original lives

- Folder: `S:\Tiramisu App by Zetazuni` (private repo `zetazuni/athirah-cozy-corner`, live at https://athirah-cozy-corner.netlify.app).
- Its `CLAUDE.md` has the full architecture and `devlog.md` has 53 sessions of history. Treat those as the feature spec. When in doubt about how something should feel, check how the 2D game does it.

## What changes in 3D

- **Camera orbits 360 degrees.** Drag (or one-finger swipe) rotates around the house freely on the horizontal axis. Vertical tilt is clamped so you never go under the floor or perfectly top down (roughly 15 to 80 degrees). Scroll or pinch zooms, right-drag or two-finger drag pans. Keyboard: Q/E rotate, WASD pan, +/- zoom. Buttons for "fit whole house" and "jump to room", each easing smoothly like the 2D camera did.
- **Walls fade on their own.** The 2D game always looked from one side, so the back walls were solid and only the front glass collapsed. With a free camera, any wall between the camera and the room you are looking at fades or cuts down automatically. The old wall mode button (full / half / down) stays as a manual override.
- **Floors.** The same three views: ground floor only, upper floor active (ground veiled), whole house with the roof.
- **Real lighting.** Day and night comes from an actual sun and moon plus lamps, LED strips and downlights. It should look soft and cozy, not realistic and harsh.

## Everything to bring over (in rough priority order)

1. House: living room, kitchen, stair hall, bathroom, garage on the ground floor. Teacher's Room, Office & Library, Engineer's Room and Gym upstairs. Garden with pool, BBQ corner and front yard. Modern style: light oak floors, white walls, black steel, smoked glass.
2. Decorate: buy, place, move, rotate, store and sell furniture on a tile grid (1 tile = 1 m). Wall items. Undo and redo. 13 colour options for every piece, real paint names for cars. Per-room floor and wall styles.
3. Economy: realistic Malaysian Ringgit prices, earning from activities and passive income, 10 levels that double from 5k earned, k/M money display, 10M cap.
4. Activities: tap furniture and your character walks over and uses it (sit, lie, swim, play an instrument, drive off).
5. Athirah and Amir as characters: customisable looks, winter wardrobe, chatting with speech bubbles, patting pets, using the stairs.
6. Pets: cat, hamster, dog, bunny, panda, otter, capybara, each with coats and moods (walk, sit, groom, sleep, eat, happy) and real sounds.
7. Time: day and night, four seasons with their own weather, colours and lofi music.
8. Love letters and the mailbox, with stickers, photos and the envelope animation.
9. Shared cloud save between two devices (the 2D game uses a Netlify Function plus Netlify Blobs; reuse that idea).
10. Settings: volumes, graphics modes (Quality, Balanced, Performance, tuned for the iPad Air 5th gen), dark mode for the menus, tutorial, cozy view.
11. Intro splash "Tiramisu 3D by Zetazuni".

## Plan by phase

| Phase | Goal | Done when |
| --- | --- | --- |
| 0 | Setup: Unity project, Blender and Unity MCP, repo, notes (done, v0.0.1) | Both tools connected, repo public, notes written |
| 1 | Greybox house in Unity with the orbit camera and wall fading (done in the editor, v0.1.0; iPad touch still to try) | You can spin around the whole house smoothly on PC and touch |
| 2 | Real house and a first furniture set modelled in Blender | House shell plus about 15 pieces exported and placed |
| 3 | Decorate and economy | Buy, place, rotate, sell, save and load all work |
| 4 | People and pets | Characters and at least the cat and dog wander and use things |
| 5 | Day, night, seasons, music | Lighting and music change through the day and year |
| 6 | Love letters and cloud sync | Two devices share one house |
| 7 | Polish and builds | Runs well on the iPad and PC, deployed when Amir says so |

## Open questions (decide with Amir, then record the answer here)

- **Target platform.** Assumed for now: WebGL build hosted on Netlify, so it still opens on the iPad and PC with no install, like the 2D game. Windows desktop build is the fallback.
- **Carry over 2D saves?** Probably not worth it (different world), but furniture ids will match the old ones just in case.
- **Art style detail.** Assumed: stylised low poly with soft pastel colours, flat or gently graded materials from a shared palette so the 13 colour options stay cheap.
