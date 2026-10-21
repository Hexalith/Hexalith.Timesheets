"""Verify installed BMAD persona wrappers and run both persona-suite mirrors."""

import os
from pathlib import Path
import re
import subprocess
import sys
import tomllib
import unittest


ROOT = Path(__file__).resolve().parents[2]
PERSONA_SKILLS = ("bmad-party-mode", "bmad-forge-idea")


class BmadToolingTests(unittest.TestCase):
    def test_copilot_wrappers_cover_installed_personas(self):
        skills = ROOT / ".agents" / "skills"
        personas = set()
        for skill in skills.glob("bmad-*"):
            customization = skill / "customize.toml"
            if not customization.is_file():
                continue
            with customization.open("rb") as stream:
                metadata = tomllib.load(stream)
            if "agent" in metadata:
                self.assertTrue((skill / "SKILL.md").is_file(), skill.name)
                personas.add(skill.name)

        self.assertTrue(personas, "No installed BMAD personas found")
        wrappers = {
            path.name.removesuffix(".agent.md"): path
            for path in (ROOT / ".github" / "agents").glob("bmad-*.agent.md")
        }
        self.assertEqual(personas, set(wrappers))
        for name, wrapper in wrappers.items():
            with self.subTest(persona=name):
                self.assertIn(
                    f"LOAD the FULL {{project-root}}/.agents/skills/{name}/SKILL.md",
                    wrapper.read_text(encoding="utf-8"),
                )

    def test_persona_skill_mirrors_have_identical_files_and_contents(self):
        def contents(directory):
            self.assertTrue(directory.is_dir(), directory)
            return {
                path.relative_to(directory): path.read_bytes()
                for path in directory.rglob("*")
                if path.is_file() and "__pycache__" not in path.parts
                and path.suffix not in (".pyc", ".pyo")
            }

        for skill in PERSONA_SKILLS:
            with self.subTest(skill=skill):
                installed = contents(ROOT / ".agents" / "skills" / skill)
                mirrored = contents(ROOT / ".claude" / "skills" / skill)
                self.assertTrue(installed, f"Installed skill is empty: {skill}")
                self.assertEqual(set(installed), set(mirrored))
                for path, payload in installed.items():
                    self.assertEqual(payload, mirrored[path], f"Mirror differs: {skill}/{path}")

    def assert_persona_suite(self, tree, skill):
        tests = ROOT / tree / "skills" / skill / "scripts" / "tests"
        self.assertTrue(tests.is_dir(), tests)
        result = subprocess.run(
            [sys.executable, "-B", "-m", "unittest", "discover", "-s", str(tests), "-v"],
            capture_output=True,
            text=True,
            encoding="utf-8",
            cwd=ROOT,
            env={**os.environ, "PYTHONDONTWRITEBYTECODE": "1", "PYTHONUTF8": "1"},
            timeout=60,
        )
        output = result.stdout + result.stderr
        self.assertEqual(result.returncode, 0, output)
        summary = re.search(r"(?m)^Ran ([0-9]+) tests? in ", output)
        self.assertIsNotNone(summary, output)
        skipped = re.search(r"(?m)^OK \([^)\n]*\bskipped=([0-9]+)", output)
        skipped_count = int(skipped[1]) if skipped is not None else 0
        self.assertGreater(int(summary[1]), skipped_count, output)

    def test_installed_party_mode_suite(self):
        self.assert_persona_suite(".agents", "bmad-party-mode")

    def test_installed_forge_idea_suite(self):
        self.assert_persona_suite(".agents", "bmad-forge-idea")

    def test_claude_party_mode_suite(self):
        self.assert_persona_suite(".claude", "bmad-party-mode")

    def test_claude_forge_idea_suite(self):
        self.assert_persona_suite(".claude", "bmad-forge-idea")


if __name__ == "__main__":
    unittest.main()
