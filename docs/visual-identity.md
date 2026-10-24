# Charte graphique — édition claire

L’identité MyTCG privilégie le blanc, les gris neutres et une typographie système
(San Francisco sur les appareils Apple). Les cartes de collection et le signe
circulaire de la marque rappellent l’univers du jeu. Un rouge discret ponctue
uniquement les détails décoratifs et les liens au survol.

## Palette

| Usage | Couleur |
| --- | --- |
| Fond gris perle | `#F5F5F7` |
| Surfaces et texte des boutons principaux | `#FFFFFF` |
| Surfaces secondaires | `#F0F0F2` |
| Texte principal et boutons | `#1D1D1F` |
| Texte secondaire | `#626269` |
| Séparateurs | `#DEDEE3` |
| Détail rouge | `#B3373E` |
| Focus clavier web | `#0066CC` |
| Erreurs | `#A52330` |

Les variables CSS de `src/MyTCG.Web/wwwroot/app.css` définissent la palette web.
Les couleurs natives sont regroupées dans `src/MyTCG.Mobile/MainPage.cs`.
Les surfaces restent claires même lorsque le système préfère un thème sombre.

Les panneaux blancs ont des bordures fines, des angles arrondis et des ombres
légères. Les boutons principaux sont anthracite et les secondaires blancs.
Les champs web utilisent un texte de 16 px pour éviter le zoom automatique sur
iPhone. Les éléments décoratifs ne sont pas annoncés par les lecteurs d’écran.
Le focus clavier est visible et les transitions respectent la préférence de
réduction des animations.

## Vérifications de cette évolution

- Compilation de `MyTCG.Server.slnf` réussie avec le SDK 10.0.402 : zéro erreur,
  zéro avertissement. Cette solution filtrée ne compile pas MAUI.
- Interface Blazor réelle contrôlée dans Chromium à 1440, 768, 390 et 320 px :
  connexion, inscription et dashboard, sans débordement horizontal, y compris
  avec un username long. Préférence système sombre également vérifiée.
- Validation du formulaire d’inscription et focus clavier contrôlés.
- Pour ces vérifications visuelles, les réponses API sont interceptées dans le
  navigateur ; les données d’aperçu ne sont pas ajoutées à l’application. Elles
  ne constituent pas une validation de bout en bout de l’authentification.
- Contrastes calculés : texte principal sur blanc 16,83:1, texte secondaire sur
  gris perle 5,56:1, texte secondaire sur blanc 6,05:1, erreurs sur leur fond
  6,86:1. Ces paires dépassent le minimum WCAG AA de 4,5:1 pour du texte normal.
- Le rendu natif Android/iOS reste à vérifier sur simulateur avec les workloads
  MAUI et les SDK des plateformes.
