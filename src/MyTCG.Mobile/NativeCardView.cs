using Microsoft.Maui.Controls;
using MyTCG.Client;
using MyTCG.Contracts;

namespace MyTCG.Mobile;
public sealed class NativeCardView : Border
{
    public NativeCardView(AuthClient auth, CardDto card, string suffix = "")
    {
        var colors = new[]
        {
            "#A7ADB7",
            "#59A37A",
            "#5D89C6",
            "#9C6AC7",
            "#C49B40"
        };
        Stroke = Color.FromArgb(colors[card.Rarity]);
        Padding = 14;
        var image = new Image
        {
            HeightRequest = 180,
            Aspect = Aspect.AspectFill
        };
        var placeholder = new Label
        {
            Text = "✦",
            FontSize = 70,
            HorizontalTextAlignment = TextAlignment.Center
        };
        Content = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                placeholder,
                image,
                new Label
                {
                    Text = $"#{card.CardNumber} · {card.Name} {suffix}",
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold
                },
                new Label
                {
                    Text = $"{new[] { "Commune", "Peu commune", "Rare", "Épique", "Légendaire" }[card.Rarity]} · Coût d’attaque {card.AttackCost} · Coût de retrait {card.RetreatCost} · {(card.CardType == 0 ? $"⚔ {card.Attack} / ◇ {card.Defense}" : card.CardType == 1 ? "Effet" : card.CardType == 2 ? "Équipement" : "Type inconnu")}"},
                new Label
                {
                    Text = card.Description
                }
            }
        };
        image.IsVisible = false;
        _ = Load();
        async Task Load()
        {
            try
            {
                var url = await auth.Image(card.TcgId, card.ImageId);
                if (string.IsNullOrWhiteSpace(url))
                    return;
                if (url.StartsWith("data:image/png;base64,", StringComparison.Ordinal))
                {
                    var bytes = Convert.FromBase64String(url[22..]);
                    image.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
                }
                else
                    image.Source = ImageSource.FromUri(new Uri(url));
                image.IsVisible = true;
                placeholder.IsVisible = false;
            }
            catch (Exception e)when (e is ClientException or HttpRequestException or UriFormatException)
            {
            }
        }
    }
}

