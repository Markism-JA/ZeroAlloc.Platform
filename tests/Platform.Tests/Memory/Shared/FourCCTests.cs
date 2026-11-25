using Platform.Memory.Shared;

namespace Platform.Tests.Memory.Shared;

public class FourCCTests
{
    [Fact]
    public void Make_CharsAndSpan_ProduceIdenticalLittleEndianCode()
    {
        uint fromChars = FourCC.Make('E', 'N', 'G', 'N');
        uint fromSpan = FourCC.Make("ENGN");

        Assert.Equal(fromChars, fromSpan);
        Assert.Equal("ENGN", FourCC.ToString(fromChars));
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("ABC")]
    [InlineData("ABCDE")]
    public void Make_InvalidLength_ThrowsArgumentException(string invalid)
    {
        Assert.Throws<ArgumentException>(() => FourCC.Make(invalid));
    }

    [Fact]
    public void RoundTrip_DistinctTags_AreUnique()
    {
        uint a = FourCC.Make("META");
        uint b = FourCC.Make("STAT");
        uint c = FourCC.Make("RING");

        Assert.NotEqual(a, b);
        Assert.NotEqual(b, c);
        Assert.Equal("META", FourCC.ToString(a));
        Assert.Equal("STAT", FourCC.ToString(b));
        Assert.Equal("RING", FourCC.ToString(c));
    }
}
