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

### When a press does nothing

Every gate a spell can fail — wrong state, cooling down, out of casts, nothing in reach — used to
fail **silently**, which makes a spell that will not fire almost impossible to chase: there is no
way to tell an empty slot from a missed ledge from a cooldown.

So `Ability.WhyNot(PlayerLogic)` says, in the player's words, why the press just now came to
nothing, and `Spellbook` prints it the moment a buffered press runs out of patience without ever
going off. Not earlier than that — until the buffer expires the press is still legitimately waiting
for a ledge to arrive, which is the whole reason the buffer exists.

It is editor-only, it covers the empty-slot case too (*"there is nothing in that slot"*), and
`Spellbook.explainRefusals` turns it off. Cooldown and per-run charges are answered generically by
`Spellbook` itself, so `WhyNot` only has to speak for the spell's own conditions.

**Sorting order is the trap it exists to catch.** Both the vine and the wall shipped at −1 and were
therefore behind the tilemap: solid, working perfectly, and completely invisible — which reads
exactly like a spell doing nothing. Anything a spell puts into the world wants an order above the
map.

The Staff is yours from the start and welded to E; everything else fights over three buttons.

| Spell | What it is | Rank 2 / 3 |
| --- | --- | --- |
| **Staff** | Plant it at a ledge and climb down. | longer staff |
| **Mage Hand** | A spectral hand you swing from. | wider swing / climb it |
| **Wall Growth** | One tile of stone, put down on the grid beside you. | further reach |
| **Glide** | A canopy. It barely slows the drop - it carries you. | wider wing |
| **Telekinesis** | Take a hazard with you and set it down where you want it. | longer reach |
| **Haste** | You quicken; the world wades. Once per level. | deeper haste |
| **Fling** | Hold to wind up and aim, let go to fly. | harder throw |

Two of them want level content and already have it: Mage Hand needs a `Vine`, Telekinesis needs a
`Carryable` — which the `Slime` and `Rock` prefabs now carry, so every slime and rock already in a
level can be picked up and put down somewhere better.

**Glide does not forgive fall damage, and that is the design.** Feather Fall — the spell that
used to live in this slot — did, and that forgiveness was the only reason it was worth a button.
Glide earns its slot a different way: it roughly **doubles how far a running jump carries you**,
turning a 2.3-box leap into nearly 4. Giving one spell both would make it two spells over three
contested buttons.

Note what `fallSpeed` does *not* buy. Fall damage counts **boxes fallen**, not how fast
(`UpdateGroundedState` bills `highestPoint - position.y`), so dropping the canopy's fall speed to
0.8 lowers the terminal speed and bills you exactly the same. That is worth letting a player
discover once. You survive under a canopy by **going somewhere else**, not by falling softer.

`AirSpeedMultiplier`, `AirControlMultiplier` and `AirDragMultiplier` are air-only for a reason:
`MoveSpeedMultiplier` would make the wizard sprint along the floor with a wing out. They are
applied after the move multiply and **before** `targetSpeed += wind.x`, so a canopy carries you
under your own steam without also amplifying a gale.

**Steering and coasting are separate multipliers, and they must be.** `Run` picks `acceleration`
when the stick is held and `groundFriction` when it is not; one multiplier over both means a wing
that bites harder when steered *also brakes harder when released* — so letting go dropped the
wizard straight down, which is the exact opposite of gliding. `AirControlMultiplier` scales the
first, `AirDragMultiplier` the second, and Glide pushes them in opposite directions: 1.6 and 0.45.

**`foldsOnLanding` needs the spell's own memory of having flown, not `Movement.Airtime`.** Airtime
is a lifetime counter that only ever increments, so `Airtime > 0` guarded exactly one cast — the
first of a session. After a single jump it was true forever, and throwing the canopy out while
stood at a ledge folded it on the next physics step, put it on cooldown, and left the wizard
falling at completely normal speed with nothing to say why. The `Wing.flown` flag is set the first
step the wizard is off the ground and cleared on every cast.

**Bubble is the opposite trade, on purpose.** It does not forgive the drop — pop it high up and you
are still high up. What it buys is that nothing can touch you (`Modifiers.Shielded`) and that wind
pushes you three and a half times as hard (`Modifiers.WindMultiplier`). A gale you would normally
brace against becomes the ride. It is the only spell that wants a hazard nearby.

**Telekinesis is one button doing two jobs**, chosen by whether your hands are full. Empty, the
press takes hold of the nearest `Carryable` and stows it; full, the press sets it down in the first
tile ahead that will have it. There is no lit window and nothing to time — `activeDuration` is 0
and the only thing standing between the two presses is a short `cooldown`, so the spell cannot
grab and drop on the same press.

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

### Ranks

A spell's rank lives in `Progress.ranks` — `Dictionary<string,int>`, permanent tier, on disk, and
it round-tripped values above 1 from the day it was written, so there was no save-format work to
do. Rank 1 is "learned"; `Buy` and `Grant` hand that out and `Progress.Upgrade` deliberately
**refuses to learn**, so no bug in a screen can hand rank 2 to a spell nobody bought.

The split matters: **`Ability.upgrades[]` is the shop** — a cost, a title and a sentence per rank,
read by the skill screen without it knowing what a `MageHandAbility` is. **The numbers a rank
actually changes live on the spell's own script**, as a `Tier[]` grouped by rank rather than by
stat, because "what does rank 2 give me" is the question a designer asks. There is no separate
`maxRank` field: `MaxRank => upgrades.Length + 1`, because two numbers that can disagree is a bug
waiting to be authored.

A spell reads its rank off the **slot**, not out of `Progress` — `ModifyStats` runs for every slot
every fixed step. `Reload` caches it, and it writes `slot.Rank` **above** its own early-out: below
that line, buying an upgrade would not take effect until the wizard next died, and nothing anywhere
would say why.

A spell overrides `protected override void Validate()` and **never writes its own `OnValidate`** —
Unity delivers `OnValidate` to the most-derived type only, so declaring one would hide the base's
and silently kill every clamp on the chain.

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

## Haste, and why it is not `Time.timeScale`

Haste is a **flag**, `Core.Haste`, that anything which moves under its own steam multiplies itself
by. Scaling time would have been fewer lines and wrong three ways: pausing already owns
`timeScale`; physics runs on a fixed step, so slowing it changes how the wizard's own collisions
resolve; and there would be no way to exempt anything.

As a flag, **the wizard is untouched by construction, and so is their ragdoll** — only things that
*ask* are slowed, and the ragdoll never asks. Wind is the first thing that asks
(`WindZone2D.OnPlayerInside` scales `push`, and its streaks scale with it so the two never
disagree). Anything that moves later reads `Haste.WorldScale` the same way.

`usesPerLevel` is the limit, and it means what it says: charges refill on the scene load that
rebuilds the spellbook, which is what a level transition is. Dying refills it too, since that also
reloads — self-punishing enough not to be worth policing.

## Fling, and the promise the dotted line makes

The arc and the launch are **the same `Vector2`**, computed once per fixed step and handed to both
the prediction and the shove. The moment those become two code paths the line stops being a
promise and becomes a suggestion.

`Movement.PredictArc` is ported from the abandoned jump-test branch, but three adaptations matter
more than the port, and each was wrong before:

- **Hazards do not stop you.** Every hazard here is a trigger you pass straight through, so an arc
  that ended at the first slime would hide where you actually land. It flies on and reports that it
  crossed one, and the line turns red without getting shorter.
- **Wind pushes you mid-flight.** `wind.y` is added outside `Run`, so it reaches a wizard whose
  steering is locked; `wind.x` is not, because `Run` early-returns on lockout. Only the vertical
  component belongs in the simulation.
- **The flight has to be locked, and for exactly the right length.** `Run` rewrites
  `linearVelocityX` every step, dragging it back toward the stick at `airControl × groundFriction`.
  Unlocked, a 14 b/s fling is spent inside half a second and the drawing is a lie. `ArcEnd.Seconds`
  is the arc reporting its own duration, and that is what the spell locks for.

**`Rooted` is set from `ModifyStats`, never from `OnHeld`.** `TryCast` runs before `Rebuild`, and
`Rebuild` opens with `stats.Reset()` — anything written during the hold is wiped on the next line,
and the wizard walks away while winding up with nothing in the console to say why.

**The release is latched in `Observe` and consumed before the hook runs.** `Observe` is an Update
and `TryCast` is a FixedUpdate: polling `WasReleasedThisFrame` from a fixed-step hook misses the
edge on a slow frame and fires twice on a fast one.

A charged spell wants **`pressBuffer: 0`**. Anything else and `WhyNot` complains to the console
every tenth of a second you spend winding up.

## Carrying a hazard

`Carryable` stows an object by **deactivating** it, not by destroying and re-spawning it. That
keeps every field the level author set, keeps its icon available to the HUD while it is stowed, and
means putting it down cannot lose anything.

`Hazard.Disarm` exists because **`Awake` does not run again when an object is switched back on** —
without it, a slime set down at your feet still has the re-arm timer it had when you picked it up,
and bounces you on the very next physics step.

**Placement settles; it does not test one row.** `TileGrid.RestingCell` takes a column and the
height the wizard aimed at, carries the thing up over anything in the way (`StepOver`, two boxes),
then walks it down onto the first floor beneath (`LookDown`, six). What comes back is where the
thing would come to rest — *on top of the tiles, as low as the column allows*.

Asking instead whether one fixed row was empty is what made the spell feel broken. Aimed at the
row the wizard's feet were in, that row is solid floor everywhere except at a ledge — so the spell
refused on flat ground, worked only at edges, and when it did work it dropped the rock into the
thin air past the lip. Settling has no such failure: the answer is right whether the row it
started from was or not.

`needsAFloor` only decides what happens to a column with **no** ground within `LookDown`. On, it
is refused. Off, the thing hangs at the height it was aimed at — which over a drop is a slime the
player placed exactly where they wanted it.

**Set down on the cell's FLOOR, not its middle.** A slime's hitbox is 0.7 of a box tall and a
rock's is 0.6, so `PutDown` on `CentreOf` left both floating a sixth of a box off the ground.
`PutDownOn` measures the thing's own footprint once it is switched back on — `Physics2D.SyncTransforms`
first, because a transform move does not reach the physics shapes until the next step and stale
bounds would settle it against wherever it used to be standing.

## The grid line is not the floor

`mainlev_build.png` is sliced **34×35 px on a 32 px grid**, so every tile is drawn — and collides
— about a pixel proud of its cell. Read it straight off the composite collider in `Level 1`: the
outline sits on `x.03125`, not on whole numbers.

That is not a rounding error to ignore. **It is the surface everything else has to line up with.**
A one-box block built to the bare grid tops out a pixel *below* the floor beside it, and a box
collider walking back onto the platform does not step up over a lip — it stops dead against it. A
wizard who walks out onto their own Wall Growth block and then cannot walk back is a spell that
reads as broken, with the cause nowhere near the spell.

The amount is a property of the **art**, not of the maths, and it is not even the same on both
axes. So nothing carries a constant for it — the two spells each ask the world:

| | takes its height from | why that is exact |
| --- | --- | --- |
| A carried slime, rock or boulder | `TileGrid.SurfaceUnder` — a cast straight down inside the cell, and `Footing.y` if it falls through | it is the tile's own top, whatever the slice did |
| Wall Growth's block | `Movement.Footing.y`, the wizard's soles | they are STOOD on the platform being extended, so their soles *are* its surface |

Re-slice the sheet to 32×32 one day and both keep working, having never been told what the old
number was.

**The cast carries `LookDown`, not one box.** Stopping at a single box meant a cell chosen even one
row high found nothing beneath it, fell back to its own grid line, and left the rock hanging there
— a grid line being the one number in this whole file that is never the floor. Reaching further
cannot pick the wrong surface: a downward cast reports the FIRST thing it meets.

**A footprint is measured in `Stow`, while the object is still switched on.** `Collider2D.bounds`
on a GameObject reactivated the same step answers with the shape the physics world still holds —
which is wherever the thing was standing when it was picked up. Setting something down against
that is how a rock ends up in mid-air, and no amount of `Physics2D.SyncTransforms` fixes it,
because the shape is not stale, it is *absent*.

**A block flush with the platform is the worst case, not the best.** Two box colliders whose top
faces are exactly level still meet at a vertical face, and gravity sinks the wizard about a fifth
of a pixel into the floor between solver steps — enough that the face catches them and the block
has to be jumped onto. The tilemap never shows this because the composite merges its tiles and
there are no internal faces at all.

So `Stone Wall`'s collider is **0.9 square with a `0.05` edge radius**: one box on the outside,
same flush top, but with rounded corners, so a wizard a fraction low rides up over the seam
instead of walking into it. Keep both numbers if you resize it — `size + 2 × edgeRadius` is what
has to come to one box.

Do **not** reach for a custom `physicsShape` on the tiles instead. `spriteMeshType` is Tight and
`physicsShape` is empty, so the slopes and half-tiles get outlines that follow their own art;
squaring all 212 of them off would wall the level in.

## Which row is the wizard standing in

Neither spell counts rows off `TileGrid.StandingCell` any more — Wall Growth finds its floor with
`FloorRowUnder` and Telekinesis settles with `RestingCell`, both of which survive the answer being
a row out. It still has to be right, because it is where they start looking, and **it has to
answer with the empty
cell the body is in, never the solid one holding it up.** One row out is not a near miss — it is
the spell aiming into the ground:

- Telekinesis asked whether the tile ahead was free, got told about the *floor*, and refused every
  cast with "every tile within 3 ahead of you is already filled".
- Wall Growth hunted for the lip of a drop along the row *below* the floor, found solid rock all
  the way out, and refused with "no lip within 3 tiles ahead of you".

Both read exactly like a spell that does nothing, and neither is.

It used to ask `Movement.FeetY + 0.05f` — the ground **probe**, lifted by what the default
`groundCheckSkin` happens to be. The probe hangs below the boots on purpose, and it drifts further
the moment the collider is resized without `FitGroundCheckTo` being run again, which is exactly
what had happened: the probe sat almost a tenth of a box under the wizard, and `floor()` landed a
row low. `Movement.Footing` asks the collider itself — `bounds.center.x` and `bounds.min.y` — and
`StandingCell` lifts that clear of the cell line before flooring it, because the soles rest exactly
*on* that line and `floor()` on a line is a coin toss decided by float error.

`FitGroundCheckTo` now measures from the collider's bottom and its own middle rather than from half
its height about the transform, so a collider carrying an offset — which is what Unity's
fit-to-sprite button writes — no longer bakes that drift in the moment someone hits Reset. The
PLAYER in `Level 1` has been refitted to its collider: `groundCheckOffset` went from
`(0, -0.596875)` to `(-0.050636888, -0.5525605)`, and `groundCheckSize.x` from `0.703125` to
`0.64969389`.

Both probes now hang from one `ProbeOrigin`. The ground check reads it, the ledge check reads it,
and `TryFindLedgeEdge` reports its lip relative to it — otherwise a collider with an x offset of
its own leaves the two disagreeing about which foot is over the drop.

**Wall Growth wants `IsGrounded`, not just `PlayerState.Normal`.** `Normal` is every state that is
not staff, vine or ragdoll, and that includes falling — so the spell would grow a block under a
wizard in mid-air, one press at a time, all the way down. Its own refusal line had been promising
"both feet under you" the whole time without anything checking.

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

## Getting to the loadout, and making it stick

Three separate faults made "I cannot swap abilities" true, and all three had to go.

**The screen could not target a slot.** Every equip path went through `Progress.FirstEmptySlot()`,
and the rail along the top was `Ui.Plate` images with no `Button` on them — a read-out, not a
control. Swapping Q and R was not expressible. The verb now is **pick a spell, then press the
button you want it on** (or click the rail cell). Those four actions already exist and are already
enabled, and the glyph on the rail is the same one the HUD prints under that slot in play — so the
binding teaches the binding.

**`Progress.Equip` was a move-into, not a swap.** It cleared the key from wherever it was and
overwrote the target, so dropping one spell onto another lost the second one silently.
`Progress.Place` trades them instead. `Equip` keeps its meaning for the paths that want it.

**`Playtest` re-dealt the loadout on every scene load.** `BeginSandbox()` → `Clear()` blanks
`ranks`, `equipped`, and the checkpoint, and it ran at execution order −100 — a frame ahead of the
spellbook reading them. Sandbox is now a property of the play **session** and seeding it is a
one-time act: `Progress.SandboxSeeded` gates it, `redealOnEveryLoad` forces it back for when you
are tuning the ticks. `SceneManager.LoadScene` does not re-run
`RuntimeInitializeOnLoadMethod`, so `Progress`'s statics would have survived the reload on their
own — Playtest was the entire failure. It also fixes a checkpoint quietly dying on every load.

**And you could barely reach the screen.** It opened only from a `RestSite` or the death screen,
both of which end the run first. Two doors now: **Play** opens it from the main menu, and while
`Progress.Sandbox` is on, **Escape** opens it mid-level. The second installs only in a sandbox, so
it cannot exist in a real playthrough and "decide before you go down" is untouched.

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

Spells welded to a slot are placed **first**, before any tick is honoured. Otherwise a ticked spell
lands in the Staff's own slot and the spellbook evicts it a moment later when it puts the Staff
where it belongs. That leaves three: four buttons, one of them the Staff's. Tick more and it says
which ones it could not fit rather than quietly dropping them.

### Why the book is a field

`Playtest` keeps a reference to the `AbilityBook` rather than looking one up when it needs it, and
that is not an optimisation. The list of tick boxes is built from the book, and it has to be built
somewhere Unity will actually **save** the result.

The first version deferred that work to `EditorApplication.delayCall`, to keep `Resources.Load` out
of `OnValidate` — where it can be refused mid-deserialisation. But a serialized field written from
`delayCall` is changed on the live C# object only. Nothing marks the component dirty, so the list
sitting in the inspector never reached the scene file, and entering Play Mode deserialised an empty
one straight back over it: no ticks, nothing granted, every slot empty.

With the book in a field, `Sync()` runs directly in `OnValidate`, where Unity re-serialises what it
changes. The deferred path survives only to fill in a component that predates the field — and it
calls `EditorUtility.SetDirty` afterwards, which was the piece missing all along.

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

`Stone Wall` is shaped the way the spell needs: the root carries nothing and the **first child**
carries the sprite and collider, one box square and centred on itself. The spell moves that child
to whichever corner it is growing away from and scales the root, so the block grows out of its
anchor rather than stretching around its middle. Any stone art of your own wants that same
two-object shape.

**Where it grows is the whole spell.** Stood at a ledge it builds *out from the lip* with its top
level with the floor, so you walk straight onto it — the same anchor the old staff bridge used,
`edgeX + facing * clearance`, found by `Movement.TryFindLedgeEdge`. The first version hunted for a
floor **ahead** of the wizard instead, which fails at exactly the one place you would ever cast it:
the whole reason to cast is that there is no floor ahead. Nowhere near a ledge, it falls back to
that floor hunt and grows a wall upward instead.

`size` decides which it reads as. Wide and thin (the default 3 x 0.5) is a plank over a gap;
narrow and tall is a wall to climb or hide behind. Only the axis it is growing along is animated,
so the other is right from the first frame and there is something solid underfoot immediately.

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

**Where it is written.** The permanent tier is pretty-printed JSON in `<project>/Saves/progress.json`
— beside `Assets`, so it is in the working copy where you can open it, read it and diff it. It used
to be PlayerPrefs, which on Windows is a handful of registry values nobody can track, and which hid a
stale save so well that turning the playtest sandbox off made a long-forgotten Glide reappear.
`Assets/Scripts/Core/SaveFile.cs` does the reading and writing: it writes to a scratch file and swaps
it in, so a crash mid-write cannot truncate the save; it sorts the ranks and the spent list so a diff
means something; and it answers **three** ways, not two — `Missing`, `Loaded` and `Unreadable`. That
third one is the whole point. A save that exists but will not open must never read back as "nothing
saved", or the next purchase writes a blank over it. A build keeps the same promise beside the
executable, dropping to the OS save folder only when that is read-only.

## Tripping

A trip **launches you onward** — your speed, plus `launchForward`, never below `minimumLaunch`, and
`launchUp` of lift so you actually leave the floor. Landing back down, the skid bleeds at
`slideFriction` boxes per second squared. All of it on `PlayerLogic ▸ Ragdoll`, and all of it the
same every time: a wet floor sign, a slime and a bad staircase throw you identically, because none of
them supply their own knock — they just call `Trip()`.

The one exception is the **rake**, which calls `Trip(int)` to throw you *back the way you came*. That
overload also drops the momentum you arrived with, because `Begin` adds `launchForward` to your
current speed: keep it, and a wizard running in at 4 boxes a second and "thrown backwards" comes out
still going forwards. Do not add a second hazard that reverses you — one is a punchline, two is a
movement system that takes things back.

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
