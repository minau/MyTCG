using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using MyTCG.Client;
using MyTCG.Contracts;

namespace MyTCG.Mobile;
// Native MAUI views share typed contracts and authenticated HTTP transport with Blazor.
public sealed class TcgPage(AuthClient auth) : ContentPage
{
    private readonly VerticalStackLayout body = new()
    {
        Padding = 20,
        Spacing = 14
    };
    private readonly Label notice = new()
    {
        TextColor = Colors.DarkRed
    };
    private TcgDto? current;
    private string tab = "Collection";
    private bool busy;
    private TcgDto[] mine = [];
    private CollectionItem[] collection = [];
    private CardDto[] cards = [];
    private string Prefix => $"api/tcgs/{current!.Id}/";

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Title = "Mes univers";
        Content = new ScrollView
        {
            Content = body
        };
        await Run(async () =>
        {
            mine = await auth.Request<TcgDto[]>(HttpMethod.Get, "api/tcgs/mine");
            current = mine.FirstOrDefault();
            await Render();
        });
    }

    private Button Button(string text, Func<Task> action)
    {
        var b = new Button
        {
            Text = text
        };
        b.Clicked += async (_, _) => await Run(action);
        return b;
    }

    private async Task Run(Func<Task> action)
    {
        if (busy)
            return;
        busy = true;
        body.IsEnabled = false;
        notice.Text = "";
        try
        {
            await action();
        }
        catch (Exception e)when (e is ClientException or HttpRequestException or IOException)
        {
            notice.Text = e.Message;
        }
        finally
        {
            busy = false;
            body.IsEnabled = true;
            if (!body.Children.Contains(notice))
                body.Add(notice);
        }
    }

    private static Entry Field(string text, string? value = null) => new()
    {
        Placeholder = text,
        Text = value
    };
    private void Heading(string text) => body.Add(new Label { Text = text, FontSize = 24, FontAttributes = FontAttributes.Bold });
    private Task Switch()
    {
        var original = Content!;
        Content = null;
        var overlay = new Grid();
        overlay.Add(original);
        var backdrop = new Button
        {
            BackgroundColor = Color.FromArgb("#66000000"),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        overlay.Add(backdrop);
        var panel = new VerticalStackLayout
        {
            BackgroundColor = Colors.White,
            Padding = 20,
            Spacing = 10,
            WidthRequest = Math.Min(340, Width * 0.9),
            HorizontalOptions = LayoutOptions.End
        };
        void Close()
        {
            overlay.Remove(original);
            Content = original;
        }

        backdrop.Clicked += (_, _) => Close();
        panel.Add(Button("Fermer ×", () =>
        {
            Close();
            return Task.CompletedTask;
        }));
        panel.Add(new Label { Text = "Mes TCG", FontSize = 24 });
        foreach (var t in mine)
            panel.Add(Button(t.Name + (t.IsAdmin ? " · Administrateur" : "") + (current?.Id == t.Id ? " ✓" : ""), async () =>
            {
                Close();
                current = t;
                tab = "Collection";
                await Render();
            }));
        panel.Add(Button("Créer un TCG", () =>
        {
            Close();
            Create();
            return Task.CompletedTask;
        }));
        panel.Add(Button("Rejoindre par code", async () =>
        {
            Close();
            var code = await DisplayPromptAsync("Invitation", "Code d’invitation");
            if (code is not null)
            {
                var before = mine.Select(x => x.Id).ToHashSet();
                await auth.Command(HttpMethod.Post, "api/tcgs/join-by-code", new JoinCodeRequest(code));
                mine = await auth.Request<TcgDto[]>(HttpMethod.Get, "api/tcgs/mine");
                current = mine.FirstOrDefault(x => !before.Contains(x.Id)) ?? current;
                await Render();
            }
        }));
        panel.Add(Button("Catalogue public", async () =>
        {
            Close();
            await Discover();
        }));
        overlay.Add(new ScrollView { Content = panel, HorizontalOptions = LayoutOptions.End, BackgroundColor = Colors.White });
        Content = overlay;
        return Task.CompletedTask;
    }

    private async Task Render()
    {
        body.Clear();
        body.Add(Button($"◇ {current?.Name ?? "Mes univers"} ▾", Switch));
        if (current is null)
        {
            Heading("Votre prochain univers vous attend");
            body.Add(Button("Créer un TCG", () =>
            {
                Create();
                return Task.CompletedTask;
            }));
            body.Add(Button("Explorer", Discover));
            return;
        }

        var nav = new HorizontalStackLayout
        {
            Spacing = 4
        };
        foreach (var name in current.IsAdmin ? new[]
        {
            "Collection",
            "Boosters",
            "Decks",
            "Combat",
            "Studio"
        }

        : new[]
        {
            "Collection",
            "Boosters",
            "Decks",
            "Combat"
        }

        )
            nav.Add(Button(name, async () =>
            {
                tab = name;
                await Render();
            }));
        body.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = nav });
        Heading(tab);
        if (tab == "Combat")
        {
            body.Add(new Label { Text = "Le combat arrive prochainement." });
            return;
        }

        cards = await auth.Request<CardDto[]>(HttpMethod.Get, Prefix + "cards");
        collection = await auth.Request<CollectionItem[]>(HttpMethod.Get, Prefix + "collection");
        if (tab is "Collection" or "Studio")
        {
            if (tab == "Studio")
            {
                body.Add(Button("Créer une carte", () =>
                {
                    Editor(null);
                    return Task.CompletedTask;
                }));
                if (current.Visibility == 1)
                    body.Add(Button("Invitation", async () =>
                    {
                        var r = await auth.Request<CodeResponse>(HttpMethod.Get, Prefix + "invitation");
                        await DisplayAlertAsync("Code d’invitation", r.Code, "Fermer");
                    }));
                if (current.Visibility == 1)
                    body.Add(Button("Régénérer l’invitation", async () =>
                    {
                        var r = await auth.Request<CodeResponse>(HttpMethod.Post, Prefix + "invitation/regenerate");
                        await DisplayAlertAsync("Nouveau code", r.Code, "Fermer");
                    }));
            }
            else
                body.Add(new Label { Text = $"{collection.Count(x => x.Quantity > 0)} / {collection.Length} découvertes · {(collection.Length == 0 ? 0 : 100.0 * collection.Count(x => x.Quantity > 0) / collection.Length):0.0} % · {collection.Sum(x => x.Quantity)} exemplaires" });
            var search = Field("Rechercher une carte");
            body.Add(search);
            var rarity = new Picker
            {
                Title = "Rareté",
                ItemsSource = new[]
                {
                    "Toutes",
                    "Commune",
                    "Peu commune",
                    "Rare",
                    "Épique",
                    "Légendaire"
                },
                SelectedIndex = 0
            };
            body.Add(rarity);
            var type = new Picker
            {
                Title = "Type",
                ItemsSource = new[]
                {
                    "Tous",
                    "Personnage",
                    "Action"
                },
                SelectedIndex = 0
            };
            body.Add(type);
            var sort = new Picker
            {
                Title = "Tri",
                ItemsSource = new[]
                {
                    "Numéro",
                    "Nom",
                    "Rareté"
                },
                SelectedIndex = 0
            };
            body.Add(sort);
            var all = new Switch();
            if (tab == "Collection")
            {
                body.Add(new Label { Text = "Afficher les cartes non possédées" });
                body.Add(all);
            }

            var list = new VerticalStackLayout
            {
                Spacing = 14
            };
            body.Add(list);
            void Fill()
            {
                list.Clear();
                var filtered = cards.Where(c => c.Name.Contains(search.Text ?? "", StringComparison.OrdinalIgnoreCase) && (rarity.SelectedIndex == 0 || c.Rarity == rarity.SelectedIndex - 1) && (type.SelectedIndex == 0 || c.CardType == type.SelectedIndex - 1) && (tab == "Studio" || all.IsToggled || collection.Any(x => x.Card.Id == c.Id && x.Quantity > 0)));
                filtered = sort.SelectedIndex == 1 ? filtered.OrderBy(x => x.Name) : sort.SelectedIndex == 2 ? filtered.OrderByDescending(x => x.Rarity) : filtered.OrderBy(x => x.CardNumber);
                foreach (var c in filtered)
                {
                    var q = collection.FirstOrDefault(x => x.Card.Id == c.Id)?.Quantity ?? 0;
                    var visual = Card(c, tab == "Collection" ? (q == 0 ? "Non découverte" : $"× {q}") : "");
                    if (tab == "Collection" && q == 0)
                        visual.Opacity = 0.45;
                    list.Add(visual);
                    list.Add(Button(tab == "Studio" ? "Modifier" : "Détails", async () =>
                    {
                        if (tab == "Studio")
                            Editor(c);
                        else
                            await DisplayAlertAsync(c.Name, c.Description + $"\nCoût {c.ActionCost} · Attaque {c.Attack} · Défense {c.Defense}", "Fermer");
                    }));
                    if (tab == "Studio")
                        list.Add(Button(c.IsActive ? "Archiver" : "Activer", async () =>
                        {
                            await auth.Command(HttpMethod.Patch, Prefix + $"cards/{c.Id}/status", new StatusRequest(!c.IsActive));
                            await Render();
                        }));
                }

                if (list.Count == 0)
                    list.Add(new Label { Text = "Aucune carte à afficher." });
            }

            search.TextChanged += (_, _) => Fill();
            rarity.SelectedIndexChanged += (_, _) => Fill();
            type.SelectedIndexChanged += (_, _) => Fill();
            sort.SelectedIndexChanged += (_, _) => Fill();
            all.Toggled += (_, _) => Fill();
            Fill();
        }

        if (tab == "Boosters")
        {
            var status = await auth.Request<BoosterStatusDto>(HttpMethod.Get, Prefix + "boosters/status");
            body.Add(new Label { Text = $"{status.Remaining} / {status.DailyLimit} boosters · Renouvellement {status.ResetsAt:u}" });
            var key = Guid.NewGuid().ToString();
            var open = Button("Ouvrir un booster", async () =>
            {
                var result = await auth.Request<OpeningDto>(HttpMethod.Post, Prefix + "boosters/open", new OpenBoosterRequest(key));
                await Render();
                Heading("Votre booster — cartes enregistrées");
                var reveal = new VerticalStackLayout();
                body.Add(reveal);
                var position = 0;
                void Next()
                {
                    if (position < 5)
                    {
                        reveal.Add(Card(result.Cards[position], position == 4 ? "✦ Cinquième carte" : ""));
                        position++;
                    }
                }

                body.Add(Button("Révéler la suivante", () =>
                {
                    Next();
                    return Task.CompletedTask;
                }));
                body.Add(Button("Tout révéler", () =>
                {
                    while (position < 5)
                        Next();
                    return Task.CompletedTask;
                }));
            });
            open.IsEnabled = status.Remaining > 0;
            body.Add(open);
            foreach (var o in await auth.Request<OpeningDto[]>(HttpMethod.Get, Prefix + "boosters/history"))
                body.Add(new Label { Text = $"{o.OpenedAt:g} · {string.Join(", ", o.Cards.Select(x => x.Name))}" });
        }

        if (tab == "Decks")
        {
            body.Add(Button("Créer un deck", () =>
            {
                DeckEditor(null);
                return Task.CompletedTask;
            }));
            var decks = await auth.Request<DeckDto[]>(HttpMethod.Get, Prefix + "decks");
            if (decks.Length == 0)
                body.Add(new Label { Text = "Aucun deck créé." });
            foreach (var d in decks)
            {
                Heading(d.Name + (d.IsPrimary ? " ★" : ""));
                body.Add(new Label { Text = $"{d.Cards.Sum(x => x.Quantity)} / 16 · {(d.IsValid ? "Valide" : "Incomplet")}" });
                body.Add(Button("Modifier / renommer", () =>
                {
                    DeckEditor(d);
                    return Task.CompletedTask;
                }));
                body.Add(Button("Définir principal", async () =>
                {
                    await auth.Command(HttpMethod.Put, Prefix + $"decks/{d.Id}/primary");
                    await Render();
                }));
                body.Add(Button("Supprimer", async () =>
                {
                    if (await DisplayAlertAsync("Supprimer ce deck ?", d.Name, "Supprimer", "Annuler"))
                    {
                        await auth.Command(HttpMethod.Delete, Prefix + $"decks/{d.Id}");
                        await Render();
                    }
                }));
            }
        }
    }

    private View Card(CardDto c, string suffix = "") => new NativeCardView(auth, c, suffix);
    private void Create()
    {
        body.Clear();
        Heading("Créer un univers");
        var name = Field("Nom");
        var description = Field("Description");
        var cover = Field("Image HTTPS");
        var visibility = new Picker
        {
            ItemsSource = new[]
            {
                "Public",
                "Privé"
            },
            SelectedIndex = 0
        };
        body.Add(name);
        body.Add(description);
        body.Add(cover);
        body.Add(visibility);
        body.Add(Button("Créer", async () =>
        {
            current = await auth.Request<TcgDto>(HttpMethod.Post, "api/tcgs", new TcgRequest(name.Text ?? "", description.Text ?? "", visibility.SelectedIndex, string.IsNullOrWhiteSpace(cover.Text) ? null : cover.Text));
            mine = await auth.Request<TcgDto[]>(HttpMethod.Get, "api/tcgs/mine");
            await Render();
        }));
        body.Add(Button("Retour", Render));
    }

    private async Task Discover()
    {
        body.Clear();
        Heading("Univers publics");
        var search = Field("Recherche par nom");
        body.Add(search);
        body.Add(Button("Rechercher", async () =>
        {
            var list = await auth.Request<TcgDto[]>(HttpMethod.Get, "api/tcgs/public?search=" + Uri.EscapeDataString(search.Text ?? ""));
            foreach (var t in list)
            {
                Heading(t.Name);
                body.Add(new Label { Text = $"{t.Description} · {t.Members} membres" });
                body.Add(Button("Rejoindre", async () =>
                {
                    await auth.Command(HttpMethod.Post, $"api/tcgs/{t.Id}/join");
                    mine = await auth.Request<TcgDto[]>(HttpMethod.Get, "api/tcgs/mine");
                    current = mine.Single(x => x.Id == t.Id);
                    await Render();
                }));
            }
        }));
        body.Add(Button("Retour", Render));
    }

    private void Editor(CardDto? c)
    {
        body.Clear();
        Heading("Studio — Éditeur");
        var name = Field("Nom", c?.Name);
        var number = Field("Numéro", c?.CardNumber);
        var description = Field("Description", c?.Description);
        var cost = Field("Coût", (c?.ActionCost ?? 1).ToString());
        var attack = Field("Attaque", (c?.Attack ?? 0).ToString());
        var defense = Field("Défense", (c?.Defense ?? 0).ToString());
        var weight = Field("Poids", (c?.DropWeight ?? 100).ToString());
        var image = Field("Image HTTPS ou média", c?.ImageUrl);
        var type = new Picker
        {
            ItemsSource = new[]
            {
                "Personnage",
                "Action"
            },
            SelectedIndex = c?.CardType ?? 0
        };
        var rarity = new Picker
        {
            ItemsSource = new[]
            {
                "Commune",
                "Peu commune",
                "Rare",
                "Épique",
                "Légendaire"
            },
            SelectedIndex = c?.Rarity ?? 0
        };
        foreach (var view in new View[]
        {
            name,
            number,
            description,
            type,
            rarity,
            cost,
            attack,
            defense,
            weight,
            image
        }

        )
            body.Add(view);
        var preview = new ContentView();
        body.Add(preview);
        void Preview()
        {
            _ = int.TryParse(cost.Text, out var co);
            _ = int.TryParse(attack.Text, out var at);
            _ = int.TryParse(defense.Text, out var de);
            preview.Content = Card(new CardDto(c?.Id ?? Guid.Empty, name.Text ?? "", description.Text ?? "", number.Text ?? "", Math.Max(0, type.SelectedIndex), Math.Max(0, rarity.SelectedIndex), co, at, de, 1, image.Text, true));
        }

        foreach (var field in new[]
        {
            name,
            description,
            number,
            cost,
            attack,
            defense,
            image
        }

        )
            field.TextChanged += (_, _) => Preview();
        type.SelectedIndexChanged += (_, _) => Preview();
        rarity.SelectedIndexChanged += (_, _) => Preview();
        Preview();
        body.Add(Button("Importer une image", async () =>
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions { FileTypes = FilePickerFileType.Images });
            if (file is null)
                return;
            await using var stream = await file.OpenReadAsync();
            var mime = Path.GetExtension(file.FileName).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
            image.Text = await auth.Upload(Prefix + "media", stream, mime);
        }));
        body.Add(Button("Enregistrer", async () =>
        {
            if (!int.TryParse(cost.Text, out var co) || !int.TryParse(attack.Text, out var at) || !int.TryParse(defense.Text, out var de) || !double.TryParse(weight.Text, out var w))
                throw new ClientException("Valeurs numériques invalides.");
            await auth.Request<CardDto>(c is null ? HttpMethod.Post : HttpMethod.Put, Prefix + "cards" + (c is null ? "" : $"/{c.Id}"), new CardRequest(name.Text ?? "", description.Text ?? "", number.Text ?? "", type.SelectedIndex, rarity.SelectedIndex, co, type.SelectedIndex == 0 ? at : null, type.SelectedIndex == 0 ? de : null, w, string.IsNullOrWhiteSpace(image.Text) ? null : image.Text, c?.IsActive ?? true));
            await Render();
        }));
        body.Add(Button("Annuler", Render));
    }

    private void DeckEditor(DeckDto? d)
    {
        body.Clear();
        Heading("Construire un deck");
        var name = Field("Nom", d?.Name ?? "Nouveau deck");
        body.Add(name);
        var entries = d?.Cards.ToDictionary(x => x.CardId, x => x.Quantity) ?? [];
        var total = new Label();
        body.Add(total);
        void Update() => total.Text = $"{entries.Values.Sum()} / 16 cartes";
        Update();
        var search = Field("Rechercher une carte");
        body.Add(search);
        var cardRows = new List<(string Name, View Card)>();
        search.TextChanged += (_, _) =>
        {
            foreach (var row in cardRows)
                row.Card.IsVisible = row.Name.Contains(search.Text ?? "", StringComparison.OrdinalIgnoreCase);
        };
        foreach (var item in collection.Where(x => x.Quantity > 0))
        {
            var row = new VerticalStackLayout
            {
                Spacing = 8
            };
            body.Add(row);
            var visual = Card(item.Card, $"{item.Quantity} possédés");
            cardRows.Add((item.Card.Name, row));
            row.Add(visual);
            var quantity = new Label
            {
                Text = entries.GetValueOrDefault(item.Card.Id).ToString()
            };
            row.Add(quantity);
            row.Add(Button("+", () =>
            {
                var count = entries.GetValueOrDefault(item.Card.Id);
                if (count < item.Quantity && entries.Values.Sum() < 16)
                    entries[item.Card.Id] = count + 1;
                quantity.Text = entries.GetValueOrDefault(item.Card.Id).ToString();
                Update();
                return Task.CompletedTask;
            }));
            row.Add(Button("−", () =>
            {
                var count = entries.GetValueOrDefault(item.Card.Id);
                if (count <= 1)
                    entries.Remove(item.Card.Id);
                else
                    entries[item.Card.Id] = count - 1;
                quantity.Text = entries.GetValueOrDefault(item.Card.Id).ToString();
                Update();
                return Task.CompletedTask;
            }));
        }

        body.Add(Button("Enregistrer", async () =>
        {
            await auth.Request<DeckDto>(d is null ? HttpMethod.Post : HttpMethod.Put, Prefix + "decks" + (d is null ? "" : $"/{d.Id}"), new DeckRequest(name.Text ?? "", entries.Select(x => new DeckEntry(x.Key, x.Value)).ToArray()));
            await Render();
        }));
        body.Add(Button("Annuler", Render));
    }

    private sealed record CodeResponse(string Code);
}
