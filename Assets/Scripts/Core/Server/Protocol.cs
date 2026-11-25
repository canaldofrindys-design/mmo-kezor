using System.Globalization;

namespace Kezor.Core.Server
{
    /// <summary>
    /// Protocolo texto (uma mensagem por linha, UTF-8). Cliente -> servidor: comandos ("MOVE 80.5 70").
    /// Servidor -> cliente: respostas "OK|ERR|FAIL ..." e mensagens empurradas que começam com "@":
    ///   @WELCOME id · @ME ... · @S |P;..|M;..|D;.. (snapshot 10 Hz) · @INV |I;..|E;.. · @EV TIPO;campos
    /// Números sempre em cultura invariante (ponto decimal), independente do idioma do Windows.
    /// </summary>
    public static class Protocol
    {
        public const int DefaultPort = 55901;
        /// <summary>Versão do jogo mostrada no STATUS e na tela de login.</summary>
        public const string GameVersion = "0.18";
        public const char Sep = ';';
        public const char ListSep = '|';
        public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string F(float v) => v.ToString("0.##", Inv);
        public static float PF(string s) => float.Parse(s, NumberStyles.Float, Inv);
        public static int PI(string s) => int.Parse(s, NumberStyles.Integer, Inv);
        public static long PL(string s) => long.Parse(s, NumberStyles.Integer, Inv);
        public static bool TryPF(string s, out float v) => float.TryParse(s, NumberStyles.Float, Inv, out v) && !float.IsNaN(v) && !float.IsInfinity(v);
        public static bool TryPL(string s, out long v) => long.TryParse(s, NumberStyles.Integer, Inv, out v);

        /// <summary>Remove separadores do protocolo de textos livres (nomes, chat).</summary>
        public static string Clean(string s) => s.Replace(';', ',').Replace('|', '/').Replace('\n', ' ').Replace('\r', ' ');
    }
}
