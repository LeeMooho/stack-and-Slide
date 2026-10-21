"""Tests of validator orchestration with a fake editor, not Unity integration tests."""

from pathlib import Path
import json
import os
import signal
import subprocess
import sys
import tempfile
import time
import unittest
from unittest.mock import patch

import validate_unity


class UnityValidationTests(unittest.TestCase):
    def setUp(self):
        # Each orchestration case gets independent source inputs and an output parent
        # outside a recognizable Git checkout. No installed Unity editor is needed.
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.checkout = self.root / "repository"
        self.checkout.mkdir()
        (self.checkout / ".git").mkdir()
        self.project = self.checkout / "game"
        for name in validate_unity.PROJECT_DIRECTORIES:
            (self.project / name).mkdir(parents=True)
        self.version_file = self.project / "ProjectSettings" / "ProjectVersion.txt"
        self.version_file.write_text("m_EditorVersion: 2019.4.16f1\n")
        (self.project / "Assets" / "Game.cs").write_text("// Original game source\n")
        (self.project / "Packages" / "manifest.json").write_text('{"dependencies": {}}\n')
        (self.project / "Packages" / "packages-lock.json").write_text('{"dependencies": {}}\n')
        self.editor = self.root / "Unity Editor" / "Unity.exe"
        self.editor.parent.mkdir()
        self.editor.write_bytes(b"fake editor - never executed")
        self.output = self.root / "validation"
        self.detected_version = "6000.6.6f1"
        self.import_log_version = self.detected_version
        self.import_exit_code = 0
        self.version_exit_code = 0
        self.extra_log = ""
        self.write_log = True
        self.write_assembly = True
        self.save_version = True
        self.import_count = 0
        self.original = validate_unity._snapshot(self.project)
        # Replace only the editor execution boundary. Copying, hashing, version checks,
        # log parsing, and JSON reporting still execute the production implementation.
        self.mock_run = patch.object(validate_unity, "_run", side_effect=self.fake_editor).start()
        self.addCleanup(patch.stopall)

    def fake_editor(self, command, output, timeout):
        self.assertIsInstance(command, list)
        self.assertEqual(str(self.editor.resolve()), command[0])
        if command[1:] == ["-version"]:
            # Version detection and import are distinct invocations and may fail separately.
            output.write_text(self.detected_version + "\n")
            return subprocess.CompletedProcess(command, self.version_exit_code, self.detected_version + "\n", "")
        self.import_count += 1
        self.assertIn("-batchmode", command)
        self.assertIn("-nographics", command)
        self.assertIn("-accept-apiupdate", command)
        self.assertIn("-quit", command)
        copy = Path(command[command.index("-projectPath") + 1])
        self.assertNotEqual(self.project, copy)
        self.assertFalse((copy / "Library").exists())
        self.assertFalse((copy / ".git").exists())
        # The fake receives pristine copied inputs, never the original project or
        # stale compilation evidence from another run's Library directory.
        self.assertEqual(self.original, validate_unity._snapshot(copy))
        if self.save_version:
            (copy / "ProjectSettings" / "ProjectVersion.txt").write_text(
                "m_EditorVersion: " + self.detected_version + "\n"
            )
        (copy / "Assets" / "Game.cs").write_text("// Fake API updater output\n")
        if self.write_assembly:
            # These bytes test artifact presence only. They are deliberately not a
            # compiled Unity assembly and must never be reported as integration evidence.
            assembly = copy / "Library" / "ScriptAssemblies" / "Assembly-CSharp.dll"
            assembly.parent.mkdir(parents=True)
            assembly.write_bytes(b"fake assembly evidence for orchestration tests")
        if self.write_log:
            log = Path(command[command.index("-logFile") + 1])
            log.write_text("Initialize engine version: " + self.import_log_version + " (revision)\n" + self.extra_log)
        output.write_text("")
        return subprocess.CompletedProcess(command, self.import_exit_code, "", "")

    def validate(self, **kwargs):
        return validate_unity.validate_project(self.editor, self.project, self.output, **kwargs)

    def assertFailed(self, report, message):
        # Shared failure assertions ensure a rejected run cannot retain conversion
        # success flags or silently modify the original versioned project inputs.
        self.assertEqual("failed", report["status"])
        self.assertNotEqual(0, report["exit_code"])
        self.assertFalse(report["project_converted_in_copy"])
        self.assertFalse(report["upgrade_applied_to_checkout"])
        self.assertIn(message, report["error"])
        self.assertTrue(report["source_unchanged"])
        self.assertEqual(self.original, validate_unity._snapshot(self.project))

    def test_migration_stays_in_copy_and_reports_actual_detected_version(self):
        # A valid import report may describe converted copy files while still making
        # no claim about applying an upgrade, entering Play mode, or building Android.
        report = self.validate(expected_version="6000.6.6f1")
        self.assertEqual("validated_import", report["status"])
        self.assertEqual(0, report["exit_code"])
        self.assertEqual("6000.6.6f1", report["editor_version"])
        self.assertEqual("final", report["editor_release_channel"])
        self.assertFalse(report["editor_is_preview"])
        self.assertTrue(report["project_converted_in_copy"])
        self.assertFalse(report["upgrade_applied_to_checkout"])
        self.assertFalse(report["play_mode_validated"])
        self.assertFalse(report["android_build_validated"])
        self.assertEqual(self.original, validate_unity._snapshot(self.project))
        self.assertEqual(["Assets/Game.cs", "ProjectSettings/ProjectVersion.txt"], report["migration_changes"]["modified"])
        saved = json.loads((Path(report["validation_directory"]) / "report.json").read_text())
        self.assertEqual(report, saved)

    def test_preview_editors_are_identified_without_calling_them_lts(self):
        # Release-channel evidence comes from the executable's version, not a marketing
        # name or the fact that the import returned success in this fake scenario.
        self.detected_version = "6000.7.0b5"
        self.import_log_version = self.detected_version
        report = self.validate()
        self.assertEqual("validated_import", report["status"])
        self.assertTrue(report["editor_is_preview"])
        self.assertEqual("beta", report["editor_release_channel"])

    def test_same_version_import_is_not_reported_as_an_upgrade(self):
        # Clean reimport and cross-version file conversion are different report outcomes.
        self.detected_version = self.import_log_version = "2019.4.16f1"
        report = self.validate()
        self.assertEqual("validated_import", report["status"])
        self.assertFalse(report["project_converted_in_copy"])

    def test_output_inside_checkout_is_rejected_before_running_editor(self):
        # A bad output boundary is rejected before any external process can touch it.
        with self.assertRaisesRegex(validate_unity.ValidationError, "outside the source checkout"):
            validate_unity.validate_project(self.editor, self.project, self.checkout / "validation")
        self.mock_run.assert_not_called()

    def test_source_symlinks_are_rejected(self):
        # Copy isolation cannot rely on following asset links to unspecified locations.
        # Accounts without symlink privileges skip this platform-specific fixture only.
        link = self.project / "Assets" / "external.cs"
        try:
            link.symlink_to(self.project / "Assets" / "Game.cs")
        except OSError:
            self.skipTest("Creating symlinks is not supported for this account.")
        with self.assertRaisesRegex(validate_unity.ValidationError, "symlink"):
            self.validate()
        self.mock_run.assert_not_called()

    def test_expected_version_mismatch_stops_before_import(self):
        # Pinning a version is an assertion, not permission to label another editor
        # as the requested version or to continue with a mismatched migration.
        report = self.validate(expected_version="6000.6.1f1")
        self.assertFailed(report, "Expected editor")
        self.assertEqual(0, self.import_count)

    def test_older_editor_is_not_used_or_reported_as_an_upgrade(self):
        # The downgrade check runs before the import invocation, not merely when
        # describing the result after incompatible project files have been created.
        self.detected_version = "2019.4.15f1"
        report = self.validate()
        self.assertFailed(report, "refusing a downgrade")
        self.assertEqual(0, self.import_count)

    def test_version_command_failure_preserves_exit_code(self):
        # Keep the editor's nonzero result visible to callers and avoid import entirely.
        self.version_exit_code = 7
        report = self.validate()
        self.assertFailed(report, "Unity -version failed")
        self.assertEqual(7, report["exit_code"])
        self.assertEqual(7, report["version_exit_code"])
        self.assertEqual(0, self.import_count)

    def test_ambiguous_version_output_is_not_accepted(self):
        # Picking one plausible version from mixed output would manufacture evidence.
        self.detected_version = "6000.6.6f1\n2019.4.16f1"
        report = self.validate()
        self.assertFailed(report, "exactly one editor version")
        self.assertEqual(0, self.import_count)

    def test_import_failure_preserves_editor_exit_code(self):
        # An explicit editor failure takes precedence over the fake's otherwise valid
        # version, artifact, and log evidence; it must not become a generic success.
        self.import_exit_code = 23
        report = self.validate()
        self.assertFailed(report, "Unity import failed")
        self.assertEqual(23, report["exit_code"])
        self.assertEqual(23, report["import_exit_code"])

    def test_compiler_error_overrides_zero_process_exit(self):
        # Regression: a process can return zero while its logs still contain a C# error.
        self.extra_log = "Assets/Game.cs(1,2): error CS0246: Missing component\n"
        report = self.validate()
        self.assertFailed(report, "compiler/license/package errors")
        self.assertEqual("compilation", report["diagnostics"][0]["category"])

    def test_license_error_overrides_zero_process_exit(self):
        # A nominal exit code is not evidence that an editor license was usable.
        self.extra_log = "No valid Unity Editor license found.\n"
        report = self.validate()
        self.assertFailed(report, "compiler/license/package errors")
        self.assertEqual("licensing", report["diagnostics"][0]["category"])

    def test_package_resolution_failure_is_reported(self):
        # Packages remain required even if another part of the fake import produced a DLL.
        self.extra_log = "An error occurred while resolving packages:\n"
        report = self.validate()
        self.assertFailed(report, "compiler/license/package errors")
        self.assertEqual("packages", report["diagnostics"][0]["category"])

    def test_missing_import_log_prevents_success(self):
        # Successful version output cannot stand in for a missing current-run import log.
        self.write_log = False
        self.assertFailed(self.validate(), "without writing its import log")

    def test_import_version_must_match_version_command(self):
        # Cross-check independent evidence instead of trusting either version string alone.
        self.import_log_version = "6000.6.5f1"
        self.assertFailed(self.validate(), "does not confirm the detected editor version")

    def test_migrated_project_version_must_be_saved_by_editor(self):
        # Merely starting a newer editor does not prove that it converted project metadata.
        self.save_version = False
        self.assertFailed(self.validate(), "did not save a ProjectVersion")

    def test_missing_compiled_gameplay_assembly_prevents_success(self):
        # Version changes and a benign log are insufficient without new gameplay output.
        self.write_assembly = False
        self.assertFailed(self.validate(), "did not produce the gameplay Assembly-CSharp.dll")

    def test_timeout_is_a_failure_with_no_upgrade_claim(self):
        # This fake checks report/exit-code semantics. Actual process-tree termination
        # is exercised separately below so a mocked timeout is not mistaken for coverage.
        def timeout(command, output, limit):
            output.write_text("Editor starting\nStill waiting\n")
            raise subprocess.TimeoutExpired(command, limit)

        self.mock_run.side_effect = timeout
        report = self.validate(timeout=1)
        self.assertFailed(report, "timed out")
        self.assertEqual(124, report["exit_code"])
        output = Path(report["validation_directory"]) / "version-output.log"
        self.assertEqual("Editor starting\nStill waiting\n", output.read_text())

    def test_source_change_during_import_invalidates_success(self):
        # Simulate an external edit after the otherwise valid copy import. The final
        # source hashes must veto success without rolling back the concurrent change.
        def changed_source(command, output, timeout):
            result = self.fake_editor(command, output, timeout)
            if "-projectPath" in command:
                (self.project / "Assets" / "Game.cs").write_text("// Concurrent source change\n")
            return result

        self.mock_run.side_effect = changed_source
        report = self.validate()
        self.assertEqual("failed", report["status"])
        self.assertEqual(1, report["exit_code"])
        self.assertFalse(report["source_unchanged"])
        self.assertFalse(report["project_converted_in_copy"])
        self.assertIn("Source files changed", report["error"])


class ProcessCleanupTests(unittest.TestCase):
    # These tests launch ordinary Python processes rather than a fake or real Unity
    # executable, so they verify OS process cleanup without claiming engine readiness.
    def process_running(self, pid):
        if os.name == "nt":
            import ctypes
            from ctypes import wintypes

            api = ctypes.WinDLL("kernel32", use_last_error=True)
            api.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
            api.OpenProcess.restype = wintypes.HANDLE
            api.GetExitCodeProcess.argtypes = [wintypes.HANDLE, ctypes.POINTER(wintypes.DWORD)]
            api.GetExitCodeProcess.restype = wintypes.BOOL
            api.CloseHandle.argtypes = [wintypes.HANDLE]
            # Query only the PID written by this test's own process. STILL_ACTIVE (259)
            # distinguishes a running child from an already-terminated process handle.
            handle = api.OpenProcess(0x1000, False, pid)  # PROCESS_QUERY_LIMITED_INFORMATION
            if not handle:
                return False
            try:
                code = wintypes.DWORD()
                return bool(api.GetExitCodeProcess(handle, ctypes.byref(code)) and code.value == 259)
            finally:
                api.CloseHandle(handle)
        try:
            os.kill(pid, 0)
        except ProcessLookupError:
            return False
        stat = Path("/proc") / str(pid) / "stat"
        if stat.is_file():
            # Container PID 1 may leave killed orphan children as zombies until reaped.
            return stat.read_text().rsplit(")", 1)[1].split()[0] != "Z"
        return True

    def test_timeout_stops_parent_and_inherited_pipe_child_and_preserves_output(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            pid_file = root / "pids.json"
            script = root / "parent.py"
            # The sleeping child inherits the parent's output pipes. Killing just the
            # parent can leave those pipes open and defeat an otherwise correct timeout.
            script.write_text(
                "import json, os, subprocess, sys, time\n"
                "child = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(60)'])\n"
                "with open(sys.argv[1], 'w') as output: json.dump([os.getpid(), child.pid], output)\n"
                "print('parent and child started', flush=True)\n"
                "print('stderr captured', file=sys.stderr, flush=True)\n"
                "time.sleep(60)\n"
            )
            pids = []
            started = time.monotonic()
            try:
                with self.assertRaises(subprocess.TimeoutExpired):
                    validate_unity._run([sys.executable, str(script), str(pid_file)], root / "output.log", timeout=2)
                self.assertLess(time.monotonic() - started, 15)
                self.assertTrue(pid_file.is_file(), "Child process did not start before the timeout.")
                pids = json.loads(pid_file.read_text())
                for pid in pids:
                    # OS termination and handle cleanup are asynchronous; wait briefly
                    # for only these known PIDs rather than matching other Python tasks.
                    for _ in range(20):
                        if not self.process_running(pid):
                            break
                        time.sleep(0.05)
                    self.assertFalse(self.process_running(pid), "Launched process survived timeout: " + str(pid))
                log = (root / "output.log").read_text()
                self.assertIn("parent and child started", log)
                self.assertIn("stderr captured", log)
            finally:
                # A failing test must not leave its own sleeping child behind.
                if not pids and pid_file.is_file():
                    pids = json.loads(pid_file.read_text())
                for pid in pids:
                    if self.process_running(pid):
                        if os.name == "nt":
                            subprocess.run(["taskkill", "/PID", str(pid), "/T", "/F"], capture_output=True, check=False)
                        else:
                            os.kill(pid, signal.SIGKILL)


if __name__ == "__main__":
    unittest.main()
