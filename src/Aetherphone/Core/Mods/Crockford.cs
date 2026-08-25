namespace Aetherphone.Core.Mods;

internal static class Crockford
{
    private const string Alphabet = "0123456789abcdefghjkmnpqrstvwxyz";
    private const int GuidBytes = 16;
    private const int EncodedLength = 26;

    public static string Encode(Guid guid)
    {
        return string.Create(EncodedLength, guid, static (output, value) =>
        {
            Span<byte> raw = stackalloc byte[GuidBytes];
            var text = value.ToString("N");
            for (var index = 0; index < GuidBytes; index++)
            {
                raw[index] = (byte)((Nibble(text[index * 2]) << 4) | Nibble(text[index * 2 + 1]));
            }

            var buffer = 0;
            var bits = 0;
            var written = 0;
            for (var index = 0; index < GuidBytes; index++)
            {
                buffer = (buffer << 8) | raw[index];
                bits += 8;
                while (bits >= 5)
                {
                    bits -= 5;
                    output[written++] = Alphabet[(buffer >> bits) & 31];
                }
            }

            if (bits > 0)
            {
                output[written] = Alphabet[(buffer << (5 - bits)) & 31];
            }
        });
    }

    private static int Nibble(char character)
    {
        if (character >= '0' && character <= '9')
        {
            return character - '0';
        }

        if (character >= 'a' && character <= 'f')
        {
            return character - 'a' + 10;
        }

        return character - 'A' + 10;
    }
}
