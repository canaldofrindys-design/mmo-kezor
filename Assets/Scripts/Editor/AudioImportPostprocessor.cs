using UnityEditor;
using UnityEngine;

namespace Kezor.EditorTools
{
    /// <summary>
    /// Importação dos sons do Kezor (Resources/Audio). Os arquivos já chegam mixados no padrão de loudness do jogo
    /// (Tools/Sons/montar_sons.py), então o importador não pode mexer no volume: sem "Force To Mono"/normalizar.
    /// Efeitos curtos ficam descomprimidos na memória (tocam na hora, sem engasgo no primeiro golpe);
    /// os loops longos de ambiente ficam comprimidos na memória (Vorbis), que pesa pouco e não lê do disco.
    /// </summary>
    public sealed class AudioImportPostprocessor : AssetPostprocessor
    {
        public override uint GetVersion() => 1;

        private void OnPreprocessAudio()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.Contains("/Resources/Audio/")) return;
            var imp = (AudioImporter)assetImporter;
            imp.forceToMono = false;
            imp.loadInBackground = false;
            var s = imp.defaultSampleSettings;
            bool ambient = path.Contains("/Resources/Audio/Ambiente/");
            s.loadType = ambient ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = ambient ? 0.6f : 0.8f;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            imp.defaultSampleSettings = s;
        }
    }
}
