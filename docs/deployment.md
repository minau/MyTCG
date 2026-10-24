# Déploiement OVH Ubuntu et maintenance

## Préparer le VPS

Installer Docker Engine et le plugin Compose depuis les dépôts officiels Docker. Créer des enregistrements DNS A (et AAAA uniquement si IPv6 fonctionne) pour le site et l’API vers le VPS. Autoriser SSH et TCP 80/443 (UDP 443 facultatif pour HTTP/3) ; ne jamais ouvrir 5432 au public. Protéger l’accès SSH et restreindre les utilisateurs du groupe Docker, équivalent à des privilèges administrateur.

```bash
git clone https://github.com/minau/MyTCG.git
cd MyTCG
python3 scripts/init-env.py
# Éditer .env : WEB_DOMAIN, API_DOMAIN et ACME_EMAIL réels.
# Utiliser des secrets propres à la production, sans les afficher dans des logs.
docker compose --env-file .env -f deploy/docker-compose.yml config --quiet
docker compose --env-file .env -f deploy/docker-compose.yml up -d --build
docker compose --env-file .env -f deploy/docker-compose.yml ps
```

Caddy obtient et renouvelle automatiquement les certificats pour les domaines configurés. Les domaines exemples ne peuvent pas servir de déploiement réel. Le web appelle `/api` sur sa propre origine ; le sous-domaine API reste disponible aux clients mobiles. Les API n’exposent pas OpenAPI en Production. Pas d’auto-migration à l’intérieur des réplicas API : le job `migrate` doit terminer avec succès avant démarrage.

`POSTGRES_PASSWORD` et `JWT_KEY` ne doivent pas être stockés dans l’image, Git ou une commande partagée. `.env` est une facilité locale/VPS avec permissions 0600 ; en CI, utiliser un gestionnaire de secrets et des variables injectées. Les administrateurs Docker peuvent lire les variables des conteneurs : limiter cet accès.

Les images .NET utilisent MCR et peuvent être redirigées vers des hôtes régionaux sous `*.data.mcr.microsoft.com`. Le réseau cloud doit autoriser ces destinations, en plus des registres et de NuGet. Le brouillon cloud contient cet ajout ; sa sauvegarde ne prouve pas qu’il est appliqué à la machine courante.

## Maintenance

Les commandes suivantes supposent le répertoire racine du dépôt. Les variables du conteneur sont évaluées **dans** celui-ci ; aucun secret n’est affiché :

```bash
docker compose --env-file .env -f deploy/docker-compose.yml logs --tail=100 api migrate reverse-proxy
docker compose --env-file .env -f deploy/docker-compose.yml exec api curl -fsS http://localhost:8080/health
# Mise à jour après sauvegarde :
git pull --ff-only
docker compose --env-file .env -f deploy/docker-compose.yml build api web
docker compose --env-file .env -f deploy/docker-compose.yml run --rm migrate
docker compose --env-file .env -f deploy/docker-compose.yml up -d
# Arrêt sans suppression des volumes :
docker compose --env-file .env -f deploy/docker-compose.yml down
```

Un changement de clé JWT invalide tous les access tokens. Pour invalider également les refresh tokens lors d’un incident, révoquer les sessions en base via une opération administrative contrôlée. Une simple rotation de JWT_KEY ne révoque pas les refresh tokens déjà émis.

La limite de débit applicative est conservatrice derrière Caddy (voir architecture). Avant une ouverture à plus d’utilisateurs, configurer un proxy connu pour les IP réelles ou ajouter une limitation de débit au niveau de l’infrastructure ; ne jamais faire confiance à tous les `X-Forwarded-For`.

## Sauvegarde PostgreSQL

Conserver des sauvegardes chiffrées hors VPS, à accès restreint, avec une politique de rétention et des tests réguliers de restauration. Le volume persistant n’est pas une sauvegarde.

```bash
mkdir -p backups
chmod 700 backups
umask 077
docker compose --env-file .env -f deploy/docker-compose.yml exec -T database   sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > backups/mytcg.dump
# Vérifier que l’archive est lisible :
docker compose --env-file .env -f deploy/docker-compose.yml exec -T database   pg_restore --list < backups/mytcg.dump
```

Restaurer d’abord sur une base dédiée pour valider l’archive. Exemple de restauration sur la base configurée, **destructif : arrêter l’API, sauvegarder l’état courant et confirmer la cible avant exécution** :

```bash
docker compose --env-file .env -f deploy/docker-compose.yml stop api
docker compose --env-file .env -f deploy/docker-compose.yml exec -T database   sh -c 'pg_restore --clean --if-exists --no-owner -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < backups/mytcg.dump
docker compose --env-file .env -f deploy/docker-compose.yml up -d api
```

Sauvegarder aussi la configuration et, selon votre politique, les volumes `caddy_data` et `caddy_config` (certificats et clés privées, à protéger). Ne pas copier les fichiers du volume PostgreSQL en cours d’écriture comme remplacement de `pg_dump`.

## Recette avant production

Compiler les images, vérifier tous les services healthy et le job migrate terminé, exécuter les tests, faire une inscription/connexion depuis le vrai site HTTPS et une rotation de cookie Secure, redémarrer les services et confirmer la persistance. Vérifier les sauvegardes/restaurations et les règles de pare-feu. Ne pas annoncer un déploiement OVH ou une émission de certificat réels tant que ces actions n’ont pas été faites sur le VPS.
