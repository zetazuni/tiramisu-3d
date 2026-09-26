"""Rigs the imported Sketchfab model "Female - Style 4 (Pose 1)" by mifusaja (CC BY) as Athirah. Import it first (Blender MCP
download_sketchfab_model uid 577be48ee19c46708a469ab502aae3d8, target_size 1.6), then exec this file."""
import os
exec(open(os.path.join(r"S:\Tiramisu Corner by Zetazuni", "tools", "blender_rig.py"), encoding="utf-8").read())
objs = {o.name: o for o in bpy.data.objects if o.type == 'MESH'}
for o in objs.values():
    mw = o.matrix_world.copy(); o.parent = None; o.matrix_world = mw
bpy.context.view_layer.update()
for n in [o.name for o in bpy.data.objects if o.type == 'EMPTY']:
    bpy.data.objects.remove(bpy.data.objects[n], do_unlink=True)
B = {
 "pelvis": (None, (0,-0.05,0.90), (0,-0.05,1.0)),
 "spine": ("pelvis", (0,-0.05,1.0), (0,-0.08,1.36)),
 "neck": ("spine", (0,-0.08,1.36), (0,-0.10,1.62)),
 "arm.L": ("spine", (0.14,-0.03,1.30), (0.25,-0.03,1.10)),
 "forearm.L": ("arm.L", (0.25,-0.03,1.10), (0.28,-0.07,1.52)),
 "arm.R": ("spine", (-0.14,-0.02,1.30), (-0.18,-0.02,1.06)),
 "forearm.R": ("arm.R", (-0.18,-0.02,1.06), (-0.31,-0.10,0.72)),
 "leg.L": ("pelvis", (0.07,-0.04,0.90), (0.065,0.0,0.47)),
 "shin.L": ("leg.L", (0.065,0.0,0.47), (0.09,0.17,0.02)),
 "leg.R": ("pelvis", (-0.07,-0.05,0.90), (-0.036,-0.11,0.47)),
 "shin.R": ("leg.R", (-0.036,-0.11,0.47), (-0.04,-0.25,0.02)),
}
arm = build_armature("athirah", B)
for n, o in objs.items():
    if n == "Object_6": skin(o, arm, B, only=["spine", "neck", "pelvis"])
    else: skin(o, arm, B, head_z=1.42)
mesh = join_meshes(list(objs.values()), "athirah_mesh")
mesh.parent = arm
if not any(m.type == 'ARMATURE' for m in mesh.modifiers):
    mod = mesh.modifiers.new("Armature", 'ARMATURE'); mod.object = arm
N = {"arm.L": (0.15,0,-1), "forearm.L": (0.05,0,-1), "arm.R": (-0.12,0,-1), "forearm.R": (-0.08,0,-1),
     "leg.L": (0,0,-1), "shin.L": (0,0.05,-1), "leg.R": (0,0,-1), "shin.R": (0,-0.05,-1)}
pose_neutral(mesh, arm, N)
