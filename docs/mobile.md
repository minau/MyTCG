# MAUI : macOS, iOS et Android

## Outils et compatibilité

Installer le SDK épinglé dans `global.json`, puis vérifier le jeu de workloads et leurs prérequis :

```bash
dotnet --info
dotnet workload --info
dotnet workload install maui
# Sur macOS :
xcodebuild -version
xcode-select -p
xcrun simctl list devices available
```

Références officielles à consulter avant une installation/mise à jour :

- https://learn.microsoft.com/dotnet/maui/get-started/installation?view=net-maui-10.0
- https://github.com/dotnet/maui/releases
- https://github.com/dotnet/macios/releases (version exacte de Xcode requise par le workload iOS)
- https://github.com/dotnet/android/releases (Android SDK/JDK requis)

Le SDK 10.0.402 fournit le jeu de manifestes `10.0.400`. Le package Controls 10.0.120 est publié sur NuGet ; l’installation des workloads doit rester cohérente avec .NET 10. Ne pas forcer un Xcode incompatible ni ignorer les contrôles de workload. Installer Xcode complet, accepter sa licence et exécuter son premier lancement ; un simple Command Line Tools n’est pas suffisant pour iOS. Rider ou VS Code avec l’extension .NET MAUI peut aider à installer Android SDK et le JDK exigé par le workload.

Cette machine cloud Linux ne possède pas `maui-android`, Android SDK/JDK ni Xcode ; **aucune compilation ou exécution native n’y est certifiée**. La tentative Android retourne `NETSDK1147`. iOS exige macOS. Le code client partagé est, lui, compilé et testé sous .NET 10.

## Compiler et lancer

```bash
dotnet restore src/MyTCG.Mobile/MyTCG.Mobile.csproj
dotnet build src/MyTCG.Mobile -f net10.0-android
# Simulateur iOS Apple Silicon ; adapter le RID pour un Mac Intel :
dotnet build src/MyTCG.Mobile -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
# Lancement Android, émulateur démarré :
dotnet build src/MyTCG.Mobile -t:Run -f net10.0-android
# Lancement iOS, simulateur choisi dans Rider/VS Code ou avec _DeviceName :
dotnet build src/MyTCG.Mobile -t:Run -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
```

La signature sur appareil réel nécessite une équipe Apple, un provisioning profile et un identifiant d’application propre. `Entitlements.plist` déclare le groupe Keychain de SecureStorage ; vérifier sa cohérence avec votre bundle identifier et votre profil. Pour Android Release, fournir un keystore hors Git et les paramètres de signature de votre CI.

## URL API

L’écran de connexion offre « Configurer le serveur ». L’URL est conservée dans Preferences, sans secret ; changer de serveur efface le refresh token. En Release, HTTPS est obligatoire ; l’URL exemple doit être remplacée par celle du déploiement. Ne pas désactiver la validation TLS.

En Debug :

- Émulateur Android : `http://10.0.2.2:5080/` pour joindre le Mac hôte, **pas localhost**.
- Simulateur iOS sur le même Mac : `http://localhost:5080/`, autorisé par `NSAllowsLocalNetworking`.
- Appareil physique : API HTTPS accessible sur le réseau ; si Debug Android HTTP local, utiliser l’IP LAN du Mac et autoriser le port dans le pare-feu. Ne pas généraliser les exceptions ATS sur iOS : préférer un endpoint HTTPS avec certificat reconnu.

L’API doit écouter une interface accessible (`scripts/dev-api.sh` utilise `0.0.0.0:5080`). Les ports Docker de la configuration locale restent loopback ; pour un appareil, lancer l’API native ou utiliser un déploiement HTTPS. CORS concerne le navigateur, pas le client HTTP MAUI.

## Recette manuelle restante

Sur chaque plateforme : inscription, erreurs de validation, connexion email/username, dashboard sans données fictives, modification de compte, fermeture/réouverture et restauration SecureStorage, expiration, panne réseau, déconnexion, redémarrage sans reconnexion automatique après logout. Vérifier rotation, verrouillage, comportement du Keychain/Keystore et disposition sur petits écrans. Ces contrôles natifs restent à exécuter avec les outils et appareils requis.
