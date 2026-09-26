# Sims 4 style gameplay: plan and progress

Rule 7 in `docs/RULES.md` asks for gameplay that imitates The Sims 4 (original art and words, gentle, no fail states). This file is the plan. Tick items off as they are built and note the version.

## Already there (v0.20.0)
- Orbit camera with wheel zoom, Q and E turning, WASD and arrow panning, floor switching, wall cutaway.
- Day and night cycle with a time slider.
- Decorate mode (M): pick furniture up and put it anywhere, turn it with the wheel, layouts are saved. People and pets freeze into see-through silhouettes while decorating.
- Two people and a pet that walk, sit, lie, chat and use doors. Sitting and lying follow the shape of the furniture.

## Done in v0.21.0: live mode and the pie menu (milestones 1 and 2, first version)
- Click a person (or pick them in the panel at the bottom) and a green diamond floats over their head. Click the floor and they walk there (a pink ring shows where). Click a piece of furniture you can sit or lie on and a round menu offers "Sit down" or "Lie down" plus "Go here". Click another person: "Talk to", "Play as", "Go there". Click a pet: "Pet", "Go there".
- Orders queue, a person finishes them before going back to their own plans (about 12 seconds later), and the "Stop" button next to the panel cancels them.
- Space pauses, 1 2 3 change the speed (1x, 2.5x, 5x), Pause and speed buttons bottom right. WASD and Q/E turn as before, middle mouse drag turns the camera (like the game), floors moved to Page Up, Page Down and Home.
- Not there yet: more actions in the pie menu (eat, cook, watch, swim, shower), menus for doors and windows, a "Move" entry that starts decorating, queue view with icons, right click on the ground.

## Also done: four seasons (v0.23.0, not in the original list, asked for by Amir)
- `SeasonCycle`: spring, summer, autumn, winter. Each changes the lawn, hedges, tree leaves, flowers, how high the sun climbs, the length of the day, warmth of the light, clouds and haze, and what falls from the sky (petals, fireflies at night, golden leaves, snow). Trees are bare in winter. Use the "Season" button under the clock, or let time run: a season lasts 3 days.

## Milestones (in order, each one playable on its own)
1. **Live mode controls.** Click a person to select them (plumbob style marker over the head), a portrait panel at the bottom left, click on the world to send the selected person there, pause and speed buttons (space, 1, 2, 3), WASD panning like the game.
2. **Pie menu.** Click an object: a round menu with the actions it offers (sit, lie, look, use, move). Actions queue and people walk over and do them with the right pose. Objects get an action list (data driven, `UseSpot` grows into an `Interaction`).
3. **Needs and mood.** Hunger, bladder, energy, fun, social, hygiene as bars that drop slowly. Objects fill them (fridge and kitchen for hunger, toilet, bed and sofa for energy, shower and bath for hygiene, TV, pool and books for fun, talking for social). A mood from the needs. Gentle: nobody gets hurt when a bar is empty, they just get grumpy. The autonomy already in `Character` starts choosing by need.
4. **Money.** Household funds, a price on every object, income (a small allowance that ticks up with time, or a job for Amir and Athirah), simple bills that never end the game.
5. **Buy mode.** A catalogue panel by category and room with prices, drag from the catalogue into the house, grid snapping, sell for a refund, wall hanging and surface placement rules like the game.
6. **Build mode.** Draw walls, place doors and windows, floors and wall paints, stairs, roofs, room sizes and a lot of tools. This is the biggest job: the house is built by code today and needs to become data driven first.
7. **Character status panel.** Needs, mood, moodlets, traits, wishes, relationship with the other people, skills.
8. **Polish.** Own sounds, own icons, tutorials as warm little hints.

## Notes
- Keep every screen and line of text warm and human (rule 4), no em dashes.
- The Sims 4 is the reference for behaviour, never for assets. Icons, names, sounds and art are our own.
