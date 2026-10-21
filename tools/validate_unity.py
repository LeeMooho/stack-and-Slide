#!/usr/bin/env python3
"""Run a real Unity import in an isolated copy without upgrading the checkout."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import signal
import subprocess
import sys
import tempfile
from typing import Any


# Copy only project input directories, including current local edits. A previous Library cache must
# never provide the DLL or import log used as evidence for a new validation run.
PROJECT_DIRECTORIES = ("Assets", "Packages", "ProjectSettings")
VERSION_PATTERN = r"\d{4}\.\d+\.\d+[abfp]\d+"
LOG_VERSION = re.compile(
    rf"(?:Unity Editor version|Initialize engine version)\s*:\s*({VERSION_PATTERN})",
    re.IGNORECASE,
)
# A zero editor exit code alone is insufficient: Unity can still record actionable
# compiler, package, or license failures in its logs. Retain categories for the report.
ERROR_PATTERNS = {
    "compilation": re.compile(
        r"\berror CS\d+\b|scripts have compiler errors|compilation failed|shader error in",
        re.IGNORECASE,
    ),
    "licensing": re.compile(
        r"no valid (?:Unity Editor )?license|failed to (?:activate|update) (?:the )?license"
        r"|license activation failed|license is not active",
        re.IGNORECASE,
    ),
    "packages": re.compile(
        r"an error occurred while resolving packages|failed to resolve packages",
        re.IGNORECASE,
    ),
    "import": re.compile(
        r"aborting batchmode due to failure|\b\w*Exception:|fatal error",
        re.IGNORECASE,
    ),
}


class ValidationError(Exception):
    """An incomplete or failed editor validation."""


def _inside(path: Path, parent: Path) -> bool:
    try:
        path.relative_to(parent)
        return True
    except ValueError:
        return False


def _checkout_root(project: Path) -> Path:
    # .git may be a directory or a file; either identifies the boundary that must
    # remain free of generated validation projects and automatic API conversions.
    for path in (project, *project.parents):
        if (path / ".git").exists():
            return path
    return project


def _source_version(project: Path) -> str:
    # The checked-in version is the original project metadata, not evidence that
    # this machine has that editor installed or that its gameplay has been tested.
    text = (project / "ProjectSettings" / "ProjectVersion.txt").read_text(encoding="utf-8-sig")
    match = re.search(rf"^m_EditorVersion:\s*({VERSION_PATTERN})\s*$", text, re.MULTILINE)
    if not match:
        raise ValidationError("ProjectVersion.txt does not contain a recognizable editor version.")
    return match.group(1)


def _version_key(version: str) -> tuple[int, int, int, int, int]:
    match = re.fullmatch(r"(\d{4})\.(\d+)\.(\d+)([abfp])(\d+)", version)
    major, minor, patch, channel, build = match.groups()
    # Alpha/beta/final/patch ordering supports downgrade checks, not LTS inference.
    return int(major), int(minor), int(patch), "abfp".index(channel), int(build)


def _snapshot(project: Path) -> dict[str, str]:
    # Relative paths plus content hashes detect additions, removals, and edits.
    # Modification times would also change during harmless copying or importing.
    result = {}
    for name in PROJECT_DIRECTORIES:
        directory = project / name
        if not directory.is_dir() or directory.is_symlink():
            raise ValidationError(f"Required source directory must be a real directory: {directory}")
        for path in sorted(directory.rglob("*")):
            if path.is_symlink():
                # A linked asset could read outside the intended copy boundary.
                raise ValidationError(f"Refusing a symlink that could escape the validation copy: {path}")
            if path.is_file():
                result[path.relative_to(project).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
    return result


def _changes(before: dict[str, str], after: dict[str, str]) -> dict[str, list[str]]:
    return {
        "added": sorted(after.keys() - before.keys()),
        "removed": sorted(before.keys() - after.keys()),
        "modified": sorted(name for name in before.keys() & after.keys() if before[name] != after[name]),
    }


def _stop_process_tree(process: subprocess.Popen) -> None:
    if os.name == "nt":
        # Only target the still-running process launched here. Never search by editor name.
        if process.poll() is None:
            try:
                subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"],
                               capture_output=True, timeout=10, check=False)
            except (OSError, subprocess.TimeoutExpired):
                pass
    else:
        try:
            # _run created this group in a new session, so it excludes other Unity
            # sessions the developer may already have open on the same machine.
            os.killpg(process.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
    if process.poll() is None:
        # Fallback/reaping concerns only the original Popen handle. Detached children
        # can escape OS tree/group tracking; the report does not claim they all exited.
        process.kill()


def _text(value: bytes | str | None) -> str:
    # TimeoutExpired may contain bytes even when Popen was configured in text mode.
    return value.decode("utf-8", errors="replace") if isinstance(value, bytes) else value or ""


def _run(command: list[str], output: Path, timeout: int) -> subprocess.CompletedProcess:
    # Keep native output in files rather than echoing possible license/authentication details.
    # New groups establish ownership for timeout cleanup without matching process names.
    options = {"creationflags": subprocess.CREATE_NEW_PROCESS_GROUP} if os.name == "nt" else {"start_new_session": True}
    process = None
    try:
        # Argument lists preserve executable/project paths with spaces on Windows
        # without shell expansion. Capture both streams for later error classification.
        process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                   text=True, errors="replace", **options)
        try:
            stdout, stderr = process.communicate(timeout=timeout)
        except subprocess.TimeoutExpired:
            _stop_process_tree(process)
            try:
                # Drain already-produced diagnostics after requesting termination.
                stdout, stderr = process.communicate(timeout=5)
            except subprocess.TimeoutExpired as error:
                # A descendant that deliberately detached on POSIX can retain a pipe handle.
                # Never let collecting its output defeat the original timeout.
                stdout, stderr = error.stdout, error.stderr
                if os.name != "nt":
                    # Windows communicate uses reader threads; do not close their
                    # pipe objects concurrently while a detached child retains them.
                    process.stdout.close()
                    process.stderr.close()
                process.wait(timeout=5)
            output.write_text(_text(stdout) + _text(stderr), encoding="utf-8")
            raise subprocess.TimeoutExpired(command, timeout, output=stdout, stderr=stderr)
        output.write_text(_text(stdout) + _text(stderr), encoding="utf-8")
        return subprocess.CompletedProcess(command, process.returncode, stdout, stderr)
    finally:
        # Exceptions during capture must not leave the launched parent running.
        if process is not None and process.poll() is None:
            _stop_process_tree(process)
            process.wait(timeout=5)


def validate_project(
    editor: Path,
    project: Path,
    output_root: Path,
    expected_version: str | None = None,
    timeout: int = 900,
) -> dict[str, Any]:
    """Return a report; success means import in the copy, never source migration or Play mode."""
    editor = editor.expanduser().resolve()
    project = project.expanduser().resolve()
    output_root = output_root.expanduser().resolve()
    # Resolve paths before enforcing the boundary, including a symlinked output parent.
    if _inside(output_root, _checkout_root(project)):
        raise ValidationError("--output-dir must be outside the source checkout.")
    if not editor.is_file():
        raise ValidationError(f"Unity editor executable was not found: {editor}")
    if timeout <= 0:
        raise ValidationError("--timeout must be positive.")

    before = _snapshot(project)
    source_version = _source_version(project)
    output_root.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(prefix="unity-validation-", dir=output_root))
    copy = work / "project"
    log_file = work / "import.log"
    # Start pessimistically and clear failure only after every evidence check passes.
    # Conversion/import never implies a source migration, Play test, or device build.
    report: dict[str, Any] = {
        "status": "failed",
        "exit_code": 1,
        "source_project": str(project),
        "source_version": source_version,
        "editor": str(editor),
        "validation_directory": str(work),
        "copied_project": str(copy),
        "import_log": str(log_file),
        "source_unchanged": True,
        "project_converted_in_copy": False,
        "upgrade_applied_to_checkout": False,
        "play_mode_validated": False,
        "android_build_validated": False,
    }
    try:
        # Ask the actual executable instead of guessing from its installation folder
        # or accepting the version the caller hoped to use as an observed result.
        version_result = _run([str(editor), "-version"], work / "version-output.log", min(timeout, 30))
        report["version_exit_code"] = version_result.returncode
        if version_result.returncode != 0:
            report["exit_code"] = version_result.returncode
            raise ValidationError("Unity -version failed; see version-output.log.")
        versions = set(re.findall(rf"\b({VERSION_PATTERN})\b", (version_result.stdout or "") + (version_result.stderr or "")))
        if len(versions) != 1:
            # Mixed-version output cannot establish which editor should perform import.
            raise ValidationError("Unity -version did not identify exactly one editor version.")
        editor_version = versions.pop()
        report["editor_version"] = editor_version
        channel = re.search(r"([abfp])\d+$", editor_version).group(1)
        report["editor_release_channel"] = {"a": "alpha", "b": "beta", "f": "final", "p": "patch"}[channel]
        report["editor_is_preview"] = channel in ("a", "b")
        if expected_version is not None and editor_version != expected_version:
            raise ValidationError(f"Expected editor {expected_version}, detected {editor_version}.")
        if _version_key(editor_version) < _version_key(source_version):
            raise ValidationError("The detected editor is older than the source project; refusing a downgrade.")

        copy.mkdir()
        # Every run starts clean. Existing caches and Git metadata are not inputs,
        # while package declarations and their lock file remain part of the copy.
        for name in PROJECT_DIRECTORIES:
            shutil.copytree(project / name, copy / name)

        # API updating is intentionally allowed only against the isolated -projectPath.
        command = [str(editor), "-batchmode", "-nographics", "-accept-apiupdate", "-quit",
                   "-projectPath", str(copy), "-logFile", str(log_file)]
        import_result = _run(command, work / "import-output.log", timeout)
        report["import_exit_code"] = import_result.returncode
        if import_result.returncode != 0:
            report["exit_code"] = import_result.returncode
            raise ValidationError("Unity import failed; see import.log and import-output.log.")
        if not log_file.is_file():
            raise ValidationError("Unity returned zero without writing its import log.")
        log_text = log_file.read_text(encoding="utf-8-sig", errors="replace")
        output_text = (work / "import-output.log").read_text(encoding="utf-8", errors="replace")
        diagnostics = []
        # Check the editor log and native streams: failures may be present in either.
        # Report locations/categories rather than copying potentially sensitive lines.
        for name, text in (("import.log", log_text), ("import-output.log", output_text)):
            for number, line in enumerate(text.splitlines(), start=1):
                for category, pattern in ERROR_PATTERNS.items():
                    if pattern.search(line):
                        diagnostics.append({"file": name, "line": number, "category": category})
        report["diagnostics"] = diagnostics
        if diagnostics:
            raise ValidationError("Unity reported import/compiler/license/package errors; inspect diagnostic log locations.")

        log_versions = set(LOG_VERSION.findall(log_text))
        # The import must identify the same editor queried above; unrelated or missing
        # log evidence must not make a nominally successful process look validated.
        if log_versions != {editor_version}:
            raise ValidationError("The import log does not confirm the detected editor version.")
        report["import_version"] = editor_version
        copied_version = _source_version(copy)
        report["copied_project_version"] = copied_version
        if copied_version != editor_version:
            raise ValidationError("Unity did not save a ProjectVersion matching the importing editor.")
        assembly = copy / "Library" / "ScriptAssemblies" / "Assembly-CSharp.dll"
        # This DLL must be newly produced because no Library directory was copied.
        # Its presence is compilation evidence, not proof of scene/physics correctness.
        if not assembly.is_file() or assembly.stat().st_size == 0:
            raise ValidationError("The clean import did not produce the gameplay Assembly-CSharp.dll.")

        report["migration_changes"] = _changes(before, _snapshot(copy))
        report["status"] = "validated_import"
        report["exit_code"] = 0
        report["project_converted_in_copy"] = source_version != editor_version
    except subprocess.TimeoutExpired:
        report["exit_code"] = 124
        report["error"] = "Unity validation timed out; cleanup was requested for the launched process group/tree. Inspect logs and any remaining detached child processes."
    except (ValidationError, OSError, ValueError) as error:
        report["error"] = str(error)
    finally:
        # Even a successful copy import is invalidated if original inputs changed.
        # Do not auto-restore them: edits may belong to another editor or the user.
        try:
            report["source_unchanged"] = _snapshot(project) == before
        except (OSError, ValidationError):
            report["source_unchanged"] = False
        if not report["source_unchanged"]:
            report["status"] = "failed"
            report["exit_code"] = 1
            report["project_converted_in_copy"] = False
            report["error"] = "Source files changed during validation. Review concurrent changes before retrying."
        # Persist failures as well as successes, retaining the isolated project and logs
        # for diagnosis instead of overwriting source files or losing import evidence.
        (work / "report.json").write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return report


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--editor", type=Path, required=True, help="Installed Unity/Unity.exe executable.")
    parser.add_argument("--project", type=Path, default=Path(__file__).resolve().parents[1] / "stack and slide")
    parser.add_argument("--output-dir", type=Path, required=True, help="Parent for new validation directories, outside checkout.")
    parser.add_argument("--expected-version", help="Optional exact editor version; mismatches stop before importing.")
    parser.add_argument("--timeout", type=int, default=900, help="Import timeout in seconds (default: 900).")
    args = parser.parse_args(argv)
    try:
        report = validate_project(args.editor, args.project, args.output_dir, args.expected_version, args.timeout)
    except (ValidationError, OSError) as error:
        print(str(error), file=sys.stderr)
        return 1
    print(json.dumps(report, indent=2, ensure_ascii=False))
    # Preserve ordinary editor failure codes; record signal codes verbatim in report.json.
    exit_code = report["exit_code"]
    return exit_code if 0 <= exit_code <= 255 else 1


if __name__ == "__main__":
    raise SystemExit(main())
