namespace Kezor.Client
{
    /// <summary>Ranking (tecla K): geral, guildas e uma aba por classe, calculado no servidor. Janela em UI/Windows.cs.</summary>
    public sealed partial class KezorGame
    {
        internal bool _showRanking;
        internal string _rankTab = "GERAL", _rankMine = "";

        internal void OpenRanking(string kind)
        {
            _rankTab = kind;
            Send($"RANKING {kind}", r => _rankMine = r.StartsWith("OK") ? r.Substring(3) : "");
        }

        /// <summary>Guilda inimiga na guerra atual (para pintar o nome de vermelho).</summary>
        private string WarEnemy => !State.AtWar ? "" : State.WarA == State.GuildName ? State.WarB : State.WarA;
    }
}
