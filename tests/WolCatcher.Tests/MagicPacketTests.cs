using Xunit;

namespace WolCatcher.Tests;

public class MagicPacketTests
{
    private static readonly byte[] Mac = { 0xAA, 0xBB, 0xCC, 0x11, 0x22, 0x33 };

    private static byte[] Build(byte[] mac, byte[]? prefix = null, byte[]? suffix = null)
    {
        var bytes = new List<byte>();
        if (prefix is not null)
            bytes.AddRange(prefix);
        bytes.AddRange(Enumerable.Repeat((byte)0xFF, 6));
        for (int i = 0; i < 16; i++)
            bytes.AddRange(mac);
        if (suffix is not null)
            bytes.AddRange(suffix);
        return bytes.ToArray();
    }

    [Fact]
    public void Duz_paketten_mac_okunur()
    {
        Assert.Equal(Mac, MagicPacket.TryExtractTargetMac(Build(Mac)));
    }

    [Fact]
    public void Basta_fazladan_bayt_ve_sonda_secureon_sifresi_olsa_da_bulunur()
    {
        var packet = Build(Mac, prefix: new byte[] { 0x01, 0x02, 0x03 }, suffix: new byte[6]);
        Assert.Equal(Mac, MagicPacket.TryExtractTargetMac(packet));
    }

    [Fact]
    public void Yedi_ff_ile_baslayan_paket_de_bulunur()
    {
        var packet = Build(Mac, prefix: new byte[] { 0xFF });
        Assert.Equal(Mac, MagicPacket.TryExtractTargetMac(packet));
    }

    [Fact]
    public void Tekrarlardan_biri_bozuksa_paket_reddedilir()
    {
        var packet = Build(Mac);
        packet[6 + 6 * 10 + 2] ^= 0x01;
        Assert.Null(MagicPacket.TryExtractTargetMac(packet));
    }

    [Fact]
    public void Kisa_paket_reddedilir()
    {
        Assert.Null(MagicPacket.TryExtractTargetMac(Build(Mac)[..101]));
    }

    [Theory]
    [InlineData("AA:BB:CC:11:22:33")]
    [InlineData("aa-bb-cc-11-22-33")]
    [InlineData("aabbcc112233")]
    public void Yaygin_mac_bicimleri_okunur(string text)
    {
        Assert.Equal(Mac, MagicPacket.ParseMac(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("AA:BB:CC")]
    [InlineData("zz:zz:zz:zz:zz:zz")]
    public void Gecersiz_mac_reddedilir(string text)
    {
        Assert.Null(MagicPacket.ParseMac(text));
    }

    [Fact]
    public void Mac_iki_nokta_ile_yazilir()
    {
        Assert.Equal("AA:BB:CC:11:22:33", MagicPacket.Format(Mac));
    }
}
