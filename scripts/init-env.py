#!/usr/bin/env python3
"""Create a local .env once; never overwrite an existing file or print secrets."""
from pathlib import Path
import secrets
import os
root = Path(__file__).resolve().parents[1]
path = root / ".env"
text = (root / ".env.example").read_text().replace("POSTGRES_PASSWORD=\n", "POSTGRES_PASSWORD=" + secrets.token_hex(24) + "\n").replace("JWT_KEY=\n", "JWT_KEY=" + secrets.token_hex(48) + "\n")
fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
with os.fdopen(fd, "w") as stream: stream.write(text)
print(".env créé avec permissions 0600. Configurer les domaines avant le déploiement.")
