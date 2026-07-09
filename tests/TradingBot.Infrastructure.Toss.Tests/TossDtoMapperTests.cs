using System.Text.Json;
using TradingBot.Infrastructure.Toss;
using TradingBot.Infrastructure.Toss.Dto;

namespace TradingBot.Infrastructure.Toss.Tests;

public class TossDtoMapperTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static T Load<T>(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
               ?? throw new InvalidOperationException($"Failed to deserialize {name}");
    }

    [Fact]
    public void Maps_token_response_without_exposing_in_type_name()
    {
        var dto = Load<OAuth2TokenResponseDto>("oauth_token.json");
        var token = TossDtoMapper.MapToken(dto);
        Assert.Equal("Bearer", token.TokenType);
        Assert.False(string.IsNullOrEmpty(token.AccessToken));
        var masked = new DomainTossRedactor().MaskToken(token.AccessToken);
        Assert.DoesNotContain(token.AccessToken, masked, StringComparison.Ordinal);
    }

    [Fact]
    public void Maps_accounts_with_masking()
    {
        var dto = Load<AccountsResponseDto>("accounts.json");
        var accounts = TossDtoMapper.MapAccounts(dto, new DomainTossRedactor());
        Assert.Single(accounts);
        Assert.Equal("1", accounts[0].AccountSeq);
        Assert.DoesNotContain("1234567890", accounts[0].AccountNoMasked, StringComparison.Ordinal);
        Assert.Contains("7890", accounts[0].AccountNoMasked, StringComparison.Ordinal);
    }

    [Fact]
    public void Maps_holdings_and_prices()
    {
        var holdings = TossDtoMapper.MapHoldings(Load<HoldingsResponseDto>("holdings.json"));
        Assert.Equal("2500.00", holdings.MarketValueUsd);
        Assert.Equal("AAPL", holdings.Items[0].Symbol);
        Assert.Equal(5m, holdings.Items[0].Quantity);

        var prices = TossDtoMapper.MapPrices(Load<PricesResponseDto>("prices.json"));
        Assert.Equal("AAPL", prices[0].Symbol);
        Assert.Equal(200m, prices[0].LastPrice);
    }

    [Fact]
    public void Maps_us_calendar()
    {
        var cal = TossDtoMapper.MapUsCalendar(Load<UsMarketCalendarResponseDto>("us_calendar.json"));
        Assert.Equal("2026-07-09", cal.Date);
        Assert.False(cal.IsHolidayOrClosed);
    }

    [Fact]
public void Maps_buying_power()
    {
        var bp = TossDtoMapper.MapBuyingPower(Load<BuyingPowerResponseDto>("buying_power.json"));
        Assert.Equal("USD", bp.Currency);
        Assert.Equal(3500.5m, bp.CashBuyingPower);
    }

    [Fact]
    public void ParseMarketValueUsd_parses_numeric_summary()
    {
        Assert.Equal(2500.00m, TossDtoMapper.ParseMarketValueUsd("2500.00"));
        Assert.Null(TossDtoMapper.ParseMarketValueUsd(null));
        Assert.Null(TossDtoMapper.ParseMarketValueUsd("not-a-number"));
    }

    [Fact]
    public void Maps_candles_newest_first_api_to_chronological_chart_points()
    {
        var points = TossDtoMapper.MapCandles(Load<CandlesResponseDto>("candles.json"));
        Assert.Equal(2, points.Count);
        // API fixture is newest-first; mapper sorts oldest-first for charts.
        Assert.True(points[0].Time < points[1].Time);
        Assert.Equal(185.50, points[0].Open, precision: 4);
        Assert.Equal(185.80, points[0].High, precision: 4);
        Assert.Equal(185.40, points[0].Low, precision: 4);
        Assert.Equal(185.70, points[0].Close, precision: 4);
        Assert.Equal(18400, points[0].Volume, precision: 1);
        Assert.Equal(186.00, points[1].Close, precision: 4);

    }
}
