"""Exercise hook input and event-file confinement in isolated directories."""

import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch


HOOK = Path(__file__).resolve().parents[2] / ".bmad-loop" / "bmad_loop_hook.py"


class BmadLoopHookInputTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        # Never relay a test signal into an actual orchestrator's control plane.
        self.env = {
            key: value for key, value in os.environ.items()
            if not key.startswith("BMAD_LOOP_")
        }
        self.env.update(
            BMAD_LOOP_RUN_DIR=str(self.root / "run"),
            BMAD_LOOP_TASK_ID="input-regression",
        )
        self.events = self.root / "run" / "events"

    def invoke(self, payload, decoder="utf-8:strict", event_name="Stop"):
        env = {**self.env, "PYTHONIOENCODING": decoder}
        result = subprocess.run(
            [sys.executable, "-B", str(HOOK), event_name],
            input=payload,
            capture_output=True,
            env=env,
            cwd=self.root,
            timeout=10,
        )
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(result.stdout, b"")
        self.assertEqual(result.stderr, b"")

    def assert_event(self, session_id=None, transcript_path=None, cwd=None, event_name="Stop"):
        files = list(self.events.iterdir())
        self.assertEqual(len(files), 1, files)
        event_file = files[0]
        event = json.loads(event_file.read_text(encoding="utf-8"))
        self.assertIsInstance(event["ts"], int)
        self.assertGreater(event["ts"], 0)
        self.assertEqual(
            event_file.name,
            f'{event["ts"]}-{self.env["BMAD_LOOP_TASK_ID"]}-{event_name}.json',
        )
        self.assertEqual(event, {
            "ts": event["ts"],
            "event": event_name,
            "task_id": self.env["BMAD_LOOP_TASK_ID"],
            "session_id": session_id,
            "transcript_path": transcript_path,
            "cwd": cwd,
        })
        # Each subcase starts with an empty event directory; a leftover .tmp
        # also fails the one-file assertion above.
        event_file.unlink()

    def test_strict_invalid_utf8_still_emits_event_without_payload_metadata(self):
        for payload in (
            b"\xff",
            b'{"session_id":"bad\xff","cwd":"workspace"}',
            b'{"session_id":"otherwise-valid"}\xff',
        ):
            with self.subTest(payload=payload):
                self.invoke(payload)
                self.assert_event()

    def test_surrogateescape_invalid_json_still_emits_event(self):
        for payload in (b"\xff", b'{"session_id":"otherwise-valid"}\xff'):
            with self.subTest(payload=payload):
                self.invoke(payload, "utf-8:surrogateescape")
                self.assert_event()

    def test_surrogateescape_byte_in_json_string_preserves_existing_metadata(self):
        self.invoke(
            b'{"session_id":"bad\xff","transcript_path":"transcript.jsonl","cwd":"workspace"}',
            "utf-8:surrogateescape",
        )
        self.assert_event("bad\udcff", "transcript.jsonl", "workspace")

    def test_malformed_json_empty_input_and_non_objects_emit_empty_metadata(self):
        for payload in (b"", b"{", b"null", b"[]", b'"text"', b"42"):
            with self.subTest(payload=payload):
                self.invoke(payload)
                self.assert_event()

    def test_valid_utf8_metadata_survives_both_decoders(self):
        metadata = {
            "session_id": "s\u00e9ance-\u03b1",
            "transcript_path": "r\u00e9sum\u00e9.jsonl",
            "cwd": "espace-\u03b2",
        }
        payload = json.dumps(metadata, ensure_ascii=False).encode("utf-8")
        for decoder in ("utf-8:strict", "utf-8:surrogateescape"):
            with self.subTest(decoder=decoder):
                self.invoke(payload, decoder)
                self.assert_event(**metadata)

    def test_inactive_sessions_ignore_malformed_bytes_without_writing_events(self):
        for variable in ("BMAD_LOOP_RUN_DIR", "BMAD_LOOP_TASK_ID"):
            for value in (None, ""):
                with self.subTest(variable=variable, value=value):
                    original = self.env.pop(variable)
                    if value is not None:
                        self.env[variable] = value
                    try:
                        self.invoke(b"\xff")
                        self.assertEqual(list(self.root.iterdir()), [])
                    finally:
                        self.env[variable] = original

    def test_inactive_sessions_exit_before_stdin_eof(self):
        for variable in ("BMAD_LOOP_RUN_DIR", "BMAD_LOOP_TASK_ID"):
            for value in (None, ""):
                with self.subTest(variable=variable, value=value):
                    env = {**self.env, "PYTHONIOENCODING": "utf-8:strict"}
                    if value is None:
                        env.pop(variable)
                    else:
                        env[variable] = value
                    with subprocess.Popen(
                        [sys.executable, "-B", str(HOOK), "Stop"],
                        stdin=subprocess.PIPE,
                        stdout=subprocess.PIPE,
                        stderr=subprocess.PIPE,
                        env=env,
                        cwd=self.root,
                    ) as process:
                        try:
                            # Keep stdin open: an inactive hook must not read it.
                            process.wait(timeout=10)
                        finally:
                            if process.poll() is None:
                                process.kill()
                            stdout, stderr = process.communicate(timeout=10)
                        self.assertEqual(process.returncode, 0, stderr)
                        self.assertEqual(stdout, b"")
                        self.assertEqual(stderr, b"")
                    self.assertEqual(list(self.root.iterdir()), [])

    def test_malformed_input_uses_configured_events_directory(self):
        self.events = self.root / "control-plane" / "events"
        self.env["BMAD_LOOP_EVENTS_DIR"] = str(self.events)
        for decoder in ("utf-8:strict", "utf-8:surrogateescape"):
            with self.subTest(decoder=decoder):
                self.invoke(b"\xff", decoder)
                self.assert_event()
                self.assertFalse((self.root / "run").exists())

    def test_unsafe_task_and_event_names_are_quiet_no_ops(self):
        for explicit_directory in (False, True):
            if explicit_directory:
                self.env["BMAD_LOOP_EVENTS_DIR"] = str(self.root / "control-plane" / "events")
            for value in ("task/../../escaped", r"task\..\..\escaped", "C:relative", "signal:stream"):
                for field in ("task", "event"):
                    with self.subTest(explicit_directory=explicit_directory, value=value, field=field):
                        self.env["BMAD_LOOP_TASK_ID"] = value if field == "task" else "input-regression"
                        self.invoke(b"{}", event_name=value if field == "event" else "Stop")
                        self.assertEqual(list(self.root.iterdir()), [])

    def test_normal_identifiers_and_canonical_events_preserve_filename_and_payload(self):
        self.env["BMAD_LOOP_TASK_ID"] = "task..-under_score-s\u00e9ance"
        for event_name in ("SessionStart", "Stop", "Unknown"):
            with self.subTest(event_name=event_name):
                self.invoke(b'{"session_id":"original"}', event_name=event_name)
                self.assert_event(session_id="original", event_name=event_name)


class BmadLoopHookFilenameTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.events = self.root / "events"
        spec = importlib.util.spec_from_file_location("hook_filename_tests", HOOK)
        self.hook = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.hook)

    def write_event(self, name, event, fallback):
        if not fallback and not (
            getattr(os, "O_NOFOLLOW", 0)
            and getattr(os, "O_DIRECTORY", 0)
            and {os.open, os.rename} <= os.supports_dir_fd
        ):
            self.skipTest("Directory-descriptor event writer is unavailable on this platform")
        supported = set() if fallback else os.supports_dir_fd
        with patch.object(self.hook.os, "supports_dir_fd", supported):
            self.hook._write_event(str(self.events), name, event)

    def test_unsafe_names_are_rejected_before_filesystem_access(self):
        for name in (
            "", ".", "..", "../escaped.json", "/escaped.json", "nested/event.json",
            r"..\escaped.json", r"\escaped.json", r"nested\event.json",
            r"C:\escaped.json", "C:escaped.json", r"\\server\share\event.json",
            "event.json:stream", "event\x00.json",
        ):
            with self.subTest(name=name):
                with patch.object(self.hook, "_is_link_like") as filesystem_probe:
                    with self.assertRaises(OSError):
                        self.hook._write_event(str(self.events), name, {})
                    filesystem_probe.assert_not_called()
                self.assertEqual(list(self.root.iterdir()), [])

    def test_traversal_cannot_overwrite_outside_file_on_either_writer(self):
        outside = self.root / "escaped-Stop.json"
        outside.write_text("unchanged", encoding="utf-8")
        # A separator alone would only fail with ENOENT. Supply the intermediate
        # directory so removing the guard demonstrates an actual outside write.
        for prefix in ("123-task", "123-input-regression-Stop"):
            (self.events / prefix).mkdir(parents=True)
        for fallback in (False, True):
            for name in (
                "123-task/../../escaped-Stop.json",
                "123-input-regression-Stop/../../escaped-Stop.json",
            ):
                with self.subTest(fallback=fallback, name=name):
                    with self.assertRaises(OSError):
                        self.write_event(name, {"event": "Stop"}, fallback)
                    self.assertEqual(outside.read_text(encoding="utf-8"), "unchanged")
                    self.assertEqual(set(self.events.iterdir()), {
                        self.events / "123-task", self.events / "123-input-regression-Stop",
                    })
                    self.assertEqual(list(self.events.rglob("*.tmp")), [])

    def test_normal_event_is_complete_without_temporary_files_on_either_writer(self):
        name = "123-task..-under_score-s\u00e9ance-Stop.json"
        event = {"ts": 123, "task_id": "task..-under_score-s\u00e9ance", "event": "Stop"}
        for fallback in (False, True):
            with self.subTest(fallback=fallback):
                self.write_event(name, event, fallback)
                self.assertEqual(list(self.events.iterdir()), [self.events / name])
                self.assertEqual(json.loads((self.events / name).read_text(encoding="utf-8")), event)
                if os.name == "posix":
                    self.assertEqual((self.events / name).stat().st_mode & 0o077, 0)
                (self.events / name).unlink()

    def test_redirected_event_directory_does_not_write_to_target(self):
        target = self.root / "outside"
        target.mkdir()
        sentinel = target / "sentinel"
        sentinel.write_text("unchanged", encoding="utf-8")
        try:
            self.events.symlink_to(target, target_is_directory=True)
        except (OSError, NotImplementedError) as error:
            self.skipTest(f"Directory symlinks are unavailable: {error}")
        for fallback in (False, True):
            with self.subTest(fallback=fallback):
                with self.assertRaises(OSError):
                    self.write_event("123-task-Stop.json", {"event": "Stop"}, fallback)
                self.assertEqual(list(target.iterdir()), [sentinel])
                self.assertEqual(sentinel.read_text(encoding="utf-8"), "unchanged")

    def test_existing_temporary_file_is_preserved_on_either_writer(self):
        self.events.mkdir()
        name = "123-task-Stop.json"
        sentinel = self.events / (name + ".tmp")
        sentinel.write_text("unchanged", encoding="utf-8")
        for fallback in (False, True):
            with self.subTest(fallback=fallback):
                with self.assertRaises(OSError):
                    self.write_event(name, {"event": "Stop"}, fallback)
                self.assertEqual(list(self.events.iterdir()), [sentinel])
                self.assertEqual(sentinel.read_text(encoding="utf-8"), "unchanged")


if __name__ == "__main__":
    unittest.main()
