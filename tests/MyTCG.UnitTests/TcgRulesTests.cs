using MyTCG.Application;
using MyTCG.Contracts;
using MyTCG.Domain;
using MyTCG.Infrastructure;
using Xunit;

namespace MyTCG.UnitTests;
public sealed class TcgRulesTests
{
    private static CardRequest Valid => new("Hero", "Text", 0, 0, 1, 0, 0, 100, null);

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void InvalidCosts(int cost) => Assert.Throws<TcgException>(() => TcgService.ValidateCard(Valid with { ActionCost = cost }));
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void NegativeStats(int a, int d) => Assert.Throws<TcgException>(() => TcgService.ValidateCard(Valid with { Attack = a, Defense = d }));
    [Fact]
    public void CharacterNeedsStats() => Assert.Throws<TcgException>(() => TcgService.ValidateCard(Valid with { Attack = null }));
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void NonCharacterTypesCanOmitStats(int type) => TcgService.ValidateCard(Valid with { CardType = type, Attack = null, Defense = null });
    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void UnknownCardTypesAreRejected(int type) => Assert.Throws<TcgException>(() => TcgService.ValidateCard(Valid with { CardType = type }));
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidWeights(double weight) => Assert.Throws<TcgException>(() => TcgService.ValidateCard(Valid with { DropWeight = weight }));
    [Fact]
    public void InactiveCardMayHaveZeroWeight() => TcgService.ValidateCard(Valid with { DropWeight = 0, IsActive = false });
    [Fact]
    public void WeightsDetermineExactIntervals()
    {
        var a = new Card
        {
            DropWeight = 100
        };
        var b = new Card
        {
            DropWeight = 50
        };
        var c = new Card
        {
            DropWeight = 10
        };
        Card[] cards = [a, b, c];
        Assert.Same(a, BoosterDrawing.Pick(cards, [1, 1, 1, 1, 1], 0.624));
        Assert.Same(b, BoosterDrawing.Pick(cards, [1, 1, 1, 1, 1], 0.626));
        Assert.Same(c, BoosterDrawing.Pick(cards, [1, 1, 1, 1, 1], 0.94));
    }

    [Fact]
    public void FifthCardImprovesRareProbability()
    {
        var common = new Card
        {
            DropWeight = 100,
            Rarity = Rarity.Common
        };
        var rare = new Card
        {
            DropWeight = 100,
            Rarity = Rarity.Rare
        };
        Card[] cards = [common, rare];
        Assert.Same(common, BoosterDrawing.Pick(cards, [1, 1, 1, 1, 1], 0.3));
        Assert.Same(rare, BoosterDrawing.Pick(cards, [1, 2, 4, 8, 16], 0.3));
    }

    [Fact]
    public void MissingRaritiesStillDrawAvailableCards()
    {
        var only = new Card
        {
            Rarity = Rarity.Common,
            DropWeight = 1
        };
        Assert.Same(only, BoosterDrawing.Pick([only], [1, 2, 4, 8, 16], 0.99));
    }
}

