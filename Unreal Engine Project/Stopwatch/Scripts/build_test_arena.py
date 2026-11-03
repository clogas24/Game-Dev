"""Builds /Game/Stopwatch/Maps/Lvl_TimeTest from nothing. Safe to run repeatedly: it deletes every actor and starts over.

Run from the editor's Python console, or headless with Unreal closed:
  UnrealEditor-Cmd.exe <path>/Stopwatch.uproject -run=pythonscript -script="<path>/Scripts/build_test_arena.py"

Layout: a hub runs along +X. Each station is a lane off the hub towards +Y, with its label at the entrance.
Heights assume the character's 127 unit jump and 500 unit/s walk.
"""

import math

import unreal

LEVEL_PATH = "/Game/Stopwatch/Maps/Lvl_TimeTest"
CUBE = "/Engine/BasicShapes/Cube.Cube"

STATION_SPACING = 3000.0
STATION_COUNT = 11
HUB_END = 1500.0 + STATION_SPACING * STATION_COUNT
LANE_START = 600.0  # where lanes meet the hub
KILL_TOP = -300.0   # below every walkable surface, above where things land
CATCH_TOP = -1500.0  # stops fallen objects, so they stay in reach of Rewind instead of falling out of the world

actors = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
levels = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
cube_mesh = unreal.EditorAssetLibrary.load_asset(CUBE)
built = []


def rot(pitch=0.0, yaw=0.0, roll=0.0):
    return unreal.Rotator(roll=roll, pitch=pitch, yaw=yaw)


def spawn(cls, location, rotation=None, label=None, scale=None, timeable=None, **props):
    """Spawns an actor. props go on the actor, and timeable on its UTimeableComponent."""
    actor = actors.spawn_actor_from_class(cls, unreal.Vector(*location), rotation or rot())
    if not actor:
        raise RuntimeError(f"Could not spawn {cls}")
    if label:
        actor.set_actor_label(label)
    if scale:
        actor.set_actor_scale3d(unreal.Vector(*scale))
    for name, value in props.items():
        actor.set_editor_property(name, value)
    for name, value in (timeable or {}).items():
        actor.get_component_by_class(unreal.TimeableComponent).set_editor_property(name, value)
    built.append(actor)
    return actor


def box(x0, x1, y0, y1, z0, z1, label="Block", rotation=None):
    """Static block covering a range. The engine's cube is 100 units wide."""
    actor = spawn(unreal.StaticMeshActor,
                  ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2),
                  rotation=rotation, label=label,
                  scale=((x1 - x0) / 100, (y1 - y0) / 100, (z1 - z0) / 100))
    actor.static_mesh_component.set_static_mesh(cube_mesh)
    return actor


def floor(x0, x1, y0, y1, top=0.0, label="Floor"):
    return box(x0, x1, y0, y1, top - 100.0, top, label)


def label_text(x, y, text):
    actor = spawn(unreal.TextRenderActor, (x, y, 260.0), rotation=rot(yaw=-90.0), label="Label")
    component = actor.text_render
    component.set_text(text)
    component.set_editor_property("world_size", 32.0)
    component.set_editor_property("horizontal_alignment", unreal.HorizTextAligment.EHTA_CENTER)
    component.set_text_render_color(unreal.Color(r=255, g=225, b=120, a=255))
    return actor


def station_x(number):
    return 1500.0 + (number - 1) * STATION_SPACING


def build_environment():
    sun = spawn(unreal.DirectionalLight, (0, 0, 2000), rotation=rot(pitch=-50.0, yaw=40.0), label="Sun")
    sun.light_component.set_mobility(unreal.ComponentMobility.MOVABLE)
    sun.light_component.set_editor_property("atmosphere_sun_light", True)
    sun.light_component.set_editor_property("intensity", 8.0)

    sky = spawn(unreal.SkyLight, (0, 0, 2200), label="SkyLight")
    sky.light_component.set_mobility(unreal.ComponentMobility.MOVABLE)
    sky.light_component.set_editor_property("real_time_capture", True)

    spawn(unreal.SkyAtmosphere, (0, 0, 2400), label="SkyAtmosphere")
    spawn(unreal.ExponentialHeightFog, (0, 0, 2600), label="Fog")

    # the big floor: the hub all the lanes connect to
    floor(-1500, HUB_END, -1500, LANE_START, label="Hub")
    spawn(unreal.PlayerStart, (0, 0, 120), label="PlayerStart")

    margin = 6000.0
    center = (HUB_END / 2, 3000.0)
    half = (HUB_END / 2 + margin, 3000.0 + margin)

    kill = spawn(unreal.KillVolume, (center[0], center[1], KILL_TOP - 100.0), label="KillVolume")
    kill.get_component_by_class(unreal.BoxComponent).set_box_extent(unreal.Vector(half[0], half[1], 100.0), True)

    box(center[0] - half[0], center[0] + half[0], center[1] - half[1], center[1] + half[1],
        CATCH_TOP - 100.0, CATCH_TOP, "CatchFloor")


def station_1():
    x = station_x(1)
    label_text(x, 500, "1. PENDULUMS\nLeft: left-click to freeze, walk through.\nRight: blade over a pit. It kills. Freeze it to cross the beam.")

    floor(x - 1200, x - 200, LANE_START, LANE_START + 1000)
    spawn(unreal.FreezePendulum, (x - 700, LANE_START + 500, 450), label="Pendulum", arm_length=380.0)

    floor(x + 200, x + 1000, LANE_START, LANE_START + 600)
    floor(x + 500, x + 700, LANE_START + 600, LANE_START + 1800, label="Beam")
    floor(x + 200, x + 1000, LANE_START + 1800, LANE_START + 2400)
    spawn(unreal.FreezePendulum, (x + 600, LANE_START + 1200, 480), label="BladePendulum",
          arm_length=400.0, swing_axis=unreal.Vector(0, 1, 0), kills_player=True)


def stairs(x0, x1, y, count, step_depth=150.0, step_height=90.0):
    """Stairs climbing towards +Y, returning the far edge and the height. Too tall to walk up, so you jump them."""
    for step in range(count):
        box(x0, x1, y + step * step_depth, y + (step + 1) * step_depth, 0.0, (step + 1) * step_height, "Stair")
    return y + count * step_depth, count * step_height


def station_2():
    x = station_x(2)
    label_text(x, 500, "2. FALLING CRATE\nClimb the stairs. Freeze the crate as it falls and step onto it.\nIt resets every 4 seconds.")

    floor(x - 1000, x + 1000, LANE_START, LANE_START + 1800)
    edge, height = stairs(x - 300, x + 300, LANE_START + 200, 3)
    box(x - 300, x + 300, edge, edge + 300, 0, height, "Ledge")
    spawn(unreal.PhysicsCrate, (x, edge + 500, 900), label="Crate", scale=(2, 2, 1), auto_reset_interval=4.0)


def station_3():
    x = station_x(3)
    label_text(x, 500, "3. HOURGLASSES\nFreeze something so the bar drops, then collect.\nLeft: once. Right: returns after 5 seconds.")

    floor(x - 800, x + 800, LANE_START, LANE_START + 1000)
    spawn(unreal.HourglassPickup, (x - 400, LANE_START + 500, 80), label="Hourglass_Once")
    spawn(unreal.HourglassPickup, (x + 400, LANE_START + 500, 80), label="Hourglass_Respawning", respawn_time=5.0)
    spawn(unreal.FreezePendulum, (x, LANE_START + 800, 450), label="SpendSandHere", arm_length=380.0)


def station_4():
    x = station_x(4)
    label_text(x, 500, "4. DROP CHUTE\nThe gap is too wide to jump.\nFreeze a falling crate level with the floor and use it as a step.")

    floor(x - 500, x + 500, LANE_START, LANE_START + 600)
    floor(x - 500, x + 500, LANE_START + 1400, LANE_START + 2000)
    spawn(unreal.DropChute, (x, LANE_START + 1000, 600), label="DropChute",
          interval=3.0, prop_scale=unreal.Vector(2.5, 2.5, 0.5))


def station_5():
    x = station_x(5)
    label_text(x, 500, "5. BOULDER RAMP\nWalk into the trigger on the right: the boulder rolls down.\nRight-click it to rewind it back up. It stays there until you walk into the trigger again.")

    floor(x - 1000, x + 1000, LANE_START, LANE_START + 2200)

    # the ramp rises towards +Y
    run = 1200.0
    rise = 500.0
    angle = math.atan2(rise, run)
    thickness = 50.0
    width = 500.0
    length = math.hypot(run, rise)
    center_y = LANE_START + 500.0 + run / 2
    center_z = rise / 2 - thickness / 2 / math.cos(angle)
    ramp = spawn(unreal.StaticMeshActor, (x, center_y, center_z), rotation=rot(roll=-math.degrees(angle)),
                 label="Ramp", scale=(width / 100, length / 100, thickness / 100))
    ramp.static_mesh_component.set_static_mesh(cube_mesh)

    # rails and a backstop keep the boulder in its lane, so it comes to rest within its recording
    box(x - width / 2 - 50, x - width / 2, LANE_START, LANE_START + 500, 0, 120, "Rail")
    box(x + width / 2, x + width / 2 + 50, LANE_START, LANE_START + 500, 0, 120, "Rail")
    box(x - width / 2, x + width / 2, LANE_START, LANE_START + 50, 0, 120, "Backstop")

    radius = 100.0
    along = length / 2 - radius - 50.0
    boulder = spawn(unreal.PhysicsProp,
                    (x,
                     center_y + along * math.cos(angle) - (thickness / 2 + radius) * math.sin(angle),
                     center_z + along * math.sin(angle) + (thickness / 2 + radius) * math.cos(angle)),
                    label="Boulder", scale=(2, 2, 2), shape=unreal.PropShape.SPHERE, mass=300.0,
                    held_until_woken=True, angular_damping=0.5,
                    timeable={"record_seconds": 16.0})

    trigger = spawn(unreal.RollTrigger, (x + 700, LANE_START + 300, 100), label="RollTrigger",
                    target_prop=boulder, push_velocity=unreal.Vector(0, -100, 0))
    trigger.get_component_by_class(unreal.BoxComponent).set_box_extent(unreal.Vector(200, 200, 100), True)


def station_6():
    x = station_x(6)
    label_text(x, 500, "6. FALLING PLATFORM\nStep on it, then get back: it shakes and drops.\nRewind it. It stays up for good, so walk across.")

    floor(x - 500, x + 500, LANE_START, LANE_START + 600)
    floor(x - 500, x + 500, LANE_START + 1700, LANE_START + 2300)

    # flush with the floor, and too long to outrun in the 1 second shake
    spawn(unreal.FallingPlatform, (x, LANE_START + 1150, -15), label="FallingPlatform", scale=(4, 11, 0.3))


def bridge_station(number, pit, length, hold, text):
    x = station_x(number)
    label_text(x, 500, text)

    floor(x - 500, x + 500, LANE_START, LANE_START + 600)
    floor(x - 500, x + 500, LANE_START + 600 + pit, LANE_START + 1200 + pit)

    # overlaps both floors, with its top barely above them to avoid z-fighting
    overlap = (length - pit) / 2
    spawn(unreal.BrokenBridge, (x, LANE_START + 600 - overlap, -14), rotation=rot(yaw=90.0),
          label=f"Bridge_Hold{hold:.0f}", bridge_length=length, plank_count=max(8, int(length / 150)),
          timeable={"hold_duration": hold})


def station_9():
    x = station_x(9)
    label_text(x, 500, "9. REWIND THEN FREEZE\nWalk into the trigger on the left: the block falls off the tower.\nStand on the low step, rewind it, and freeze it as it rises past you. Jump on, then up to the platform.")

    floor(x - 1000, x + 1200, LANE_START, LANE_START + 2000)

    # too high to reach from the step alone, so the block is needed at between 140 and 220 units
    box(x - 300, x + 300, LANE_START + 500, LANE_START + 800, 0, 100, "Step")
    box(x - 300, x + 300, LANE_START + 1300, LANE_START + 1700, 0, 260, "RaisedPlatform")
    box(x + 150, x + 1000, LANE_START + 800, LANE_START + 1300, 0, 480, "Tower")

    # held at the tower's lip with nothing under it. Sliding it off the top makes it stop short or tip and hang on the corner
    block = spawn(unreal.PhysicsProp, (x - 10, LANE_START + 1050, 510), label="Block", scale=(3, 3, 0.6),
                  held_until_woken=True, angular_damping=20.0,
                  # slow, to widen the moment when the block is in the right place
                  timeable={"rewind_speed": 0.25})

    trigger = spawn(unreal.RollTrigger, (x - 700, LANE_START + 300, 100), label="RollTrigger",
                    target_prop=block, push_velocity=unreal.Vector(-20, 0, 0))
    trigger.get_component_by_class(unreal.BoxComponent).set_box_extent(unreal.Vector(200, 200, 100), True)

    spawn(unreal.HourglassPickup, (x - 700, LANE_START + 1000, 80), label="Hourglass_Respawning", respawn_time=5.0)


def station_10():
    x = station_x(10)
    label_text(x, 500, "10. CRUSHER CORRIDOR\nA crusher kills you when it slams. Time your run, or freeze one while it's up.")

    floor(x - 300, x + 300, LANE_START, LANE_START + 2300)
    box(x - 300, x - 240, LANE_START + 200, LANE_START + 2300, 0, 700, "Wall")
    box(x + 240, x + 300, LANE_START + 200, LANE_START + 2300, 0, 700, "Wall")

    head = unreal.Vector(460, 460, 150)
    spawn(unreal.Crusher, (x, LANE_START + 800, 0), label="Crusher_1", head_size=head)
    spawn(unreal.Crusher, (x, LANE_START + 1600, 0), label="Crusher_2", head_size=head, phase_offset=1.65)


def station_11():
    x = station_x(11)
    label_text(x, 500, "11. GEAR AND PLATFORM\nRide the platform. Jump the blade or freeze it: it kills while it spins.\nThe disc on the left is safe to ride.")

    floor(x - 1200, x + 500, LANE_START, LANE_START + 600)
    floor(x - 500, x + 500, LANE_START + 2600, LANE_START + 3200)

    spawn(unreal.MovingPlatform, (x, LANE_START, -20), label="MovingPlatform", segment_time=5.0,
          points=[unreal.Vector(0, 800, 0), unreal.Vector(0, 2400, 0)])

    # low enough that its kill zone stops short of a jump's 127 units
    spawn(unreal.SpinningGear, (x, LANE_START + 1600, 60), label="BladeGear", is_blade=True, radius=280.0, speed=100.0)
    spawn(unreal.SpinningGear, (x - 850, LANE_START + 300, 20), label="SafeDisc", radius=250.0, speed=40.0)


def main():
    if unreal.EditorAssetLibrary.does_asset_exist(LEVEL_PATH):
        if not levels.load_level(LEVEL_PATH):
            raise RuntimeError(f"Could not load {LEVEL_PATH}")
        actors.destroy_actors(actors.get_all_level_actors())
        action = "Rebuilt"
    else:
        if not levels.new_level(LEVEL_PATH):
            raise RuntimeError(f"Could not create {LEVEL_PATH}")
        action = "Created"

    build_environment()
    station_1()
    station_2()
    station_3()
    station_4()
    station_5()
    station_6()
    bridge_station(7, 1300.0, 1500.0, 0.0, "7. BROKEN BRIDGE, SAFE\nRewind the fallen planks. They stay. Walk across.")
    bridge_station(8, 2400.0, 2600.0, 4.0, "8. BROKEN BRIDGE, 4 SECONDS\nRewind it: it lasts 4 seconds and takes 5 to cross.\nFreeze it during the hold to stop the countdown.")
    station_9()
    station_10()
    station_11()

    if not levels.save_current_level():
        raise RuntimeError(f"Could not save {LEVEL_PATH}")

    print(f"{action} and saved {LEVEL_PATH}: {len(built)} actors, {STATION_COUNT} stations")


main()
