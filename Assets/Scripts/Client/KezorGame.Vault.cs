namespace Kezor.Client
{
    /// <summary>Baú da cidade (Ilsa, a Guardiã): a janela fica em UI/Windows.cs; o servidor valida tudo.</summary>
    public sealed partial class KezorGame
    {
        internal bool VaultVisible => State.VaultOpen;
    }
}
