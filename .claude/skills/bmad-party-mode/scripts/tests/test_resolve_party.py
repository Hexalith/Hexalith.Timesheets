#!/usr/bin/env python3
# /// script
# requires-python = ">=3.11"
# ///
"""Unit tests for resolve_party.py — merge, alias, override, group resolution."""

import shutil
import sys
import tempfile
import unittest
from contextlib import chdir
from pathlib import Path
from textwrap import dedent

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
import resolve_party as rp  # noqa: E402

AGENTS = {
    "bmad-agent-analyst": {"name": "Mary", "icon": "📊", "title": "Analyst"},
    "bmad-agent-pm": {"name": "John", "icon": "📋", "title": "PM"},
}


class TestAlias(unittest.TestCase):
    def test_strips_known_prefixes(self):
        self.assertEqual(rp._alias("bmad-agent-analyst"), "analyst")
        self.assertEqual(rp._alias("bmad-foo"), "foo")

    def test_passes_through_unprefixed(self):
        self.assertEqual(rp._alias("morpheus"), "morpheus")


class TestBuildCollective(unittest.TestCase):
    def test_installed_agents_indexed_by_code_alias_and_name(self):
        col, idx, _ = rp.build_collective(AGENTS, [])
        self.assertEqual(set(col), {"bmad-agent-analyst", "bmad-agent-pm"})
        self.assertEqual(idx["analyst"], "bmad-agent-analyst")      # alias
        self.assertEqual(idx["mary"], "bmad-agent-analyst")         # name (ci)
        self.assertEqual(idx["bmad-agent-pm"], "bmad-agent-pm")     # full code
        self.assertEqual(col["bmad-agent-analyst"]["source"], "installed")

    def test_custom_member_appends(self):
        col, _, _ = rp.build_collective(AGENTS, [{"code": "morpheus", "name": "Morpheus", "persona": "riddles"}])
        self.assertIn("morpheus", col)
        self.assertEqual(col["morpheus"]["source"], "custom")
        self.assertEqual(col["morpheus"]["persona"], "riddles")

    def test_custom_override_preserves_omitted_installed_fields(self):
        agents = {code: dict(info) for code, info in AGENTS.items()}
        agents["bmad-agent-analyst"].update({
            "description": "Finds the evidence.", "module": "bmm", "team": "planning",
        })
        for token in ("analyst", "AnAlYsT", "bmad-agent-analyst", "Mary", "mArY"):
            with self.subTest(code=token):
                override = {
                    "code": token, "name": "Mary-Custom", "persona": "p",
                    "capabilities": ["critique"], "model": "custom-model",
                }
                col, index, installed = rp.build_collective(agents, [override])
                self.assertNotIn("analyst", col)
                self.assertEqual(index["mary-custom"], "bmad-agent-analyst")
                self.assertEqual(installed, ["bmad-agent-analyst", "bmad-agent-pm"])
                self.assertEqual(col["bmad-agent-analyst"], {
                    "code": "bmad-agent-analyst", "name": "Mary-Custom",
                    "icon": "📊", "title": "Analyst", "description": "Finds the evidence.",
                    "module": "bmm", "team": "planning",
                    "source": "custom", "persona": "p",
                    "capabilities": ["critique"], "model": "custom-model",
                })
                self.assertEqual(agents["bmad-agent-analyst"]["name"], "Mary")

    def test_custom_override_preserves_installed_name_when_omitted(self):
        col, *_ = rp.build_collective(AGENTS, [{"code": "analyst", "persona": "p"}])
        self.assertEqual(col["bmad-agent-analyst"]["name"], "Mary")

    def test_custom_override_can_clear_fields(self):
        overrides = [
            {"code": "analyst", "persona": "p", "capabilities": ["critique"], "model": "custom-model"},
            {"code": "analyst", "name": "", "icon": "", "title": "",
             "persona": "", "capabilities": [], "model": ""},
        ]
        col, *_ = rp.build_collective(AGENTS, overrides)
        for field in ("name", "icon", "title", "persona", "model"):
            with self.subTest(field=field):
                self.assertEqual(col["bmad-agent-analyst"][field], "")
        self.assertEqual(col["bmad-agent-analyst"]["capabilities"], [])

    def test_member_without_code_skipped(self):
        col, _, _ = rp.build_collective(AGENTS, [{"name": "Nameless"}])
        self.assertEqual(set(col), {"bmad-agent-analyst", "bmad-agent-pm"})


class TestResolveMembers(unittest.TestCase):
    def setUp(self):
        self.col, self.idx, _ = rp.build_collective(AGENTS, [{"code": "morpheus", "name": "Morpheus"}])

    def test_resolves_in_listed_order_and_flags_unknowns(self):
        resolved, unresolved = rp.resolve_members(["morpheus", "analyst", "ghost"], self.col, self.idx)
        self.assertEqual([m["code"] for m in resolved], ["morpheus", "bmad-agent-analyst"])
        self.assertEqual(unresolved, ["ghost"])

    def test_non_string_members_remain_unresolved_without_disrupting_valid_members(self):
        malformed = [123, ["analyst"], {"code": "analyst"}, None, False]
        tokens = ["morpheus", *malformed, "ghost", "Analyst"]
        resolved, unresolved = rp.resolve_members(tokens, self.col, self.idx)
        self.assertEqual([m["code"] for m in resolved], ["morpheus", "bmad-agent-analyst"])
        self.assertEqual(unresolved, [*malformed, "ghost"])

    def test_empty(self):
        self.assertEqual(rp.resolve_members([], self.col, self.idx), ([], []))


class TestCodeMap(unittest.TestCase):
    def test_maps_aliases_names_and_custom_only_codes_to_their_member(self):
        col, idx, _ = rp.build_collective(AGENTS, [{"code": "morpheus", "name": "Morpheus"}])
        codes = rp.code_map(col, idx)
        self.assertEqual(codes["analyst"], {"code": "bmad-agent-analyst", "name": "Mary"})
        self.assertEqual(codes["mary"]["code"], "bmad-agent-analyst")
        self.assertEqual(codes["bmad-agent-pm"]["code"], "bmad-agent-pm")
        self.assertEqual(codes["morpheus"], {"code": "morpheus", "name": "Morpheus"})

    def test_override_reports_the_overriding_name(self):
        col, idx, _ = rp.build_collective(AGENTS, [{"code": "analyst", "name": "Mary-Custom"}])
        self.assertEqual(rp.code_map(col, idx)["analyst"],
                         {"code": "bmad-agent-analyst", "name": "Mary-Custom"})


class TestGroups(unittest.TestCase):
    GROUPS = [
        {"id": "wr", "name": "Writers", "members": ["analyst", "morpheus"]},
        {"id": "bad"},  # no name -> falls back to id; no members -> count 0
        {"name": "no-id"},  # dropped from menu
    ]

    def test_menu_is_names_only_with_counts_and_open_cast_flag(self):
        menu = rp.group_menu(self.GROUPS)
        self.assertEqual(menu, [
            {"id": "wr", "name": "Writers", "member_count": 2},
            {"id": "bad", "name": "bad", "member_count": 0, "open_cast": True},
        ])

    def test_find_group(self):
        self.assertEqual(rp.find_group(self.GROUPS, "wr")["name"], "Writers")
        self.assertIsNone(rp.find_group(self.GROUPS, "missing"))


class TestGroupDetail(unittest.TestCase):
    def setUp(self):
        self.col, self.idx, _ = rp.build_collective(AGENTS, [{"code": "morpheus", "name": "Morpheus"}])

    def test_scene_passes_through_when_present(self):
        g = {"id": "tos-10-forward", "name": "Ten Forward", "members": ["morpheus"],
             "scene": "Late evening, a few rounds in."}
        d = rp.group_detail(g, self.col, self.idx)
        self.assertEqual(d["scene"], "Late evening, a few rounds in.")
        self.assertEqual([m["code"] for m in d["members"]], ["morpheus"])

    def test_scene_omitted_when_absent_or_empty(self):
        for g in ({"id": "g", "members": ["morpheus"]},
                  {"id": "g", "members": ["morpheus"], "scene": ""}):
            self.assertNotIn("scene", rp.group_detail(g, self.col, self.idx))

    def test_anchored_group_is_not_open_cast(self):
        g = {"id": "g", "members": ["morpheus"]}
        self.assertNotIn("open_cast", rp.group_detail(g, self.col, self.idx))

    def test_open_cast_group_flagged_with_empty_members(self):
        g = {"id": "rebels", "name": "Star Wars Rebels",
             "scene": "Figures from the Rebels universe drop in as the topic calls for them."}
        d = rp.group_detail(g, self.col, self.idx)
        self.assertTrue(d["open_cast"])
        self.assertEqual(d["members"], [])
        self.assertEqual(d["scene"][:7], "Figures")

    def test_memory_enabled_follows_group_flag_and_defaults_off(self):
        on = rp.group_detail({"id": "g", "members": ["morpheus"], "memory": True}, self.col, self.idx)
        self.assertTrue(on["memory_enabled"])
        off = rp.group_detail({"id": "g", "members": ["morpheus"], "memory": False}, self.col, self.idx)
        self.assertFalse(off["memory_enabled"])
        absent = rp.group_detail({"id": "g", "members": ["morpheus"]}, self.col, self.idx)
        self.assertFalse(absent["memory_enabled"])  # opt-in per named group


class TestInstalledCodesIsDefaultRoom(unittest.TestCase):
    """The default room is installed agents only; pure customs stay in the pool."""

    def test_pure_custom_excluded_override_kept_in_default_room(self):
        col, _, installed = rp.build_collective(AGENTS, [
            {"code": "morpheus", "name": "Morpheus"},                 # pure custom
            {"code": "analyst", "name": "Mary-Custom", "persona": "p"},  # override
            {"code": "sec-hawk", "name": "Vex"},                      # shipped crew member
        ])
        # Pure customs are in the pool...
        self.assertIn("morpheus", col)
        self.assertIn("sec-hawk", col)
        # ...but NOT in the default room.
        self.assertEqual(installed, ["bmad-agent-analyst", "bmad-agent-pm"])
        default_room = [col[c]["code"] for c in installed]
        self.assertEqual(default_room, ["bmad-agent-analyst", "bmad-agent-pm"])
        # An override keeps its installed slot (and its custom content).
        self.assertEqual(col["bmad-agent-analyst"]["name"], "Mary-Custom")


class TestResolverInvocation(unittest.TestCase):
    """The wrapper knows the project root, so it must not let the resolver
    infer one from the working directory (#2796)."""

    def _captured_command(self, tmp):
        captured = []
        original = rp._run_json
        rp._run_json = lambda cmd: captured.append(cmd) or {"workflow": {}}
        try:
            rp.load_workflow(Path(tmp) / "project", Path(tmp) / "skill")
        finally:
            rp._run_json = original
        return captured[0]

    def test_passes_project_root_to_the_customization_resolver(self):
        with tempfile.TemporaryDirectory() as tmp:
            cmd = self._captured_command(tmp)
            self.assertIn("--project-root", cmd)
            self.assertEqual(cmd[cmd.index("--project-root") + 1], str(Path(tmp) / "project"))

    def test_loads_real_layers_from_explicit_project_root(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            project = root / "project"
            scripts = project / "_bmad" / "scripts"
            scripts.mkdir(parents=True)
            project_scripts = Path(__file__).resolve().parents[5] / "_bmad" / "scripts"
            for filename in ("resolve_customization.py", "config_utils.py"):
                shutil.copy2(project_scripts / filename, scripts / filename)
            skill = root / "skills" / "bmad-party-mode"
            custom = project / "_bmad" / "custom"
            layers = {
                skill / "customize.toml": """
                    [workflow]
                    default_party = "base"
                    party_mode = "session"
                    party_memory = true
                    persistent_facts = ["z-base fact", "shared fact"]
                    [[workflow.party_members]]
                    code = "analyst"
                    name = "Base analyst"
                    persona = "base persona"
                    [[workflow.party_members]]
                    code = "z-base"
                    name = "Base member"
                    [[workflow.party_groups]]
                    id = "shared"
                    name = "Base group"
                    members = ["analyst"]
                    scene = "base scene"
                    [[workflow.party_groups]]
                    id = "z-base"
                    members = ["z-base"]
                """,
                custom / "bmad-party-mode.toml": """
                    [workflow]
                    default_party = "team"
                    party_memory = false
                    persistent_facts = ["a-team fact", "shared fact"]
                    [[workflow.party_members]]
                    code = "analyst"
                    name = "Team analyst"
                    persona = "team persona"
                    [[workflow.party_members]]
                    code = "a-team"
                    name = "Team member"
                    [[workflow.party_groups]]
                    id = "shared"
                    name = "Team group"
                    members = ["a-team"]
                    [[workflow.party_groups]]
                    id = "a-team"
                    members = ["a-team"]
                """,
                custom / "bmad-party-mode.user.toml": """
                    [workflow]
                    default_party = "shared"
                    persistent_facts = ["m-user fact"]
                    [[workflow.party_members]]
                    code = "analyst"
                    name = "User analyst"
                    [[workflow.party_members]]
                    code = "m-user"
                    name = "User member"
                    [[workflow.party_groups]]
                    id = "shared"
                    name = "User group"
                    members = ["analyst", "m-user"]
                    [[workflow.party_groups]]
                    id = "m-user"
                    members = ["m-user"]
                """,
            }
            for path, body in layers.items():
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(dedent(body), encoding="utf-8")
            # A wrong or omitted --project-root would select these ambient overrides.
            decoy = root / "other-project"
            decoy_custom = decoy / "_bmad" / "custom"
            decoy_custom.mkdir(parents=True)
            (decoy_custom / "bmad-party-mode.user.toml").write_text(
                '[workflow]\ndefault_party = "wrong-project"\n', encoding="utf-8")
            with chdir(decoy):
                workflow = rp.load_workflow(project, skill)
            self.assertEqual(workflow, {
                "default_party": "shared",
                "party_mode": "session",
                "party_memory": False,
                "persistent_facts": ["z-base fact", "shared fact", "a-team fact", "shared fact", "m-user fact"],
                "party_members": [
                    {"code": "analyst", "name": "User analyst"},
                    {"code": "z-base", "name": "Base member"},
                    {"code": "a-team", "name": "Team member"},
                    {"code": "m-user", "name": "User member"},
                ],
                "party_groups": [
                    {"id": "shared", "name": "User group", "members": ["analyst", "m-user"]},
                    {"id": "z-base", "members": ["z-base"]},
                    {"id": "a-team", "members": ["a-team"]},
                    {"id": "m-user", "members": ["m-user"]},
                ],
            })


if __name__ == "__main__":
    unittest.main()
