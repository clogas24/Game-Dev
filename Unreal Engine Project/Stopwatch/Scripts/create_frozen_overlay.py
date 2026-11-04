"""Creates or updates the time ability overlays and the M_Clockwork base material in /Game/Stopwatch/Materials. Safe to run repeatedly.

Run from the editor's Python console, or headless:
  UnrealEditor-Cmd.exe <path>/Stopwatch.uproject -run=pythonscript -script="<path>/Scripts/create_frozen_overlay.py"
"""

import unreal

PACKAGE_PATH = "/Game/Stopwatch/Materials"

# name: (color, intensity, opacity)
OVERLAYS = {
    "M_FrozenOverlay": (unreal.LinearColor(0.3, 0.75, 1.0, 1.0), 2.0, 0.25),
    "M_RewindOverlay": (unreal.LinearColor(1.0, 0.55, 0.1, 1.0), 2.5, 0.3),
}

lib = unreal.MaterialEditingLibrary


def add_parameter(material, node_class, name, value, y):
    node = lib.create_material_expression(material, node_class, -600, y)
    node.set_editor_property("parameter_name", name)
    node.set_editor_property("default_value", value)
    return node


def multiply(material, a, b, y):
    node = lib.create_material_expression(material, unreal.MaterialExpressionMultiply, -200, y)
    lib.connect_material_expressions(a, "", node, "A")
    lib.connect_material_expressions(b, "", node, "B")
    return node


def open_material(asset_name):
    """Returns the material emptied of its nodes, creating it if needed, and what was done"""
    asset_path = f"{PACKAGE_PATH}/{asset_name}"

    if unreal.EditorAssetLibrary.does_asset_exist(asset_path):
        material = unreal.EditorAssetLibrary.load_asset(asset_path)
        # delete_all_material_expressions leaves some nodes behind, so remove them one at a time
        for expression in lib.get_material_expressions(material):
            lib.delete_material_expression(material, expression)
        if lib.get_num_material_expressions(material) != 0:
            raise RuntimeError(f"Could not clear the existing nodes in {asset_path}")
        action = "Updated"
    else:
        material = unreal.AssetToolsHelpers.get_asset_tools().create_asset(
            asset_name, PACKAGE_PATH, unreal.Material, unreal.MaterialFactoryNew())
        action = "Created"

    if not material:
        raise RuntimeError(f"Could not create {asset_path}")

    return material, action


def save_material(material, asset_name, description):
    lib.recompile_material(material)

    if not unreal.EditorAssetLibrary.save_loaded_asset(material, only_if_is_dirty=False):
        raise RuntimeError(f"Could not save {PACKAGE_PATH}/{asset_name}")

    return f"{PACKAGE_PATH}/{asset_name}: {description}, {lib.get_num_material_expressions(material)} nodes"


def build_overlay(asset_name, color_value, intensity_value, opacity_value):
    material, action = open_material(asset_name)

    material.set_editor_property("blend_mode", unreal.BlendMode.BLEND_TRANSLUCENT)
    material.set_editor_property("shading_model", unreal.MaterialShadingModel.MSM_UNLIT)

    color = add_parameter(material, unreal.MaterialExpressionVectorParameter, "Color", color_value, -300)
    intensity = add_parameter(material, unreal.MaterialExpressionScalarParameter, "Intensity", intensity_value, -100)
    opacity = add_parameter(material, unreal.MaterialExpressionScalarParameter, "Opacity", opacity_value, 300)

    # the base fraction keeps a faint tint across the whole surface, not only at the rim
    fresnel = lib.create_material_expression(material, unreal.MaterialExpressionFresnel, -600, 100)
    fresnel.set_editor_property("exponent", 3.0)
    fresnel.set_editor_property("base_reflect_fraction", 0.25)

    glow = multiply(material, color, intensity, -200)
    emissive = multiply(material, glow, fresnel, 0)
    faint = multiply(material, opacity, fresnel, 200)

    lib.connect_material_property(emissive, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)
    lib.connect_material_property(faint, "", unreal.MaterialProperty.MP_OPACITY)

    print(f"{action} and saved " + save_material(material, asset_name, "translucent, unlit, parameters Color/Intensity/Opacity"))


def constant_math(material, node_class, a, constant, y):
    """a times or plus a constant"""
    node = lib.create_material_expression(material, node_class, -200, y)
    node.set_editor_property("const_b", constant)
    lib.connect_material_expressions(a, "", node, "A")
    return node


def build_clockwork():
    """The brass base material of everything timeable, with a pulsing rim for aiming and a red flash for warnings"""
    material, action = open_material("M_Clockwork")

    base = add_parameter(material, unreal.MaterialExpressionVectorParameter, "BaseColor", unreal.LinearColor(0.72, 0.5, 0.2, 1.0), -800)
    rim_color = add_parameter(material, unreal.MaterialExpressionVectorParameter, "RimColor", unreal.LinearColor(0.3, 0.8, 1.0, 1.0), -600)
    rim_amount = add_parameter(material, unreal.MaterialExpressionScalarParameter, "RimAmount", 0.0, -400)
    flash = add_parameter(material, unreal.MaterialExpressionScalarParameter, "Flash", 0.0, 400)

    metallic = lib.create_material_expression(material, unreal.MaterialExpressionConstant, -600, -1000)
    metallic.set_editor_property("r", 0.7)
    roughness = lib.create_material_expression(material, unreal.MaterialExpressionConstant, -600, -1100)
    roughness.set_editor_property("r", 0.35)

    # between 0.6 and 1, a little over once a second
    time = lib.create_material_expression(material, unreal.MaterialExpressionTime, -800, -200)
    sine = lib.create_material_expression(material, unreal.MaterialExpressionSine, -600, -200)
    sine.set_editor_property("period", 0.8)
    lib.connect_material_expressions(time, "", sine, "")
    pulse = constant_math(material, unreal.MaterialExpressionAdd, constant_math(material, unreal.MaterialExpressionMultiply, sine, 0.2, -200), 0.8, -100)

    # a glow over the whole surface, so flat faces seen head on still light up, and brighter at the edges
    fresnel = lib.create_material_expression(material, unreal.MaterialExpressionFresnel, -800, 0)
    fresnel.set_editor_property("exponent", 3.0)
    fresnel.set_editor_property("base_reflect_fraction", 0.0)
    shape = constant_math(material, unreal.MaterialExpressionAdd, constant_math(material, unreal.MaterialExpressionMultiply, fresnel, 3.0, 0), 0.4, 100)

    rim = multiply(material, multiply(material, multiply(material, rim_color, rim_amount, -500), pulse, -300), shape, 200)

    red = lib.create_material_expression(material, unreal.MaterialExpressionConstant3Vector, -600, 500)
    red.set_editor_property("constant", unreal.LinearColor(4.0, 0.3, 0.1, 1.0))
    warning = multiply(material, flash, red, 400)

    emissive = lib.create_material_expression(material, unreal.MaterialExpressionAdd, 0, 300)
    lib.connect_material_expressions(rim, "", emissive, "A")
    lib.connect_material_expressions(warning, "", emissive, "B")

    lib.connect_material_property(base, "", unreal.MaterialProperty.MP_BASE_COLOR)
    lib.connect_material_property(metallic, "", unreal.MaterialProperty.MP_METALLIC)
    lib.connect_material_property(roughness, "", unreal.MaterialProperty.MP_ROUGHNESS)
    lib.connect_material_property(emissive, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)

    print(f"{action} and saved " + save_material(material, "M_Clockwork", "opaque, parameters BaseColor/RimColor/RimAmount/Flash"))


for name, values in OVERLAYS.items():
    build_overlay(name, *values)

build_clockwork()
