#!/usr/bin/env python3
"""Prepare a KayKit character .glb as a single-skinned-mesh import source for Arisen.

KayKit's Adventurers pack (CC0, https://kaylousberg.itch.io/kaykit-adventurers) ships one .glb per
character holding one skin, one gradient-atlas texture, the skinned body parts, static
weapon/accessory meshes, and 76 animation clips. The engine's skinned-mesh cook imports a source
with exactly one skin on exactly one skinned node, and the sample that uses this content has no clip
runtime yet, so this script keeps what the sample needs and drops what it does not:

* animations are dropped (no clip runtime exists; they are most of the pack's bytes),
* the static weapon/accessory meshes are dropped (they are not skinned, so they cannot follow the
  pose, and a static mesh may not share a source with a skinned one),
* the skinned body parts are merged into one mesh, which is what a single skinned node can carry,
* the gradient atlas is written beside the mesh as a standalone texture source,
* the skeleton is kept verbatim: joint nodes, their hierarchy, and the inverse bind matrices.

The script is deterministic: it rewrites the parts of the binary chunk that survive, so rerunning it
over the same input reproduces the same bytes. It prints the parents-first joint order the mesh cook
assigns, which is the ordering a pose driver indexes.

Usage:
    python prepare_kaykit_character.py <character.glb> --name Knight --output-root <package>/Assets
"""

from __future__ import annotations

import argparse
import json
import struct
import sys
from pathlib import Path

COMPONENT_FORMAT = {
    5120: ("b", 1),  # BYTE
    5121: ("B", 1),  # UNSIGNED_BYTE
    5122: ("h", 2),  # SHORT
    5123: ("H", 2),  # UNSIGNED_SHORT
    5125: ("I", 4),  # UNSIGNED_INT
    5126: ("f", 4),  # FLOAT
}

TYPE_COUNT = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


class Glb:
    def __init__(self, path: Path):
        with open(path, "rb") as handle:
            magic, version, _length = struct.unpack("<III", handle.read(12))
            if magic != 0x46546C67 or version != 2:
                raise SystemExit(f"{path} is not a glTF 2.0 binary container")
            json_chunk, binary_chunk = b"", b""
            while True:
                header = handle.read(8)
                if len(header) < 8:
                    break
                chunk_length, chunk_type = struct.unpack("<II", header)
                payload = handle.read(chunk_length)
                if chunk_type == 0x4E4F534A:
                    json_chunk = payload
                elif chunk_type == 0x004E4942:
                    binary_chunk = payload
        self.json = json.loads(json_chunk.decode("utf-8"))
        self.binary = bytearray(binary_chunk)

    def read_accessor(self, index: int) -> list:
        accessor = self.json["accessors"][index]
        component_format, component_size = COMPONENT_FORMAT[accessor["componentType"]]
        count = TYPE_COUNT[accessor["type"]]
        view = self.json["bufferViews"][accessor["bufferView"]]
        base = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
        stride = view.get("byteStride") or component_size * count
        values = []
        for element in range(accessor["count"]):
            offset = base + element * stride
            values.append(struct.unpack_from("<" + component_format * count, self.binary, offset))
        return values

    def image_buffer_view_payload(self, image_index: int) -> bytes:
        view = self.json["bufferViews"][self.json["images"][image_index]["bufferView"]]
        start = view.get("byteOffset", 0)
        return bytes(self.binary[start:start + view["byteLength"]])


def pack_component(component_type: int, values: list) -> bytes:
    component_format, _size = COMPONENT_FORMAT[component_type]
    return struct.pack("<" + component_format * len(values), *values)


def pad_to_four(buffer: bytearray, fill: int = 0) -> None:
    while len(buffer) % 4:
        buffer.append(fill)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, help="KayKit character .glb")
    parser.add_argument("--name", required=True, help="character name used for the output files")
    parser.add_argument("--output-root", type=Path, required=True,
                        help="package Assets directory the mesh and texture are written under")
    parser.add_argument("--dry-run", action="store_true", help="report without writing files")
    args = parser.parse_args()

    source = Glb(args.source)
    root = source.json
    if len(root.get("skins", [])) != 1:
        raise SystemExit("expected exactly one skin in the source")

    skin = root["skins"][0]
    joint_nodes = list(skin["joints"])
    joint_set = set(joint_nodes)

    skinned_meshes = []
    for node_index, node in enumerate(root["nodes"]):
        mesh_index = node.get("mesh")
        if mesh_index is None or node.get("skin") is None:
            continue
        primitive = root["meshes"][mesh_index]["primitives"][0]
        if "JOINTS_0" in primitive["attributes"]:
            skinned_meshes.append((node_index, mesh_index, primitive))
    if not skinned_meshes:
        raise SystemExit("the source has no skinned mesh")
    if len({mesh for _node, mesh, _primitive in skinned_meshes
            for _ in [0]}) != len(skinned_meshes):
        raise SystemExit("a mesh is used by more than one skinned node")

    # Merge every skinned primitive into one indexed triangle list. KayKit's parts share the skin,
    # the attribute layout, and the material, so concatenation is the whole merge.
    positions, normals, uvs, joints, weights, indices = [], [], [], [], [], []
    for _node, _mesh, primitive in skinned_meshes:
        attributes = primitive["attributes"]
        vertex_offset = len(positions)
        positions.extend(source.read_accessor(attributes["POSITION"]))
        normals.extend(source.read_accessor(attributes["NORMAL"]))
        uvs.extend(source.read_accessor(attributes["TEXCOORD_0"]))
        joints.extend(source.read_accessor(attributes["JOINTS_0"]))
        weights.extend(source.read_accessor(attributes["WEIGHTS_0"]))
        if "indices" not in primitive:
            indices.extend(range(vertex_offset, len(positions)))
        else:
            indices.extend(index[0] + vertex_offset
                           for index in source.read_accessor(primitive["indices"]))

    binary = bytearray()
    buffer_views: list = []
    accessors: list = []

    def append_vec_accessor(component_type: int, type_name: str, values: list,
                            min_max: bool = False) -> int:
        payload = b"".join(pack_component(component_type, list(value)) for value in values)
        offset = len(binary)
        binary.extend(payload)
        pad_to_four(binary)
        buffer_views.append({"buffer": 0, "byteOffset": offset, "byteLength": len(payload)})
        accessor = {
            "bufferView": len(buffer_views) - 1,
            "componentType": component_type,
            "count": len(values),
            "type": type_name,
        }
        if min_max:
            components = TYPE_COUNT[type_name]
            accessor["min"] = [min(value[i] for value in values) for i in range(components)]
            accessor["max"] = [max(value[i] for value in values) for i in range(components)]
        accessors.append(accessor)
        return len(accessors) - 1

    position_accessor = append_vec_accessor(5126, "VEC3", positions, min_max=True)
    normal_accessor = append_vec_accessor(5126, "VEC3", normals)
    uv_accessor = append_vec_accessor(5126, "VEC2", uvs)
    joint_accessor = append_vec_accessor(5121, "VEC4", joints)
    weight_accessor = append_vec_accessor(5126, "VEC4", weights)
    index_accessor = append_vec_accessor(5125, "SCALAR", [(value,) for value in indices])

    # The skeleton is copied verbatim, including the inverse bind matrices the palette needs.
    inverse_bind_accessor = -1
    if "inverseBindMatrices" in skin:
        source_accessor = root["accessors"][skin["inverseBindMatrices"]]
        values = source.read_accessor(skin["inverseBindMatrices"])
        payload = b"".join(pack_component(5126, list(value)) for value in values)
        offset = len(binary)
        binary.extend(payload)
        pad_to_four(binary)
        buffer_views.append({"buffer": 0, "byteOffset": offset, "byteLength": len(payload)})
        accessors.append({
            "bufferView": len(buffer_views) - 1,
            "componentType": 5126,
            "count": len(values),
            "type": "MAT4",
        })
        inverse_bind_accessor = len(accessors) - 1

    # Nodes: the joints, every ancestor a joint hangs from, and the one skinned node.
    joint_parent: dict[int, int] = {}
    for parent_index, node in enumerate(root["nodes"]):
        for child_index in node.get("children", []):
            joint_parent[child_index] = parent_index

    keep: list[int] = []
    seen = set()

    def require(index: int) -> None:
        if index in seen:
            return
        seen.add(index)
        parent = joint_parent.get(index)
        if parent is not None:
            require(parent)
        keep.append(index)

    for joint in joint_nodes:
        require(joint)
    armature_roots = [index for index in keep if joint_parent.get(index) not in seen]
    skinned_node_index = len(root["nodes"])
    keep.append(skinned_node_index)

    remap = {old: new for new, old in enumerate(keep)}
    nodes: list = []
    for old_index in keep:
        if old_index == skinned_node_index:
            nodes.append({"name": args.name, "mesh": 0, "skin": 0})
            continue
        source_node = root["nodes"][old_index]
        node = {"name": source_node.get("name", f"Node_{old_index}")}
        for key in ("translation", "rotation", "scale", "matrix"):
            if key in source_node:
                node[key] = source_node[key]
        children = [remap[child] for child in source_node.get("children", []) if child in remap]
        if children:
            node["children"] = children
        nodes.append(node)

    # The pack's clips are authored against this skeleton, so they travel with the mesh: their
    # channels keep the node identities above, their referenced accessors are copied into the new
    # binary chunk, and a channel whose target node did not survive is dropped.
    copied_accessors: dict[int, int] = {}

    def copy_accessor(index: int, with_extents: bool = False) -> int:
        if index in copied_accessors:
            return copied_accessors[index]
        accessor = root["accessors"][index]
        new_index = append_vec_accessor(
            accessor["componentType"],
            accessor["type"],
            source.read_accessor(index),
            min_max=with_extents or "min" in accessor)
        copied_accessors[index] = new_index
        return new_index

    animations: list = []
    for animation in root.get("animations", []):
        samplers: list = []
        sampler_remap: dict[int, int] = {}
        channels: list = []
        for channel in animation["channels"]:
            node = channel["target"]["node"]
            if node not in remap:
                continue
            source_sampler = channel["sampler"]
            if source_sampler not in sampler_remap:
                sampler = animation["samplers"][source_sampler]
                sampler_remap[source_sampler] = len(samplers)
                copied = {
                    "input": copy_accessor(sampler["input"], with_extents=True),
                    "output": copy_accessor(sampler["output"]),
                }
                if "interpolation" in sampler:
                    copied["interpolation"] = sampler["interpolation"]
                samplers.append(copied)
            channels.append({
                "sampler": sampler_remap[source_sampler],
                "target": {"node": remap[node], "path": channel["target"]["path"]},
            })
        if channels:
            animations.append({
                # A clip is addressed by name, so an unnamed source clip gets the name its index
                # implies rather than failing the cook.
                "name": animation.get("name", "") or f"Clip_{len(animations)}",
                "samplers": samplers,
                "channels": channels,
            })

    joint_remap = [remap[joint] for joint in joint_nodes]
    mesh = {
        "name": f"{args.name}_Skinned",
        "primitives": [{
            "attributes": {
                "POSITION": position_accessor,
                "NORMAL": normal_accessor,
                "TEXCOORD_0": uv_accessor,
                "JOINTS_0": joint_accessor,
                "WEIGHTS_0": weight_accessor,
            },
            "indices": index_accessor,
            "mode": 4,
        }],
    }

    scene_roots = [remap[index] for index in armature_roots] + [remap[skinned_node_index]]
    document = {
        "asset": {"version": "2.0", "generator": "Arisen prepare_kaykit_character.py"},
        "scene": 0,
        "scenes": [{"name": args.name, "nodes": scene_roots}],
        "nodes": nodes,
        "skins": [{
            "joints": joint_remap,
            "inverseBindMatrices": inverse_bind_accessor,
        }],
        "meshes": [mesh],
        "buffers": [{"byteLength": len(binary)}],
        "bufferViews": buffer_views,
        "accessors": accessors,
    }
    if animations:
        document["animations"] = animations

    json_bytes = json.dumps(document, separators=(",", ":")).encode("utf-8")
    while len(json_bytes) % 4:
        json_bytes += b" "
    pad_to_four(binary)
    total = 12 + 8 + len(json_bytes) + 8 + len(binary)
    glb = bytearray()
    glb += struct.pack("<III", 0x46546C67, 2, total)
    glb += struct.pack("<II", len(json_bytes), 0x4E4F534A) + json_bytes
    glb += struct.pack("<II", len(binary), 0x004E4942) + bytes(binary)

    image = root.get("images", [{}])[0]
    texture_extension = {"image/png": ".png", "image/jpeg": ".jpg"}.get(
        image.get("mimeType", "image/png"), ".png")
    texture_name = f"{args.name}_BaseColor{texture_extension}"
    texture_bytes = source.image_buffer_view_payload(0)

    # The mesh cook orders joints parents-first by bucketing each joint's *joint* ancestor depth and
    # keeping the source order inside a depth, so the cooked index of a joint is predictable here.
    joint_names = [source.json["nodes"][node].get("name", f"Node_{node}") for node in joint_nodes]
    depths = []
    for joint in joint_nodes:
        depth = 0
        walker = joint_parent.get(joint)
        while walker is not None:
            if walker in joint_set:
                depth += 1
            walker = joint_parent.get(walker)
        depths.append(depth)
    cooked_order = sorted(range(len(joint_nodes)), key=lambda slot: (depths[slot], slot))
    cooked_index_of = {slot: index for index, slot in enumerate(cooked_order)}

    output_root = args.output_root
    mesh_path = output_root / "Meshes" / f"{args.name}.glb"
    texture_path = output_root / "Textures" / texture_name
    if not args.dry_run:
        mesh_path.parent.mkdir(parents=True, exist_ok=True)
        texture_path.parent.mkdir(parents=True, exist_ok=True)
        mesh_path.write_bytes(bytes(glb))
        texture_path.write_bytes(texture_bytes)

    bounds_min = accessors[position_accessor]["min"]
    bounds_max = accessors[position_accessor]["max"]
    print(f"source        : {args.source}")
    print(f"joints        : {len(joint_nodes)}")
    print(f"vertices      : {len(positions)}")
    print(f"triangles     : {len(indices) // 3}")
    print(f"bounds min    : {[round(value, 3) for value in bounds_min]}")
    print(f"bounds max    : {[round(value, 3) for value in bounds_max]}")
    print(f"mesh bytes    : {len(glb)}  -> {mesh_path}")
    print(f"texture bytes : {len(texture_bytes)}  -> {texture_path}")
    print("cooked joint order (parents-first; cooked index: name):")
    for slot in cooked_order:
        print(f"  {cooked_index_of[slot]:3d}: {joint_names[slot]}  (source slot {slot}, depth {depths[slot]})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
