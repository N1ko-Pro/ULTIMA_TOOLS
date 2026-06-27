using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace UltimaLoc
{
    /// <summary>
    /// Minimal, dependency-free JSON reader — just enough for ULTIMA translation
    /// tables (an object with string fields and an `entries` string→string map).
    ///
    /// We deliberately avoid Newtonsoft.Json: on My Summer Car's stripped Unity
    /// Mono runtime, Json.NET references types missing from the game's System.dll
    /// (e.g. System.ComponentModel.INotifyPropertyChanging) and throws at load.
    /// This parser only touches mscorlib types, so it runs anywhere.
    ///
    /// Parses to: Dictionary&lt;string,object&gt; | List&lt;object&gt; | string |
    /// double | bool | null. Tolerant: returns null / partial on malformed input.
    /// </summary>
    internal static class MiniJson
    {
        public static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int i = 0;
            return ParseValue(json, ref i);
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') i++;
                else break;
            }
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) return null;
            switch (s[i])
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return ParseString(s, ref i);
                case 't': i += 4; return true;   // true
                case 'f': i += 5; return false;  // false
                case 'n': i += 4; return null;   // null
                default:  return ParseNumber(s, ref i);
            }
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var obj = new Dictionary<string, object>();
            i++; // '{'
            while (i < s.Length)
            {
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == '}') { i++; break; }
                if (i >= s.Length || s[i] != '"') break; // malformed
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ':') i++;
                obj[key] = ParseValue(s, ref i);
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; break; }
                break;
            }
            return obj;
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            var arr = new List<object>();
            i++; // '['
            while (i < s.Length)
            {
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ']') { i++; break; }
                arr.Add(ParseValue(s, ref i));
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; break; }
                break;
            }
            return arr;
        }

        private static string ParseString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // opening quote
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c == '\\' && i < s.Length)
                {
                    char e = s[i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 <= s.Length)
                            {
                                int code;
                                if (int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber,
                                        CultureInfo.InvariantCulture, out code))
                                {
                                    sb.Append((char)code);
                                }
                                i += 4;
                            }
                            break;
                        default: sb.Append(e); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static object ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length)
            {
                char c = s[i];
                if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E') i++;
                else break;
            }
            double d;
            double.TryParse(s.Substring(start, i - start), NumberStyles.Any,
                CultureInfo.InvariantCulture, out d);
            return d;
        }
    }
}
