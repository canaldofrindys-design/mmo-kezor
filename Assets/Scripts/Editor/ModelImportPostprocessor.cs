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
        public override uint GetVersion() => 2;

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
            mi.addCollider = false;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.isReadable = path.Contains("/Environment/");
            // mid poly: heróis, equipamentos, asas e monstros ganham LODs automáticos (Mesh LOD do Unity 6: LOD0/1/2)
            bool lods = !path.Contains("/Environment/");
            mi.generateMeshLods = lods;
            if (lods) mi.maximumMeshLod = 3;
        }
    }
}
