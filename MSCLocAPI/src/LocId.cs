using System.Security.Cryptography;
using System.Text;

namespace UltimaLoc
{
    /// <summary>
    /// Stable string-id contract, shared byte-for-byte across the toolchain:
    ///   'u' + first 16 hex chars of sha256(utf8(text))
    /// Must match Node's stringId.makeStringId and MscLocTool's MakeId so ids
    /// line up between extract / inject / this runtime patcher.
    /// </summary>
    public static class LocId
    {
        public static string Make(string text)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
                StringBuilder sb = new StringBuilder(17);
                sb.Append('u');
                for (int i = 0; i < 8; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }
}
