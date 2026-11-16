using UnityEditor;

namespace GrowthSpurt.EditorTools
{
    /// <summary>
    /// Any model dropped into Assets/_Project/Art/Characters/ (e.g. a Mixamo FBX) is imported as a Humanoid
    /// with its own avatar, so the Starter Assets animator controller can drive it.
    /// Only applies on the FIRST import, so later changes made in the Inspector are kept.
    /// </summary>
    public class CharacterModelPostprocessor : AssetPostprocessor
    {
        public const string CharactersFolder = "Assets/_Project/Art/Characters/";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(CharactersFolder)) return;
            var importer = (ModelImporter)assetImporter;
            if (!importer.importSettingsMissing) return;

            ConfigureHumanoid(importer);
        }

        public static void ConfigureHumanoid(ModelImporter importer)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        }
    }
}
