#!/usr/bin/env python3
"""Configure Gmail SMTP interactively; passwords never appear in arguments or logs."""
import argparse
import getpass
from pathlib import Path
import smtplib
import ssl
from smtp_config import save_private


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sender", required=True)
    parser.add_argument("--recipient", required=True)
    parser.add_argument("--host", default="smtp.gmail.com")
    parser.add_argument("--port", type=int, default=587)
    parser.add_argument("--config", type=Path, default=Path(__file__).resolve().parents[1] / ".local" / "smtp.json")
    args = parser.parse_args()
    if any("@" not in email or any(c.isspace() for c in email) for email in (args.sender, args.recipient)):
        raise ValueError("Use full sender and recipient email addresses.")
    password = getpass.getpass("Gmail uygulama sifresi (normal hesap sifreni yazma; giris gizlidir): ")
    if args.host == "smtp.gmail.com":
        password = password.replace(" ", "")
    if not password:
        raise ValueError("An SMTP app password is required.")
    # Validate authentication before replacing a working local configuration.
    with smtplib.SMTP(args.host, args.port, timeout=20) as client:
        client.ehlo()
        client.starttls(context=ssl.create_default_context())
        client.ehlo()
        client.login(args.sender, password)
    save_private(args.config, {"Email": {"Transport": "Smtp", "From": args.sender,
        "Smtp": {"Host": args.host, "Port": args.port, "EnableSsl": True,
                 "Username": args.sender, "Password": password}}, "DemoRecipient": args.recipient})
    print("SMTP authentication OK. Private configuration saved; password was not printed.")


if __name__ == "__main__":
    try:
        main()
    except (KeyboardInterrupt, EOFError):
        raise SystemExit("SMTP setup cancelled; configuration unchanged.")
    except Exception as error:
        raise SystemExit("SMTP setup failed (" + type(error).__name__ + "). Check app-password/account permissions; no secret was logged.")
