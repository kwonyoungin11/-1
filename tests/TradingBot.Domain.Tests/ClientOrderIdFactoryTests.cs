using TradingBot.Domain;

namespace TradingBot.Domain.Tests;

public class ClientOrderIdFactoryTests
{
    private static readonly DateTimeOffset FixedUtc =
        DateTimeOffset.Parse("2026-07-09T16:00:00Z");

    [Fact]
    public void Create_is_deterministic_for_same_inputs()
    {
        var a = ClientOrderIdFactory.Create("AAPL", "BUY", FixedUtc);
        var b = ClientOrderIdFactory.Create("AAPL", "BUY", FixedUtc);

        Assert.Equal(a, b);
        Assert.False(string.IsNullOrWhiteSpace(a));
        ClientOrderIdFactory.Validate(a);
    }

    [Fact]
    public void Create_changes_with_symbol_side_or_time()
    {
        var baseId = ClientOrderIdFactory.Create("AAPL", "BUY", FixedUtc);
        var otherSymbol = ClientOrderIdFactory.Create("MSFT", "BUY", FixedUtc);
        var otherSide = ClientOrderIdFactory.Create("AAPL", "SELL", FixedUtc);
        var otherTime = ClientOrderIdFactory.Create("AAPL", "BUY", FixedUtc.AddSeconds(1));

        Assert.NotEqual(baseId, otherSymbol);
        Assert.NotEqual(baseId, otherSide);
        Assert.NotEqual(baseId, otherTime);
    }

    [Fact]
    public void Create_includes_prefix_and_sanitized_symbol()
    {
        var id = ClientOrderIdFactory.Create("aapl", "buy", FixedUtc);

        Assert.StartsWith(ClientOrderIdFactory.Prefix + "-", id, StringComparison.Ordinal);
        Assert.Contains("AAPL", id, StringComparison.Ordinal);
        Assert.Contains("-B-", id, StringComparison.Ordinal);
        Assert.True(id.Length <= ClientOrderIdFactory.MaxLength);
    }

    [Fact]
    public void Create_strips_non_alnum_from_symbol()
    {
        var id = ClientOrderIdFactory.Create("BRK.B", "SELL", FixedUtc);

        Assert.Contains("BRKB", id, StringComparison.Ordinal);
        Assert.DoesNotContain(".", id, StringComparison.Ordinal);
        ClientOrderIdFactory.Validate(id);
    }

    [Fact]
    public void Create_handles_empty_symbol_with_placeholder()
    {
        var id = ClientOrderIdFactory.Create("   ", "BUY", FixedUtc);

        Assert.Contains("UNK", id, StringComparison.Ordinal);
        ClientOrderIdFactory.Validate(id);
    }

    [Fact]
    public void CreateUnique_parameterless_is_always_non_empty_and_valid()
    {
        var ids = Enumerable.Range(0, 20).Select(_ => ClientOrderIdFactory.CreateUnique()).ToArray();

        Assert.All(ids, id =>
        {
            Assert.False(string.IsNullOrWhiteSpace(id));
            Assert.True(id.Length <= ClientOrderIdFactory.MaxLength);
            ClientOrderIdFactory.Validate(id);
            Assert.True(ClientOrderIdFactory.IsValid(id));
        });

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void CreateUnique_with_context_is_unique_and_valid()
    {
        var a = ClientOrderIdFactory.CreateUnique("AAPL", "BUY", FixedUtc);
        var b = ClientOrderIdFactory.CreateUnique("AAPL", "BUY", FixedUtc);

        Assert.NotEqual(a, b);
        Assert.StartsWith(ClientOrderIdFactory.Prefix + "-", a, StringComparison.Ordinal);
        Assert.StartsWith(ClientOrderIdFactory.Prefix + "-", b, StringComparison.Ordinal);
        ClientOrderIdFactory.Validate(a);
        ClientOrderIdFactory.Validate(b);
    }

    [Fact]
    public void CreateUnique_with_null_context_still_non_empty()
    {
        var id = ClientOrderIdFactory.CreateUnique(null, null, FixedUtc);

        Assert.False(string.IsNullOrWhiteSpace(id));
        ClientOrderIdFactory.Validate(id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void ValidateNonEmpty_rejects_blank(string? value)
    {
        Assert.Throws<ArgumentException>(() => ClientOrderIdFactory.ValidateNonEmpty(value));
        Assert.Throws<ArgumentException>(() => ClientOrderIdFactory.Validate(value));
        Assert.False(ClientOrderIdFactory.IsValid(value));
        Assert.False(ClientOrderIdFactory.TryValidate(value, out var normalized));
        Assert.Null(normalized);
    }

    [Fact]
    public void Validate_rejects_too_long()
    {
        var tooLong = new string('a', ClientOrderIdFactory.MaxLength + 1);
        Assert.Throws<ArgumentException>(() => ClientOrderIdFactory.Validate(tooLong));
        Assert.False(ClientOrderIdFactory.IsValid(tooLong));
    }

    [Theory]
    [InlineData("bad id")]
    [InlineData("bad.id")]
    [InlineData("id/with/slash")]
    [InlineData("한글")]
    public void Validate_rejects_illegal_characters(string value)
    {
        Assert.Throws<ArgumentException>(() => ClientOrderIdFactory.Validate(value));
        Assert.False(ClientOrderIdFactory.IsValid(value));
    }

    [Theory]
    [InlineData("my-order-001")]
    [InlineData("cand-AAPL-B-1")]
    [InlineData("abc_DEF-012")]
    [InlineData("a")]
    public void Validate_accepts_toss_compliant_ids(string value)
    {
        ClientOrderIdFactory.Validate(value);
        ClientOrderIdFactory.ValidateNonEmpty(value);
        Assert.True(ClientOrderIdFactory.IsValid(value));
        Assert.True(ClientOrderIdFactory.TryValidate(value, out var normalized));
        Assert.Equal(value, normalized);
    }

    [Fact]
    public void Validate_accepts_max_length_boundary()
    {
        var exact = new string('x', ClientOrderIdFactory.MaxLength);
        ClientOrderIdFactory.Validate(exact);
        Assert.True(ClientOrderIdFactory.IsValid(exact));
    }

    [Fact]
    public void MaxLength_matches_toss_openapi_limit()
    {
        Assert.Equal(36, ClientOrderIdFactory.MaxLength);
    }
}
