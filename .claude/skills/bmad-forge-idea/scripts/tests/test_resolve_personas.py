#!/usr/bin/env python3
# /// script
# requires-python = ">=3.11"
# ///
"""Unit tests for resolve_personas.py — pool merge, alias, party resolution."""

import shutil
import sys
import tempfile
import unittest
from contextlib import chdir
from pathlib import Path
from textwrap import dedent

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
import resolve_personas as rp  # noqa: E402

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


class TestBuildPool(unittest.TestCase):
    def test_installed_become_default_room_indexed_every_way(self):
        pool, idx, installed, custom = rp.build_pool(AGENTS, [])
        self.assertEqual(installed, ["bmad-agent-analyst", "bmad-agent-pm"])
        self.assertEqual(custom, [])
        self.assertEqual(idx["analyst"], "bmad-agent-analyst")   # alias
        self.assertEqual(idx["mary"], "bmad-agent-analyst")      # name (ci)
        self.assertEqual(pool["bmad-agent-analyst"]["source"], "installed")

    def test_pure_custom_member_stays_out_of_default_room(self):
        pool, _, installed, custom = rp.build_pool(
            AGENTS, [{"code": "morpheus", "name": "Morpheus", "persona": "riddles"}])
        self.assertEqual(custom, ["morpheus"])
        self.assertNotIn("morpheus", installed)
        self.assertEqual(pool["morpheus"]["persona"], "riddles")

    def test_custom_override_preserves_omitted_installed_fields(self):
        agents = {code: dict(info) for code, info in AGENTS.items()}
        agents["bmad-agent-analyst"]["description"] = "Finds the evidence."
        for token in ("analyst", "AnAlYsT", "bmad-agent-analyst", "Mary", "mArY"):
            with self.subTest(code=token):
                override = {
                    "code": token, "name": "Mary-Custom", "persona": "p",
                    "capabilities": ["critique"], "model": "custom-model",
                }
                pool, index, installed, custom = rp.build_pool(agents, [override])
                self.assertNotIn("analyst", pool)
                self.assertEqual(index["mary-custom"], "bmad-agent-analyst")
                self.assertEqual(installed, ["bmad-agent-analyst", "bmad-agent-pm"])
                self.assertEqual(custom, [])
                self.assertEqual(pool["bmad-agent-analyst"], {
                    "code": "bmad-agent-analyst", "name": "Mary-Custom",
                    "icon": "📊", "title": "Analyst", "description": "Finds the evidence.",
                    "source": "custom", "persona": "p",
                    "capabilities": ["critique"], "model": "custom-model",
                })
                self.assertEqual(agents["bmad-agent-analyst"]["name"], "Mary")

    def test_custom_override_preserves_installed_name_when_omitted(self):
        pool, *_ = rp.build_pool(AGENTS, [{"code": "analyst", "persona": "p"}])
        self.assertEqual(pool["bmad-agent-analyst"]["name"], "Mary")

    def test_custom_override_can_clear_fields(self):
        overrides = [
            {"code": "analyst", "persona": "p", "capabilities": ["critique"], "model": "custom-model"},
            {"code": "analyst", "name": "", "icon": "", "title": "",
             "persona": "", "capabilities": [], "model": ""},
        ]
        pool, *_ = rp.build_pool(AGENTS, overrides)
        for field in ("name", "icon", "title", "persona", "model"):
            with self.subTest(field=field):
                self.assertEqual(pool["bmad-agent-analyst"][field], "")
        self.assertEqual(pool["bmad-agent-analyst"]["capabilities"], [])

    def test_member_without_code_skipped(self):
        pool, _, _, custom = rp.build_pool(AGENTS, [{"name": "Nameless"}])
        self.assertEqual(custom, [])
        self.assertEqual(set(pool), {"bmad-agent-analyst", "bmad-agent-pm"})

    def test_custom_rename_does_not_hijack_another_agents_name(self):
        # Override the analyst slot, renaming it to "John" — the PM's name.
        # The PM's name lookup must survive (last-writer-wins would corrupt it).
        _, idx, _, _ = rp.build_pool(AGENTS, [{"code": "analyst", "name": "John"}])
        self.assertEqual(idx["john"], "bmad-agent-pm")

    def test_brief_carries_model_and_capabilities(self):
        pool, _, _, _ = rp.build_pool(
            AGENTS, [{"code": "neo", "name": "Neo", "model": "opus", "capabilities": ["x"]}])
        brief = rp._brief(pool["neo"])
        self.assertEqual(brief["model"], "opus")
        self.assertEqual(brief["capabilities"], ["x"])

    def test_non_list_party_members_is_safe(self):
        pool, _, installed, custom = rp.build_pool(AGENTS, "not-a-list")
        self.assertEqual(custom, [])
        self.assertEqual(set(pool), {"bmad-agent-analyst", "bmad-agent-pm"})


class TestResolveParties(unittest.TestCase):
    def setUp(self):
        self.pool, self.idx, _, _ = rp.build_pool(
            AGENTS, [{"code": "shark", "name": "Marcus", "title": "CFO"}])

    def test_resolves_members_by_alias_and_custom_code(self):
        parties = rp.resolve_parties(
            [{"id": "tank", "name": "Tank", "scene": "hostile",
              "members": ["shark", "analyst"]}], self.pool, self.idx)
        self.assertEqual(len(parties), 1)
        self.assertEqual([m["name"] for m in parties[0]["members"]], ["Marcus", "Mary"])
        self.assertEqual(parties[0]["scene"], "hostile")

    def test_unknown_member_dropped_silently(self):
        parties = rp.resolve_parties(
            [{"id": "g", "members": ["analyst", "ghost"]}], self.pool, self.idx)
        self.assertEqual([m["name"] for m in parties[0]["members"]], ["Mary"])

    def test_member_resolution_is_case_insensitive(self):
        # A TOML author naturally writes "Analyst"/"Shark"; the filter accepts
        # them via the lowercase index, so resolution must too (no KeyError).
        parties = rp.resolve_parties(
            [{"id": "g", "members": ["Analyst", "Shark"]}], self.pool, self.idx)
        self.assertEqual([m["name"] for m in parties[0]["members"]], ["Mary", "Marcus"])

    def test_non_string_member_does_not_crash(self):
        # Malformed members (int, list) must drop silently, never raise.
        parties = rp.resolve_parties(
            [{"id": "g", "members": [123, ["x"], "analyst"]}], self.pool, self.idx)
        self.assertEqual([m["name"] for m in parties[0]["members"]], ["Mary"])

    def test_open_cast_group_flagged(self):
        parties = rp.resolve_parties(
            [{"id": "rebels", "name": "Rebels", "scene": "the Ghost"}], self.pool, self.idx)
        self.assertTrue(parties[0]["open_cast"])
        self.assertEqual(parties[0]["members"], [])

    def test_group_without_id_skipped(self):
        self.assertEqual(rp.resolve_parties([{"name": "no id"}], self.pool, self.idx), [])


class TestOverrideMergeFallback(unittest.TestCase):
    """When party-mode isn't installed, user override TOMLs are read directly."""

    def test_arrays_append_scalars_override(self):
        import tempfile, os
        with tempfile.TemporaryDirectory() as d:
            custom = Path(d) / "_bmad" / "custom"
            custom.mkdir(parents=True)
            (custom / "bmad-party-mode.toml").write_text(
                '[workflow]\ndefault_party = "a"\n'
                '[[workflow.party_members]]\ncode = "x"\nname = "X"\n')
            (custom / "bmad-party-mode.user.toml").write_text(
                '[workflow]\ndefault_party = "b"\n'
                '[[workflow.party_members]]\ncode = "y"\nname = "Y"\n')
            wf = rp.load_party_overrides(Path(d))
            self.assertEqual(wf["default_party"], "b")  # personal wins
            self.assertEqual([m["code"] for m in wf["party_members"]], ["x", "y"])  # appended


class TestResolverInvocation(unittest.TestCase):
    """The wrapper knows the project root, so it must not let the resolver
    infer one from the working directory (#2796)."""

    def _captured_command(self, tmp):
        captured = []
        original = rp._run_json
        rp._run_json = lambda cmd: captured.append(cmd) or {"workflow": {}}
        try:
            rp.load_party_workflow(Path(tmp) / "project", Path(tmp) / "skill")
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
                workflow = rp.load_party_workflow(project, skill)
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
