# 3D locations (`mods_private/scene3d_base`)

`scene3d_base` owns location presentation and local instance state. Characters, inventory,
equipment, quests and scenario execution retain their existing owners. A logical scene selects
the registered shell with `"Shell": "3d"`; `"SaveAllowed": true` permits saving in a location
without a default dialog. An absent shell keeps the existing 2D path.

## Authoring and ownership

Create `location3d` entities under `locations3d/`, with `Scene` matching the logical scene and
`Entry` naming a point. Each `Objects` row names either an ordinary world3d `Placement`
(`placement3d`) or a reusable `Object` (`object3d`). A local `Transform` composes before the
referenced placement transform; neither edit changes the reusable catalogue. Its local
`Id` identifies an instance independently of its reusable placement/model. `Frames` and objects
form a validated parent graph; points, zones and navigation surfaces name a parent in that graph.
Transforms use world3d metres, degrees and scale. Moving a frame retains every descendant's local
transform, including an actor anchored to an upper deck of a compound support.

The Data Editor supplied by `schema_tools` exposes location, environment and collision-layer
forms. Choose the writable mod, choose **3D environment**, press **New**, edit fog/sun/ambient
energy and GI quality, then Save. Select that environment in a location's `EnvironmentProfile`
field. Point, object, frame, zone, surface and link lists use ordinary schema cards;
vector/geometry values are JSON. The visual workbench is `scene3d_editor` (see below).
Unknown authored fields and keyed inheritance follow the entity pipeline.

`Scene3dHost` owns its content tree, native environmental resources, navigation cache and world3d
instances. Imports use asset3d's cancellation token and owned result. It renders the visible
location once behind the opaque loading overlay before enabling input. Cancellation/exit frees
native trees before owned resources and drops used asset memory entries while preserving disk
caches. A state-generation guard prevents an old host's exit snapshot overwriting a newly loaded
save or another game.

`Scene3dConstruction` coordinates isolated previews and gameplay construction through
separate object, annotation, navigation and environment builders. Its resource owner
releases instances, the content tree, retained native resources, navigation meshes and
asset-memory leases in that order. Extensions hand their own resources to
`OwnResource(resource)` for that same release order. Catalogue placements are copied through a named JSON
copy operation so unknown provider payloads survive without mutating their source.

Callers register the content root with `RegisterNode("", content)` before building;
extensions may register already-parented supports the same way. `Nodes`, `Objects`,
`Points`, `Zones`, `Links` and `Gangways` expose borrowed read-only collection contracts.
Build objects first, then the environment and its optional contributions, then points,
navigation and zones. Join cancelled imports before `Release`; the construction frees
its content and resources, while the caller retains ownership of the surrounding viewport.

## Visual location editor (`scene3d_editor`, Stage 6)

The independent tool depends on scene3d_base, world3d_editor and shared tools/UI. It
edits location composition; profile bindings/import recipes remain in world3d_editor.
Launch a game with `--with-mods=scene3d_editor,<content pack>` and open **3D Location
Editor** from Tools. In the front end, `--frontend --with-mods=scene3d_editor,proto_sea_pack`
offers the temporary sea pack. The palette includes every loaded `object3d` provider,
not just objects owned by the destination mod. Select the writable destination, create
or choose a location, then add an object or place it by clicking the viewport.

The hierarchy supports nested movable frames and whole imported compound objects.
The inspector edits position/rotation/scale, named collision layers, visibility/LOD,
environment/light, points/capacity/spacing, scenario interaction/trigger/exit zones,
and point-to-point gangways. Choose a model's semantic role to create an arrival
point at that socket. Reparenting preserves world pose; a sheared transform outside
the saved TRS contract is rejected. IDs do not follow hierarchy order. Copy remaps
the copied subtree and internal references; delete removes descendants/incident links.
Undo/redo treats a drag or subtree operation as one change.

Right drag orbits, middle drag pans, the wheel zooms. Move/rotate/scale handles use
parent-local axes; move can snap to the grid or actual transformed visual triangles.
Escape cancels a live drag before it creates history. Frame All ignores locators;
Focus Selection frames a support, object, point or physical gangway. Unselected
handles stay small; only the selected handle shows its label/axes.

**Bake selected support** collects its actual mesh triangles in that support's local
coordinates, then runs native asynchronous navigation baking. Agent radius/height/slope
are explicit controls. Radius rounds up to the 0.25 m voxel size; vertical voxels are
0.1 m. The saved vertices/polygons are the runtime's `Nav3dSurface`, not a visual proxy.
Navigation and zone overlays are independently switchable. Changing model geometry,
local children or bake parameters requires rebaking; moving the parent support does not.

`Location3dDocument` keeps original JSON/unknown fields and delegates history/minimal
foreign patches to EntityDocument. Foreign source files stay untouched. Saving a new
own location also registers its logical 3D scene and refreshes the normal resolvers;
referenced object/scenario providers use the shared dependency helper. Inline light
edits detach a chosen environment profile into an own layer rather than altering it.

Preview owns a separate World3D and detached location state. It shares
`Scene3dConstruction` with Scene3dHost for geometry, profile/placement transforms,
environment, collisions, points, zones and navigation. A preview never becomes the
current host, starts scenarios or writes save state. For a location instance,
`Collision` selects imported `asset`, owned `trimesh`, or `none`; collision/navigation/
zone layers retain their separate meanings. Replacement cancels and joins the prior
import before freeing its trees/resources and releasing used asset-memory entries.

**Save and play here** saves the authored location and enters its real logical scene
with the selected arrival point. It snapshots the normal save package; F8 or Stop
restores that package and returns to the prior scene/editor. It does not implement
an actor controller: presentation and input remain the actor/character workstreams.
The acceptance probe uses the public PlaceActor contract to verify the arrival.

The opt-in `scene3d_editor__contracts` suite constructs a room and boarding deck,
a nested small-ship support/deck and a medium ship. Its physical gangway joins their actual
starboard/port semantic sockets. Tests verify baked paths, named-layer collision,
source bytes, patch reset/reopen, history, cancelled drags/replacements, runtime parity
and play-state restoration. Exact evidence and isolation limits: WORLD3D_PLAN.md,
Stage 6. Captures/provenance stay outside all repositories.

## Environment, visibility and physics

Optional `IScene3dEnvironmentBuilder` contributions run after the base camera and
environment and leave with their provider mod. Water uses this hook for location
`Water`/`Floating` extension fields; see [water and floating supports](water3d.md).
The hook adds no water or terrain dependency to the location owner.

The inline environment or referenced `environment3d` profile supplies sky/ground colours,
directional sun, ambient colour/energy and fog. `Gi` is `off`, `low` or `high`: supported Forward+
rendering uses dynamic SDFGI with two/six cascades. Mobile/Compatibility retain direct and ambient
lighting. This never changes the project's renderer, HDR, exposure or display mode.

Object `VisibilityBegin`/`VisibilityEnd` map to native visibility ranges; `LodBias` controls native
mesh LOD selection. LOD geometry comes from the asset recipe's `GenerateLods` option. A visibility
range cannot create absent mesh LODs. Neither rendering choice changes logical state.

`collision3d_layer` entities allocate unique bits 1–32. Runtime consumers resolve semantic names
(`scene3d/world`, `scene3d/actor`, `scene3d/trigger`, `scene3d/navigation`), never literal masks.
Physical collision, navigation surfaces and interaction volumes are separate contracts.

## Points, navigation and interactions

Points have a local transform, capacity and spacing. Reservation is per logical actor ID and
slot; a full destination leaves the previous reservation untouched. `PlaceActor` accepts an owned
presentation node and parents it to the point's support. Revisit/load restores its local anchor;
the caller recreates presentation from the logical character owner. `ReleaseActor` transfers a
detached node back to the caller, which must reparent or free it.

Navigation surfaces contain authored local vertices and convex polygon indices. The cache key
includes surface data, algorithm version and Godot version; moving a support does not bake a new
mesh. Cached native resources have a SHA256 receipt checked before ResourceLoader, atomic
publication and a 64-entry/16 MiB disk bound. Navigation regions follow their support. Links join
named points, obey distance/enabled/bidirectional policy and can also own a physical gangway.
`SynchronizeNavigation` waits for initial region iterations and synchronizes the map before an
immediate fresh path query after a batch of support moves. Let the scene's physics transform
update run first: native region transforms are published during internal physics processing.
Ordinary `Path` queries do not force
updates. `Transfer` reserves the other endpoint before reparenting the same actor node. Continuous
locomotion and grounding are implemented in `actor3d_base`, with combat in
`combat3d_base`; Transfer is point placement, not their continuous movement path.

The native update order is visible in Godot's
[NavigationRegion3D implementation](https://github.com/godotengine/godot/blob/4.6-stable/scene/3d/navigation/navigation_region_3d.cpp).
Two frame signals alone did not guarantee a ready navigation map in the acceptance fixture;
the explicit region-iteration/map synchronization is covered by the moving-support contract.

Box zones use transformed local geometry. `interaction` activates on the interaction action (E);
`trigger`/`exit` respond to physical actor entry. Dispatch is deferred out of the physics callback
because a scenario/exit can pause or free physical objects. A zone runs its referenced scenario
through optional dialog integration with scope `<location-id>:<zone-id>`. The base location and
readiness do not depend on dialog_base. `Once` completion is stored per local zone. Rewards still
use normal item/equipment/quest actions.

The command palette describes:

```text
action scene3d.set instance=chest.a field=opened value=true
action scene3d.travel scene=town point=gate return=door
action scene3d.return
```

`field` avoids the dialog DSL's reserved `key` argument. `value` is JSON; quote/escape according to
the ordinary DSL for strings/compound values. Do not pass `point` for a 2D destination. Exits
validate the destination point/capacity before changing state. Failed asynchronous 3D preparation
restores the pre-transition slot/return stack and re-enters the previous logical scene.

## Actor integration contract

The owner approved `TryReanchorActor(actorId, parentId, reservationPoint = null)`
after the initial integration review. The location remains the sole owner of saved
support identity and occupancy. `PlaceActor` restores or places an entry anchor;
`Transfer` uses point placement; `ReleaseActor` unregisters it. Continuous grounding
uses reanchoring to preserve world pose, node identity, velocity and animation.

Reanchoring validates the active host, registered actor, parent and optional point
before mutation. It updates parent/local transform/point/slot/occupancy together.
Null reservation releases the entry slot and stores Point null / Slot 0; an explicit
reservation must belong to the selected parent and obey capacity, without teleporting.
Same-parent calls are allowed. Invalid/full destinations preserve all previous values.
Paused or obsolete save generations, cyclic parents, noninvertible transforms and
sheared local transforms outside the saved TRS contract are rejected. Actor code must
not edit scene3d save dictionaries to emulate this operation.

Verified on the host, 2026-11-15: the two new location tests preserve world pose,
node identity and velocity, reject missing/wrong/full destinations atomically,
release a reservation on the same support, and save/load the new support without
reviving released slots. All eight location contracts pass; complete logs:
`/workspace/art_sources/cthangover/actor3d_base/verification/20261115/final/scene3d/`.
Actor stage 7 now supplies continuous control through `actor3d_base`.

Architecture boundary: actor3d owns the capsule, acceleration, walk/run/strafe/jump,
contact-normal grounding, physical step/headroom sweeps, support tracking, camera,
input, interaction, navigation and orders. `Actor3dPresentation` owns a
`Character3dPresentation` from `characters3d_base`; it calls semantic `Play`,
`SetDistance` and exactly one `Advance` per simulation tick. Body/skeleton policy,
visual equipment, sockets, replacement, hitboxes and visual sampling budgets stay
in characters3d. The adapter supplies the actor ID as `LogicalId`; it creates no
second health, inventory, equipment or character roster.

An `actor3d` recipe in `actors3d/*.json` references a `character3d_presentation`
through `Presentation`. `Location` selects the logical location ID, `Point` is an
initial arrival, and `Player` enables input/camera. Use the reserved actor ID
`player` for scene3d travel arrivals. Existing scene3d anchors take precedence on
reload. Character visuals and controller nodes are recreated; freed entry slots
remain freed. Recipes and unknown extension fields round-trip through ordinary
entity JSON. The generic Data Editor exposes the character picker and physical
numbers; there is no duplicate representation editor in actor3d.

```json
{"Kind":"actor3d","Items":[{
  "Id":"player","Location":"my_game/harbour","Point":"entry",
  "Presentation":"my_game/hero","Player":true,"WalkSpeed":2.6,"RunSpeed":5
}]}
```

`VisualYawDegrees` aligns a model's authored forward axis with the collider's -Z
forward (the demo's +Z models use 180). Optional `RootMotion` converts canonical
clip translation through visual orientation/height into requested horizontal
velocity, then resolves it with the same capsule. Gravity/jump and collisions
remain authoritative. Character LOD always advances events/motion; actor3d chooses
distance from the current camera. Far simulation and crowd scheduling are
implemented through `Actor3dSimulationSession` and optional `navigation3d_base`;
they retain logical action/order checkpoints rather than using a second model.

The grounded actor is parented to its contacted support. Native platform transport
is disabled to avoid applying that transform twice. A gangway has no saved frame
identity: the actor temporarily uses the location root and follows its actual
moving transform, then reanchors on the next deck. Physics queries determine
floor, slope, step and headroom; no analytic floor snap bypasses collision.

WASD/arrows, Shift, Space, Ctrl, E, R and Tab are defaults for movement/run/jump/
strafe/interact/draw-or-sheathe/orbit. Collection Settings provides ru/en key choices.
Owned defaults refresh when a game/location is entered, preserving foreign bindings.
Orbit releases the pointer through a shell lease; modal UI blocks input and simulation.
The controller consumes the scene stub's old interaction action even after remapping,
so the legacy E key cannot bypass its visibility/occlusion checks.

NPC `Orders.Manual` overrides `Orders.Automatic` until explicitly cleared with null.
Commands move to a point/world position, follow an actor, maintain distance or stop.
Navigation supplies intent; the capsule performs motion. Missing targets and partial
unreachable paths do not teleport. A moving NPC's scene interaction zone follows it;
scenario dispatch and one-shot state remain scene3d-owned. The DSL shares parameters
between actions: specify `automatic=true` or `automatic=false` explicitly in commands.

```text
action actor3d.order actor=crew kind=follow target=player distance=1.5 run=true automatic=true
action actor3d.order actor=crew kind=stop automatic=false
action actor3d.order actor=crew kind=clear automatic=false
```

Host evidence and reviewed real frames are outside repositories:
`/workspace/art_sources/cthangover/actor3d_base/REPORT.txt` and
`verification/20261115/` beneath that root. The opt-in demo runs through
`authoring3d/actor3d/run_checks.py --case actor3d_demo`, via the host watcher only.
The ordinary mod does not load the two-ship fixtures or private sea fixtures.

## Opt-in swimming and supported traversal

`Actor3dDefinition.Movement` enables traversal for an individual actor. Omitting it
keeps the existing ground controller and order/activity interfaces. The profile
owns capability flags, water/climb speeds, capsule envelopes, ledge reach and
fall/breathing parameters. Its air-stat ID refers to a pool supplied by stats;
actor3d does not create a second air or health counter.

The body-owned traversal activity acquires `Actor3dActionControl` over the same
native capsule. Swimming uses the horizontal envelope and an explicit CPU-water
target. Climbing and ladders use a compact supported envelope and swept targets;
the target takes precedence over extracted root translation. Ordinary ground
motion returns to the legacy root-motion policy. A blocked standing expansion
retains the safe envelope and reports restoration pending instead of forcing the
actor through geometry. Falls keep native collision authority even at distant LOD.

Water sampling belongs to the optional water integration. It reads the matching
location's current CPU wave height, flow, bottom and dry-volume policy without
advancing that world's clock. Horizontal current accumulates below the action's
arrival tolerance, with bounded backlog when obstructed. Shore exit requires a
real floor and full standing clearance. Dive requires both its capability and a
participating air pool. Breathing measures the published head position, refills
only in breathable air and applies drowning damage through existing attributes.
Fall damage also uses those attributes and does not require an air pool.

Detected ledges require a wall, a reachable walkable top, standing headroom and
permission from no-climb volumes. The wall reach starts beyond the effective
capsule envelope, including a swimmer's horizontal length. The top probe follows
the actual wall contact so a narrow raised edge cannot be mistaken for the lower
surface behind it. Dock, reach, hold, pull and transfer remain collision-driven.
Hand IK targets are support-local, and the low grip leans around the pelvis rather
than stretching limbs or moving planted feet through the floor. Capsule control
returns only after the standing body reports native floor contact.

Authored `traversal3d` markers contain location, parent-local Start/End transforms,
one capability, direction policy and an optional interactive/action identity.
`BlockClimbing` changes a marker into an oriented no-climb volume. This catalogue
is optional: consumers use `Entities.Collect<Actor3dTraversalDefinition>(required:
false)`, so ordinary locations need no placeholder file. Markers do not create
collision or authorize a missing landing. Their ladder endpoint rotations supply
facing while the support moves; player up/down intent is independent of camera yaw.

Ladders and ship ropes borrow the existing interactive reservation. Its optional
native-activity factory supplies a traversal adapter, while existing activities
keep the default implementation. The interactive action's `TraversalLink` extension
names its matching marker; the marker names the same interactive/action pair.
The `WaitForArrival` phase rejects animation
event/synchronization confirmation: only the adapter's physical arrival completes
it, with timeout and effect receipts still owned by interactive3d. Descending uses
separate reversed enter/loop/exit roles. Dropping retires support control and lets
ordinary gravity produce the eventual landing result.

Navigation portals carry capability requirements and traversal IDs through the
managed worker into `Actor3dPath`. Unsupported routes remain unreachable. Water
crossings retain their admitted endpoint until reaching its destination region;
periodic replanning must not send a swimmer back to the entry bank. Ladders reserve
their interactive before physical traversal. Navigation remains an intent source;
neither a ready corridor nor a capability flag teleports a capsule. On a supported
climb, Space requests a voluntary drop; W/S drive ladder travel independently of
camera yaw. Interactive cancellation also releases its seat and support through
the admitted owner, including actors without voluntary JumpDown capability.

The existing actor simulation save slot contains traversal mode, local endpoints,
progress, transition time, fractional drowning interval and effective capsule.
Native references are reacquired after loading. A ladder also retains its original
interactive identity, so the interactive owner restores the seat before the action
lease binds. Stats and character/combat owners continue saving air and health.
Living traversal suspends combat navigation/pose takeover and cancels an existing
combat interval through its order API. Defeated characters release that pose guard.

The optional editor integration joins the location workbench: movement fields,
normal entity history/foreign patches, isolated character/capsule previews and
support-local crossing/no-climb markers. These utility controls use `ModWidget`;
opening a preview does not enter gameplay or replace its save generation.

## Saves and transitions

The `scene3d_base` mod slot contains version-2 `Locations`: the unique world-instance registry plus local instance JSON, movable frame transforms,
actor parent/point/slot/local transform, completed zones, pending arrival and the return stack.
Malformed or unsupported data is rejected so the normal dispatcher retains it dormant. Reset
replaces state; core mod-slot dormancy/revival preserve bytes while this mod is absent. Hot reload
parks a normal save package before unloading and restores it after the new owner loads. Starting
another game follows `GameSession`, without a second save or inventory system.

Acceptance fixtures are independent abstract rooms/platforms in the opt-in contracts suite.
`test_scene3d_lifecycle` depends only on shared UI and actually removes/reloads scene3d during
game switches, avoiding a test reference that would pin the location assembly.

See [shared shell](ui-and-shell.md), [save slots](saves.md), [scene composition](scenes-and-conditions.md)
and [3D assets](assets3d.md). Verified runs and limits are recorded in `WORLD3D_PLAN.md`.


## Prepared terrain-map integration

`map3d_base` selects a unique recipe for the ready location and observes actual actor poses through an optional
integration. It owns no terrain residency or additional gameplay camera. Its modal prefab borrows shell pause
and pointer policy; prepared image/fog textures belong to its visible panel. Shared `exploration_base` owns
explored-cell persistence. Authoring and one-shot capture/terrain rasterization remain in `map3d_editor`,
without publishing preview state into scene saves. See the [map ownership contract](../../mods_private/map3d_base/docs/architecture.md)
and [author forms](../../mods_private/map3d_editor/docs/en.md).

## Actor cosmetic layers — P04

`Actor3dDefinition.Layers` is a keyed list of named role/region/weight/fade profiles.
The actor movement editor provides primary forms, actual controller/body choices,
undo, sparse foreign patches and isolated preview. `TryPlayLayer` returns a reason
when a full-body owner, missing profile or unresolved mask refuses admission.
`actor3d.layer` exposes play/stop through the ordinary command palette.

Only ground idle/walk/run/strafe admit these cosmetic profiles. Explicit full-body
roles/actions, action-capsule acquisition and non-ground R4 modes cancel the slot.
A disposed lease permits admission once capsule restoration has succeeded; a
pending restoration still refuses it. On reconstruction the new body has no floor
flag yet: the same capsule must verify support before preserving a restored gait.
This check does not move the body or bypass collision. Presentation pause preserves
both clocks and layer state during reconstruction.

The existing body controller remains the sole gameplay Advance owner. Base root
motion/capsule/support, combat receipts and interaction ownership are unchanged.
The actor simulation binding transfers the managed layer journal/fade at demotion
and promotion and advances it only when no participant claims animation. Its cached
profile masks come from the actual departing rig; far play/stop uses those managed
metadata without constructing a hidden skeleton. LayerDelivered identifies the
logical actor and separate cosmetic stream. Full-body participants cancel the
cosmetic slot while retaining its retired action sequence.

## Persistent mutable world instances — world-first slice

An opted-in `Location3dObject.Mutable` leaf placement enrolls in the existing scene
slot only during actual gameplay construction, never an editor preview. Authored
identity derives from the global location/stable placement pair, with separate
provider provenance; a runtime birth allocates its own identity. One record owns
the world destination/support-local TRS or an explicit removed tombstone. Its
scene payload is not copied to region views or other domain owners. Registered
origins are suppressed on every reconstruction, including when their providers
are absent. Native absence and terrain eviction never imply removal.

Registry mutations validate generation, revision, dependencies and optional
domain guards, then publish the record and operation receipt synchronously,
without callbacks/awaits in the commit. Identical operation retries return the
committed result; conflicts and failed preparation leave ownership unchanged.
Native publication checks generation/revision/destination/lifetime after import.
The host owns retired roots, generated shapes and the shutdown import join.
Missing providers/supports retain inert state; refresh reconciles when ready.

The `scene3d_editor` author checkbox and separate live-world utility forms expose
creation, explicit relocation, world-pose-preserving reanchor and confirmed
removal. These do not search for collision-free drop positions. Actor, annotation,
compound, rigid-body and optional interactive bindings conservatively refuse
independent transfer; no foreign session is cancelled. Domain providers must
reconcile their own state through read-only admission guards.

The normal Locations slot is version 2, validated before replacing the save
generation; version-1 development slots are not migrated. Tombstones/receipts
never expire by time/LRU. Unique instances remain individual native views,
excluded from any future immutable aggregation. The optional existing-inventory
adapter now preserves unique identity through pickup/drop and validated two-owner
commits. Ordinary item-count APIs exclude protected rows; no parallel bag is
created. Quantity lineage and full rejected-slot recovery remain separate work.
See [runtime API](../../mods_private/scene3d_base/docs/en.md#persistent-mutable-world-objects)
and [author forms](../../mods_private/scene3d_editor/docs/en.md#live-world-instance-forms).
