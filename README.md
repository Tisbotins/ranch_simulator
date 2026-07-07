# Ranch Simulator

An interactive ranch harvesting and combat game built with Unity.

## Features

- Runtime-built ranch world
- Single-player ranch progression
- Harvesting, bottling, selling, shops, upgrades, and save/load support
- Combat, waves, enemies, bosses, equipment, traps, and class progression
- **The Cosmic Journey** — a post-CJ space endgame across planets with new Ranch types
- Two-player co-op multiplayer where each player has full access to the game

## Unity Version

Open the project with Unity `6000.5.0f1`.

## Multiplayer

Ranch Simulator supports three two-player modes:

- **LAN**: the host and guest are on the same local network.
- **Online Direct**: the guest connects straight to the host over the internet. This requires port forwarding.
- **Online Relay**: both players connect outward to the same relay. This does not require router port forwarding.

### Online Direct

Online Direct does not use a dedicated game server or relay. The host computer must be reachable from the internet:

1. The host starts **Host Direct**.
2. The host forwards TCP port `7777` on their router to the host computer.
3. The host allows the game through their firewall.
4. The guest enters the host's public IP or DNS name, optionally with a port, such as `203.0.113.10:7777`.
5. The guest starts **Join Direct**.

### Online Relay

Online Relay is the no-port-forwarding option. It needs a small public relay process, but neither player has to open inbound router ports.

1. Run `Tools/ranch_relay_server.js` on a public machine:

   ```bash
   node Tools/ranch_relay_server.js
   ```

2. Make sure the relay machine allows inbound TCP `7778`, or set a different relay port with `PORT=9000`.
3. Host enters the relay address, such as `relay.example.com:7778`, and a room code, such as `RANCH`.
4. Host starts **Host Relay**.
5. Guest enters the same relay address and room code.
6. Guest starts **Join Relay**.

The relay only forwards text packets between the host and guest.

### How co-op works

Both players run the **full** game. The guest is no longer a stripped-down helper:

- Each player has their **own** ranch, money, bottles, shop, upgrades, class, laboratory, Ranch Knowledge, traps, and their **own save file** (the guest saves to `RanchSimulatorGuestSave.json`, the host to `RanchSimulatorSave.json`). Extracting, shopping, changing class, and buying upgrades all work for the guest exactly as they do for the host, and never touch the other player's wallet.
- The two players **share the enemy threat**. The host spawns the waves and bosses; the guest sees those same enemies, deals damage to them through networked attacks, and takes damage from them.
- When a player's health hits zero they are **downed**, not killed — the session keeps running. A teammate can stand close and **hold `E`** to revive them. If nobody reaches them in time, a downed player auto-recovers after 30 seconds, so the game can never soft-lock (including when the host goes down).

Because progression is per-player, each machine simulates its own world (tree growth, empire buildings, area access, passive income); the players share their avatars, the enemy waves, and the revive flow.

## The Cosmic Journey (endgame)

Defeating CJ, the Ultimate Ranchenator, is no longer the end of the game — it opens the **Cosmic Journey**. A rift tears open, the Ranch Rocket launches, and the main character travels the galaxy, Mario-Odyssey style, discovering a new type of Ranch on every planet:

- **Verdant Moon** — Mint Ranch
- **Ember Reach** — Ember Ranch
- **Frost Halo** — Frost Ranch
- **Nebula Bazaar** — Prism Ranch
- **The Cosmic Core** — Cosmic Ranch (final planet)

Any Ranch you harvest, bottle, or automate on a planet is refined into **rocket fuel**. Fill the tank, then open the Ranch Rocket console (press **`J`**) and **launch** to the next world. Each planet re-themes the sky and the Ranch Tree and advances the crew story — including Drew's pull toward the Core. On **The Cosmic Core**, confront **Cosmic CJ**, who controls all the Ranch in the galaxy; defeating him is the true ending. Journey progress is saved.

## Controls

- `Enter`: start single player from the title screen
- `F10`: disconnect multiplayer and return to the title screen
- `Z`: save
- `X`: load
- `J`: open the Ranch Rocket console (after the Cosmic Journey unlocks)
- `Hold E` near a downed teammate: revive them (multiplayer)
- `Left Click` or `Space`: guest attack against shared enemies in multiplayer
- `Q`: guest heavy attack in multiplayer

The guest plays with the same controls as the host (movement, `E` to extract/interact, `P` shop, `K` knowledge, class and laboratory menus, `[` / `]` to change bottle, traps, etc.).

## Project Layout

- `Assets/Scripts`: gameplay systems and runtime world/UI generation
- `Assets/Scenes`: Unity scenes
- `Assets/Resources/Prefabs`: runtime-loaded player model prefabs
- `Packages`: Unity package manifest and lock file
- `ProjectSettings`: Unity project settings
