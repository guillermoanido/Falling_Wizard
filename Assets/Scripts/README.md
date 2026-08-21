# Falling Wizard — scripts

Unity 6.3, URP 2D, new Input System only. Everything reads the project-wide actions in
`Assets/InputSystem_Actions`, already bound to keyboard and gamepad, so PC and consoles work from
the same code with no branches.

## The unit everything is measured in

**One box = 32 px = 1 world unit ≈ one mage.** The art is drawn on a 32 px grid and imported at
32 pixels per unit, so a box is the same thing on the canvas, in the inspector and in the physics.

Every number in the game is expressed in boxes or boxes per second:

| | |
| --- | --- |
| Run / walk | 6 and 2 boxes per second |
| Jump | 2 boxes |
| Free fall | 3 boxes |
| Fall damage | 1 heart per box past that — so 8 boxes kills a full-health wizard |
| Staff reach | pole height + the wizard's hand-hang, about 1.9 boxes |

Jump height being under the damage floor is deliberate: a jump can never hurt you.

## Layout

| Folder | What lives there |
| --- | --- |
| `Core/` | `Game` (pause, scene flow, quit), `GameSettings`, `Controls` (every input lookup, plus which device is in use), `Progress` (what the wizard has learned), `SingletonBehaviour` |
| `Player/` | `PlayerCharacter`, `PlayerLogic`, `Staff` |
| `Player/Abilities/` | `Ability` and the spells, `AbilityBook`, `AbilityShrine` |
| `World/` | `PlayerTrigger`, `Hazard`, `Rock`, `Slime`, `WindZone2D`, `RoughGround`, `FollowCamera` |
| `UI/` | `PlayerHud` |
| `Menus/`, `Cutscenes/` | `MenuScreen` and the three menus; `CutsceneRunner` |

Three files hold the whole wizard. `Movement`, `Ragdoll`, `Health`, `Modifiers`, `Spellbook`,
`Intent` and `Command` are all **nested classes of `PlayerLogic`** — they are parts of a wizard and
meaningless on their own, so they live inside it rather than in seven files of their own. `Staff`
likewise contains `Staff.Pole`. Nested `[Serializable]` classes serialize exactly like top-level
ones and show up as foldouts in the inspector.

### Rules that keep it working

- **Never rename `PlayerCharacter.cs`, `Staff.cs`, `RoughGround.cs` or `FollowCamera.cs`**, or their
  classes. `Level 1.unity` refers to them by GUID, and the GUID lives in the `.meta` beside the
  file. Moving a file is fine — the `.meta` travels with it. Renaming is not.
- **Never use `[SerializeReference]`.** It writes assembly, namespace and class names into the
  scene; renaming a class silently nulls every reference to it.
- **`OnValidate` does not reach nested classes.** Each block has a `Validate()` instead, chained
  from `PlayerCharacter.OnValidate` and `Staff.OnValidate`.
- **Public runtime state needs `[NonSerialized]`.** Inside a `[Serializable]` class a public field
  serializes by default, so a public timer would be baked into the scene at whatever value it held
  when you last saved.

## Numbers

Three kinds of field, and which one a thing is decides how it is declared:

| Kind | Declared as | Examples |
| --- | --- | --- |
| **Tuning** — has a unit | `public` under a `[Header]`, with `[Tooltip]` and `[Min]`/`[Range]` | `runSpeed`, `jumpHeight`, `tripSpeed`, `bounceHeight` |
| **Wiring** — points at something | `public` under a `[Header]` | `hitbox`, `visual`, `bridgeCollider`, `book`, `ability` |
| **Runtime** — has a lifetime, not a value | `{ get; private set; }` or `[NonSerialized]` private | `IsGrounded`, `Facing`, `Current`, `Progress` |

Every tunable is public so it is visible and editable in the inspector. That is a deliberate
trade: nothing stops another script writing `wizard.Logic.movement.runSpeed = 99`, so **don't**.
If you find yourself wanting to, you wanted a `Modifiers` multiplier or one of the verbs below.

## How the world talks to the wizard

Hazards and spells never reach in and set a number. They call verbs on `PlayerLogic`, each of which
decides for itself whether it applies:

```csharp
wizard.Trip();                                   // rough ground, rocks
wizard.Bounce(heightInBoxes, sideways, resetsFall);  // slimes
wizard.Push(boxesPerSecond, rampup, groundScale);    // wind, every step you are inside it
wizard.Shove(velocity, controlLockout);          // one-off knock
wizard.Hurt(hearts);  wizard.Heal(hearts);
wizard.TryPlantStaff(mode);  wizard.RecoverStaff();  wizard.DropFromStaff();
```

`Movement.Run` writes `linearVelocityX` absolutely every physics step, so an `AddForce` from
outside is wiped within a quarter second. That is why external force has exactly one way in: wind
is folded into `Run`'s target speed — so you can lean into it and partly win, and it self-limits —
and impulses land immediately with a short steering lockout. **Impulses must not be queued for the
next `FixedTick`:** whatever shoves you usually trips you too, and `FixedTick` never runs while
tumbling, so a queued shove would sit unspent and then fire as you stood back up.

## Spells

A spell is a `ScriptableObject` asset. They are **stateless flyweights** — every wizard shares the
one asset, so all the mutable state lives in `PlayerLogic.Spellbook.Slot`. Adding a mutable field
to an `Ability` would leak between play sessions and into the build.

Passive or active is decided by one thing: **a spell with an empty `actionName` has no button**,
shows no key on the HUD, and simply applies while it is owned.

| Hook | When |
| --- | --- |
| `ModifyStats` | Every step, while OWNED. Where a passive lives. Multiply, never assign. |
| `ModifyStatsWhileLit` | Every step, only during the seconds after a cast. |
| `CanCast` | Whether the button would do anything. Greys the HUD slot. |
| `OnCast` | Do it. **Return false for "not yet"** — the press stays buffered and retries, which is what lets you press the staff button just before reaching a ledge. |
| `OnLit` / `OnEnded` | During and at the end of the lit window. |
| `OnLearned` / `OnRunReset` | Picked up; and died. |

The four that exist: **Staff** (ability #1, the one you start with), **Glide**, **Higher Jump**
(a `StatAbility`, passive), **Staff Bridge**.

### Adding spell #5

1. One `.cs` in `Player/Abilities/` — or none at all, if it is only stat changes: make another
   `StatAbility` asset instead.
2. One asset via **Assets ▸ Create ▸ Falling Wizard ▸ Abilities ▸ …**.
3. If it needs a button, one action + two bindings in `Assets/InputSystem_Actions`, and put the
   action's name in the asset's `actionName`.
4. Drag it into `Assets/Resources/Spellbook.asset` → `spells`.

HUD slot, button glyph, press buffering, cooldown, unlocking and persistence all come for free.

### Order and unlocking

`Assets/Resources/Spellbook.asset` is the single source of truth. `spells` is the bar, left to
right — **drag to reorder, nothing in any scene depends on it**. `known` is what a new game starts
with; the Staff belongs there.

Everything else is learned from an `AbilityShrine`: one component, one field, and the icon and
sparkle come from the spell itself. Learning is **permanent** — `Core.Progress` holds it outside
the wizard, because dying reloads the level and builds a brand new one. A shrine you already drank
from does not come back.

Progress lasts the play session: it survives dying and moving between levels, and starts empty
every time you press Play. When the game grows a save file, swap the `HashSet` in `Progress` for
`PlayerPrefs` and nothing else changes.

## Tripping

A trip **launches you onward** — your speed, plus `launchForward`, never below `minimumLaunch`, and
`launchUp` of lift so you actually leave the floor. Landing back down, the skid bleeds at
`slideFriction` boxes per second squared. All of it on `PlayerLogic ▸ Ragdoll`, and all of it the
same every time: a rock, a slime and a bad staircase throw you identically, because none of them
supply their own knock — they just call `Trip()`.

**The sprite tumbles; the collider does not.** A rotating box levers itself up on its corners — its
half-diagonal is longer than its half-height, 0.64 against 0.52 here, so the solver must lift it
0.11 boxes every time a corner swings down. That reads as bouncing along the floor, and no material
setting removes it because it is geometry, not bounciness. It also makes how far you slide depend
on which corner happens to be down. Spinning the art instead costs nothing and looks the same.

The wizard's `Rigidbody2D` rotation is frozen and nothing in the game ever unfreezes it.

## Contact friction is zero

`Movement.surfaceFriction` is 0, applied as a material in `Attach`. Horizontal speed is written
outright every physics step, so contact friction never helps the wizard move — it only ever fights
them, catching on platform corners and seams and bleeding speed for no visible reason. The only
thing that slows them is `groundFriction`, which is a number you can see.

That makes `Ragdoll.slideFriction` the *only* thing stopping a tumble. Set it to 0 and a tripped
wizard slides forever and can never get up; `Validate()` warns if you do.

## Small ledges

`Movement.TryStepUp` climbs anything up to `stepHeight` (0.35 boxes) instead of letting it stop the
wizard, so a lip, a kerb or the seam between two platforms is not a wall. Three probes: is
something in the way at shin height, would the body clear it once lifted (if not, it is a wall and
nothing happens), and how tall is it really. The rise is spread over a couple of physics steps so
it reads as a step rather than a teleport, and it is hard-clamped to half the wizard's height so a
big number cannot become wall-climbing.

**Up only, deliberately.** Snapping down small drops as well would stop the wizard running off
ledges, and running off ledges is the whole game.

If you are catching on colliders that are perfectly *flush* rather than stepped, that is a
different problem — Unity's 2D solver snagging on the seam between two separate box colliders. The
fix for that is a `CompositeCollider2D` over the platforms, not a bigger `stepHeight`.

## The staff

A child of the wizard with its own hitbox and sprite. **The hitbox's height is the mechanic** —
the wizard travels its span and then the length of their own hand-hang past the tip, so a taller
collider is a longer climb and nothing else has to be told about it.

Two modes:

- **Ladder** (the `Staff` spell). Driven in just past the lip with its top flush to the ledge, so
  the far end of the pole is where your feet will end up and you can read the drop off it. Slide
  down, and keep pushing down at the bottom to let go.
- **Bridge** (the `Staff Bridge` spell). Laid flat as a plank you walk out onto. The thing you
  stand on is a **separate solid collider on a child, on the Ground layer** — the staff itself is
  on the Player layer, which the ground check deliberately ignores, so a collider on the staff
  would be one you fall straight through.

## Hazards

| | What it is | Notes |
| --- | --- | --- |
| `RoughGround` | A surface you stumble **across** | Not a hazard — it goes on the ground collider itself, or a parent of it. Carries its own `tripSpeed`. Stairs 4, gravel 3, scree 2.5. |
| `Rock` | A stone you clip at a run | `minimumSpeed` 4 means a run trips and a walk does not. |
| `Slime` | Fall into it, get thrown back up | Launches you on the way past. |
| `WindZone2D` | A volume that pushes you | One `Vector2` covers left, right, up and down. `groundScale` decides how much you feel with both feet down. |

**Nothing on the Hazard layer blocks you.** `Hazard.passThrough` is on by default and applied on
`Awake`, so hazards are things you pass straight through that do something to you on the way, not
things you bump into. That is also why they belong on layer 8: the ground check ignores that layer,
so a *solid* hazard there would be something the wizard comes to rest on while the game still
believes they are falling — no jump, and no way out of a tumble, since a ragdoll only recovers once
grounded. Ticking `passThrough` off is supported, but move it off layer 8 if you do.

All three sit on `Hazard`, which handles speed gating, re-arming, damage, and whether it can reach
a wizard who is on their staff or already tumbling. **Adding hazard #6 is one subclass with one
`Affect` method.**

**Hazards push you onward, never back.** Rocks, slimes and tumbles all send you the way you were
already travelling — `Movement.TravelDirection`, which is the direction you *arrived* in, not the
direction you happen to be facing and not away from the hazard's own centre. Being thrown back the
way you came is the least predictable thing a hazard can do: you lose the ground you covered and
land somewhere you were not looking. Tripping keeps your speed too (`Ragdoll.momentumKept`), so a
trip is a loss of footing rather than a wall.

Two things follow from that, and they matter when you place hazards: a hazard read live from
`HorizontalSpeed` would measure *after* the impact — which against a solid rock is nearly zero, so
every speed gate would refuse to fire. Both direction and speed come from the value recorded at the
end of the previous physics step instead.

Two things worth knowing:

- Hazards go on **layer 8 (Hazard)** and at the **scene root**. Several platforms in the test level
  carry non-uniform scales that would squash anything parented under them.
- `PlayerTrigger` filters contacts down to the wizard's body collider. The wizard emits two
  colliders — their body and the staff's trigger, which shares their rigidbody — so without that
  filter every hazard would fire twice.

## HUD

An ordinary screen-space `Canvas` you can open up and restyle. Build it with
**Tools ▸ Falling Wizard ▸ Add HUD To Open Scene**:

```
HUD              Canvas (Overlay, order 10) + CanvasScaler + PlayerHud
├── Hearts       top-left, HorizontalLayoutGroup + ContentSizeFitter
│   └── Heart        TEMPLATE, inactive
└── Spell Bar    bottom-left, same
    └── Spell Slot   TEMPLATE, inactive - Image + HudSlot
        ├── Icon         the spell's own icon
        ├── Charge       Image, Filled/Radial360 - the running and cooling wipe
        └── Button       TextMeshProUGUI
```

Hearts and slots are **copies of the two templates**, made at runtime, one per point of health and
one per spell. Restyle a template — sprite, size, colour, add a border — and every heart or every
slot follows. `PlayerHud` only ever fills them in; it never builds layout.

Set `CanvasScaler.referencePixelsPerUnit` to **32**, not the default 100, or every icon comes out
at a third of its size. The builder does this for you.

It finds the wizard through the singleton and re-checks every frame, because dying destroys them
and builds a new one — anything that had subscribed would be holding a destroyed object.

The button under each spell follows **whichever device is actually in use**: `E` on a keyboard,
`X` on an Xbox pad, `Square` on a DualSense. `Core.Controls` watches only the actions this game
asked for, which is what stops a mouse twitch flipping the HUD back to keyboard letters mid
gamepad play. Asking for the glyph without naming a device returns `"E | X"` — always go through
`Controls.Glyph`.

No `GraphicRaycaster`, on purpose: the HUD must never swallow a pause-menu click or steal
controller focus from the menus. The `Charge` image must stay set to **Filled** — an `Image` with
no sprite, or one set to Simple, draws a plain quad and silently ignores `fillAmount`, so every
cooldown would sit frozen at full with nothing in the console.

## Input

| Action | Keyboard | Gamepad | Read by |
| --- | --- | --- | --- |
| `Player/Move` | WASD / arrows | left stick | `PlayerCharacter.Controls` |
| `Player/Jump` | Space | south | `PlayerCharacter.Controls` |
| `Player/Walk` | Left Shift | left trigger | `PlayerCharacter.Controls` |
| `Player/Staff` | E | west | `Staff.asset` |
| `Player/Glide` | Q | right shoulder | `Glide.asset` |
| `Player/Bridge` | F | left shoulder | `Staff Bridge.asset` |
| `UI/Pause` | Esc | start | `Core.Controls` |
| `UI/Skip` | Space, Enter, click | south, start | `Core.Controls` |

Looking down and climbing the staff read `Move`'s Y, so they need no action of their own. Spell
buttons are looked up **by name from the asset**, so rebinding or adding one never touches code.

## Editor commands

`Tools ▸ Falling Wizard ▸ …` builds ordinary scene objects and nothing else.

Build Main Menu · Create Pause Menu Prefab · Add Pause Menu To Open Scene · Create Player ·
Create Ground Platform · Build Test Level · **Add HUD** · **Create Hazard ▸ Rock / Slime / Wind** ·
**Create Ability Shrine** · Add Game Scenes To Build Settings

## Worth knowing

- `groundLayers` must exclude the wizard's own layer or they stand on themselves. It defaults to
  Ground (layer 6) in code, and `Validate()` warns if you widen it. Layers: 6 Ground, 7 Player,
  8 Hazard.
- The project queries triggers by default, so every ground query passes
  `ContactFilter2D { useTriggers = false }`. Without it any trigger on the Ground layer becomes
  walkable floor.
- Never write `body.gravityScale` from a spell — `ApplyFallGravity` reassigns it 50 times a second.
  Use `Modifiers.FallSpeedMultiplier`, which is already threaded through both the gravity and the
  terminal-speed clamp.
- Buttons are wired in `Awake` in code, not through the inspector's OnClick list. Add both and they
  fire twice.
- The **Exit** button is always shown. Console certification usually forbids quitting to the OS from
  a menu, so hide it in `MainMenuController.Awake` for a console build.
