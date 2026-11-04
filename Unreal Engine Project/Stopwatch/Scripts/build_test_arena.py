"""Builds /Game/Stopwatch/Maps/Lvl_TimeTest from nothing. Safe to run repeatedly: it deletes every actor and starts over.

Run from the editor's Python console, or headless with Unreal closed:
  UnrealEditor-Cmd.exe <path>/Stopwatch.uproject -run=pythonscript -script="<path>/Scripts/build_test_arena.py"

Layout: a hub runs along +X. Each station is a lane off the hub towards +Y, with its label at the entrance
and a pad back to the hub at its end. Walking into a lane you face +Y, so +X is on your LEFT.
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
PIT_TOP = -450.0    # pit floors under the gaps, where fallen pieces lie in view of the edge. A falling player dies before reaching them
CATCH_TOP = -1500.0  # stops fallen objects, so they stay in reach of Rewind instead of falling out of the world
LABEL_TOP = 180.0   # eye height is 160

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


def label_text(x, y, text, z=LABEL_TOP, size=24.0, color=unreal.Color(r=255, g=225, b=120, a=255)):
    """Text hanging down from z, read by someone walking towards +Y"""
    actor = spawn(unreal.TextRenderActor, (x, y, z), rotation=rot(yaw=-90.0), label="Label")
    component = actor.text_render
    component.set_text(text)
    component.set_editor_property("world_size", size)
    component.set_editor_property("horizontal_alignment", unreal.HorizTextAligment.EHTA_CENTER)
    component.set_text_render_color(color)
    return actor


def station_x(number):
    return 1500.0 + (number - 1) * STATION_SPACING


def station_label(number, text):
    """At the lane's entrance, facing the hub"""
    label_text(station_x(number), LANE_START - 100.0, text)


def return_pad(number, y, z=0.0, dx=0.0):
    """Takes the player back to the hub, in front of the station's label"""
    x = station_x(number)
    spawn(unreal.ReturnPad, (x + dx, y, z), label="ReturnPad",
          destination=unreal.Vector(x, LANE_START - 450.0, 0.0), destination_yaw=90.0)
    label_text(x + dx, y, "BACK TO HUB", z=z + 140.0, size=20.0, color=unreal.Color(r=60, g=255, b=120, a=255))


def pit_floor(x0, x1, y0, y1):
    """Under a gap, so whatever falls in lies where the player can see and aim at it"""
    box(x0, x1, y0, y1, PIT_TOP - 100.0, PIT_TOP, "PitFloor")


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
    station_label(1, "1. PENDULUMS\n"
                     "1. Aim at a pendulum, LMB: it freezes for 5 s\n"
                     "2. Right lane: walk past the frozen pendulum\n"
                     "3. Left lane: the blade kills. Freeze it, cross the beam")

    floor(x - 1200, x - 200, LANE_START, LANE_START + 1000)
    spawn(unreal.FreezePendulum, (x - 700, LANE_START + 500, 450), label="Pendulum", arm_length=380.0)
    return_pad(1, LANE_START + 900, dx=-700)

    floor(x + 200, x + 1000, LANE_START, LANE_START + 600)
    floor(x + 500, x + 700, LANE_START + 600, LANE_START + 1800, label="Beam")
    floor(x + 200, x + 1000, LANE_START + 1800, LANE_START + 2400)
    spawn(unreal.FreezePendulum, (x + 600, LANE_START + 1200, 480), label="BladePendulum",
          arm_length=400.0, swing_axis=unreal.Vector(0, 1, 0), kills_player=True)
    return_pad(1, LANE_START + 2200, dx=600)


def stairs(x0, x1, y, count, step_depth=150.0, step_height=90.0):
    """Stairs climbing towards +Y, returning the far edge and the height. Too tall to walk up, so you jump them."""
    for step in range(count):
        box(x0, x1, y + step * step_depth, y + (step + 1) * step_depth, 0.0, (step + 1) * step_height, "Stair")
    return y + count * step_depth, count * step_height


def station_2():
    x = station_x(2)
    station_label(2, "2. FALLING CRATE\n"
                     "1. Jump up the stairs to the ledge\n"
                     "2. As the crate falls past the ledge, LMB to freeze it\n"
                     "3. Jump onto it. It resets every 4 s")

    floor(x - 1000, x + 1000, LANE_START, LANE_START + 1800)
    edge, height = stairs(x - 300, x + 300, LANE_START + 200, 3)
    box(x - 300, x + 300, edge, edge + 300, 0, height, "Ledge")
    spawn(unreal.PhysicsCrate, (x, edge + 500, 900), label="Crate", scale=(2, 2, 1), auto_reset_interval=4.0)
    return_pad(2, LANE_START + 1600, dx=-600)


def station_3():
    x = station_x(3)
    station_label(3, "3. HOURGLASSES\n"
                     "1. Aim at the pendulum, LMB: the sand bar drops\n"
                     "2. Left hourglass: +35 sand, gone for good\n"
                     "3. Right hourglass: +35 sand, back after 5 s\n"
                     "With a full bar they wait for you to spend")

    # everything in view from the entrance: the pickups either side, and the pendulum to spend sand on between them
    floor(x - 800, x + 800, LANE_START, LANE_START + 1000)
    spawn(unreal.HourglassPickup, (x + 300, LANE_START + 250, 80), label="Hourglass_Once")
    spawn(unreal.HourglassPickup, (x - 300, LANE_START + 250, 80), label="Hourglass_Respawning", respawn_time=5.0)
    spawn(unreal.FreezePendulum, (x, LANE_START + 700, 450), label="SpendSandHere", arm_length=380.0)
    return_pad(3, LANE_START + 850, dx=600)


def station_4():
    x = station_x(4)
    station_label(4, "4. DROP CHUTE\n"
                     "1. The gap is too wide to jump\n"
                     "2. A crate drops every 3 s. LMB it level with the floor\n"
                     "3. Step across on it")

    floor(x - 500, x + 500, LANE_START, LANE_START + 600)
    floor(x - 500, x + 500, LANE_START + 1400, LANE_START + 2000)
    spawn(unreal.DropChute, (x, LANE_START + 1000, 600), label="DropChute",
          interval=3.0, prop_scale=unreal.Vector(2.5, 2.5, 0.5))
    return_pad(4, LANE_START + 1800)


def station_5():
    x = station_x(5)
    station_label(5, "5. BOULDER RAMP\n"
                     "1. Step on the red plate on the left: the boulder rolls down\n"
                     "2. Aim at the boulder, RMB to rewind it up the ramp\n"
                     "3. Step off and back on the plate to roll it again")

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

    # beside the foot of the ramp, out of the boulder's way, with the whole ramp in view
    spawn(unreal.RollTrigger, (x + 550, LANE_START + 250, 0), label="PressurePlate",
          target_prop=boulder, push_velocity=unreal.Vector(0, -100, 0))
    return_pad(5, LANE_START + 1900, dx=-600)


def station_6():
    x = station_x(6)
    station_label(6, "6. FALLING PLATFORM\n"
                     "1. Step on the platform: it shakes and flashes red\n"
                     "2. Jump back to this side: it falls into the pit\n"
                     "3. Aim at it down there, RMB: it rewinds and stays up\n"
                     "4. Walk across")

    floor(x - 500, x + 500, LANE_START, LANE_START + 600)
    floor(x - 500, x + 500, LANE_START + 1700, LANE_START + 2300)
    pit_floor(x - 500, x + 500, LANE_START + 600, LANE_START + 1700)

    # level with the floors, and too long to outrun in the 1 second shake. The gaps either side stop physics flinging it away when it drops
    gap = 20.0
    spawn(unreal.FallingPlatform, (x, LANE_START + 1150, -15), label="FallingPlatform", scale=(4, (1100 - gap * 2) / 100, 0.3))
    return_pad(6, LANE_START + 2100)


def bridge_station(number, pit, length, hold, text):
    x = station_x(number)
    station_label(number, text)

    floor(x - 500, x + 500, LANE_START, LANE_START + 600)
    floor(x - 500, x + 500, LANE_START + 600 + pit, LANE_START + 1200 + pit)
    pit_floor(x - 500, x + 500, LANE_START + 600, LANE_START + 600 + pit)

    # overlaps both floors, with its top barely above them to avoid z-fighting. Fallen, the 30 thick planks lie on the pit floor
    overlap = (length - pit) / 2
    top = -14.0
    spawn(unreal.BrokenBridge, (x, LANE_START + 600 - overlap, top), rotation=rot(yaw=90.0),
          label=f"Bridge_Hold{hold:.0f}", bridge_length=length, plank_count=max(8, int(length / 150)),
          fall_depth=top - (PIT_TOP + 15.0), scatter=150.0,
          timeable={"hold_duration": hold})
    return_pad(number, LANE_START + 1000 + pit)


def station_9():
    x = station_x(9)
    station_label(9, "9. REWIND THEN FREEZE\n"
                     "1. Step on the red plate on the right: the block falls off the tower\n"
                     "2. Stand on the low step, aim at the block, RMB to rewind it\n"
                     "3. As it rises past you, LMB to freeze it\n"
                     "4. Jump onto it, then up to the high platform")

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

    spawn(unreal.RollTrigger, (x - 700, LANE_START + 300, 0), label="PressurePlate",
          target_prop=block, push_velocity=unreal.Vector(-20, 0, 0))

    spawn(unreal.HourglassPickup, (x - 700, LANE_START + 1000, 80), label="Hourglass_Respawning", respawn_time=5.0)
    return_pad(9, LANE_START + 1550, z=260.0)


def station_10():
    x = station_x(10)
    station_label(10, "10. CRUSHER CORRIDOR\n"
                      "1. A crusher kills you when it slams\n"
                      "2. Run under one while it's up, or LMB to freeze it up there\n"
                      "3. The green pad at the end takes you back")

    floor(x - 300, x + 300, LANE_START, LANE_START + 2300)
    box(x - 300, x - 240, LANE_START + 200, LANE_START + 2300, 0, 700, "Wall")
    box(x + 240, x + 300, LANE_START + 200, LANE_START + 2300, 0, 700, "Wall")

    head = unreal.Vector(460, 460, 150)
    spawn(unreal.Crusher, (x, LANE_START + 800, 0), label="Crusher_1", head_size=head)
    spawn(unreal.Crusher, (x, LANE_START + 1600, 0), label="Crusher_2", head_size=head, phase_offset=1.65)
    return_pad(10, LANE_START + 2150)


def station_11():
    x = station_x(11)
    station_label(11, "11. GEAR AND PLATFORM\n"
                      "1. Ride the moving platform across\n"
                      "2. The spinning blade kills: jump it, or LMB to freeze it\n"
                      "3. The disc on the right is safe to ride")

    floor(x - 1200, x + 500, LANE_START, LANE_START + 600)
    floor(x - 500, x + 500, LANE_START + 2600, LANE_START + 3200)

    spawn(unreal.MovingPlatform, (x, LANE_START, -20), label="MovingPlatform", segment_time=5.0,
          points=[unreal.Vector(0, 800, 0), unreal.Vector(0, 2400, 0)])

    # low enough that its kill zone stops short of a jump's 127 units
    spawn(unreal.SpinningGear, (x, LANE_START + 1600, 60), label="BladeGear", is_blade=True, radius=280.0, speed=100.0)
    spawn(unreal.SpinningGear, (x - 850, LANE_START + 300, 20), label="SafeDisc", radius=250.0, speed=40.0)
    return_pad(11, LANE_START + 3000)


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
    bridge_station(7, 1300.0, 1500.0, 0.0, "7. BROKEN BRIDGE, SAFE\n"
                                           "1. Aim at the broken planks in the pit\n"
                                           "2. RMB to rewind the bridge. It stays up\n"
                                           "3. Walk across")
    bridge_station(8, 2400.0, 2600.0, 4.0, "8. BROKEN BRIDGE, 4 SECONDS\n"
                                           "1. Aim at the broken planks in the pit, RMB to rewind the bridge\n"
                                           "2. It falls again after 4 s, and crossing takes 5\n"
                                           "3. On the bridge, aim at it and LMB: the countdown pauses")
    station_9()
    station_10()
    station_11()

    if not levels.save_current_level():
        raise RuntimeError(f"Could not save {LEVEL_PATH}")

    print(f"{action} and saved {LEVEL_PATH}: {len(built)} actors, {STATION_COUNT} stations")


main()
