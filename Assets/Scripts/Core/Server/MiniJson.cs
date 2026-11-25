using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Kezor.Core.Server
{
    /// <summary>
    /// JSON mínimo para os arquivos de save do servidor (o Core não depende de pacotes):
    /// objeto = Dictionary&lt;string, object?&gt;, lista = List&lt;object?&gt;, números = long ou double, string, bool, null.
    /// </summary>
    public static class MiniJson
    {
        public static string Write(object? value, bool pretty = true)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value, pretty, 0);
            return sb.ToString();
        }

        private static void Indent(StringBuilder sb, bool pretty, int level)
        {
            if (!pretty) return;
            sb.Append('\n').Append(' ', level * 2);
        }

        private static void WriteValue(StringBuilder sb, object? v, bool pretty, int level)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case string s: WriteString(sb, s); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case float f: sb.Append(((double)f).ToString("R", CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
                case IDictionary<string, object?> obj:
                {
                    sb.Append('{');
                    bool first = true;
                    foreach (var kv in obj)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        Indent(sb, pretty, level + 1);
                        WriteString(sb, kv.Key);
                        sb.Append(pretty ? ": " : ":");
                        WriteValue(sb, kv.Value, pretty, level + 1);
                    }
                    if (!first) Indent(sb, pretty, level);
                    sb.Append('}');
                    break;
                }
                case System.Collections.IEnumerable list:
                {
                    sb.Append('[');
                    bool first = true;
                    foreach (var item in list)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        Indent(sb, pretty, level + 1);
                        WriteValue(sb, item, pretty, level + 1);
                    }
                    if (!first) Indent(sb, pretty, level);
                    sb.Append(']');
                    break;
                }
                default: WriteString(sb, Convert.ToString(v, CultureInfo.InvariantCulture) ?? ""); break;
            }
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var ch in s)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < 0x20) sb.Append("\\u").Append(((int)ch).ToString("x4"));
                        else sb.Append(ch);
                        break;
                }
            }
            sb.Append('"');
        }

        public static object? Read(string json)
        {
            int i = 0;
            var v = ReadValue(json, ref i);
            SkipWs(json, ref i);
            if (i != json.Length) throw new FormatException("JSON: texto sobrando");
            return v;
        }

        private static void SkipWs(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        private static object? ReadValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON: fim inesperado");
            char c = s[i];
            if (c == '{')
            {
                i++;
                var obj = new Dictionary<string, object?>();
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == '}') { i++; return obj; }
                while (true)
                {
                    SkipWs(s, ref i);
                    var key = ReadString(s, ref i);
                    SkipWs(s, ref i);
                    if (i >= s.Length || s[i] != ':') throw new FormatException("JSON: esperava ':'");
                    i++;
                    obj[key] = ReadValue(s, ref i);
                    SkipWs(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == '}') { i++; return obj; }
                    throw new FormatException("JSON: esperava ',' ou '}'");
                }
            }
            if (c == '[')
            {
                i++;
                var list = new List<object?>();
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ']') { i++; return list; }
                while (true)
                {
                    list.Add(ReadValue(s, ref i));
                    SkipWs(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == ']') { i++; return list; }
                    throw new FormatException("JSON: esperava ',' ou ']'");
                }
            }
            if (c == '"') return ReadString(s, ref i);
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            var num = s.Substring(start, i - start);
            if (num.Length == 0) throw new FormatException("JSON: valor inválido");
            if (num.IndexOfAny(new[] { '.', 'e', 'E' }) < 0 && long.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return l;
            return double.Parse(num, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static string ReadString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') throw new FormatException("JSON: esperava string");
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }
            throw new FormatException("JSON: string sem fim");
        }

        // Ajudantes de leitura
        public static long L(this IDictionary<string, object?> o, string k, long def = 0) => o.TryGetValue(k, out var v) && v != null ? Convert.ToInt64(v, CultureInfo.InvariantCulture) : def;
        public static int I(this IDictionary<string, object?> o, string k, int def = 0) => (int)L(o, k, def);
        public static float F(this IDictionary<string, object?> o, string k, float def = 0) => o.TryGetValue(k, out var v) && v != null ? Convert.ToSingle(v, CultureInfo.InvariantCulture) : def;
        public static string S(this IDictionary<string, object?> o, string k, string def = "") => o.TryGetValue(k, out var v) && v is string s ? s : def;
        public static bool B(this IDictionary<string, object?> o, string k) => o.TryGetValue(k, out var v) && v is bool b && b;
        public static List<object?> A(this IDictionary<string, object?> o, string k) => o.TryGetValue(k, out var v) && v is List<object?> l ? l : new List<object?>();
        public static IDictionary<string, object?> O(this IDictionary<string, object?> o, string k) => o.TryGetValue(k, out var v) && v is IDictionary<string, object?> d ? d : new Dictionary<string, object?>();
    }
}
