"""Private SMTP configuration shared by local launchers. Never prints credentials."""
import json
import os
from pathlib import Path
import tempfile


def smtp_environment(root, inherited=None):
    inherited = os.environ if inherited is None else inherited
    path = Path(inherited.get("ONLINESTORE_SMTP_FILE", Path(root) / ".local" / "smtp.json"))
    if not path.exists():
        return {}
    value = json.loads(path.read_text())
    email = value.get("Email", {})
    smtp = email.get("Smtp", {})
    if email.get("Transport") != "Smtp" or not isinstance(email.get("From"), str) or not email["From"].strip():
        raise ValueError("Invalid private SMTP configuration.")
    host, port, tls = smtp.get("Host"), smtp.get("Port", 587), smtp.get("EnableSsl", True)
    if not isinstance(host, str) or not host.strip() or isinstance(port, bool) or not isinstance(port, int) or not 1 <= port <= 65535 or not isinstance(tls, bool):
        raise ValueError("Invalid private SMTP connection settings.")
    if not tls and host not in ("localhost", "127.0.0.1", "::1"):
        raise ValueError("Remote SMTP requires TLS.")
    env = {"Email__Transport": "Smtp", "Email__From": email["From"], "Email__Smtp__Host": host,
           "Email__Smtp__Port": str(port), "Email__Smtp__EnableSsl": str(tls).lower()}
    for name in ("Username", "Password"):
        if smtp.get(name) is not None:
            if not isinstance(smtp[name], str):
                raise ValueError("Invalid private SMTP credentials.")
            env["Email__Smtp__" + name] = smtp[name]
    # Explicit process configuration wins over the private file, including choosing Local.
    env.update({key: inherited[key] for key in env if key in inherited})
    return env


def save_private(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    descriptor, temporary = tempfile.mkstemp(prefix=".smtp-", dir=path.parent)
    try:
        with os.fdopen(descriptor, "w") as stream:
            json.dump(value, stream, indent=2)
            stream.write("\n")
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)
