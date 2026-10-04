using System.Net.NetworkInformation;

namespace WolCatcher;

/// <summary>
/// Wake-on-LAN "magic packet" ayrıştırma ve MAC adresi yardımcıları.
///
/// Bir magic packet, 6 bayt 0xFF ile başlar ve ardından hedef MAC adresi (6 bayt)
/// arka arkaya 16 kez tekrarlanır. Toplam çekirdek 102 bayttır. Paket UDP yükünün
/// herhangi bir yerinde gömülü olabileceği için yük baştan sona taranır.
/// </summary>
public static class MagicPacket
{
    private const int MacLen = 6;
    private const int Repeat = 16;
    private const int CoreLen = MacLen + MacLen * Repeat; // 6 + 96 = 102

    /// <summary>
    /// Verilen UDP yükü içinde geçerli bir magic packet arar. Bulunursa hedef
    /// MAC adresini döndürür, yoksa null.
    /// </summary>
    public static byte[]? TryExtractTargetMac(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < CoreLen)
            return null;

        for (int i = 0; i <= payload.Length - CoreLen; i++)
        {
            // 6 bayt 0xFF başlığını ara.
            if (!AllByte(payload.Slice(i, MacLen), 0xFF))
                continue;

            var mac = payload.Slice(i + MacLen, MacLen);

            // MAC'in 16 kez tekrarlandığını doğrula.
            bool valid = true;
            for (int r = 1; r < Repeat; r++)
            {
                if (!mac.SequenceEqual(payload.Slice(i + MacLen + r * MacLen, MacLen)))
                {
                    valid = false;
                    break;
                }
            }

            if (valid)
                return mac.ToArray();
        }

        return null;
    }

    private static bool AllByte(ReadOnlySpan<byte> span, byte value)
    {
        foreach (var b in span)
            if (b != value)
                return false;
        return true;
    }

    /// <summary>MAC baytlarını "AA:BB:CC:DD:EE:FF" biçiminde yazar.</summary>
    public static string Format(byte[] mac) => string.Join(":", mac.Select(b => b.ToString("X2")));

    /// <summary>"AA:BB:CC:DD:EE:FF" / "aa-bb-..." / "aabbcc..." gibi metinleri 6 bayta çevirir.</summary>
    public static byte[]? ParseMac(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var hex = new string(text.Where(Uri.IsHexDigit).ToArray());
        if (hex.Length != 12)
            return null;

        var mac = new byte[MacLen];
        for (int i = 0; i < MacLen; i++)
            mac[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return mac;
    }

    /// <summary>
    /// Bu bilgisayardaki çalışır durumdaki fiziksel adaptörlerin (Ethernet/Wi-Fi)
    /// MAC adreslerini döndürür. Loopback ve tünel adaptörleri hariç tutulur.
    /// </summary>
    public static List<byte[]> GetLocalMacs()
    {
        var result = new List<byte[]>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                continue;

            var addr = nic.GetPhysicalAddress().GetAddressBytes();
            if (addr.Length != MacLen)
                continue;
            if (AllByte(addr, 0x00)) // boş MAC'leri atla
                continue;

            // Aynı MAC birden fazla adaptörde görünebilir; tekrarları engelle.
            if (!result.Any(existing => existing.AsSpan().SequenceEqual(addr)))
                result.Add(addr);
        }
        return result;
    }
}
