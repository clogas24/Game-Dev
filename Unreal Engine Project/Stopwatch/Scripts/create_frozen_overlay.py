"""Creates or updates the time ability overlays in /Game/Stopwatch/Materials. Safe to run repeatedly.

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


def build_overlay(asset_name, color_value, intensity_value, opacity_value):
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

    lib.recompile_material(material)

    if not unreal.EditorAssetLibrary.save_loaded_asset(material, only_if_is_dirty=False):
        raise RuntimeError(f"Could not save {asset_path}")

    print(f"{action} and saved {asset_path}: translucent, unlit, parameters Color/Intensity/Opacity, "
          f"{lib.get_num_material_expressions(material)} nodes")


for name, values in OVERLAYS.items():
    build_overlay(name, *values)
