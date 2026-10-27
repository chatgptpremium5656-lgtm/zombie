using UnityEditor;

namespace Zombie.EditorTools
{
    /// <summary>
    /// Import rules for generated models in Assets/Art/Models: no embedded materials (everything uses
    /// the shared toon materials), vertex colours kept, only the zombie imports a rig and a looping clip.
    /// </summary>
    sealed class ModelImportSettings : AssetPostprocessor
    {
        const string ModelFolder = "Assets/Art/Models/";

        static bool IsCharacter(string path) => path.Contains("Zombie");

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelFolder))
                return;
            var importer = (ModelImporter)assetImporter;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importBlendShapes = false;
            importer.addCollider = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.useFileScale = true;
            bool character = IsCharacter(assetPath);
            importer.animationType = character ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            importer.importAnimation = character;
            importer.avatarSetup = character ? ModelImporterAvatarSetup.CreateFromThisModel : ModelImporterAvatarSetup.NoAvatar;
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(ModelFolder) || !IsCharacter(assetPath))
                return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].name = "Shamble";
                clips[i].loopTime = true;
                clips[i].loopPose = true;
            }
            importer.clipAnimations = clips;
        }
    }
}
