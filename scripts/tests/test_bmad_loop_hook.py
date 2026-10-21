"""Exercise hook stdin decoding through real subprocesses and isolated event files."""

import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


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

    def invoke(self, payload, decoder="utf-8:strict"):
        env = {**self.env, "PYTHONIOENCODING": decoder}
        result = subprocess.run(
            [sys.executable, "-B", str(HOOK), "Stop"],
            input=payload,
            capture_output=True,
            env=env,
            cwd=self.root,
            timeout=10,
        )
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(result.stdout, b"")
        self.assertEqual(result.stderr, b"")

    def assert_event(self, session_id=None, transcript_path=None, cwd=None):
        files = list(self.events.iterdir())
        self.assertEqual(len(files), 1, files)
        event_file = files[0]
        event = json.loads(event_file.read_text(encoding="utf-8"))
        self.assertIsInstance(event["ts"], int)
        self.assertGreater(event["ts"], 0)
        self.assertEqual(
            event_file.name,
            f'{event["ts"]}-input-regression-Stop.json',
        )
        self.assertEqual(event, {
            "ts": event["ts"],
            "event": "Stop",
            "task_id": "input-regression",
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


if __name__ == "__main__":
    unittest.main()
