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
| `Player/` | `PlayerCharacter` (the wizard, its state machine, `PlayerStats`, `Health`), `PlayerMovement`, `StaffDescent`, `Ragdoll`, `PowerUp`, `PowerUpPickup` |
| `World/` | `RoughGround` (marks stairs and rocks), `FollowCamera` |

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
5. Open `Assets/Scenes/Level 1.unity`, delete anything already in it except the camera and light,
   then run **Build Test Level In Open Scene** and **Add Pause Menu To Open Scene**. Save.
   The test level lays out flat ground, a staircase, and two drops sized around the staff.

The three scenes are already listed in Build Settings, main menu first.
**Add Game Scenes To Build Settings** puts them back if they ever get out of order.

## Input

Nothing reads a key or button directly. Every binding lives in `Assets/InputSystem_Actions`,
so rebinding is done in the Input Actions editor, not in code:

| Action | Read by | Default bindings |
| --- | --- | --- |
| `Player/Move` | `PlayerCharacter` | WASD, arrows, left stick |
| `Player/Jump` | `PlayerCharacter` | Space, gamepad south |
| `Player/Walk` | `PlayerCharacter` | Left Shift, gamepad left trigger |
| `Player/Staff` | `PlayerCharacter` | E, gamepad west |
| `UI/Pause` | `MenuInput` | Esc, gamepad start |
| `UI/Skip` | `MenuInput` | Space, Enter, left click, gamepad south, gamepad start |

Looking down, lowering onto the staff, dropping off it and climbing back all read `Move`'s Y
(S / Down and W / Up), so they need no action of their own. `Player/Staff` is only a shortcut
that skips the hold.
`Player/Walk`, `Player/Staff`, `UI/Pause` and `UI/Skip` were added for this project; the rest ship
with Unity's default asset. Add or change bindings there and the code picks them up with no edits.
The stock `Sprint` action is unused — delete it if you like.

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

## Tuning the wizard

Everything is on the **PlayerCharacter** component of the Wizard, grouped into foldouts.
Speed and jump height live under **Movement**:

| Field | What it does |
| --- | --- |
| `runSpeed` | Top speed at a normal run. Running off a ledge drops you. |
| `walkSpeed` | Top speed holding Walk. Walking also refuses to step off a ledge. |
| `jumpHeight` | Height of a full jump, in units. Literal — see the gravity note below. |
| `acceleration` | How quickly speed builds. Lower = heavier, slower to get going. |
| `groundFriction` | How quickly they coast to a stop with no input. |
| `airControl` | Scales both while airborne. 0.45 = committed to your jump but still steerable. |
| `fallGravityMultiplier` / `maxFallSpeed` | Falls are heavier than the rise, and cap out. |
| `ledgeCheckAhead` / `ledgeCheckDepth` | How far ahead and down to look for a missing floor. |

Under **Staff**: `staffLength` is the whole mechanic — how far down the wizard can lower
themselves, and therefore how much of a drop it removes — plus `holdToDescend`, how long you
must hold Down before looking becomes lowering. Under **Ragdoll**: `tripSpeed`, `spinSpeed`,
`fallKick`, `minimumDuration`, `recoverSpeed`, `standUpDuration`.

## The five mechanics

- **Run off a ledge and you fall.** Normal running does nothing to stop you.
- **Walk stops at edges.** Holding Walk caps speed at `walkSpeed`, and `PlayerMovement.Run`
  zeroes the target speed when `IsAtEdge` and you are pushing toward the drop.
- **Look down, then go down.** Hold **S / Down** at an edge. `IsPeeking` goes true straight away
  and `FollowCamera` slides the view down by `peekDistance`; keep holding past
  `holdToDescend` and the wizard commits to the staff. A quick tap is just a look.
- **The staff.** `StaffDescent` takes the body kinematic and lowers it `staffLength` below the
  lip, then holds. Press **Down** again to drop the rest, **Up** to climb back. The drop is
  measured from the hang point via `movement.BeginFallFrom`, which is exactly why it turns a
  killing fall into a survivable one. The Staff button is an instant alternative to the hold.
- **Rough ground → ragdoll.** Put `RoughGround` on stairs and rocks. Cross one faster than
  `tripSpeed` and `PlayerCharacter` enters `Ragdoll`: the body unfreezes its rotation, takes a
  spin and a downward kick, and physics owns it until it is grounded, slow, and past
  `minimumDuration`. Then it stands back up over `standUpDuration`.

`PlayerState` is the whole state machine — `Normal`, `Descending`, `Hanging`, `Climbing`,
`Ragdoll` — and `PlayerCharacter.FixedUpdate` is a single switch over it. New state, new case.
`State` is public, so an Animator can drive clips straight off it when you add sprites.

## Power-ups

A power-up is a ScriptableObject you subclass, so nothing is assumed about what any of them do:

```csharp
[CreateAssetMenu(menuName = "Falling Wizard/Power Ups/Feather Fall")]
public class FeatherFall : PowerUp
{
    [SerializeField] float fallSpeedMultiplier = 0.5f;

    public override void ModifyStats(PlayerStats stats) =>
        stats.FallSpeedMultiplier *= fallSpeedMultiplier;
}
```

Three hooks: `OnCollected` for instant effects, `ModifyStats` for continuous ones,
`OnExpired` for cleanup. `ActivePowerUps` holds the live ones and rebuilds `PlayerStats`
from scratch whenever the set changes. Drop the asset on a `PowerUpPickup`.

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
