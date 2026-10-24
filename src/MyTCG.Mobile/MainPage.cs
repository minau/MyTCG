using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Devices;
using Microsoft.Extensions.DependencyInjection;
using MyTCG.Client;
namespace MyTCG.Mobile;
public sealed class MainPage : ContentPage
{
    private readonly AuthClient auth;
    private readonly VerticalStackLayout content = new() { Spacing = 16, Padding = new Thickness(26, 50, 26, 30) };
    private readonly Label error = new() { TextColor = Color.FromArgb("A52330"), FontSize = 14 };
    private bool registering, initialized;
    private static readonly Color Canvas = Color.FromArgb("F5F5F7");
    private static readonly Color Ink = Color.FromArgb("1D1D1F");
    private static readonly Color Muted = Color.FromArgb("626269");
    private static readonly Color Line = Color.FromArgb("DEDEE3");
    public MainPage(AuthClient auth)
    {
        this.auth = auth;
        BackgroundColor = Canvas; Content = new ScrollView { Content = content };
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (initialized) return;
        initialized = true;
        content.Add(Title("✦ MyTCG")); content.Add(new ActivityIndicator { IsRunning = true, Color = Ink });
        try { await auth.Initialize(); }
        catch (Exception ex) when (ex is ClientException or System.Security.Cryptography.CryptographicException) { error.Text = ex is ClientException ? ex.Message : "Stockage sécurisé indisponible. Réessayez."; }
        Render();
    }
    private void Render()
    {
        content.Clear(); content.Add(Title("✦ MyTCG"));
        if (auth.User is { } u)
        {
            content.Add(Title($"Bienvenue, {u.Username}.", 28));
            content.Add(Text("Votre cercle. Vos cartes. Votre univers."));
            foreach (var name in new[] { "◇ Ma collection", "▱ Mes decks", "⚔ Jouer", "♧ Mes groupes" })
                content.Add(new Border { BackgroundColor = Colors.White, Stroke = Line, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 }, Padding = 22, Content = new VerticalStackLayout { Children = { Title(name, 20), Text("À venir") } } });
            var username = Field("Username", u.Username); var email = Field("Email", u.Email); email.Keyboard = Keyboard.Email;
            content.Add(Title("Mon compte", 22)); content.Add(username); content.Add(email);
            content.Add(ActionButton("Enregistrer", async () => { await auth.UpdateMe(new(username.Text ?? "", email.Text ?? "")); Render(); }));
            content.Add(ActionButton("Déconnexion", async () => { await auth.Logout(); Render(); }));
        }
        else
        {
            content.Add(Title(registering ? "Créer mon compte" : "Ravi de vous retrouver", 28));
            content.Add(Text("Votre prochain univers commence ici."));
            var login = Field(registering ? "Username" : "Email ou username"); var email = Field("Email"); email.Keyboard = Keyboard.Email;
            var password = Field("Mot de passe"); password.IsPassword = true;
            var confirm = Field("Confirmer le mot de passe"); confirm.IsPassword = true;
            content.Add(login); if (registering) content.Add(email); content.Add(password);
            if (registering) { content.Add(Text("12 caractères minimum, majuscule, minuscule, chiffre et caractère spécial.")); content.Add(confirm); }
            content.Add(ActionButton(registering ? "Créer mon compte" : "Connexion", async () => {
                if (registering) { await auth.Register(new(login.Text ?? "", email.Text ?? "", password.Text ?? "", confirm.Text ?? "")); registering = false; Render(); error.Text = "Compte créé. Connectez-vous."; }
                else { await auth.Login(new(login.Text ?? "", password.Text ?? "")); Render(); }
            }));
            content.Add(ActionButton(registering ? "J’ai déjà un compte" : "Créer un compte", () => { registering = !registering; error.Text = ""; Render(); return Task.CompletedTask; }));
            content.Add(ActionButton("Restaurer ma session", async () => { await auth.Initialize(); Render(); }));
            content.Add(Text("Serveur API (HTTPS en production)"));
            var url = Field("https://api.votre-domaine.fr/", ApiConfiguration.BaseUri().ToString()); content.Add(url);
            content.Add(ActionButton("Configurer le serveur", async () => {
                if (!Uri.TryCreate(url.Text, UriKind.Absolute, out var uri) || (uri.Scheme != "https"
#if DEBUG
                    && uri.Scheme != "http"
#endif
                )) throw new ClientException("Saisissez une URL HTTPS valide.");
                // Changing server must never forward another server's refresh secret.
                SecureStorage.Default.Remove("mytcg.refresh");
                Preferences.Default.Set("api.baseUrl", uri.AbsoluteUri.TrimEnd('/') + "/");
                await auth.Reset(); Render();
            }));
        }
        content.Add(error);
    }
    private Button ActionButton(string text, Func<Task> action)
    {
        var b = new Button { Text = text, BackgroundColor = Ink, TextColor = Colors.White, CornerRadius = 12, Padding = 14 };
        b.Clicked += async (_, _) => {
            if (!content.IsEnabled) return;
            content.IsEnabled = false; error.Text = ""; var original = b.Text; b.Text = "Veuillez patienter…";
            try { await action(); }
            catch (ClientException ex) { error.Text = ex.Message; if (auth.User is null) Render(); }
            catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or InvalidOperationException) { error.Text = "Stockage sécurisé ou configuration indisponible. Réessayez."; }
            finally { b.Text = original; content.IsEnabled = true; }
        }; return b;
    }
    private static Entry Field(string placeholder, string? value = "") => new() { Placeholder = placeholder, Text = value, TextColor = Ink, PlaceholderColor = Muted, BackgroundColor = Colors.White, MaxLength = 254 };
    private static Label Title(string value, int size = 34) => new() { Text = value, FontSize = size, FontAttributes = FontAttributes.Bold, TextColor = Ink };
    private static Label Text(string value) => new() { Text = value, FontSize = 14, TextColor = Muted };
}
