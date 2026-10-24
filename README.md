# MyTCG

Fondation d’une plateforme de jeux de cartes entre amis. Cette V1 contient l’authentification, le compte utilisateur et un dashboard. Collection, decks, jeu et groupes restent **à venir**, sans données fictives.

## Architecture

- `MyTCG.Domain` : sessions, invariants persistants, aucune dépendance externe.
- `MyTCG.Contracts` : contrats REST et validation, indépendants d’EF.
- `MyTCG.Application` : cas d’utilisation et abstraction `IAccountService`.
- `MyTCG.Infrastructure` : Identity, EF Core/Npgsql, migrations, JWT, rotation.
- `MyTCG.Api` : endpoints, autorisation, CORS, CSRF, limitation de débit, santé et OpenAPI.
- `MyTCG.Client` : client HTTP et cycle de session partagés.
- `MyTCG.Web` : Blazor WebAssembly responsive.
- `MyTCG.Mobile` : MAUI Android/iOS, stockage sécurisé natif.

Voir [les décisions d’architecture](docs/architecture.md), [le déploiement](docs/deployment.md), [les contraintes mobiles](docs/mobile.md) et [les vérifications réellement exécutées](docs/validation.md).

## Prérequis et versions

- SDK **.NET 10.0.402** (`global.json`), LTS ; ASP.NET Core/EF Core **10.0.13**.
- Npgsql EF Core **10.0.2**, compatible EF Core 10 ; PostgreSQL **16**.
- MAUI Controls **10.0.120** ; workload .NET 10 correspondant, Xcode et Android SDK compatibles (voir documentation mobile).
- Docker Engine/Desktop avec Compose **2.24.4+** (`!override` dans la configuration locale).
- Python 3 pour générer `.env` ; macOS avec Rider ou VS Code/C# Dev Kit pour le développement.

Les versions des packages sont explicites dans les projets ; les dépendances backend/web/tests sont verrouillées dans leurs `packages.lock.json`. `dotnet restore` conserve l’audit de vulnérabilités et les avertissements sont traités en erreurs. Mettre à jour les correctifs de sécurité régulièrement, y compris les images PostgreSQL/Caddy. Les images `postgres:16-alpine` et `caddy:2-alpine` suivent volontairement les correctifs de leur branche ; les figer par digest pour une promotion de production strictement reproductible.

## Installation locale macOS

```bash
git clone https://github.com/minau/MyTCG.git
cd MyTCG
# Installer le SDK .NET 10 de Microsoft puis vérifier :
dotnet --version
python3 scripts/init-env.py       # crée .env une seule fois, permissions 0600
dotnet tool restore
dotnet restore MyTCG.Server.slnf --locked-mode
```

Ne jamais committer `.env`. Le générateur produit des secrets aléatoires locaux sans les afficher. Le lancement échoue si une clé JWT d’au moins 32 octets n’est pas fournie. Les noms de domaines de `.env.example` sont des exemples, à remplacer avant production.

Démarrer uniquement PostgreSQL :

```bash
docker compose --env-file .env -f deploy/docker-compose.yml -f deploy/docker-compose.dev.yml up -d --wait database
bash scripts/dev-api.sh --migrate
bash scripts/dev-api.sh
# Autre terminal :
dotnet run --project src/MyTCG.Web --urls http://localhost:5180
```

Le backend écoute sur `http://localhost:5080`, le web sur `http://localhost:5180`. Ces adresses sont réservées au développement sur votre machine. OpenAPI est consultable sur `/docs` et `/openapi/v1.json` en **Development uniquement**. L’URL API du web est dans `wwwroot/appsettings.Development.json`. En production, l’URL par défaut est celle du site et Caddy relaie `/api/*` ; aucune URL de service Docker n’est exposée au navigateur.

```bash
dotnet build MyTCG.Server.slnf
dotnet publish src/MyTCG.Api -c Release
dotnet publish src/MyTCG.Web -c Release
```

`MyTCG.sln` contient aussi MAUI ; `MyTCG.Server.slnf` permet de développer backend/web/tests sans SDK mobile sous Linux. Ne pas installer les workloads MAUI pour compiler uniquement l’API et le web.

## Parcours de la V1

Inscription publique avec username (3–32 lettres/chiffres/underscores), email et mot de passe (12–128 caractères, majuscule, minuscule, chiffre et caractère spécial). Le mot de passe est haché par Identity. Le username est l’unique nom public.

Connexion avec email **ou** username ; dashboard personnel ; modification du username/email ; déconnexion. Les sessions sont restaurées au démarrage si valides. Les erreurs réseau permettent une nouvelle tentative ; une déconnexion échouée sur le réseau reste signalée et doit être réessayée pour garantir la révocation serveur.

| Méthode | Route | Accès |
| --- | --- | --- |
| POST | `/api/auth/register` | Public |
| POST | `/api/auth/login` | Public |
| POST | `/api/auth/refresh` | Secret de session valide |
| POST | `/api/auth/logout` | Bearer JWT + session valide |
| GET | `/api/users/me` | Bearer JWT + session valide |
| PUT | `/api/users/me` | Bearer JWT + session valide |
| GET | `/health` | Public, vérifie PostgreSQL |

Les POST d’authentification exigent `X-MyTCG-Client: web` ou `mobile`. Le web exige en plus une origine autorisée ; son refresh token reste en cookie HttpOnly. Le mobile transmet son refresh token dans le corps JSON, jamais depuis les cookies. Voir [les garanties et limites de sécurité](docs/architecture.md).

## Tests

Docker doit fonctionner ; Testcontainers démarre une base PostgreSQL 16 isolée et la supprime à la fin :

```bash
dotnet test MyTCG.Server.slnf --logger trx
# Uniquement les tests unitaires (sans Docker) :
dotnet test tests/MyTCG.UnitTests
```

L’alternative `MYTCG_TEST_DATABASE` doit pointer **uniquement vers une base dédiée aux tests**, jamais la production : les migrations et données de test y seront écrites. Les tests couvrent les doublons normalisés, les mots de passe, la connexion, le verrouillage, le compte, les JWT invalides, l’expiration, les utilisateurs inactifs, la rotation concurrente, le rejeu, la révocation, CSRF et les secrets hachés. Les tests ne sont pas remplacés par EF InMemory ou SQLite.

## Migrations

```bash
dotnet tool restore
dotnet ef migrations add NomMigration --project src/MyTCG.Infrastructure --startup-project src/MyTCG.Api
bash scripts/dev-api.sh --migrate
# Générer un script auditable :
dotnet ef migrations script --idempotent --project src/MyTCG.Infrastructure --startup-project src/MyTCG.Api -o migration.sql
```

La fabrique design-time permet de générer les migrations sans clé JWT et sans connexion à une base. Les appliquer requiert une connexion réelle. Aucun `EnsureCreated` : les migrations sont la source du schéma. En Compose, le service dédié `migrate` applique les migrations puis initialise le rôle `User` avant de démarrer l’API. EF Core/Npgsql utilise un verrou de migration PostgreSQL. L’API normale ne migre pas implicitement à chaque démarrage.

## Docker complet

```bash
# Local : HTTP, base/API accessibles sur les ports loopback documentés.
docker compose --env-file .env -f deploy/docker-compose.yml -f deploy/docker-compose.dev.yml up -d --build
# Production : domaines DNS réels et ports 80/443 ouverts.
docker compose --env-file .env -f deploy/docker-compose.yml up -d --build
```

Les secrets sont injectés par environnement, sans copie dans les images. Le port PostgreSQL n’est pas publié en production ; le réseau applicatif est interne. Le mode local rend ce réseau joignable pour les outils de développement et lie les ports uniquement à `127.0.0.1`. Les volumes conservent PostgreSQL et les certificats Caddy. **Ne pas utiliser `down -v` sur une base à conserver.**

Les instructions OVH, sauvegardes/restaurations et commandes de maintenance sont dans [docs/deployment.md](docs/deployment.md).

## V2 — Univers, Studio, collections, boosters et decks

Voir [la documentation V2](docs/V2.md) pour les migrations, routes API, probabilités, volumes et instructions Docker.
