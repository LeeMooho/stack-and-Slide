"""Static geometry checks against real Unity YAML, not PhysX or Play Mode tests.

The parser deliberately supports only the inline vectors, references and transform
overrides used by these assets. Unexpected authoring shapes fail instead of being guessed.
"""

from dataclasses import dataclass
from itertools import product
import math
from pathlib import Path
import re
import unittest


PROJECT = Path(__file__).resolve().parents[1] / "stack and slide"
EPSILON = 1e-6
DISTANCE = 8.0
EXPECTED = {
    102: ({19, 27, 35, 43}, {58, 59, 60, 61}, {59}),
    103: ({27, 28, 35, 36}, {59, 60}, {59, 60}),
}


def documents(path):
    matches = list(re.finditer(
        r"^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)",
        path.read_text(encoding="utf-8-sig"), re.MULTILINE | re.DOTALL))
    result = {int(match[2]): (int(match[1]), match[3]) for match in matches}
    assert result and len(result) == len(matches), "Missing or duplicate Unity document IDs"
    return result


def field(text, name):
    match = re.search(r"^\s*" + re.escape(name) + r":\s*([^\n]+)$", text, re.MULTILINE)
    assert match, "Missing YAML field: " + name
    return match[1].strip()


def reference(text, name):
    match = re.search(r"fileID:\s*(-?\d+)", field(text, name))
    assert match, "Missing file reference: " + name
    return int(match[1])


def vector(text, name, axes="xyz"):
    values = dict(re.findall(r"([xyzw]):\s*([^,}]+)", field(text, name)))
    assert set(values) == set(axes), "Unexpected vector: " + name
    return tuple(float(values[axis]) for axis in axes)


def add(a, b):
    return tuple(x + y for x, y in zip(a, b))


def multiply(a, b):
    return tuple(x * y for x, y in zip(a, b))


def rotate(point, quaternion):
    length = math.sqrt(sum(value * value for value in quaternion))
    assert length > EPSILON, "A zero quaternion has no usable rotation"
    x, y, z, w = (value / length for value in quaternion)
    matrix = (
        (1 - 2*y*y - 2*z*z, 2*x*y - 2*z*w, 2*x*z + 2*y*w),
        (2*x*y + 2*z*w, 1 - 2*x*x - 2*z*z, 2*y*z - 2*x*w),
        (2*x*z - 2*y*w, 2*y*z + 2*x*w, 1 - 2*x*x - 2*y*y),
    )
    return tuple(sum(a*b for a, b in zip(row, point)) for row in matrix)


@dataclass(frozen=True)
class Transform:
    position: tuple
    rotation: tuple
    scale: tuple
    parent: int

    @classmethod
    def read(cls, text):
        return cls(vector(text, "m_LocalPosition"), vector(text, "m_LocalRotation", "xyzw"),
                   vector(text, "m_LocalScale"), reference(text, "m_Father"))

    def point(self, point):
        return add(self.position, rotate(multiply(point, self.scale), self.rotation))


def hierarchy_point(objects, parent, point, direction=False):
    visited = set()
    while parent:
        assert parent not in visited, "Transform hierarchy contains a cycle"
        visited.add(parent)
        kind, text = objects[parent]
        assert kind == 4, "Expected a non-stripped Transform parent"
        assert field(objects[reference(text, "m_GameObject")][1], "m_IsActive") == "1"
        transform = Transform.read(text)
        point = rotate(point, transform.rotation) if direction else transform.point(point)
        parent = transform.parent
    return point


def corners(center, size):
    return [tuple(center[axis] + sign[axis] * size[axis] / 2 for axis in range(3))
            for sign in product((-1, 1), repeat=3)]


def bounds(points):
    return tuple(min(point[axis] for point in points) for axis in range(3)), \
        tuple(max(point[axis] for point in points) for axis in range(3))


def ray_hits_box(origin, direction, box):
    # Slab intersection is exact here: the guarded colliders remain axis-aligned
    # after the documented zero/quarter-turn root rotations.
    lower, upper = 0.0, DISTANCE
    for axis in range(3):
        if abs(direction[axis]) < EPSILON:
            if not box[0][axis] - EPSILON <= origin[axis] <= box[1][axis] + EPSILON:
                return False
        else:
            a = (box[0][axis] - origin[axis]) / direction[axis]
            b = (box[1][axis] - origin[axis]) / direction[axis]
            lower, upper = max(lower, min(a, b)), min(upper, max(a, b))
            if lower > upper + EPSILON:
                return False
    return True


def overlaps(a, b):
    # Touching a support face is allowed; positive volume penetration is not.
    return all(min(a[1][axis], b[1][axis]) - max(a[0][axis], b[0][axis]) > EPSILON
               for axis in range(3))


class ChallengeGeometryTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.scene = documents(PROJECT / "Assets/Scenes/stage1_1.unity")
        script_guid = field((PROJECT / "Assets/script/stage.cs.meta").read_text(), "guid")
        cls.stage = next(text for kind, text in cls.scene.values()
                         if kind == 114 and "guid: " + script_guid in text)
        # The checkerboard alternates two material variants with identical collider geometry.
        cls.cube_guids = set()
        for name in ("Cube (0.0)", "Cube (0.1)"):
            path = PROJECT / "Assets/Prefabs" / (name + ".prefab")
            cube = documents(path)
            cls.cube_guids.add(field(Path(str(path) + ".meta").read_text(), "guid"))
            cube_transform = next(Transform.read(text) for kind, text in cube.values() if kind == 4)
            cube_object = next(text for kind, text in cube.values() if kind == 1)
            assert field(cube_object, "m_IsActive") == "1" and field(cube_object, "m_TagString") == "Floor"
            assert cube_transform.position == (0, 0, 0) and cube_transform.scale == (1, 1, 1)
            assert cube_transform.parent == 0
            assert all(abs(a-b) < EPSILON for a, b in zip(
                rotate((0, 1, 0), cube_transform.rotation), (0, 1, 0)))
            collider = next(text for kind, text in cube.values() if kind == 65 and field(text, "m_Enabled") == "1")
            assert field(collider, "m_IsTrigger") == "0"
            assert vector(collider, "m_Size") == (1, 1, 1) and vector(collider, "m_Center") == (0, 0, 0)
        cls.grids = {}
        for name in ("floor", "wall", "frontwall"):
            match = re.search(r"^  " + name + r":\n((?:  - \{fileID: -?\d+\}\n)+)", cls.stage, re.MULTILINE)
            assert match, "Missing grid reference array: " + name
            ids = [int(value) for value in re.findall(r"fileID: (-?\d+)", match[1])]
            assert len(ids) == len(set(ids)) == 64
            cls.grids[name] = [cls.read_cell(identifier) for identifier in ids]

    @classmethod
    def read_cell(cls, identifier):
        game_object = cls.scene[identifier][1]
        instance = cls.scene[reference(game_object, "m_PrefabInstance")][1]
        source_guid = re.search(r"guid: (\w+)", field(instance, "m_SourcePrefab"))[1]
        assert source_guid in cls.cube_guids
        assert not re.search(r"propertyPath: m_(?:IsActive|Enabled)\n\s+value: 0(?:\n|$)", instance), \
            "A disabled grid cell cannot provide occupancy rays and floor support"
        values = dict(re.findall(r"^      propertyPath: (m_Local(?:Position|Rotation|Scale)\.[xyzw])\n"
                                 r"      value: ([^\n]+)$", instance, re.MULTILINE))
        position = tuple(float(values["m_LocalPosition." + axis]) for axis in "xyz")
        rotation = tuple(float(values["m_LocalRotation." + axis]) for axis in "xyzw")
        scale = tuple(float(values.get("m_LocalScale." + axis, "1")) for axis in "xyz")
        transform = Transform(position, rotation, scale, reference(instance, "m_TransformParent"))
        point = lambda local: hierarchy_point(cls.scene, transform.parent, transform.point(local))
        origin = point((0, 0, 0))
        direction = hierarchy_point(cls.scene, transform.parent, rotate((0, -1, 0), rotation), direction=True)
        cell_box = bounds([point(corner) for corner in corners((0, 0, 0), (1, 1, 1))])
        return origin, direction, cell_box

    @classmethod
    def piece_boxes(cls, name, root_position, root_rotation):
        objects = documents(PROJECT / "Assets/Prefabs" / (name + ".prefab"))
        transforms = {reference(text, "m_GameObject"): (identifier, Transform.read(text))
                      for identifier, (kind, text) in objects.items() if kind == 4}
        root_transform_id = next(identifier for identifier, transform in transforms.values() if transform.parent == 0)

        def world_point(identifier, point):
            text = objects[identifier][1]
            transform = Transform.read(text)
            assert field(objects[reference(text, "m_GameObject")][1], "m_IsActive") == "1"
            if identifier == root_transform_id:
                # Instantiate replaces the root position/rotation but retains its scale.
                return add(root_position, rotate(multiply(point, transform.scale), root_rotation))
            return world_point(transform.parent, transform.point(point))

        boxes = []
        for kind, text in objects.values():
            if kind != 65 or field(text, "m_Enabled") != "1":
                continue
            owner = reference(text, "m_GameObject")
            assert field(text, "m_IsTrigger") == "1", "Playable occupancy must remain a trigger"
            assert field(objects[owner][1], "m_TagString") == "Block"
            size = vector(text, "m_Size")
            assert all(abs(value - .95) < EPSILON for value in size), "Expected four .95 cube colliders"
            transform_id = transforms[owner][0]
            box = bounds([world_point(transform_id, point) for point in corners(vector(text, "m_Center"), size)])
            assert all(abs(box[1][axis] - box[0][axis] - .95) < EPSILON for axis in range(3)), \
                "Only axis-aligned, unit-scale quarter-turn solutions are proved"
            boxes.append(box)
        assert len(boxes) == 4, "The challenge assumes the authored four-cube piece"
        return boxes

    def assert_pose(self, stage_number, prefab, root_position, rotation):
        boxes = self.piece_boxes(prefab, root_position, rotation)
        actual = tuple({index for index, (origin, direction, _) in enumerate(self.grids[name])
                        if any(ray_hits_box(origin, direction, box) for box in boxes)}
                       for name in ("floor", "wall", "frontwall"))
        self.assertEqual(EXPECTED[stage_number], actual)
        floor_boxes = [cell[2] for cell in self.grids["floor"]]
        footprint = bounds([point for box in floor_boxes for point in box])
        for box in boxes:
            self.assertGreaterEqual(box[0][0] + EPSILON, footprint[0][0])
            self.assertLessEqual(box[1][0] - EPSILON, footprint[1][0])
            self.assertGreaterEqual(box[0][2] + EPSILON, footprint[0][2])
            self.assertLessEqual(box[1][2] - EPSILON, footprint[1][2])
            self.assertFalse(any(overlaps(box, cell[2]) for grid in self.grids.values() for cell in grid))
        self.assertFalse(any(overlaps(a, b) for index, a in enumerate(boxes) for b in boxes[index+1:]))

    def test_scene_grid_normals_and_spawn_origin_match_proof(self):
        self.assertEqual((6, 9, 6), vector(self.stage, "Origin"))
        for name, expected in (("floor", (0, 1, 0)), ("wall", (0, 0, 1)), ("frontwall", (1, 0, 0))):
            for _, direction, _ in self.grids[name]:
                for a, b in zip(direction, expected):
                    self.assertAlmostEqual(a, b, places=6)

    def test_half_step_routes_and_contact_height(self):
        origin = vector(self.stage, "Origin")
        source = (PROJECT / "Assets/script/Block.cs").read_text()
        move = float(re.search(r"const float MoveStep\s*=\s*([\d.]+)f", source)[1])
        turn = float(re.search(r"const float RotationStep\s*=\s*([\d.]+)f", source)[1])
        self.assertEqual(.5, move)
        self.assertEqual(90, turn)
        for offset, target in (((0, 0, .5), (3.5, 1, 3)), ((.5, 0, 0), (3.5, 1, 3.5))):
            for start, finish in zip(add(origin, offset), target):
                steps = (finish - start) / move
                self.assertAlmostEqual(steps, round(steps), places=6)
        # .5 is the actual grid cube's top; a .95 occupancy cube touches it at .975.
        floor_top = max(cell[2][1][1] for cell in self.grids["floor"])
        self.assertAlmostEqual(.5, floor_top)
        self.assertAlmostEqual(.975, floor_top + .95 / 2)

    def test_stage102_actual_prefab_and_grids_match_the_authored_masks(self):
        for height in (1.0, .975):
            with self.subTest(root_height=height):
                self.assert_pose(102, "IBlock", (3.5, height, 3), (0, 0, 0, 1))

    def test_stage103_quarter_turn_matches_the_authored_masks(self):
        quarter_turn_x = (math.sqrt(.5), 0, 0, math.sqrt(.5))
        for height in (1.0, .975):
            with self.subTest(root_height=height):
                self.assert_pose(103, "OBlock", (3.5, height, 3.5), quarter_turn_x)


if __name__ == "__main__":
    unittest.main()
