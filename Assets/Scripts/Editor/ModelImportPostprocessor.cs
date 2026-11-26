using UnityEditor;

namespace Kezor.EditorTools
{
    /// <summary>
    /// Importação dos FBX do Blender em Assets/Resources/Models: escala 1, sem animação/câmeras/luzes/materiais
    /// (as cores vêm na cor de vértice e o jogo usa o material Kezor/LowPoly), hierarquia preservada (Body → Head/ArmL...).
    /// Environment fica com Read/Write ligado para o WorldBuilder combinar as instâncias por chunk.
    /// </summary>
    public sealed class ModelImportPostprocessor : AssetPostprocessor
    {
        public override uint GetVersion() => 3;

        private void OnPreprocessModel()
        {
            var path = assetPath.Replace('\\', '/');
            if (!path.StartsWith("Assets/Resources/Models/")) return;
            if (!(assetImporter is ModelImporter mi)) return;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = false;
            mi.preserveHierarchy = true;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.importBlendShapes = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.CalculateMikk;      // relevo (normal map) das peças com textura
            mi.addCollider = false;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            // Environment (WorldBuilder combina) e personagens/equipamentos (o SkinRig cria a pele que dobra nas juntas)
            mi.isReadable = path.Contains("/Environment/") || path.Contains("/Classes/") || path.Contains("/Equip/") || path.Contains("/Npcs/");
            // mid poly: heróis, equipamentos, asas e monstros ganham LODs automáticos (Mesh LOD do Unity 6: LOD0/1/2)
            bool lods = !path.Contains("/Environment/");
            mi.generateMeshLods = lods;
            if (lods) mi.maximumMeshLod = 3;
        }

        /// <summary>Texturas assadas dos equipamentos: _Normal vira normal map; _Mask fica linear (metal/oclusão/lisura).</summary>
        private void OnPreprocessTexture()
        {
            var path = assetPath.Replace('\\', '/');
            if (!path.StartsWith("Assets/Resources/Textures/Equip/")) return;
            if (!(assetImporter is TextureImporter ti)) return;
            ti.mipmapEnabled = true;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.anisoLevel = 4;
            if (path.EndsWith("_Normal.png")) ti.textureType = TextureImporterType.NormalMap;
            else
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = !path.EndsWith("_Mask.png");
            }
        }
    }
}
