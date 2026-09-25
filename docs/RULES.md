# Rules for this project

These are Amir's rules for Tiramisu 3D. Read them before doing anything, every session, on any account. They win over any default habit.

## Amir's rules (set 2026-09-26)

1. **Keep the markdown notes up to date.** This repo carries its own memory so work can pick up in a new conversation or on a different account without getting lost. Start at `CLAUDE.md`, which links everything else. Update `docs/devlog.md` at the end of every session and fix any note that has gone stale.
2. **Public GitHub repo.** The code lives at https://github.com/zetazuni/tiramisu-3d (public). Because it is public, never commit tokens, API keys, exported save files, the cloud room code or anything private. Check `git status` before every commit.
3. **S:\ drive first.** Every file, download, cache, tool and install goes on `S:\` unless it truly cannot (the Unity Editor itself already sits in `C:\Program Files\Unity\Hub\Editor`, leave it there). New tools go under `S:\Tools\`.
4. **Write like a person.** Anything the player sees (buttons, toasts, menus, dialogue) and anything a person reads in the back end (commit messages, docs, log lines, code comments) should sound warm and human, never like a machine wrote it. **No em dashes at all.** Use a comma, a period, a colon, brackets or the " · " separator instead. This applies to these docs too.

## Rules carried over from the 2D Tiramisu App

- **Cozy and gentle.** It is a personal game made by Amir Ariffin for Athirah. Keep it warm and pastel. No punishment, no fail states, no permadeath.
- **Keep the personal touches.** Keep the "Amir Ariffin / made for my love, Athirah ♥" signature, the love note and "Athirah" as the default name.
- **Original art only.** Do not copy copyrighted characters or sticker packs. Ask first if Amir wants a named third-party pack.
- **Claude picks the version numbers** (semver). Bump the version shown in game with each release and note it in the devlog.
- **Deploying costs credits.** Never deploy to Netlify unless Amir asks, but remind him when the live build is behind.
- **Commit and push** to GitHub after each finished piece of work, with a clear human message.
- **Ask before big, hard-to-undo steps** (deleting assets, rewriting history, changing the save format).
- **Save compatibility.** Once saves exist, new fields need defaults when loading and the save key never gets renamed.
