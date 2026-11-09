#!/usr/bin/env python3
"""Create version-1 encryption/HMAC keys for a NEW database; never overwrite a key file."""
import argparse
import base64
import json
import os
from pathlib import Path
import secrets


def initialize(path):
    path = Path(path)
    if path.exists():
        value = json.loads(path.read_text())
        if value.get('version') != 1 or any(len(base64.b64decode(value[key], validate=True)) != 32
                                           for key in ('encryptionKey', 'blindIndexKey')):
            raise ValueError('Existing data-key file is invalid; it was not overwritten.')
        print('Existing data keys retained.')
        return
    path.parent.mkdir(parents=True, mode=0o700, exist_ok=True)
    content = {'version': 1, 'encryptionKey': base64.b64encode(secrets.token_bytes(32)).decode(),
               'blindIndexKey': base64.b64encode(secrets.token_bytes(32)).decode()}
    with os.fdopen(os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), 'w') as file:
        json.dump(content, file, indent=2)
        file.write('\n')
    print('Created private data keys. Back up this file with the database; it is never regenerated automatically.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('path', nargs='?', default=str(Path(__file__).resolve().parents[1] / '.local/db-redesign/data-keys.json'))
    initialize(parser.parse_args().path)
