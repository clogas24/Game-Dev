"""Builds /Game/Stopwatch/Maps/Lvl_ClockTower, the real level, from nothing. Safe to run repeatedly: it deletes every actor and starts over.

Run from the editor's Python console, or headless with Unreal closed:
  UnrealEditor-Cmd.exe <path>/Stopwatch.uproject -run=pythonscript -script="<path>/Scripts/build_level.py"

Layout: five sections stacked as stories of a clock tower, 1500 apart. Each runs along X, the odd ones towards +X and
the even ones back towards -X, and a stairwell of two 30 degree ramps at its end climbs to the next. The last section
climbs on its own, against the collapse, to the goal at a giant clock face. See docs/LEVEL_DESIGN.md.

Every section is written in its own frame: u along the path from its start, y across it (the lane is y -650 to 650),
z up from its story's floor. Section.world turns that into the level's coordinates.

Heights and gaps assume the character's 127 unit jump and 500 unit/s walk. Every jump the path needs is marked with a
JumpFrom and JumpTo pair of TargetPoints, which Stopwatch.ClockTower.Layout checks against the measured jump.
"""

import math

import unreal

LEVEL_PATH = "/Game/Stopwatch/Maps/Lvl_ClockTower"
MATERIAL_PATH = "/Game/Stopwatch/Materials"
CUBE = "/Engine/BasicShapes/Cube.Cube"
CYLINDER = "/Engine/BasicShapes/Cylinder.Cylinder"

STORY = 1500.0      # height between sections
HALF_WIDTH = 650.0  # the lane runs y -650 to 650
RAIL = 150.0        # side rails stand this far above the floor beside them, higher than a jump
PIT_DEPTH = 450.0   # pit floors lie this far below the walkway, where fallen pieces stay in view
KILL_BELOW = 250.0  # a fall into a pit kills this far below the walkway, before the pit floor
RAMP_RUN = 1300.0   # each stairwell ramp climbs 750 over this, 30 degrees
LABEL_SIZE = 26.0

actors = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
levels = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
material_lib = unreal.MaterialEditingLibrary
cube_mesh = unreal.EditorAssetLibrary.load_asset(CUBE)
cylinder_mesh = unreal.EditorAssetLibrary.load_asset(CYLINDER)
built = []
materials = {}

# what a checkpoint of a section resets
RESETTABLE = (unreal.FreezePendulum, unreal.Crusher, unreal.MovingPlatform, unreal.SpinningGear, unreal.FallingPlatform,
              unreal.BrokenBridge, unreal.DropChute, unreal.PhysicsProp, unreal.HourglassPickup, unreal.RollTrigger)
current_section = 0
jump_count = 0


# ---------------------------------------------------------------- materials

def make_greybox_material():
    """M_Greybox: opaque and rough, with a Color parameter. Rebuilt each run like the other materials"""
    path = f"{MATERIAL_PATH}/M_Greybox"
    if unreal.EditorAssetLibrary.does_asset_exist(path):
        material = unreal.EditorAssetLibrary.load_asset(path)
        for expression in material_lib.get_material_expressions(material):
            material_lib.delete_material_expression(material, expression)
    else:
        material = unreal.AssetToolsHelpers.get_asset_tools().create_asset("M_Greybox", MATERIAL_PATH, unreal.Material, unreal.MaterialFactoryNew())

    color = material_lib.create_material_expression(material, unreal.MaterialExpressionVectorParameter, -400, 0)
    color.set_editor_property("parameter_name", "Color")
    color.set_editor_property("default_value", unreal.LinearColor(0.5, 0.5, 0.5, 1.0))
    roughness = material_lib.create_material_expression(material, unreal.MaterialExpressionConstant, -400, 200)
    roughness.set_editor_property("r", 0.85)
    material_lib.connect_material_property(color, "", unreal.MaterialProperty.MP_BASE_COLOR)
    material_lib.connect_material_property(roughness, "", unreal.MaterialProperty.MP_ROUGHNESS)
    material_lib.recompile_material(material)
    unreal.EditorAssetLibrary.save_loaded_asset(material, only_if_is_dirty=False)
    return material


def make_material_instance(name, parent, color):
    path = f"{MATERIAL_PATH}/{name}"
    if unreal.EditorAssetLibrary.does_asset_exist(path):
        instance = unreal.EditorAssetLibrary.load_asset(path)
    else:
        instance = unreal.AssetToolsHelpers.get_asset_tools().create_asset(
            name, MATERIAL_PATH, unreal.MaterialInstanceConstant, unreal.MaterialInstanceConstantFactoryNew())
    instance.set_editor_property("parent", parent)
    material_lib.set_material_instance_vector_parameter_value(instance, "Color", color)
    material_lib.update_material_instance(instance)
    unreal.EditorAssetLibrary.save_loaded_asset(instance, only_if_is_dirty=False)
    return instance


def make_materials():
    parent = make_greybox_material()
    materials["stone"] = make_material_instance("MI_Greybox_Stone", parent, unreal.LinearColor(0.42, 0.4, 0.37, 1.0))
    materials["pit"] = make_material_instance("MI_Greybox_PitFloor", parent, unreal.LinearColor(0.16, 0.15, 0.14, 1.0))
    materials["wall"] = make_material_instance("MI_Greybox_Wall", parent, unreal.LinearColor(0.1, 0.1, 0.12, 1.0))
    materials["brass"] = make_material_instance("MI_Greybox_ClockFace", parent, unreal.LinearColor(0.75, 0.55, 0.2, 1.0))
    materials["hand"] = make_material_instance("MI_Greybox_ClockHand", parent, unreal.LinearColor(0.03, 0.03, 0.03, 1.0))


# ---------------------------------------------------------------- spawning, in world coordinates

def rot(pitch=0.0, yaw=0.0, roll=0.0):
    return unreal.Rotator(roll=roll, pitch=pitch, yaw=yaw)


def spawn(cls, location, rotation=None, label=None, scale=None, timeable=None, **props):
    """Spawns an actor. props go on the actor, and timeable on its UTimeableComponent"""
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
    if isinstance(actor, RESETTABLE):
        actor.set_editor_property("section_id", current_section)
    built.append(actor)
    return actor


def world_box(x0, x1, y0, y1, z0, z1, label, material, mesh=None, rotation=None):
    """Static block covering a range. The engine's cube is 100 units wide"""
    x0, x1 = sorted((x0, x1))
    y0, y1 = sorted((y0, y1))
    actor = spawn(unreal.StaticMeshActor, ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), rotation=rotation, label=label,
                  scale=((x1 - x0) / 100, (y1 - y0) / 100, (z1 - z0) / 100))
    actor.static_mesh_component.set_static_mesh(mesh or cube_mesh)
    actor.static_mesh_component.set_material(0, materials[material])
    return actor


def kill_box(x0, x1, y0, y1, z0, z1):
    x0, x1 = sorted((x0, x1))
    y0, y1 = sorted((y0, y1))
    kill = spawn(unreal.KillVolume, ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), label="KillVolume")
    kill.get_component_by_class(unreal.BoxComponent).set_box_extent(unreal.Vector((x1 - x0) / 2, (y1 - y0) / 2, (z1 - z0) / 2), True)
    return kill


def trigger(cls, location, extent, rotation=None, label=None, **props):
    """A box trigger standing on location, the floor point"""
    actor = spawn(cls, location, rotation=rotation, label=label, **props)
    box_component = actor.get_component_by_class(unreal.BoxComponent)
    box_component.set_box_extent(unreal.Vector(*extent), True)
    box_component.set_relative_location(unreal.Vector(0.0, 0.0, extent[2]), False, False)
    return actor


# ---------------------------------------------------------------- a section's frame

class Section:
    def __init__(self, number, name, x0, z0, direction, length):
        self.number = number
        self.name = name
        self.x0 = x0
        self.z0 = z0
        self.d = direction
        self.length = length
        self.yaw = 0.0 if direction > 0 else 180.0
        self.checkpoints = 0
        global current_section
        current_section = number

    @property
    def x_end(self):
        return self.x0 + self.d * self.length

    def world(self, u, y=0.0, z=0.0):
        """A point in the section's frame, in the level. Turning the frame 180 degrees for the -X sections keeps it a rotation"""
        return (self.x0 + self.d * u, self.d * y, self.z0 + z)

    def vector(self, du, dy=0.0, dz=0.0):
        return unreal.Vector(self.d * du, self.d * dy, dz)

    def rotation(self, pitch=0.0, yaw=0.0, roll=0.0):
        return rot(pitch=pitch, yaw=self.yaw + yaw, roll=roll)

    def box(self, u0, u1, y0, y1, z0, z1, label="Block", material="stone"):
        a = self.world(u0, y0, z0)
        b = self.world(u1, y1, z1)
        return world_box(a[0], b[0], a[1], b[1], a[2], b[2], label, material)

    def floor(self, u0, u1, top=0.0, y0=-HALF_WIDTH, y1=HALF_WIDTH, rails=True, label="Floor"):
        """Walkway with its top at top, and rails along the lane's edges so nobody walks off the tower"""
        self.box(u0, u1, y0, y1, top - 100.0, top, label)
        if rails:
            self.rails(u0, u1, top)

    def rails(self, u0, u1, top):
        self.box(u0, u1, HALF_WIDTH, HALF_WIDTH + 50.0, top - 100.0, top + RAIL, "Rail", "wall")
        self.box(u0, u1, -HALF_WIDTH - 50.0, -HALF_WIDTH, top - 100.0, top + RAIL, "Rail", "wall")

    def pit(self, u0, u1, top=0.0, y0=-HALF_WIDTH, y1=HALF_WIDTH):
        """A gap in the walkway: a dark floor down below where fallen pieces lie in view, and death on the way down to it"""
        self.box(u0, u1, y0, y1, top - PIT_DEPTH - 100.0, top - PIT_DEPTH, "PitFloor", "pit")
        a = self.world(u0, y0 - 100.0, top - PIT_DEPTH + 50.0)
        b = self.world(u1, y1 + 100.0, top - KILL_BELOW)
        kill_box(a[0], b[0], a[1], b[1], a[2], b[2])
        self.rails(u0, u1, top)

    def ramp(self, u0, u1, z0, z1, y0, y1, label="Ramp"):
        """A walkable slab from z0 at u0 to z1 at u1, with u0 < u1. It climbs or descends along the path"""
        run = u1 - u0
        rise = z1 - z0
        angle = math.atan2(rise, run)
        thickness = 50.0
        length = math.hypot(run, rise)
        center = self.world((u0 + u1) / 2, (y0 + y1) / 2, (z0 + z1) / 2 - thickness / 2 / math.cos(angle))
        actor = spawn(unreal.StaticMeshActor, center, rotation=self.rotation(pitch=math.degrees(angle)), label=label,
                      scale=(length / 100, (y1 - y0) / 100, thickness / 100))
        actor.static_mesh_component.set_static_mesh(cube_mesh)
        actor.static_mesh_component.set_material(0, materials["stone"])
        return actor

    def ramp_rails(self, u0, u1, z_low, z_high):
        """Rails beside a ramp, from below its low end to above its high end"""
        self.box(u0, u1, HALF_WIDTH, HALF_WIDTH + 50.0, z_low - 100.0, z_high + RAIL, "Rail", "wall")
        self.box(u0, u1, -HALF_WIDTH - 50.0, -HALF_WIDTH, z_low - 100.0, z_high + RAIL, "Rail", "wall")

    def label(self, u, y, z, text, size=LABEL_SIZE):
        """Text read by someone walking along the path"""
        actor = spawn(unreal.TextRenderActor, self.world(u, y, z), rotation=self.rotation(yaw=180.0), label="Label")
        component = actor.text_render
        component.set_text(text)
        component.set_editor_property("world_size", size)
        component.set_editor_property("horizontal_alignment", unreal.HorizTextAligment.EHTA_CENTER)
        component.set_text_render_color(unreal.Color(r=255, g=225, b=120, a=255))
        return actor

    def start(self, min_sand=60.0):
        """The section's first checkpoint and its section trigger, on its start landing"""
        trigger(unreal.SectionTrigger, self.world(350.0), (350.0, HALF_WIDTH, 250.0), rotation=self.rotation(),
                label=f"Section{self.number}", section_id=self.number, display_name=unreal.Text(self.name))
        self.checkpoint(400.0, min_sand=min_sand)

    def checkpoint(self, u, y=0.0, z=0.0, min_sand=60.0):
        actor = spawn(unreal.Checkpoint, self.world(u, y, z), rotation=self.rotation(),
                      label=f"Checkpoint{self.number}_{self.checkpoints}",
                      section_id=self.number, order_in_section=self.checkpoints, min_sand_on_respawn=min_sand)
        self.checkpoints += 1
        return actor

    def hourglass(self, u, y=0.0, z=0.0, respawn=8.0):
        """Sand. Respawning ones go where a retry is likely"""
        return spawn(unreal.HourglassPickup, self.world(u, y, z + 80.0), label="Hourglass", respawn_time=respawn, sand_amount=35.0)

    def tutorial(self, u, focus, prompt, z=0.0, depth=60.0):
        """A prompt across the lane, shown once. Its numbered steps name the player's keys through {freeze}, {rewind} and {jump}"""
        return trigger(unreal.TutorialTrigger, self.world(u, 0.0, z), (depth, HALF_WIDTH, 150.0), rotation=self.rotation(),
                       label="Tutorial", prompt=unreal.Text(prompt), focus_target=focus)

    def jump(self, u_from, z_from, u_to, z_to, y=0.0):
        """Marks a jump the path needs: from the takeoff edge to the landing edge"""
        global jump_count
        tag = f"Jump{jump_count}"
        jump_count += 1
        for name, u, z in (("JumpFrom", u_from, z_from), ("JumpTo", u_to, z_to)):
            point = spawn(unreal.TargetPoint, self.world(u, y, z), label=f"{tag}_{name}")
            point.set_editor_property("tags", [unreal.Name(name), unreal.Name(tag)])

    def stairwell(self, start_top=0.0):
        """Climbs from the section's end to the next story: up one ramp, along a landing and back up the other, beside it"""
        end = self.length
        mid = 750.0
        self.ramp(end, end + RAMP_RUN, start_top, mid, -HALF_WIDTH, -150.0, "StairRamp")
        self.box(end + RAMP_RUN, end + RAMP_RUN + 250.0, -HALF_WIDTH, HALF_WIDTH, mid - 100.0, mid, "StairLanding")
        self.ramp(end, end + RAMP_RUN, STORY, mid, 150.0, HALF_WIDTH, "StairRamp")
        # the spine between the ramps, the far wall and the sides
        self.box(end, end + RAMP_RUN, -150.0, 150.0, start_top - 100.0, STORY + 300.0, "StairWall", "wall")
        self.box(end + RAMP_RUN + 250.0, end + RAMP_RUN + 350.0, -HALF_WIDTH - 50.0, HALF_WIDTH + 50.0, start_top - 100.0, STORY + 300.0, "StairWall", "wall")
        self.box(end, end + RAMP_RUN + 350.0, HALF_WIDTH, HALF_WIDTH + 50.0, start_top - 100.0, STORY + 300.0, "StairWall", "wall")
        self.box(end, end + RAMP_RUN + 350.0, -HALF_WIDTH - 50.0, -HALF_WIDTH, start_top - 100.0, STORY + 300.0, "StairWall", "wall")


# ---------------------------------------------------------------- the sections

def section_1(x0):
    """The Workshop: move and jump, then Freeze taught on a harmless pendulum, then a gate that needs it"""
    s = Section(1, "The Workshop", x0, 0.0, 1, 6000.0)
    spawn(unreal.PlayerStart, s.world(150.0, 0.0, 120.0), rotation=s.rotation(), label="PlayerStart")
    s.floor(0, 1100)
    s.start(min_sand=100.0)
    s.label(-20.0, 0.0, 420.0, "THE WORKSHOP", size=60.0)

    # jumps, taught by the layout: two blocks up, down, and a trench that only costs a jump back out
    s.floor(1100, 1400, 80.0, label="JumpBlock")
    s.floor(1400, 1700, 160.0, label="JumpBlock")
    s.jump(1100, 0, 1100, 80)
    s.jump(1400, 80, 1400, 160)
    s.floor(1700, 1900)
    s.floor(1900, 2150, -100.0, label="Trench")
    s.floor(2150, 2500)
    s.jump(1900, 0, 2150, 0)
    s.jump(2150, -100, 2150, 0)

    # a pendulum swinging across a narrow beam. It doesn't kill: it knocks you off onto the ledge below, and steps lead back
    s.floor(2500, 3300, -200.0, label="FallbackLedge")
    s.box(2500, 3300, -100.0, 100.0, -100.0, 0.0, "Beam")
    s.box(2500, 2700, 400.0, HALF_WIDTH, -200.0, -100.0, "LedgeStep")
    s.jump(2700, -200, 2700, -100, y=500.0)
    s.jump(2500, -100, 2500, 0, y=500.0)
    pendulum = spawn(unreal.FreezePendulum, s.world(2900.0, 0.0, 520.0), rotation=s.rotation(), label="BeamPendulum",
                     arm_length=400.0, swing_axis=unreal.Vector(1, 0, 0), period=2.0, amplitude=55.0)
    # frozen in the middle it would block the beam, so the prompt says when
    s.tutorial(2380.0, pendulum, "1. Aim at the brass pendulum\n"
                                 "2. Press {freeze} when it swings to the side\n"
                                 "3. Cross while it's stopped")
    s.hourglass(2350.0, 450.0)
    s.floor(3300, 4300)
    s.checkpoint(3500.0, min_sand=100.0)
    s.hourglass(3450.0, 450.0)

    # the gate: a fast blade across a doorway. Its kill zone covers the doorway's whole width at every point of the swing
    # (400 sin 50 + 110 + the capsule's 42 is more than the half width of 180), so it can't be timed, only frozen.
    # Frozen in the middle, its 30 unit bob leaves room either side to walk past
    door = 180.0
    s.box(4300, 4400, -HALF_WIDTH - 50.0, -door, -100.0, 700.0, "GateWall", "wall")
    s.box(4300, 4400, door, HALF_WIDTH + 50.0, -100.0, 700.0, "GateWall", "wall")
    s.box(4300, 4400, -door, door, 300.0, 700.0, "GateWall", "wall")
    s.floor(4300, 6000)
    gate = spawn(unreal.FreezePendulum, s.world(4180.0, 0.0, 520.0), rotation=s.rotation(), label="GatePendulum",
                 arm_length=400.0, swing_axis=unreal.Vector(1, 0, 0), period=1.0, amplitude=50.0, kills_player=True, kill_radius=110.0)
    s.tutorial(3900.0, gate, "1. Red blades kill, and this one is too fast to dodge\n"
                             "2. Press {freeze} on it\n"
                             "3. Walk through")
    s.hourglass(3950.0, 450.0)
    s.hourglass(4700.0, 0.0)
    s.label(5600.0, 0.0, 300.0, "UP THE STAIRS", size=40.0)
    s.stairwell()
    return s


def section_2(x0):
    """The Gear Hall: Freeze applied to timing"""
    s = Section(2, "The Gear Hall", x0, STORY, -1, 6000.0)
    s.floor(0, 800)
    s.start()
    s.label(-20.0, 0.0, 420.0, "THE GEAR HALL", size=60.0)
    s.hourglass(600.0, 450.0)

    # a crusher corridor: run under while they're up, or freeze one up there
    s.floor(800, 2600)
    s.box(900, 2500, 300.0, HALF_WIDTH, 0.0, 800.0, "CorridorWall", "wall")
    s.box(900, 2500, -HALF_WIDTH, -300.0, 0.0, 800.0, "CorridorWall", "wall")
    head = unreal.Vector(500, 600, 150)
    spawn(unreal.Crusher, s.world(1350.0), rotation=s.rotation(), label="Crusher", head_size=head)
    spawn(unreal.Crusher, s.world(2050.0), rotation=s.rotation(), label="Crusher", head_size=head, phase_offset=1.6)
    s.hourglass(2550.0, 450.0)
    s.checkpoint(2650.0)

    # a platform sliding across a pit between two narrow piers: freeze it lined up with them
    s.pit(2600, 4200)
    s.floor(2600, 2800, y0=-150.0, y1=150.0, rails=False, label="Pier")
    spawn(unreal.MovingPlatform, s.world(3400.0, 0.0, -20.0), rotation=s.rotation(), label="SlidingPlatform",
          segment_time=2.5, size=unreal.Vector(600, 400, 40), points=[unreal.Vector(0, -450, 0), unreal.Vector(0, 450, 0)])
    s.jump(2800, 0, 3100, 0)
    s.jump(3700, 0, 4000, 0)
    s.floor(4000, 4200, y0=-150.0, y1=150.0, rails=False, label="Pier")
    s.hourglass(4150.0, 0.0)

    # spinning blades in a narrowed hall: jump the arms or freeze them
    s.floor(4200, 6000)
    s.checkpoint(4350.0)
    s.box(4550, 5650, 350.0, HALF_WIDTH, 0.0, 300.0, "HallWall", "wall")
    s.box(4550, 5650, -HALF_WIDTH, -350.0, 0.0, 300.0, "HallWall", "wall")
    spawn(unreal.SpinningGear, s.world(4800.0, 0.0, 60.0), rotation=s.rotation(), label="BladeGear", is_blade=True, radius=300.0, speed=110.0)
    spawn(unreal.SpinningGear, s.world(5400.0, 0.0, 60.0), rotation=s.rotation(), label="BladeGear", is_blade=True, radius=300.0, speed=-90.0)
    s.hourglass(5800.0, 0.0)
    s.stairwell()
    return s


def section_3(x0):
    """The Broken Bridge: Rewind taught on a bridge, then a falling platform and a boulder"""
    s = Section(3, "The Broken Bridge", x0, STORY * 2, 1, 6000.0)
    s.floor(0, 1000)
    s.start()
    s.label(-20.0, 0.0, 420.0, "THE BROKEN BRIDGE", size=60.0)
    s.hourglass(600.0, 450.0)

    # the collapsed bridge, its planks lying in view on the pit floor. Rebuilt, it stays
    pit, length = 1300.0, 1500.0
    s.pit(1000, 1000 + pit)
    top = -14.0
    bridge = spawn(unreal.BrokenBridge, s.world(1000.0 - (length - pit) / 2, 0.0, top), rotation=s.rotation(), label="Bridge",
                   bridge_length=length, plank_count=10, fall_depth=top - (-PIT_DEPTH + 15.0), scatter=150.0,
                   timeable={"hold_duration": 0.0})
    s.tutorial(800.0, bridge, "1. Aim at the planks in the pit\n"
                              "2. Press {rewind} to rebuild the bridge\n"
                              "3. Walk across")
    s.floor(2300, 3000)
    s.hourglass(2450.0, 450.0)

    # the falling platform: step on, jump back, rewind it out of the pit
    s.checkpoint(2800.0)
    s.pit(3000, 4100)
    falling = spawn(unreal.FallingPlatform, s.world(3550.0, 0.0, -15.0), rotation=s.rotation(), label="FallingPlatform",
                    scale=((1100 - 40) / 100, 4.0, 0.3))
    s.tutorial(2650.0, falling, "1. Step on, then jump back as it shakes\n"
                                "2. Press {rewind} on the fallen platform\n"
                                "3. Walk across")
    s.hourglass(2850.0, -450.0)

    # the boulder: a plate in a narrow pass rolls it down a ramp beside the path, into the doorway. Rewind it back up the ramp
    # an open landing first, so stepping off the falling platform doesn't walk into the rails
    ramp_y0, ramp_y1 = 50.0, 550.0
    door_y = (ramp_y0 + ramp_y1) / 2
    top_u, foot_u = 4400.0, 5200.0
    s.floor(4100, 5500)
    s.hourglass(4250.0, 400.0)
    s.checkpoint(4200.0, y=-400.0)
    s.box(4550, 4850, -HALF_WIDTH, -420.0, 0.0, 300.0, "PassWall", "wall")
    s.box(4550, 4850, -180.0, 0.0, 0.0, 300.0, "PassWall", "wall")
    s.ramp(top_u, foot_u, 400.0, 0.0, ramp_y0, ramp_y1, "BoulderRamp")
    s.box(top_u - 100.0, foot_u, 0.0, ramp_y0, 0.0, 600.0, "BoulderRail", "wall")
    s.box(top_u - 100.0, foot_u, ramp_y1, HALF_WIDTH, 0.0, 600.0, "BoulderRail", "wall")
    s.box(top_u - 100.0, top_u, ramp_y0, ramp_y1, 0.0, 600.0, "BoulderBackstop", "wall")

    # in front of the door, a socket as wide as the boulder and 40 deep, so it settles there instead of bouncing back out.
    # 40 is a step, so the player walks through it
    s.floor(5500, 5700, y0=-HALF_WIDTH, y1=door_y - 150.0, rails=False)
    s.floor(5500, 5700, y0=door_y + 150.0, y1=HALF_WIDTH, rails=False)
    s.floor(5500, 5700, -40.0, y0=door_y - 150.0, y1=door_y + 150.0, rails=False, label="Socket")
    s.rails(5500, 5700, 0.0)

    # the doorway, narrower and lower than the boulder, so it jams there
    s.box(5700, 5800, -HALF_WIDTH - 50.0, door_y - 130.0, -100.0, 900.0, "DoorWall", "wall")
    s.box(5700, 5800, door_y + 130.0, HALF_WIDTH + 50.0, -100.0, 900.0, "DoorWall", "wall")
    s.box(5700, 5800, door_y - 130.0, door_y + 130.0, 230.0, 900.0, "DoorWall", "wall")

    # resting on the slope near its top: up the slope from the foot, then out along the surface's normal by the radius
    radius = 150.0
    angle = math.atan2(400.0, foot_u - top_u)
    along = 650.0
    boulder = spawn(unreal.PhysicsProp,
                    s.world(foot_u - along * math.cos(angle) + radius * math.sin(angle), door_y,
                            along * math.sin(angle) + radius * math.cos(angle) + 2.0),
                    label="Boulder", scale=(3, 3, 3), shape=unreal.PropShape.SPHERE, mass=400.0,
                    held_until_woken=True, angular_damping=0.5, timeable={"record_seconds": 16.0})
    # at the start of the pass, and pushed hard, so the boulder reaches the door before anyone walking
    spawn(unreal.RollTrigger, s.world(4600.0, -300.0), rotation=s.rotation(), label="PressurePlate",
          target_prop=boulder, push_velocity=s.vector(300.0, 0.0, 0.0))
    # near the door, by when the boulder has jammed it, and from where the ramp is in view over the end of the rail
    s.tutorial(5450.0, boulder, "1. Press {rewind} on the boulder\n"
                                "2. Go through the door", depth=40.0)
    s.floor(5800, 6000)
    s.hourglass(5900.0, 0.0)
    s.stairwell()
    return s, boulder


def section_4(x0):
    """The Belfry: Freeze and Rewind remixed, with less sand"""
    s = Section(4, "The Belfry", x0, STORY * 3, -1, 6600.0)
    raised = 240.0
    s.floor(0, 900)
    s.start()
    s.label(-20.0, 0.0, 420.0, "THE BELFRY", size=60.0)
    s.hourglass(700.0, 450.0)

    # a drop chute over a gap too wide to jump. Its crates drift down slowly: freeze one level with the floor and step across
    s.pit(900, 1700)
    spawn(unreal.DropChute, s.world(1300.0, 0.0, 650.0), rotation=s.rotation(), label="DropChute",
          interval=3.0, prop_scale=unreal.Vector(2.5, 2.5, 0.5), prop_linear_damping=4.0)
    s.jump(900, 0, 1175, 0)
    s.jump(1425, 0, 1700, 0)

    # rewind then freeze: the block falls off the tower, and rising back past the step it can be frozen as a stair to the ledge
    s.floor(1700, 2800)
    s.checkpoint(1800.0)
    s.hourglass(1800.0, -450.0)
    # the step, then the block's landing spot between it and the ledge. The block is held beside the tower's top with
    # nothing under it, so it drops straight down when pushed away from the tower
    s.box(2100, 2400, -300.0, 0.0, 0.0, 100.0, "Step")
    s.box(2400, 2700, 150.0, HALF_WIDTH, 0.0, 480.0, "BlockTower", "wall")
    # slow, so it rises through the band where it works as a stair for at least 0.6 s
    # 280 long, so it drops between the step and the ledge without catching on either
    block = spawn(unreal.PhysicsProp, s.world(2550.0, -150.0, 510.0), rotation=s.rotation(), label="Block", scale=(2.8, 3, 0.6),
                  held_until_woken=True, angular_damping=20.0, timeable={"rewind_speed": 0.1})
    spawn(unreal.RollTrigger, s.world(1950.0, -350.0), rotation=s.rotation(), label="PressurePlate",
          target_prop=block, push_velocity=s.vector(0.0, -20.0, 0.0))
    # the ledge: everything after it is raised, so the block is the only way up
    s.box(2700, 2800, -HALF_WIDTH, HALF_WIDTH, 0.0, raised, "LedgeFace", "wall")
    s.jump(2400, 100, 2410, 200, y=-150.0)
    s.jump(2690, 140, 2700, raised, y=-150.0)
    s.label(2100.0, -150.0, 380.0, "Rewind the block, freeze it as a step", size=22.0)

    # a bridge that only holds 3 seconds, and takes longer to cross: freeze it on the way
    s.floor(2800, 3200, raised)
    s.checkpoint(2950.0, z=raised)
    s.hourglass(3100.0, 450.0, raised)
    pit, length, hold = 1800.0, 2000.0, 3.0
    s.pit(3200, 3200 + pit, raised)
    top = raised - 14.0
    spawn(unreal.BrokenBridge, s.world(3200.0 - (length - pit) / 2, 0.0, top), rotation=s.rotation(), label="ShortBridge",
          bridge_length=length, plank_count=13, fall_depth=top - (raised - PIT_DEPTH + 15.0), scatter=150.0,
          timeable={"hold_duration": hold})
    s.label(3050.0, 0.0, raised + 380.0, "Holds 3 s: freeze it halfway", size=22.0)
    s.floor(5000, 5300, raised)
    s.hourglass(5150.0, 450.0, raised)

    # the choice: freeze the falling platform while it shakes for 25 sand, or let it fall and rewind it for 35. Both cross
    s.checkpoint(5150.0, y=-300.0, z=raised)
    s.pit(5300, 6000, raised)
    spawn(unreal.FallingPlatform, s.world(5650.0, 0.0, raised - 15.0), rotation=s.rotation(), label="ChoicePlatform",
          scale=((700 - 40) / 100, 4.0, 0.3), shake_time=1.5)
    s.label(5250.0, 0.0, raised + 380.0, "Freeze it (25 sand) or rewind it (35 sand)", size=22.0)
    s.floor(6000, 6600, raised)
    s.hourglass(6300.0, 0.0, raised)
    s.stairwell(start_top=raised)
    return s, block


def section_5(x0):
    """The Collapse: everything, against a 120 second countdown"""
    s = Section(5, "The Collapse", x0, STORY * 4, 1, 10400.0)
    s.floor(0, 800)
    s.start()
    s.label(-20.0, 0.0, 420.0, "THE COLLAPSE", size=60.0)
    s.hourglass(150.0, 450.0)
    trigger(unreal.CollapseTrigger, s.world(700.0), (50.0, HALF_WIDTH, 250.0), rotation=s.rotation(), label="CollapseTrigger",
            collapse_seconds=120.0)
    s.label(700.0, 0.0, 360.0, "RUN FOR THE CLOCK", size=30.0)

    # a lift: it rides up and across the pit to the ledge
    s.pit(800, 2000)
    spawn(unreal.MovingPlatform, s.world(1000.0, 0.0, -20.0), rotation=s.rotation(), label="Lift", segment_time=3.0,
          size=unreal.Vector(400, 500, 40), points=[unreal.Vector(0, 0, 0), unreal.Vector(800, 0, 400)])
    s.floor(2000, 3400, 400.0)

    # crushers on the ledge
    s.box(2200, 3200, 300.0, HALF_WIDTH, 400.0, 1200.0, "CorridorWall", "wall")
    s.box(2200, 3200, -HALF_WIDTH, -300.0, 400.0, 1200.0, "CorridorWall", "wall")
    head = unreal.Vector(450, 600, 150)
    spawn(unreal.Crusher, s.world(2500.0, 0.0, 400.0), rotation=s.rotation(), label="Crusher", head_size=head)
    spawn(unreal.Crusher, s.world(2950.0, 0.0, 400.0), rotation=s.rotation(), label="Crusher", head_size=head, phase_offset=1.4)
    s.hourglass(3300.0, 0.0, 400.0)

    # up a ramp to a drop chute gap
    s.ramp(3400, 4266, 400.0, 900.0, -HALF_WIDTH, HALF_WIDTH)
    s.ramp_rails(3400, 4266, 400.0, 900.0)
    s.floor(4266, 4600, 900.0)
    s.checkpoint(4350.0, z=900.0)
    s.hourglass(4450.0, 450.0, 900.0)
    s.pit(4600, 5400, 900.0)
    spawn(unreal.DropChute, s.world(5000.0, 0.0, 900.0 + 650.0), rotation=s.rotation(), label="DropChute",
          interval=3.0, prop_scale=unreal.Vector(2.5, 2.5, 0.5), prop_linear_damping=4.0)
    s.jump(4600, 900, 4875, 900)
    s.jump(5125, 900, 5400, 900)
    s.floor(5400, 5800, 900.0)
    s.hourglass(5600.0, 450.0, 900.0)

    # a falling platform to rewind
    s.pit(5800, 6900, 900.0)
    spawn(unreal.FallingPlatform, s.world(6350.0, 0.0, 900.0 - 15.0), rotation=s.rotation(), label="FallingPlatform",
          scale=((1100 - 40) / 100, 4.0, 0.3))
    s.floor(6900, 7200, 900.0)
    s.checkpoint(7050.0, z=900.0)

    # up again, to a bridge that holds 2 seconds: freeze it on the way
    s.ramp(7200, 8066, 900.0, 1400.0, -HALF_WIDTH, HALF_WIDTH)
    s.ramp_rails(7200, 8066, 900.0, 1400.0)
    s.floor(8066, 8400, 1400.0)
    s.hourglass(8200.0, 450.0, 1400.0)
    pit, length, hold = 1400.0, 1600.0, 2.0
    s.pit(8400, 8400 + pit, 1400.0)
    top = 1400.0 - 14.0
    spawn(unreal.BrokenBridge, s.world(8400.0 - (length - pit) / 2, 0.0, top), rotation=s.rotation(), label="ShortBridge",
          bridge_length=length, plank_count=11, fall_depth=top - (1400.0 - PIT_DEPTH + 15.0), scatter=150.0,
          timeable={"hold_duration": hold})

    # the top: the goal under a giant clock face
    s.floor(9800, 10400, 1400.0)
    trigger(unreal.GoalTrigger, s.world(10100.0, 0.0, 1400.0), (250.0, HALF_WIDTH, 200.0), rotation=s.rotation(), label="Goal")
    face_u = 10500.0
    face = spawn(unreal.StaticMeshActor, s.world(face_u, 0.0, 1400.0 + 700.0), rotation=s.rotation(pitch=90.0), label="ClockFace", scale=(14, 14, 0.5))
    face.static_mesh_component.set_static_mesh(cylinder_mesh)
    face.static_mesh_component.set_material(0, materials["brass"])
    hand_a = s.world(face_u - 40.0, 0.0, 1400.0 + 700.0 + 250.0)
    world_box(hand_a[0] - 10, hand_a[0] + 10, -25, 25, hand_a[2] - 250, hand_a[2] + 250, "ClockHand", "hand")
    hand_b = s.world(face_u - 40.0, 175.0, 1400.0 + 700.0)
    world_box(hand_b[0] - 10, hand_b[0] + 10, hand_b[1] - 175, hand_b[1] + 175, hand_b[2] - 20, hand_b[2] + 20, "ClockHand", "hand")
    s.label(10000.0, 0.0, 1400.0 + 450.0, "THE CLOCK", size=50.0)
    return s


def build_environment(min_x, max_x, top):
    sun = spawn(unreal.DirectionalLight, (0, 0, top + 2000), rotation=rot(pitch=-55.0, yaw=30.0), label="Sun")
    sun.light_component.set_mobility(unreal.ComponentMobility.MOVABLE)
    sun.light_component.set_editor_property("atmosphere_sun_light", True)
    sun.light_component.set_editor_property("intensity", 8.0)

    sky = spawn(unreal.SkyLight, (0, 0, top + 2200), label="SkyLight")
    sky.light_component.set_mobility(unreal.ComponentMobility.MOVABLE)
    sky.light_component.set_editor_property("real_time_capture", True)
    sky.light_component.set_editor_property("intensity", 1.5)

    spawn(unreal.SkyAtmosphere, (0, 0, top + 2400), label="SkyAtmosphere")
    spawn(unreal.ExponentialHeightFog, (0, 0, -500), label="Fog")

    # under everything, for anything that gets past the rails and pits
    margin = 5000.0
    kill_box(min_x - margin, max_x + margin, -margin, margin, -2000.0, -1500.0)

    # the tower's corners, for the look of it
    for x in (min_x, max_x):
        for y in (-HALF_WIDTH - 150.0, HALF_WIDTH + 150.0):
            world_box(x - 150.0, x + 150.0, y - 100.0, y + 100.0, -1500.0, top, "TowerPillar", "wall")


def main():
    global current_section
    if unreal.EditorAssetLibrary.does_asset_exist(LEVEL_PATH):
        if not levels.load_level(LEVEL_PATH):
            raise RuntimeError(f"Could not load {LEVEL_PATH}")
        actors.destroy_actors(actors.get_all_level_actors())
        action = "Rebuilt"
    else:
        if not levels.new_level(LEVEL_PATH):
            raise RuntimeError(f"Could not create {LEVEL_PATH}")
        action = "Created"

    make_materials()

    s1 = section_1(0.0)
    s2 = section_2(s1.x_end)
    s3, _ = section_3(s2.x_end)
    s4, _ = section_4(s3.x_end)
    s5 = section_5(s4.x_end)

    current_section = 0
    xs = [s.x0 for s in (s1, s2, s3, s4, s5)] + [s.x_end for s in (s1, s2, s3, s4, s5)]
    build_environment(min(xs) - RAMP_RUN - 400.0, max(xs) + RAMP_RUN + 400.0, STORY * 4 + 2800.0)

    if not levels.save_current_level():
        raise RuntimeError(f"Could not save {LEVEL_PATH}")

    unreal.log(f"{action} and saved {LEVEL_PATH}: {len(built)} actors, {jump_count} marked jumps")


main()
