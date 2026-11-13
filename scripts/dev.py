#!/usr/bin/env python3
"""Local development commands. Secrets stay in ignored, private files."""
import argparse
import json
import os
from pathlib import Path
import secrets
import shutil
import subprocess
import sys
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parents[1]


def init_config(port, root=ROOT):
    if not 1 <= port <= 65535:
        raise ValueError("MySQL port must be between 1 and 65535.")
    local = root / ".local"
    paths = [local / name for name in ("dev.json", "mysql-password", "mysql-root-password")]
    if any(path.exists() for path in paths):
        if not all(path.is_file() for path in paths):
            raise ValueError("Local configuration is incomplete; restore the missing files instead of rotating database credentials.")
        existing = json.loads(paths[0].read_text())
        if existing["mysqlPort"] != port:
            raise ValueError("Configuration already exists on a different port. Edit .local/dev.json to change the host port.")
        print("Existing local configuration retained.")
        return
    local.mkdir(mode=0o700, parents=True, exist_ok=True)
    contents = [json.dumps({"mysqlPort": port, "serverVersion": "8.4.13"}, indent=2) + "\n",
                secrets.token_hex(32) + "\n", secrets.token_hex(32) + "\n"]
    for path, content in zip(paths, contents):
        with os.fdopen(os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), "wb") as stream:
            stream.write(content.encode("ascii"))
    print("Created .local configuration and random database passwords. No passwords are printed.")


def development_environment(root=ROOT):
    local = root / ".local"
    if not (local / "dev.json").is_file():
        raise ValueError("Run python3 scripts/dev.py init first.")
    config = json.loads((local / "dev.json").read_text())
    try:
        password = (local / "mysql-password").read_bytes().decode("ascii").rstrip("\n")
    except UnicodeDecodeError as error:
        raise ValueError("Invalid local database configuration.") from error
    port = int(config["mysqlPort"])
    hex_password = password[:-1] if password.endswith("\r") else password
    if len(hex_password) != 64 or any(char not in "0123456789abcdef" for char in hex_password) or not 1 <= port <= 65535:
        raise ValueError("Invalid local database configuration.")
    connection_password = f'"{password}"' if password.endswith("\r") else password
    env = os.environ.copy()
    env.update({
        "ASPNETCORE_ENVIRONMENT": "Development",
        "MYSQL_PORT": str(port),
        "MySql__ServerVersion": config["serverVersion"],
        "ConnectionStrings__DefaultConnection":
            f"Server=127.0.0.1;Port={port};Database=onlinestore;User ID=onlinestore;Password={connection_password}",
    })
    return env


def run(command, env):
    executable = shutil.which(command[0])
    if executable is None:
        raise ValueError(f"Required executable not found: {command[0]}. See docs/local-development.md.")
    # Start the file that which() found, not the bare name. On Windows npm is npm.cmd and CreateProcess does not
    # search PATHEXT, so the bare name "npm" cannot be started although which() finds it (SCRUM-129).
    # Secrets are passed through the child environment, never command-line arguments.
    try:
        subprocess.run([executable, *command[1:]], cwd=ROOT, env=env, check=True)
    except FileNotFoundError as error:
        raise ValueError(f"Could not start {command[0]} ({executable}). See docs/local-development.md.") from error


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    init = commands.add_parser("init", help="create local configuration once")
    init.add_argument("--port", type=int, default=3306)
    for command in ("database", "migrate", "seed", "seed-demo", "run", "stop", "frontend", "frontend-build"):
        commands.add_parser(command)
    check = commands.add_parser("check", help="check the real API-to-database connection")
    check.add_argument("--url", default="http://127.0.0.1:5200")
    role = commands.add_parser("role", help="trusted operator staff-role assignment")
    role.add_argument("email")
    role.add_argument("role", choices=["Customer", "SalesManager", "ProductManager", "SupportAgent"])
    args = parser.parse_args(argv)
    if args.command == "init":
        init_config(args.port)
        return
    if args.command == "check":
        for resource in ("categories", "products"):
            with urllib.request.urlopen(f"{args.url.rstrip('/')}/api/{resource}", timeout=10) as response:
                rows = json.load(response)
            if not isinstance(rows, list):
                raise ValueError("Unexpected API response.")
            print(f"Database-backed {resource} API: OK ({len(rows)} rows)")
        return
    if args.command in ("frontend", "frontend-build"):
        # The browser build/dev server needs no database credentials.
        env = os.environ.copy()
        if args.command == "frontend-build":
            run(["npm", "--prefix", "frontend", "ci"], env)
            run(["npm", "--prefix", "frontend", "run", "build"], env)
        else:
            run(["npm", "--prefix", "frontend", "run", "dev"], env)
        return
    env = development_environment()
    if args.command == "database":
        run(["docker", "compose", "up", "--detach", "--wait", "mysql"], env)
    elif args.command == "stop":
        run(["docker", "compose", "down"], env)
    elif args.command == "migrate":
        run(["dotnet", "tool", "restore"], env)
        run(["dotnet", "ef", "database", "update", "--project", "OnlineStore"], env)
    elif args.command in ("seed", "seed-demo"):
        run(["dotnet", "run", "--project", "OnlineStore", "--no-launch-profile", "--", "--" + args.command], env)
    elif args.command == "role":
        run(["dotnet", "run", "--project", "OnlineStore", "--no-launch-profile", "--", "--set-role", args.email, args.role], env)
    else:
        run(["dotnet", "run", "--project", "OnlineStore", "--launch-profile", "http"], env)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        sys.exit(130)
    except ValueError as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
    except (OSError, subprocess.CalledProcessError, urllib.error.URLError):
        # Never print a raw environment/connection-string-bearing exception.
        print("Local command failed. Check prerequisites, .local configuration, MySQL health and docs/local-development.md.", file=sys.stderr)
        sys.exit(1)
