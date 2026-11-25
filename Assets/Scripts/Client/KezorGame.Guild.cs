namespace Kezor.Client
{
    /// <summary>Guilda (tecla G ou o Hadrik): estado da janela e nomes dos cargos. A janela fica em UI/Windows.cs.</summary>
    public sealed partial class KezorGame
    {
        internal bool _showGuild;
        internal static readonly string[] RankNames = { "Membro", "Oficial", "Líder" };
    }
}
