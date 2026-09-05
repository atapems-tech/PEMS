using PEMS.Application.Common;

namespace PEMS.Tests.Common;

/// <summary>
/// طبق سند «PEMS — Global Data Grid Standard» بند ۳۳ (Required Tests - Pagination):
/// Default page size = 50، اندازه‌های مجاز ۲۰/۵۰/۱۰۰، و بازگشت امن هر مقدار نامعتبر به ۵۰.
/// </summary>
public class DataGridPageSizeTests
{
    [Theory]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public void Normalize_AllowedValue_ReturnsSameValue(int allowed)
    {
        Assert.Equal(allowed, DataGridPageSize.Normalize(allowed));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(25)]
    [InlineData(30)]
    [InlineData(200)]
    [InlineData(999999)]
    public void Normalize_InvalidValue_FallsBackToDefault(int invalid)
    {
        Assert.Equal(DataGridPageSize.Default, DataGridPageSize.Normalize(invalid));
    }

    [Fact]
    public void Normalize_NullValue_FallsBackToDefault()
    {
        Assert.Equal(DataGridPageSize.Default, DataGridPageSize.Normalize(null));
    }

    [Fact]
    public void Default_Is50()
    {
        Assert.Equal(50, DataGridPageSize.Default);
    }

    [Fact]
    public void Allowed_IsExactly20_50_100()
    {
        Assert.Equal(new[] { 20, 50, 100 }, DataGridPageSize.Allowed);
    }
}
