# Sims 4 style gameplay: plan and progress

Rule 7 in `docs/RULES.md` asks for gameplay that imitates The Sims 4 (original art and words, gentle, no fail states). This file is the plan. Tick items off as they are built and note the version.

## Already there (v0.20.0)
- Orbit camera with wheel zoom, Q and E turning, WASD and arrow panning, floor switching, wall cutaway.
- Day and night cycle with a time slider.
- Decorate mode (M): pick furniture up and put it anywhere, turn it with the wheel, layouts are saved. People and pets freeze into see-through silhouettes while decorating.
- Two people and a pet that walk, sit, lie, chat and use doors. Sitting and lying follow the shape of the furniture.

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
