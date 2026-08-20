# Falling Wizard — scripts

Unity 6.3, URP 2D, new Input System only. Everything reads the project-wide actions in
`Assets/InputSystem_Actions`, which are already bound to keyboard and gamepad, so PC and
consoles work from the same code.

## Layout

| Folder | What lives there |
| --- | --- |
| `Core/` | Scene flow (`GameScenes`, `SceneLoader`), pause state (`GamePause`), options (`GameSettings`), menu-level key reads (`MenuInput`) |
| `Menus/` | `MainMenuController`, `PauseMenuController`, the shared `SettingsPanel` |
| `Cutscenes/` | `CutsceneRunner` — plays the intro, then loads Level 1 |
| `Player/` | `PlayerMotor` (movement), `FallDamage`, `Health`, `PlayerInputReader`, `PlayerCharacter`, and the power-up trio |

`Assets/Editor/` holds one-shot build commands. They only create ordinary scene objects —
delete that folder once you have run them if you want it gone.

## First-time setup

1. Open `Assets/Scenes/Main Menu.unity`.
2. **Tools ▸ Falling Wizard ▸ Build Main Menu In Open Scene**. The first run may ask to import
   the TextMeshPro essentials; accept, wait, then run it again. Save the scene.
3. **Tools ▸ Falling Wizard ▸ Create Pause Menu Prefab** (writes `Assets/Prefabs/Pause Menu.prefab`).
4. Optional, for later: open `Assets/Scenes/Cutscene.unity` and add an empty object with
   `CutsceneRunner` on it. With no Timeline assigned it waits a few seconds and moves on.
   Play skips straight to Level 1 until you switch `SceneLoader.StartNewGame()` over.
5. Open `Assets/Scenes/Level 1.unity` and run **Create Player In Open Scene**,
   **Create Ground Platform In Open Scene** and **Add Pause Menu To Open Scene**. Save.

The three scenes are already listed in Build Settings, main menu first.
**Add Game Scenes To Build Settings** puts them back if they ever get out of order.

## How it fits together

- **Play** → `SceneLoader.StartNewGame()` → Level 1. The cutscene is wired up but skipped:
  change that method to call `LoadCutscene()` and the intro plays first, then hands off to
  Level 1 on its own.
- **Esc / Start** → `PauseMenuController` freezes the game through `GamePause`, which sets
  `Time.timeScale` and pauses audio. `PlayerInputReader` reads as zero while paused, so nothing
  moves behind the menu. Pressing it again resumes; if the options panel is open it backs out first.
- **Settings** apply the moment you change them and save to PlayerPrefs when the panel closes.
  `GameSettings.Load()` runs automatically before the first scene.
- Every scene change goes through `SceneLoader`, which clears the pause state first, so a paused
  game can never carry a frozen time scale into the next scene.

## Movement

`PlayerMotor` never snaps to full speed. Each physics step the horizontal velocity is moved
towards a target, so the wizard builds up speed and keeps sliding when you let go:

- `acceleration` — how quickly speed builds. Lower = heavier, slower to get going.
- `groundFriction` — how quickly they coast to a stop with no input.
- `airControl` — scales both while airborne. 0.45 means you commit to a jump but can still steer.
- `fallGravityMultiplier` / `maxFallSpeed` — falls are heavier than the rise and cap out.

Every landing raises `PlayerMotor.Landed` with the distance fallen since the last apex.
`FallDamage` turns anything past `safeFallDistance` into damage, which is the core loop.

## Power-ups

No script per power-up. Put `PowerUpPickup` on an object with a trigger collider and fill in the
effect in the inspector: `fallSpeedMultiplier` 0.5 is a feather, `speedMultiplier` 1.5 is boots,
`healAmount` 2 with `duration` 0 is a potion. `PlayerPowerUps` keeps the active ones and multiplies
them together into the values the motor and fall damage read; with nothing active everything is 1.

## Worth knowing

- `PlayerMotor.groundLayers` must exclude the player's own layer, or the ground box finds the
  wizard's collider and they are permanently "grounded". The Create Player command sets it to
  **Ground**; layers 6 and 7 are now `Ground` and `Player`. Select the wizard to see the cyan
  ground-check gizmo.
- The **Exit** button is always shown. Console certification usually forbids quitting to the OS
  from a menu, so hide it in `MainMenuController.Awake` when you get to a console build.
- Resolution and fullscreen rows hide themselves off desktop (`SettingsPanel.desktopOnlyRows`).
- Volume drives `AudioListener.volume`. Swap in an `AudioMixer` when you want separate
  music and SFX sliders.
- Buttons are wired in `Awake` in code, not through the inspector's OnClick list. Add both and
  they will fire twice.
