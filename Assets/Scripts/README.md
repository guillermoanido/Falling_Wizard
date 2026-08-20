# Falling Wizard — scripts

Unity 6.3, URP 2D, new Input System only. Everything reads the project-wide actions in
`Assets/InputSystem_Actions`, which are already bound to keyboard and gamepad, so PC and
consoles work from the same code.

## Layout

| Folder | What lives there |
| --- | --- |
| `Core/` | `Game` (pause state, scene flow, quit), `GameSettings`, `MenuInput` |
| `Menus/` | `MenuScreen` base class, `MainMenuController`, `PauseMenuController`, `SettingsPanel` |
| `Cutscenes/` | `CutsceneRunner` — plays the intro, then loads Level 1 |
| `Player/` | `PlayerCharacter.cs` (the wizard + `PlayerMovement` + `Health`), `PowerUpPickup.cs` (the pickup + `PowerUp` + `ActivePowerUps`) |

The wizard is **one** component. `PlayerMovement`, `Health` and `ActivePowerUps` are plain classes
that `PlayerCharacter` owns and drives, and they live in the same file as their owner so Unity does
not create draggable script assets for them. The two menus share `MenuScreen`, which owns the panel
swapping, the back button and controller focus.

Changing a serialized field name or turning a MonoBehaviour into a plain class breaks any scene that
already references it. If Unity reports a missing script or `ExtensionOfNativeClass`, the fix is to
delete the stale component and rebuild the object with the Tools menu.

`Assets/Editor/` holds one-shot build commands. They only create ordinary scene objects —
delete that folder once you have run them if you want it gone.

## First-time setup

1. Open `Assets/Scenes/Main Menu.unity`.
2. **Tools ▸ Falling Wizard ▸ Build Main Menu In Open Scene**. The first run may ask to import
   the TextMeshPro essentials; accept, wait, then run it again. Save the scene.
3. **Tools ▸ Falling Wizard ▸ Create Pause Menu Prefab** (writes `Assets/Prefabs/Pause Menu.prefab`).
4. Optional, for later: open `Assets/Scenes/Cutscene.unity` and add an empty object with
   `CutsceneRunner` on it. With no Timeline assigned it waits a few seconds and moves on.
   Play skips straight to Level 1 until you switch `Game.StartNewGame()` over.
5. Open `Assets/Scenes/Level 1.unity` and run **Create Player In Open Scene**,
   **Create Ground Platform In Open Scene** and **Add Pause Menu To Open Scene**. Save.

The three scenes are already listed in Build Settings, main menu first.
**Add Game Scenes To Build Settings** puts them back if they ever get out of order.

## Input

Nothing reads a key or button directly. Every binding lives in `Assets/InputSystem_Actions`,
so rebinding is done in the Input Actions editor, not in code:

| Action | Read by | Default bindings |
| --- | --- | --- |
| `Player/Move` | `PlayerCharacter` | WASD, arrows, left stick |
| `Player/Jump` | `PlayerCharacter` | Space, gamepad south |
| `UI/Pause` | `MenuInput` | Esc, gamepad start |
| `UI/Skip` | `MenuInput` | Space, Enter, left click, gamepad south, gamepad start |

`UI/Pause` and `UI/Skip` were added to the UI map for this project; the rest ship with Unity's
default asset. Add or change bindings there and the code picks them up with no edits.

## How it fits together

- **Play** → `Game.StartNewGame()` → Level 1. The cutscene is wired up but skipped:
  change that method to call `LoadCutscene()` and the intro plays first, then hands off to
  Level 1 on its own.
- **UI/Pause** → `PauseMenuController` freezes the game through `Game`, which sets
  `Time.timeScale` and pauses audio. `PlayerInputReader` reads as zero while paused, so nothing
  moves behind the menu. Pressing it again resumes; if the options panel is open it backs out first.
- **Settings** apply the moment you change them and save to PlayerPrefs when the panel closes.
  `GameSettings.Load()` runs automatically before the first scene.
- Every scene change goes through `Game`, which clears the pause state first, so a paused
  game can never carry a frozen time scale into the next scene.

## Movement

`PlayerMovement` never snaps to full speed. Each physics step the horizontal velocity is moved
towards a target, so the wizard builds up speed and keeps sliding when you let go:

- `acceleration` — how quickly speed builds. Lower = heavier, slower to get going.
- `groundFriction` — how quickly they coast to a stop with no input.
- `airControl` — scales both while airborne. 0.45 means you commit to a jump but can still steer.
- `fallGravityMultiplier` / `maxFallSpeed` — falls are heavier than the rise and cap out.

`PlayerMovement` records the distance fallen since the last apex; `PlayerCharacter` reads it with
`TryGetLanding` each physics step and turns anything past `safeFallDistance` into damage.

## Power-ups

No script per power-up. Put `PowerUpPickup` on an object with a trigger collider and fill in the
`PowerUp` in the inspector: `fallSpeedMultiplier` 0.5 is a feather, `speedMultiplier` 1.5 is boots,
`healAmount` 2 with `duration` 0 is a potion. A `PowerUp` runs its own timer; `ActivePowerUps`
holds the live ones and multiplies them together. With nothing active every multiplier is 1.

## Worth knowing

- The movement block's `groundLayers` must exclude the player's own layer, or the ground box finds
  the wizard's collider and they are permanently "grounded". The Create Player command sets it to
  **Ground**; layers 6 and 7 are now `Ground` and `Player`. Select the wizard to see the cyan
  ground-check gizmo.
- The **Exit** button is always shown. Console certification usually forbids quitting to the OS
  from a menu, so hide it in `MainMenuController.Awake` when you get to a console build.
- Resolution and fullscreen rows hide themselves off desktop (`SettingsPanel.desktopOnlyRows`).
- Volume drives `AudioListener.volume`. Swap in an `AudioMixer` when you want separate
  music and SFX sliders.
- Buttons are wired in `Awake` in code, not through the inspector's OnClick list. Add both and
  they will fire twice.
