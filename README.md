# 3D Project - RED LOOP

![Picture6](https://github.com/user-attachments/assets/e0383dd6-f260-4bf2-b608-4130d704553e)

## Overview
This project is a Unity-based 3D application designed to showcase various assets, terrains, and interactive elements. It includes a variety of 3D models, animations, and scripts to create an immersive experience. The project structure is organized to facilitate easy navigation and scalability.

## RED LOOP — Kızıl Döngü (story)
The city clock stopped at **03:33** and the same red night has repeated ever since: the **Red Loop**. Those who gave in became rusty guardians. **Ayla**, an archer, wakes up in the same courtyard every time she falls. Tonight, a mysterious voice — **the Echo** — tells her the loop can be broken.

| Chapter | Scene | Goal |
|---|---|---|
| I — Kızıl Avlu | `robot` | Collect every time shard while dodging the guardians' red orbs. The door at the top of the stairs opens only when all shards are collected. |
| II — Yankılar Ormanı | `kk` | Find 4 memory lights in the dark palm forest. Each one reveals a piece of what happened to Ayla's little brother **Deniz**. |
| III — Eski Liman | `Old Sea Port/Dock Thing` | Follow the red light beam to **the Heart of the Loop** on the highest roof while it rains red orbs on you. |

The finale reveals who started the loop and why — and the clock finally moves to 03:34.

### How to play
- **WASD / arrows** move, **mouse** camera, **Space** jump, **Shift** sprint, **Esc / P** pause
- **Space / E / click** advances dialogue
- 3 hearts per chapter; falling off the map costs a heart and respawns you. Losing all hearts restarts the chapter ("Döngü #N").
- The yellow diamond on screen always points to your next objective.

### What was added / fixed (2026 update)
- **Game flow:** title screen with prologue, intro dialogues per chapter, chapter title cards, pause menu (resume / restart / mute / main menu / quit), game over screen with retry, and a real **ending screen** with stats and best time.
- **Story:** full Turkish narrative with typewriter dialogue and subtitles (`Assets/Scripts/RedLoop/Story.cs` — edit texts there).
- **Sound:** every sound effect and the music for each chapter is synthesized at runtime (`SoundFX.cs`): shard pickup, memory chime, hurt, enemy throw, impact, jump, UI clicks, door locked/opened, heartbeat at low health, game over, victory, heart shatter, falling orb whistle, plus 5 seamless music loops.
- **Fixes:**
  - `RuntimeMovement.cs` used `UnityEditor.Animations` → builds failed. Removed (also removed the per-frame `Debug.Log`).
  - Damage code in `Collide.cs` was commented out, so enemies could never hurt the player. Restored with invulnerability frames.
  - `CoinCollection` threw `NullReferenceException` in scenes 2 and 3 (no text assigned) and could double-count a coin.
  - One robot had no player reference → `NullReferenceException` when chasing. Robots now find the player by tag, don't spam NavMesh errors, don't tilt when looking at the player, and stop attacking during menus/dialogue.
  - Each robot's projectile template is a gravity rigidbody child that fell out of its hand at start and rolled around hurting the player. It is now hidden and only clones are thrown; thrown orbs are cleaned up.
  - The last scene had no ending (only a "You Win" text that disappeared when you walked away) and `LoadScene` would try to load a non-existent scene. Now there's a proper finale.
  - Doors no longer open before the chapter goal is done, and also trigger if you are already standing inside when the last shard is collected.
- Everything new is created at runtime by `GameManager` (auto-bootstrapped), so **scenes, the character and the red atmosphere are unchanged**.


## Game Story / Description

The main character of the game is a brave archer girl. She must survive against enemies consisting of robots and zombies. These enemies attack the character by throwing red balls, aiming to reduce her health. At the beginning of the game, the character has 3 lives, and each hit from a red ball causes her to lose one life.

<img width="524" height="304" alt="Picture1" src="https://github.com/user-attachments/assets/992ce948-477e-47b3-a472-5942dfc54575" />

![Picture5](https://github.com/user-attachments/assets/ced97127-a0c6-4eb9-8ae9-3302a866fe03)


The game takes place in a dark, dimly lit environment dominated by red tones. The level is built as a three-dimensional map filled with stairs and various structures. The player earns points by collecting coins scattered throughout the environment. When the final coin on the staircase is collected, the current scene ends and the player is transported to a new level.

![Picture4](https://github.com/user-attachments/assets/8c4bbd6a-57f4-4c05-add8-1464e53c6cb4)

<img width="1200" height="562" alt="Picture1111" src="https://github.com/user-attachments/assets/28591b32-518c-4651-9483-96339bfb79b2" />


The next level is the Coconut Palm Tree scene. In this area, lighting and camera systems are used dynamically with various techniques. A light attached to the character acts as a flashlight, illuminating the dark surroundings. As the character moves forward, the camera rotates around her, creating a cinematic and immersive experience.

![Picture2](https://github.com/user-attachments/assets/73d6301f-2ce7-4472-bfab-68dd9782f121)

![Picture3](https://github.com/user-attachments/assets/e5a99722-3d4d-4c1c-b584-94959269c506)


The game continues in this manner as the player progresses through different scenes and challenges.



## Features
- **3D Models**: Includes a collection of detailed 3D models such as coconut palm trees and old sea port assets.
- **Animations**: Predefined animations for objects and characters.
- **Terrain**: Custom terrains created using Unity's terrain tools.
- **Audio**: Background music and sound effects to enhance the user experience.
- **Prefabs**: Reusable prefabs for efficient scene building.
- **Scripts**: Custom scripts to add interactivity and functionality.

## Project Structure
- **Assets/**: Contains all the project assets, including models, textures, animations, and scripts.
  - **Animations/**: Stores animation files.
  - **Audio/**: Contains audio files for the project.
  - **Coconut Palm Tree Pack/**: Includes 3D models of palm trees.
  - **Scenes/**: Unity scenes for the project.
  - **Scripts/**: Custom C# scripts for interactivity.
  - **Textures/**: Textures used in the project.
- **Library/**: Unity's internal cache and metadata.
- **Logs/**: Logs generated during development.
- **Packages/**: Package dependencies for the project.
- **ProjectSettings/**: Configuration files for the Unity project.
- **UserSettings/**: User-specific settings.

## Requirements
- Unity Editor 2023 or later.
- A system capable of running 3D applications.

## Setup Instructions
1. Clone the repository to your local machine.
2. Open the project in Unity Editor (2023 or later).
3. Allow Unity to load and resolve dependencies.
4. Open the desired scene from the `Assets/Scenes` folder.
5. Press the Play button to run the project.

## Contribution
Feel free to contribute to this project by adding new features, fixing bugs, or improving existing assets. Please follow the standard Git workflow:
1. Fork the repository.
2. Create a new branch for your feature or bugfix.
3. Commit your changes with clear messages.
4. Submit a pull request.


