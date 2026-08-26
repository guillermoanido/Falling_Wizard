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
| `World/` | `PlayerTrigger`, `Hazard`, `Rock`, `Slime`, `WindZone2D`, `FollowCamera` |
| `UI/` | `PlayerHud` |
| `Menus/`, `Cutscenes/` | `MenuScreen` and the three menus; `CutsceneRunner` |

Three files hold the whole wizard. `Movement`, `Ragdoll`, `Health`, `Modifiers`, `Spellbook`,
`Intent` and `Command` are all **nested classes of `PlayerLogic`** — they are parts of a wizard and
meaningless on their own, so they live inside it rather than in seven files of their own. `Staff`
likewise contains `Staff.Pole`. Nested `[Serializable]` classes serialize exactly like top-level
ones and show up as foldouts in the inspector.

### Rules that keep it working

- **Never rename `PlayerCharacter.cs`, `Staff.cs` or `FollowCamera.cs`**, or their
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
wizard.Trip();                                   // rocks
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

**A spell does not own a button. A slot does.** There are four of them — Q, E, R, F, bound by
index to the `Player/Spell1..Spell4` actions — and what a press does depends entirely on what is
sitting in that slot. A spell you own but did not bring does nothing at all. This is the point of
the whole design: you cannot carry everything, so what you bring is a decision you make before you
go down.

The Staff is the exception, and only because it is the one spell every route assumes: `locked: 1`
and `fixedSlot: 1` pin it to **E** for good, and the skill screen will not let it out. That leaves
**Q, R and F** to fight over.

A spell with `passive: 1` has no button. It still takes a slot, so bringing one costs you an
active — otherwise a passive is free and there is no choice in it.

| Hook | When |
| --- | --- |
| `ModifyStats` | Every step, while OWNED. Where a passive lives. Multiply, never assign. |
| `ModifyStatsWhileLit` | Every step, only during the seconds after a cast. |
| `CanCast` | Whether the button would do anything. Greys the HUD slot. |
| `OnCast` | Do it. **Return false for "not yet"** — the press stays buffered and retries, which is what lets you press the staff button just before reaching a ledge. |
| `OnLit` / `OnEnded` | During and at the end of the lit window. |
| `OnEquipped` / `OnUnequipped` | Put into a slot; taken out of one. Undo anything you spawned in `OnUnequipped`. |
| `OnRunReset` | Died, or rested. Refills `usesPerRun` and is where anything left lying in the level gets cleaned up. |

`usesPerRun` above 0 gives a spell a number of casts that only a rest brings back — for something
a cooldown alone cannot hold back. `Slot.UsesLeft` counts them and the HUD prints it.

A spell that needs to remember something between frames — a wall it grew, a rope it is holding —
keeps it in `spellbook.StateOf<T>(this)`, never in a field on itself. The asset is shared; a field
on it would survive a scene load and follow you into the build.

The six that exist. The Staff is yours from the start and welded to E; the other five fight over
three buttons.

| Spell | What it is | Where it lives |
| --- | --- | --- |
| **Staff** | Plant it at a ledge and climb down. | `StaffAbility` + `Staff` on the wizard |
| **Feather Fall** | Drift, and forget how far you have already fallen. | `GlideAbility` |
| **Wall Growth** | Raise a wall out of the floor ahead of you. | `WallGrowthAbility` + the `Stone Wall` prefab |
| **Vine Grasp** | Catch a vine and swing. | `VineAbility` + `VineAnchor` in the level |
| **Bubble** | Float, untouchable, and let the wind take you. | `BubbleAbility` |
| **Telekinesis** | Lift a loose stone at range, carry it, throw it. | `TelekinesisAbility` + `Liftable` in the level |

Two of them need something to act on, and that is deliberate — a spell that needs level content is
a spell the level can be designed around. Vine Grasp does nothing without a `Vine`; Telekinesis
does nothing without a `Boulder`.

Feather Fall is worth reading before writing another movement spell. Slowing a fall does **not**
make it survivable: fall damage counts boxes fallen, and gliding down a killing drop only kills you
later. `forgivesFall` is what makes the spell worth a slot — while it is lit it keeps resetting the
height the fall is measured from, so catching a long drop late saves you and *how* late you dare
leave it is the whole skill.

That same forgiveness is what makes it **one cast per fall** (`oncePerFall`). A cooldown alone does
not hold it: with a one second float and half a second to wait, you can re-cast in mid-air and each
cast wipes the drop again, so chaining it makes every fall survivable and fall damage stops
existing. The rule is enforced against `Movement.Airtime`, a counter that ticks up every time the
wizard leaves the ground. The spell remembers the number it last fired on and refuses to fire on
that number twice — which needs no per-frame hook, and cannot drift out of step the way a flag
cleared somewhere else would. Touch the ground and the number moves on.

**Bubble is the opposite trade, on purpose.** It does not forgive the drop — pop it high up and you
are still high up. What it buys is that nothing can touch you (`Modifiers.Shielded`) and that wind
pushes you three and a half times as hard (`Modifiers.WindMultiplier`). A gale you would normally
brace against becomes the ride. It is the only spell that wants a hazard nearby.

**Telekinesis lights while it holds.** Pressing once takes hold, pressing again lets go — and
because `TryCast` re-lights a spell whose `OnCast` returned true, the release cannot end the spell
itself. It drops the stone and lets `OnLit` notice the empty hand and `Extinguish` on the next
step. Let go with a direction held and the stone is thrown; let go with nothing held and it simply
falls where it hangs.

### Adding spell #5

1. One `.cs` in `Player/Abilities/` — or none at all, if it is only stat changes: make another
   `StatAbility` asset instead.
2. One asset via **Assets ▸ Create ▸ Falling Wizard ▸ Abilities ▸ …**, with a `cost` in Wisps.
3. Drag it into `Assets/Resources/Spellbook.asset` → `spells`.

**No input edits, no scene edits, no HUD edits.** The buttons already exist and belong to the
slots; the skill screen lists whatever is in the catalogue. Press buffering, cooldown, per-run
charges, buying, equipping and persistence all come for free.

A spell that wants to drive the wizard itself takes over a `PlayerState` — see the vine below,
which is the worked example.

### Order and unlocking

`Assets/Resources/Spellbook.asset` is the single source of truth. `spells` is the skill screen's
list, top to bottom — **drag to reorder, nothing in any scene depends on it**. `known` is granted
free on a new game; the Staff belongs there and nothing else has to.

Everything else is **bought with Wisps** at the skill screen. An `AbilityShrine` is still there for
the one spell you want every player to meet whether or not they went looking — it grants
permanently and free, and drops the spell into the first empty slot so it can be tried at once.

`Core.Progress` holds all of it, outside the wizard, because dying reloads the level and builds a
brand new one.

## Wisps, hearts and runs

The risk. Three tiers, and which tier a thing lives in is the entire design:

| Tier | What | Survives death | Survives turning back | On disk |
| --- | --- | --- | --- | --- |
| **Dive** | `CarriedWisps`, `carrying` | **no** | banked | no |
| **Run** | `found`, the checkpoint | yes | **no** | no |
| **Permanent** | `Wisps`, `spent`, `BonusHearts`, `ranks`, the loadout | yes | yes | yes |

**Dying costs the Wisps you are carrying and nothing else.** You keep the hearts, keep the run, and
pick up from the last rest site. The pickups you already took stay taken — `found` is run-scoped,
not dive-scoped — so there is nothing to farm by dying on purpose.

**Turning back at a rest site banks the Wisps** and ends the run.

### A level is a finite thing

This is the part that makes the game move. Three id sets, and the difference between them is the
whole economy:

| Set | Holds | Emptied by |
| --- | --- | --- |
| `found` | every pickup touched this run | turning back |
| `carrying` | the Wisps riding on you right now | **dying**, or banking |
| `spent` | pickups whose value you actually kept | never |

`Pickup.Awake` destroys itself if the id is in `found` **or** `spent`, and each pickup says how
long being taken lasts:

| | `StaysTaken` | What it means |
| --- | --- | --- |
| `WispPickup` | `OnceBanked` | Rides in `carrying`. **Bank it and that Wisp is gone from the world forever.** Die first and it never reaches `spent`, so it is standing there again next run. |
| `HeartPickup` | `ForGood` | Into `spent` the instant it is touched. One heart, once, permanently. |

So a level does not refill. Bank Level 1's Wisps and Level 1 has no more Wisps to give — you go
deeper for the next ones. What Level 1 *does* still have is the parts of it you could not reach,
and the spells you bought with those Wisps are how you reach them. **The first level opens up as
you go deeper**, rather than being farmed.

Dying gives the Wisp back. That is deliberate: the punishment for dying is losing the descent, not
losing the pickup, so a bad run is repeatable and a greedy one is not free.

Max HP is permanent and one-way. `Health.maxHealth` is what a brand new save starts with and
`Health.maxBonusHearts` caps what can ever be added on top of it, across the whole save. A
`HeartPickup` that finds the bar already at that cap pays out in Wisps instead — never leave a
player standing over dead loot — and is spent all the same, so there is no heart to farm.

`WispPickup` and `HeartPickup` both derive from `Pickup`, which remembers itself by **where it
stands** (quarter-box resolution, scene-qualified) unless you type an `id`. That survives renaming
and re-parenting; moving one makes it a new pickup. Give two pickups sharing a spot explicit ids.

The name is worked out once, in `Awake`, and kept. It has to be: the idle bob would otherwise drag
the position across a quarter-box boundary and quietly rename the pickup between being seen and
being touched. For the same reason **only the art bobs** — `Dress` refuses to bob a
`SpriteRenderer` sitting on the root, because that would walk the trigger around with it.

Drag in `Assets/Prefabs/Wisp` and `Assets/Prefabs/Heart Upgrade` and they work as they land: a
trigger `CircleCollider2D`, the component, and an `Art` child to drop a sprite onto. Leave that
sprite empty and a flat tinted block stands in, so a freshly placed pickup is always visible rather
than invisibly working.

`RestSite` is a `Checkpoint` that stops to ask. Reaching one marks the respawn point, then offers
**rest and press on** (full hearts, charges back, cooldowns cleared) or **turn back** (bank, end
the run, open the skill screen). Death offers the mirror image: back to the last rest, or give the
run up and go spend what is banked.

`ChoiceScreen` and `SkillScreen` build their own canvas at runtime out of `Ui`, which is the old
editor `UiFactory` with the editor-only parts taken out — including
`AssetDatabase.GetBuiltinExtraResource`, which does not exist in a player and silently produced
untinted white boxes when it was tried. They sit at sorting order 200 and 220, above the Pause
Menu's 100. Both set `Screens.ModalOpen`, and `MenuScreen.Update` checks it, so the pause menu
underneath does not eat Escape.

**A panel that sizes itself must be built with `Ui.Sheet`, never `Ui.Plate` plus a
`ContentSizeFitter`.** `Ui.SetSize` attaches a `LayoutElement`, and a `LayoutElement` outranks a
`VerticalLayoutGroup` when a `ContentSizeFitter` asks how tall the thing wants to be — so a panel
built that way with height 0 fits itself to **zero**. The group then has less room than its
children need and shrinks every one of them toward its minimum. The result is a screen you can read
and cannot use: the text still draws, but each button is 0 pixels tall and has no area to click.
`SetSize` now writes `minWidth`/`minHeight` as well as the preferred pair, so nothing built through
it can ever be squeezed to nothing again.

## The vine

The worked example of a spell owning the wizard's movement.

`PlayerState.OnVine` is a fourth state beside `Normal`, `OnStaff` and `Ragdoll` — **appended, not
inserted**, because the enum serialises as an int in scene YAML.

**The body stays Dynamic and is steered by velocity.** This is the one thing not to change. Going
kinematic and teleporting with `MovePosition` — which is exactly what the staff does — swings the
wizard *straight through the level*, because a kinematic body is not stopped by static geometry.
The staff gets away with it by standing still against a ledge it has already checked; a swing
covers ground, and the ground has walls in it.

So each step works out where the swing wants the wizard, and asks for the velocity that gets them
there: `(wanted - position) / dt`. Physics then does what physics does, and a wall stops them. The
next step reads their **actual** position back and re-derives the angle and the length from it, so
the arc always agrees with where the wizard really is rather than grinding along the inside of a
wall insisting otherwise. Ending a step more than `Blocked` boxes from where the last one asked
means they hit something, and the swing is killed. That check is against **the step's own target**,
not against the arc recomputed from their position — a rope pulling taut on the first step of a
grab is not the same thing as hitting a wall, and confusing the two throws away the run they
arrived with.

**It is a real pendulum**, which is one line of the fixed tick:

```
spin += (-(gravity / rope) * sin(angle) + lean.x * (swingPush / rope)) * dt
spin *= 1 - damping * dt
```

Gravity always pulls the wizard back under the knot and pulls harder the further out they are, so
**letting go of the stick settles them at the bottom on its own**. Left and right are a *push*, not
a speed: you pump a swing the way you would on a real one, and how much you get out depends on when
you push. `damping` is what stops it swinging forever.

Catching a vine keeps whatever you were already doing — the run you arrived with is projected onto
the arc — and letting go leaves at the speed you were genuinely travelling, `spin * depth`, capped
by `maxReleaseSpeed` so a long vine swung hard cannot fire the wizard across the level. The arc has
been showing the player that speed for the last second or two, which is what makes it aimable.

Hitting `maxSwing` only kills the swing if it is still trying to go *further* out. A swing that
reaches the limit already on its way back keeps its speed, or every big swing stalls at the top.

### The knot is the whole mechanic

A vine hangs invisible. What the level shows is a **knot** tied to the ceiling, and it pulses
between `dormant` and `glow` whenever the wizard is close enough to reach it. Press the button and
`CallDown()` unrolls the vine over `unrollTime` and you are already swinging on it.

That means a vine costs the player nothing to walk past and everything to notice — and it is why
`glow` matters more than any number here. If a player never learns what a glowing knot means, no
vine in the game ever gets used.

`staysDown` (on by default) leaves a called vine hanging for the rest of the run: finding it was
the achievement, not re-finding it. Turn it off and it rolls back up — and it rolls itself up by
**asking** whether the wizard is still on it rather than waiting to be told, because letting go
with Jump never passes through the spell at all.

Reach is measured to the nearest point *on the vine* rather than to the knot, so a long vine can be
caught anywhere down its length. Both the knot and the rope want a `sortingOrder` above the
tilemap — at −1 a vine hangs behind the wall it is tied to and simply cannot be seen.

The drawn rope leans with the swing: it is turned to the angle the wizard is actually hanging at,
rather than standing bolt upright while they arc away from underneath it.

## Components do not resize your art

A component that builds a stand-in when you have given it nothing may size that stand-in however it
likes. **A component that finds art you put there must not touch its transform.** `VineAnchor`
tracks whether it built its own knot and rope and only fits the ones it made; `WindZone2D` has
`fitHazeToZone` to switch its own fitting off.

This is not a style point. `OnValidate` runs on every inspector change, so a component that writes
`localScale` there will snap a prop back to its own idea of the right size the instant you finish
dragging the handle — and it will do it every time, which reads as the editor being broken rather
than as a component being helpful.

The one exception is a size that *is* the mechanic: the vine's length, which is the rope unrolling,
and the wall's height, which is it growing. Even then only that one axis is driven — the vine keeps
whatever width it was authored at.

## Testing a spell without earning it

Drop a `Playtest` component on anything in the scene. It lists every spell in the book with a box
each — tick the ones you want, press Play, and they are already learned and in a slot. The list
fills itself in from `Assets/Resources/Spellbook.asset` and keeps itself in step, so a spell added
later turns up on its own. `wisps` and `bonusHearts` start you with a purse and a longer bar for
trying the skill screen.

**Nothing it does is written to the save.** `Progress.BeginSandbox()` wipes the in-memory tiers and
puts `Save()` to sleep for the session, so a playtest cannot spend, unlock or consume anything in
the real one. It runs at `[DefaultExecutionOrder(-100)]`, ahead of `PlayerCharacter.OnAwake`, which
is the frame the spellbook reads `Progress` and builds its slots.

Three ticks is the most that can be carried: there are four buttons and the Staff owns one. Tick
more and it says which ones it could not fit rather than quietly dropping them.

## Prefabs

Everything below is a plain object you drag in — no menu items, no tooling. All the art is Unity's
built-in Square tinted a flat colour, so each one is visible the moment it lands and is waiting for
a sprite of yours.

| Prefab | What it does | Layer |
| --- | --- | --- |
| `Wisp` | The currency. Banking it spends it for good. | Default |
| `Heart Upgrade` | +1 max HP, permanently, once ever. | Default |
| `Rock` | Trips a runner. `minimumSpeed` 4, so a walk is safe. | Hazard |
| `Slime` | Bounces you three boxes and sends you tumbling. | Hazard |
| `Wind` | Pushes you sideways, and shows it. Bubble turns this into a lift. | Hazard |
| `Vine` | What Vine Grasp catches. | Default |
| `Boulder` | What Telekinesis lifts. Solid, so it is also a platform. | **Ground** |
| `Stone Wall` | What Wall Growth raises. Already wired into the spell. | **Ground** |
| `Level Exit` | The bottom. Starts the level again for now. | Default |

The two on **Ground** are there because the wizard has to be able to stand on them, and the ground
check only looks at that layer. The three on **Hazard** are triggers you pass straight through —
`Hazard.passThrough` sets that on Awake, so ticking the box fixes one already placed.

`Stone Wall` is shaped the way the spell needs: the root carries nothing and the `Body` child sits
half a box up with unit size, so scaling the root grows the wall upward off its own footing instead
of stretching it around its middle. Any wall art of your own wants the same two-object shape.

`Level Exit` reloads the level and clears the last rest site, so a lap starts from the top rather
than dropping you back where you sat down. `nextScene` is there for when there is somewhere to go;
`banksWisps` stays off while this only goes round again, or a lap would pay out forever.


## Checkpoints

`PlayerCharacter` moves to `Progress.CheckpointPoint` in `OnAwake`, **before** `logic.Attach` —
attaching records the current height as the one to measure the next fall from, so moving afterwards
would bill the wizard for the trip. Health comes back full because `Attach` restores it.

A checkpoint is an empty object with a trigger `BoxCollider2D`, a `Checkpoint` and a sprite
child. `respawnOffset` lifts
the spawn point clear of the floor, and the live checkpoint tints itself — read back from
`Progress` rather than remembered in a static, since the level reloads on every death and a static
would be pointing at a destroyed object by the time it mattered.

**The checkpoint is deliberately not saved to disk.** It belongs to the run, and a run is a
sitting; quitting halfway down loses the descent — and with it the Wisps you were carrying, which
means the pickups they came from are waiting for you again. Only the permanent tier is written, and unlike
the old save it is also **read back** — `Progress.Load()` runs on `BeforeSceneLoad`, after
`ResetOnPlay` clears the statics. The previous save was write-only: three keys were written on
every checkpoint and nothing ever called `Load()`, so banked anything would have quietly evaporated
on the next launch. `Progress.ForgetAll()` (and `Game.EraseProgress`) wipes it.

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

## Ground, friction and getting stuck

**Contact friction is 0** (`Movement.surfaceFriction`), applied as a material in `Attach`.
Horizontal speed is written outright every physics step, so contact friction never helps the wizard
move — it only fights them, catching on corners and seams. The only thing that slows them is
`groundFriction`, which is a number you can see. That makes `Ragdoll.slideFriction` the only thing
that stops a tumble.

**Everything the wizard stands on must be on a layer in `Movement.groundLayers`** (Ground, layer 6).
Physics does not care about that mask, so a wrong layer still holds the wizard up — but every query
comes back empty, and then `IsGrounded` is never true, which means no jump, permanent air control,
no ledge detection and no staff. Tilemaps start on Default, which is how you walk into it. The
wizard logs a warning naming the mask if it has not found ground after three seconds.

A tumble also ends on a timer (`Ragdoll.maximumDuration`) as well as on landing. A ragdoll that can
only recover once grounded is one bad drop — or one wrongly-layered floor — away from a wizard who
can never move again.

## The staff

A child of the wizard with its own hitbox and sprite. **The hitbox's height is the mechanic** —
the wizard travels its span and then the length of their own hand-hang past the tip, so a taller
collider is a longer climb and nothing else has to be told about it.

**Ladder** is the only mode a spell reaches today. The pole is driven in just past the lip with its
top flush to the ledge, so the far end is where your feet will end up and you can read the drop off
it. Slide down, and keep pushing down at the bottom to let go.

`Staff.cs` can still lay the pole flat as a **Bridge** — `StaffMode.Bridge`, `PlantAsBridge`, and a
separate solid collider on a child on the Ground layer, because the staff itself is on the Player
layer, which the ground check deliberately ignores. Nothing casts it since the Staff Bridge spell
was retired. It is left in because it works and is one small class away from coming back.

The guards it left behind are still earning their keep. `StaffIsFree` and `StaffIsPlantedAs(mode)`
say what the pole is busy *with*, not merely that it is busy, and `TryPlantStaff` refuses a pole
already in the ground. Without that, standing on your own bridge puts you at a lip with `IsAtEdge`
true and the Staff spell would happily re-plant the same pole as a ladder, pulling the floor out
from under you.

## Hazards

| | What it is | Notes |
| --- | --- | --- |
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

### Seeing the wind

A rock and a slime are objects — you can see them coming. Wind is a volume, and a flat tinted
rectangle tells you nothing about which way it blows or how hard, which makes it the one hazard a
player can only learn by being caught out. `WindZone2D` draws itself three ways:

- **Streaks.** A drifting scatter of thin bars, turned to face the push and travelling at
  `streakSpeed` times it. They wrap around the zone, and each one fades in at the edge it enters by
  and out at the one it leaves by, so nothing blinks into being mid-air. Built at runtime — they
  live under one container at the scene root rather than parented to the zone, because stretching
  a wind zone would otherwise stretch every streak with it.
- **Haze.** The flat rectangle, resized to the collider in `OnValidate`. Stretch the zone and the
  tint follows; there is no second thing to keep in step.
- **Arrows.** Scene-view gizmos, drawn *without selecting the zone first* (`alwaysShowArrows`),
  because the whole point is seeing where your wind is while laying a level out. Arrow length reads
  the strength against the same scale as everything else: a run is 6 boxes a second, so a gale you
  cannot walk out of draws longer than the wizard is tall.

`FitHaze` deliberately never creates a sprite, only resizes one — it runs from `OnValidate`, and
building a texture inside a serialisation callback earns a console full of warnings. `Awake` puts
the stand-in in place before the first call.

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

An ordinary screen-space `Canvas` you can open up and restyle:

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
at a third of its size.

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
| `Player/Spell1` | Q | left shoulder | slot 1 |
| `Player/Spell2` | E | west | slot 2 — the Staff, always |
| `Player/Spell3` | R | right shoulder | slot 3 |
| `Player/Spell4` | F | north | slot 4 |
| `UI/Pause` | Esc | start | `Core.Controls` |
| `UI/Skip` | Space, Enter, click | south, start | `Core.Controls` |

Looking down, climbing the staff and swinging on a vine all read `Move`, so they need no action of
their own.

The four spell actions are bound to the four slots **by index**, once, in `Spellbook.Attach` — the
action never changes, only what is sitting in front of it. That is why adding a spell needs no
input work at all, and why the HUD's per-slot glyph cache is safe: a slot's button is fixed for the
life of the scene even as its contents change.

## Setting a scene up by hand

There is no editor tooling any more. These are the things it used to know.

**Tilemap ground**, on the Tilemap that actually has tiles painted on it, never the Grid:

| | |
| --- | --- |
| `TilemapCollider2D` | the shape of the painted tiles |
| `Rigidbody2D`, **Static** | a composite needs a body, and a Dynamic one makes the level fall on Play |
| `CompositeCollider2D` | welds the per-tile boxes into one outline, so you cannot catch on a seam |
| `compositeOperation` = **Merge** on the tilemap collider | without it the composite stays empty |
| layer **Ground** | or the wizard never registers as standing on it |

A `Grid` is a coordinate system. A collider or a rigidbody on one is always a mistake.

**Hazards.** A trigger collider plus a `Rock`, `Slime` or `WindZone2D`, on layer **Hazard**, at the
scene root — several platforms carry non-uniform scales that would squash a child.
`Hazard.passThrough` sets the collider to a trigger on Awake, so an existing one is fixed by
ticking a box.

**HUD.** A `Canvas` (Screen Space Overlay, order 10) with a `CanvasScaler` at
`referencePixelsPerUnit` **32** and **no `GraphicRaycaster`**, laid out as the diagram above. The
two templates start inactive; `PlayerHud` copies them.

**Background.** One object per sheet, unparented, roughly where the camera starts, each with a
`ParallaxLayer`. Put them on a sorting layer below `Default` — **and add that layer to the
`Light2D`'s Target Sorting Layers**, or in URP 2D they render pure black. Simpler alternative:
leave them on `Default` at negative sorting orders, which the existing light already covers.

**Jump arc.** An empty object at the scene root with a `JumpArc`. Not parented to the wizard, or
every dot gets dragged along as they fly.

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
