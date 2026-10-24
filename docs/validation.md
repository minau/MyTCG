# Validation de la V1 — 24 octobre 2026

## Résultats observés dans cette machine cloud Linux x64

| Vérification | Résultat |
| --- | --- |
| SDK .NET | 10.0.402 téléchargé depuis Microsoft, SHA-512 vérifié contre les métadonnées officielles |
| Restore et audit NuGet | Réussis ; aucune alerte ignorée ; dépendances backend/web/tests verrouillées |
| `dotnet build MyTCG.Server.slnf` | Réussi, 0 erreur et 0 avertissement |
| `dotnet test MyTCG.Server.slnf --no-restore --logger trx` | **31 réussis : 9 unitaires et 22 intégration, 0 échec, 0 ignoré** |
| Base de tests | PostgreSQL 16 réel, isolé par Testcontainers ; aucun fournisseur InMemory |
| `dotnet publish src/MyTCG.Api -c Release -p:RestoreLockedMode=true` | Réussi |
| `dotnet publish src/MyTCG.Web -c Release -p:RestoreLockedMode=true` | Réussi ; workload wasm-tools optionnel absent, pas de compilation AOT annoncée |
| Migration initiale PostgreSQL de développement | Appliquée ; une seconde exécution confirme que la base est à jour |
| Backend natif + PostgreSQL Docker | Démarrage, `/health`, Swagger UI et contrat OpenAPI Bearer vérifiés |
| Navigateur Chromium, web Development | Inscription, connexion, redirection de route privée, dashboard, cookie HttpOnly/SameSite, absence de localStorage, restauration, responsive 390 px, logout : réussis |
| Navigateur Chromium, web publié Release | Même parcours réussi sur les fichiers publiés servis par un conteneur Caddy, avec API native via reverse proxy |
| Redémarrage du conteneur PostgreSQL | Compte, mot de passe, session et refresh token conservés ; connexion et rotation réussies après redémarrage |
| `docker compose … config --quiet` | Configurations locale et production valides |
| `caddy validate` | Trois fichiers Caddy (production, local, serveur statique) valides dans l’image officielle |
| Construction Docker multi-stage API/web | **Bloquée** pendant le téléchargement des images Microsoft, HTTP 403 sur le CDN régional MCR |
| Démarrage de toute la pile Compose | **Non validé**, dépend de la construction des images .NET |
| Compilation Android | **Non validée** : tentative arrêtée par `NETSDK1147`, workload `maui-android` absent |
| Compilation et simulateur iOS | **Non exécutés** : nécessitent macOS/Xcode |
| Déploiement OVH et certificats publics | **Non exécutés** : aucun VPS/domaine réel fourni |

Les premiers essais ont permis de corriger la navigation Blazor entre routes partageant le même composant, le débordement d’un username long à 390 px, la publication loopback PostgreSQL en développement, la syntaxe des blocs Caddy et le chargement différé de configuration des tests. Une ancienne dépendance Testcontainers amenait un SSH.NET vulnérable : passage à Testcontainers 4.16.0, sans désactiver l’audit ni supprimer les avertissements.

Les tests d’intégration couvrent : inscription, unicité email/username normalisée, mots de passe/confirmation, connexion username et email, refus uniforme, verrouillage, protection du compte, mise à jour de compte, rotation/rejeu/concurrence, logout, expiration, désactivation, JWT falsifié, cookies web/CSRF, isolation du mode mobile, stockage haché, limitation HTTP 429, documentation Development et absence de documentation Production, santé de la base.

## Rejouer les tests navigateur

Le script `scripts/smoke-web.cjs` utilise Playwright Core et un Chromium installé. Il crée un compte de test unique sur la cible, vérifie le parcours et ferme le navigateur. L’exécuter uniquement sur une instance de développement/recette.

Exemple avec outils installés hors dépôt (adapter les chemins selon le système) :

```bash
npm install --prefix /tmp/mytcg-browser-tools playwright-core
PLAYWRIGHT_MODULE=/tmp/mytcg-browser-tools/node_modules/playwright-core \
CHROMIUM_EXECUTABLE=/usr/bin/chromium \
WEB_URL=http://localhost:5180 node scripts/smoke-web.cjs
```

Le script active `--no-sandbox` pour Chromium dans cet environnement cloud isolé ; ne pas utiliser ce lancement pour naviguer sur des sites non fiables. Les captures de test sont écrites dans `/tmp`, pas dans Git. Le client applicatif et les tests xUnit restent en C#.

## Configuration cloud enregistrée

Le brouillon contient :

- `install_script` : exécution de `scripts/cloud-setup.sh` (SDK vérifié si absent, activation, restauration verrouillée des outils/dépendances et compilation).
- `start_skill` : utilisation du checkout existant, démarrage PostgreSQL, migrateur, API et web, vérification de santé et tests. Aucun worktree à créer par défaut.
- Réseau : `*.data.mcr.microsoft.com` et les hôtes observés `westus.data.mcr.microsoft.com`, `centralus.data.mcr.microsoft.com`, `westcentralus.data.mcr.microsoft.com`, sans modifier les presets existants.

**Sauvegarder un brouillon ne publie pas l’environnement et ne prouve pas que ses autorisations réseau sont appliquées à la machine courante.** Revoir et enregistrer les paramètres, puis publier via l’interface. Après application, reprendre `docker compose --env-file .env -f deploy/docker-compose.yml -f deploy/docker-compose.dev.yml up -d --build`, inspecter le job migrate et les health checks, et rejouer le smoke web sur le port 8088.

Les secrets de développement sont générés dans `.env` ignoré et protégé en 0600 ; aucun secret de production n’est fourni ou committé. Une restauration dans une nouvelle tâche après publication n’a pas été testée.

## Numéros automatiques et illustrations uploadées — 24 octobre 2026

- Compilation `MyTCG.Server.slnf` réussie : API, client web et projets de tests, zéro avertissement/erreur.
- Suite xUnit : **27 tests unitaires + 40 tests d’intégration PostgreSQL réussis**, aucun échec ni test ignoré.
- Tests ajoutés : 12 créations concurrentes (numéros uniques et croissants par set), numéro conservé à la modification/archivage, refus d’un numéro ou d’une URL externe dans la requête, image existante uploadée dans le même set, migration de données héritées et aller-retour de migration.
- Restauration verrouillée depuis le cache local, avec `-p:NuGetAudit=false` faute d’accès réseau dans le terminal ; l’audit de vulnérabilités n’a pas été exécuté. Les sources de packages et versions n’ont pas été modifiées.
- Le script smoke navigateur a été adapté et sa syntaxe JavaScript vérifiée ; le parcours navigateur et la compilation native MAUI n’ont pas été exécutés pour cette modification.
## Coûts d’attaque et de retrait — 24 octobre 2026

- Compilation API/web réussie sans avertissement ni erreur.
- **32 tests unitaires et 48 tests d’intégration PostgreSQL réussis**, aucun échec ni test ignoré.
- Vérifications : coûts indépendants, bornes inclusives 0 et 10, rejet de -1 et 11 à la création/modification, lecture des coûts dans les boosters, conservation des données lors de la migration et de son aller-retour.
- Ancien coût repris comme coût d’attaque, limité à 10 ; coût de retrait initialisé à 0. Les anciens coûts 11 et 12 sont couverts par le test de migration.
- Migration EF Core cohérente avec le modèle ; script smoke adapté aux deux champs et syntaxe JavaScript vérifiée.
- Même limite de vérification que précédemment : audit NuGet désactivé lors des commandes locales, parcours navigateur et compilation native MAUI non exécutés.
