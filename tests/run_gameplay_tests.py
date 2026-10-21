#!/usr/bin/env python3
"""Compile and run isolated gameplay tests without NuGet or writable home folders."""

import argparse
import glob
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET


# These tests compile the real gameplay sources with small Unity API substitutes.
# The runner verifies isolated C# logic; it does not import assets or launch Unity.
def version(value):
    # Numeric components keep SDK patch/feature-band ordering independent of string width.
    return tuple(int(part) for part in value.split("."))


def find_dotnet(explicit):
    # An explicit installation takes priority over PATH, which can select another SDK.
    executable = explicit or shutil.which("dotnet")
    if not executable and os.environ.get("DOTNET_ROOT"):
        # DOTNET_ROOT also supports local SDK installations that have no shell PATH entry.
        executable = str(Path(os.environ["DOTNET_ROOT"]) / ("dotnet.exe" if os.name == "nt" else "dotnet"))
    if not executable or not Path(executable).is_file():
        raise RuntimeError("Install the .NET 8 SDK or pass --dotnet /path/to/dotnet.")
    return str(Path(executable).resolve())


def find_sdk(dotnet):
    # This native host query works before the SDK's first-use setup. In contrast,
    # `dotnet run` can initialize NuGet/mutex files in a read-only home directory.
    result = subprocess.run([dotnet, "--list-sdks"], text=True, capture_output=True)
    if result.returncode:
        raise RuntimeError(result.stderr.strip() or "Cannot list installed .NET SDKs.")
    sdks = []
    for line in result.stdout.splitlines():
        # Accept stable .NET 8 SDKs only, rather than silently compiling with a preview
        # or another major version that happens to appear later in the host output.
        match = re.fullmatch(r"(8\.0\.\d+) \[(.+)\]", line.strip())
        if match:
            sdks.append((version(match[1]), Path(match[2]) / match[1]))
    if not sdks:
        raise RuntimeError("The .NET 8 SDK is required; a runtime alone cannot compile tests.")
    return max(sdks)[1]


def response_argument(value):
    # Roslyn response-file quoting preserves paths containing spaces on either OS.
    # These values are compiler arguments, not text passed through a command shell.
    if any(character in value for character in '\"\r\n'):
        raise RuntimeError("Compiler paths must not contain quotes or newlines.")
    return '"' + value + '"'


def run(dotnet):
    project = Path(__file__).resolve().parent / "Gameplay.Tests"
    # Read the existing project so language and warning settings have one source of truth.
    config = ET.parse(project / "Gameplay.Tests.csproj").getroot()
    properties = {element.tag: element.text for group in config.findall("PropertyGroup") for element in group}
    if properties.get("TargetFramework") != "net8.0" or properties.get("OutputType") != "Exe":
        raise RuntimeError("The isolated runner expects an executable targeting net8.0.")

    sdk = find_sdk(dotnet)
    compiler = sdk / "Roslyn" / "bincore" / "csc.dll"
    # Use the selected host's installation for both compiler and framework references.
    # An unrelated reference pack from PATH could otherwise hide a broken SDK setup.
    pack_root = sdk.parent.parent / "packs" / "Microsoft.NETCore.App.Ref"
    packs = [path for path in pack_root.glob("8.0.*") if re.fullmatch(r"8\.0\.\d+", path.name)]
    if not packs or not compiler.is_file():
        raise RuntimeError("The installed .NET 8 SDK is missing its compiler or reference pack.")
    references = max(packs, key=lambda path: version(path.name)) / "ref" / "net8.0"

    # Mirror the SDK project's default Compile items and its explicit external source globs.
    # Excluding bin/obj prevents generated C# from earlier runs entering the next build.
    sources = {path for path in project.rglob("*.cs") if not {"bin", "obj"}.intersection(path.relative_to(project).parts)}
    for item in config.findall("ItemGroup/Compile"):
        include = item.get("Include")
        if include:
            # The external glob includes the actual game files, so tests cannot keep
            # passing against an obsolete copy of the implementation kept under tests/.
            sources.update(Path(path).resolve() for path in glob.glob(str(project / include), recursive=True))
    if not sources:
        raise RuntimeError("No gameplay test sources were found.")

    output_root = project / "bin" / "isolated"
    output_root.mkdir(parents=True, exist_ok=True)
    environment = os.environ.copy()
    environment.setdefault("DOTNET_CLI_TELEMETRY_OPTOUT", "1")
    # Each invocation gets a fresh, ignored build directory. Failed compilation cannot
    # reuse a previous passing DLL, and cleanup does not touch the checked-in sources.
    with tempfile.TemporaryDirectory(prefix="run-", dir=output_root) as temporary:
        output = Path(temporary)
        assembly = output / "Gameplay.Tests.dll"
        arguments = ["-nologo", "-nostdlib+", "-target:exe", "-deterministic+",
                     "-langversion:" + properties["LangVersion"], "-out:" + str(assembly)]
        if properties.get("TreatWarningsAsErrors", "").lower() == "true":
            arguments.append("-warnaserror+")
        arguments.extend("-r:" + str(path) for path in sorted(references.glob("*.dll")))
        arguments.extend(str(path) for path in sorted(sources))
        response = output / "compile.rsp"
        response.write_text("\n".join(response_argument(argument) for argument in arguments), encoding="utf-8")
        print("Compiling gameplay tests with .NET SDK " + sdk.name + " / C# " + properties["LangVersion"], flush=True)
        # Invoke Roslyn through the host directly: no MSBuild restore, SDK first-use
        # migration, or user-level NuGet cache is needed for this package-free suite.
        # -noconfig must be a command-line option; Roslyn ignores it in a response file.
        compiled = subprocess.run([dotnet, str(compiler), "-noconfig", "@" + str(response)], env=environment)
        if compiled.returncode:
            # Compilation failure is a failed run, not a reason to try another binary.
            return compiled.returncode

        # The SDK supplies its matching runtime; select the latest installed .NET 8 patch.
        # Framework references and runtime stay on net8.0, while C# syntax stays at the
        # project's declared language level. This is not a Unity engine compatibility test.
        (output / "Gameplay.Tests.runtimeconfig.json").write_text(
            '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}',
            encoding="utf-8")
        # Preserve the test executable's result so CI cannot report a failing suite as passed.
        return subprocess.run([dotnet, str(assembly)], env=environment).returncode


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", help="Path to the installed .NET host (dotnet or dotnet.exe).")
    arguments = parser.parse_args()
    try:
        return run(find_dotnet(arguments.dotnet))
    except (OSError, RuntimeError, ET.ParseError) as error:
        print("Gameplay test setup failed: " + str(error), file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
