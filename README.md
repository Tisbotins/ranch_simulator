# Ranch Simulator

An interactive ranch harvesting and combat game built with Unity.

## Features

- Runtime-built ranch world
- Single-player ranch progression
- Harvesting, bottling, selling, shops, upgrades, and save/load support
- Combat, waves, enemies, bosses, equipment, traps, and class progression
- Two-player direct multiplayer with an authoritative host

## Unity Version

Open the project with Unity `6000.5.0f1`.

## Multiplayer

Ranch Simulator supports two direct two-player modes:

- **LAN**: the host and guest are on the same local network.
- **Online Direct**: the guest connects straight to the host over the internet.

Online Direct does not use a dedicated game server or relay. The host computer must be reachable from the internet:

1. The host starts **Host Online Ranch**.
2. The host forwards TCP port `7777` on their router to the host computer.
3. The host allows the game through their firewall.
4. The guest enters the host's public IP or DNS name, optionally with a port, such as `203.0.113.10:7777`.
5. The guest starts **Join Online Ranch**.

The host owns the ranch state, waves, rewards, and save file. The guest can move, see the host and enemies, and make basic attacks against host enemies.

## Controls

- `Enter`: start single player from the title screen
- `F10`: disconnect multiplayer and return to the title screen
- `Z`: save
- `X`: load
- `Left Click` or `Space`: guest attack in multiplayer
- `Q`: guest heavy attack in multiplayer

## Project Layout

- `Assets/Scripts`: gameplay systems and runtime world/UI generation
- `Assets/Scenes`: Unity scenes
- `Assets/Resources/Prefabs`: runtime-loaded player model prefabs
- `Packages`: Unity package manifest and lock file
- `ProjectSettings`: Unity project settings
